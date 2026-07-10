using System;
using System.Collections.Generic;
using Tower.Core;

namespace Tower.Gen
{
    // T26: Gen-layer combiner that CONSUMES a resolved EncounterBudget and
    // fills a room's FloorEncounter within that budget. Pure and deterministic:
    // no UnityEngine, no RNG state. Composition is a pure function of
    // (budget, seed, roomId, depth, biome, roomKind), mixed with the same
    // FNV-1a hash style PortalAssigner uses, so the same inputs always yield
    // the identical encounter (same seed -> same result).
    //
    // Budget consumption (never exceeded):
    //   * enemy count  <= EncounterBudget.ActiveEnemyCapAt(depth)   (concurrency cap)
    //   * enemy count  <= floor(DifficultyAt(depth) / DifficultyPerEnemy), min 1
    //   * elite count  <= EncounterBudget.EliteCap
    //   * distinct non-elite types == min(TypeCountAt(depth), pool size)
    //
    // The two count bounds are both monotonic non-decreasing in depth, so their
    // min ramps monotonically too: deeper rooms field at least as many enemies.
    public static class FloorEncounterComposer
    {
        // Difficulty cost charged per placed enemy. Exposed so callers/tests can
        // reason about the difficulty-derived count bound exactly.
        public const int DifficultyPerEnemy = 10;

        public static FloorEncounter Compose(
            EncounterBudget budget,
            RoomKind roomKind,
            int seed,
            int roomId,
            int depth,
            BiomeId biome,
            IReadOnlyList<string> enemyKindSlots,
            string bossKindSlot,
            string eliteKindSlot = null)
        {
            if (budget == null)
            {
                throw new ArgumentNullException(nameof(budget));
            }

            if (enemyKindSlots == null)
            {
                throw new ArgumentNullException(nameof(enemyKindSlots));
            }

            if (roomId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roomId), "Room id cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(bossKindSlot))
            {
                throw new ArgumentException("Boss kind slot is required.", nameof(bossKindSlot));
            }

            List<string> pool = new List<string>();
            for (int i = 0; i < enemyKindSlots.Count; i++)
            {
                string slot = enemyKindSlots[i];
                if (string.IsNullOrWhiteSpace(slot))
                {
                    throw new ArgumentException("Enemy kind slots cannot contain blank values.", nameof(enemyKindSlots));
                }

                pool.Add(slot);
            }

            if (pool.Count == 0)
            {
                throw new ArgumentException("At least one enemy kind slot is required.", nameof(enemyKindSlots));
            }

            // Peaceful room kinds field no enemies: this is the zero-budget path.
            if (roomKind == RoomKind.Entrance || roomKind == RoomKind.Camp)
            {
                return FloorEncounter.None();
            }

            // Boss rooms are a single boss regardless of the numeric budget.
            if (roomKind == RoomKind.Boss)
            {
                FloorEnemySlot[] bossSlots = { new FloorEnemySlot(0, bossKindSlot) };
                return new FloorEncounter(true, 1, bossSlots);
            }

            return ComposeCombat(budget, seed, roomId, depth, biome, pool, eliteKindSlot);
        }

        private static FloorEncounter ComposeCombat(
            EncounterBudget budget,
            int seed,
            int roomId,
            int depth,
            BiomeId biome,
            IReadOnlyList<string> pool,
            string eliteKindSlot)
        {
            // Normal (non-elite) type pool. When an elite kind slot is supplied
            // it is drawn only via the elite budget, never as a normal type.
            List<string> normalPool = new List<string>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                if (eliteKindSlot != null && string.Equals(pool[i], eliteKindSlot, StringComparison.Ordinal))
                {
                    continue;
                }

                normalPool.Add(pool[i]);
            }

            // Guard: if the pool was entirely elite, fall back to the raw pool so
            // a combat room is never empty of normal types.
            if (normalPool.Count == 0)
            {
                normalPool = new List<string>(pool);
            }

            uint hash = Hash(seed, roomId, depth, (int)biome);

            // --- enemy count: min of the two budget bounds, at least 1 ---
            int cap = budget.ActiveEnemyCapAt(depth);
            int difficultyCount = budget.DifficultyAt(depth) / DifficultyPerEnemy;
            if (difficultyCount < 1)
            {
                difficultyCount = 1;
            }

            int enemyCount = cap < difficultyCount ? cap : difficultyCount;
            if (enemyCount < 1)
            {
                enemyCount = 1;
            }

            // --- elite budget: cannot exceed EliteCap or the enemy count ---
            int eliteCount = 0;
            if (eliteKindSlot != null)
            {
                eliteCount = budget.EliteCap;
                if (eliteCount > enemyCount)
                {
                    eliteCount = enemyCount;
                }
            }

            // --- distinct normal types mixed this encounter ---
            int typeCount = budget.TypeCountAt(depth);
            if (typeCount > normalPool.Count)
            {
                typeCount = normalPool.Count;
            }

            if (typeCount < 1)
            {
                typeCount = 1;
            }

            // Seed-rotated window of distinct normal types.
            int typeStart = (int)(hash % (uint)normalPool.Count);
            List<string> activeTypes = new List<string>(typeCount);
            for (int k = 0; k < typeCount; k++)
            {
                activeTypes.Add(normalPool[(typeStart + k) % normalPool.Count]);
            }

            List<FloorEnemySlot> slots = new List<FloorEnemySlot>(enemyCount);
            for (int i = 0; i < enemyCount; i++)
            {
                string kindSlot;
                if (i < eliteCount)
                {
                    kindSlot = eliteKindSlot;
                }
                else
                {
                    int normalIndex = i - eliteCount;
                    kindSlot = activeTypes[normalIndex % activeTypes.Count];
                }

                slots.Add(new FloorEnemySlot(i, kindSlot));
            }

            return new FloorEncounter(false, enemyCount, slots);
        }

        // Deterministic FNV-1a style mix, mirroring PortalAssigner.Hash so the
        // Gen layer exposes no RNG surface (T25/T26 hard constraint).
        private static uint Hash(int seed, int roomId, int depth, int biome)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = (hash ^ (uint)seed) * 16777619u;
                hash = (hash ^ (uint)roomId) * 16777619u;
                hash = (hash ^ (uint)depth) * 16777619u;
                hash = (hash ^ (uint)biome) * 16777619u;
                return hash;
            }
        }
    }
}
