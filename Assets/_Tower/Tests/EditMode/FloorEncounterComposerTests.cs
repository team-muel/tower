using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;
using Tower.Gen;

namespace Tower.Tests.EditMode
{
    // T26: coverage for the Gen-layer combiner that consumes a resolved
    // EncounterBudget to fill a FloorEncounter. Deterministic and Core-pure:
    // no UnityEngine, no RNG. Mirrors the EncounterBudgetTests / FloorGeneratorTests
    // style (NUnit, Tower.Tests.EditMode).
    public sealed class FloorEncounterComposerTests
    {
        private static readonly string[] Pool = { "melee", "ranged" };
        private const string BossSlot = "boss";
        private const string EliteSlot = "elite";

        private static EncounterBudget SampleBudget()
        {
            return new EncounterBudget(
                baseDifficulty: 30,
                depthDifficultyRamp: 10,
                activeEnemyCapBase: 3f,
                activeEnemyCapDepthRamp: 0.5f,
                activeEnemyCapMax: 5,
                minTypes: 1,
                maxTypes: 3,
                typeCountDepthRamp: 0.5f,
                minWaves: 1,
                maxWaves: 3,
                eliteCap: 1);
        }

        private static FloorEncounter Compose(
            EncounterBudget budget,
            RoomKind kind,
            int seed,
            int roomId,
            int depth,
            string eliteSlot = null)
        {
            return FloorEncounterComposer.Compose(
                budget, kind, seed, roomId, depth, BiomeId.Forest, Pool, BossSlot, eliteSlot);
        }

        private static int CountKind(FloorEncounter encounter, string kindSlot)
        {
            int count = 0;
            for (int i = 0; i < encounter.EnemySlots.Count; i++)
            {
                if (string.Equals(encounter.EnemySlots[i].KindSlot, kindSlot, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static string Signature(FloorEncounter encounter)
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.Append(encounter.IsBoss ? "boss" : "combat")
                .Append(':')
                .Append(encounter.EnemyCount);
            for (int i = 0; i < encounter.EnemySlots.Count; i++)
            {
                builder.Append('|')
                    .Append(encounter.EnemySlots[i].Index)
                    .Append('=')
                    .Append(encounter.EnemySlots[i].KindSlot);
            }

            return builder.ToString();
        }

        // --- Determinism: same seed / inputs => identical composition ---

        [Test]
        public void Compose_IsDeterministicForSameInputs()
        {
            var budget = SampleBudget();

            FloorEncounter first = Compose(budget, RoomKind.Normal, 12345, 2, 3, EliteSlot);
            FloorEncounter second = Compose(budget, RoomKind.Normal, 12345, 2, 3, EliteSlot);

            Assert.AreEqual(Signature(first), Signature(second));
        }

        [Test]
        public void Compose_DifferentSeedsCanChangeComposition()
        {
            var budget = SampleBudget();

            // Same numeric budget bounds, but the seed rotates the type window,
            // so at least one of a spread of seeds must differ from the first.
            string baseline = Signature(Compose(budget, RoomKind.Normal, 1, 0, 4, EliteSlot));
            bool anyDifferent = false;
            for (int seed = 2; seed <= 40; seed++)
            {
                if (Signature(Compose(budget, RoomKind.Normal, seed, 0, 4, EliteSlot)) != baseline)
                {
                    anyDifferent = true;
                    break;
                }
            }

            Assert.IsTrue(anyDifferent, "Seed should influence enemy type composition.");
        }

        // --- Budget respected: never exceeds any budget bound ---

        [Test]
        public void Compose_NeverExceedsBudgetBounds()
        {
            var budget = SampleBudget();

            for (int seed = 0; seed < 25; seed++)
            {
                for (int depth = 0; depth <= 12; depth++)
                {
                    FloorEncounter encounter = Compose(budget, RoomKind.Normal, seed, 1, depth, EliteSlot);

                    int cap = budget.ActiveEnemyCapAt(depth);
                    int difficultyCount = Math.Max(1, budget.DifficultyAt(depth) / FloorEncounterComposer.DifficultyPerEnemy);

                    // Concurrency cap is never exceeded.
                    Assert.That(encounter.EnemyCount, Is.LessThanOrEqualTo(cap),
                        "Enemy count must not exceed the active enemy cap.");
                    // Difficulty-derived count is never exceeded.
                    Assert.That(encounter.EnemyCount, Is.LessThanOrEqualTo(difficultyCount),
                        "Enemy count must not exceed the difficulty budget.");
                    // A combat room always fields at least one enemy.
                    Assert.That(encounter.EnemyCount, Is.GreaterThanOrEqualTo(1));
                    // Slots always match the declared count.
                    Assert.That(encounter.EnemySlots.Count, Is.EqualTo(encounter.EnemyCount));
                    // Elite budget is respected.
                    Assert.That(CountKind(encounter, EliteSlot), Is.LessThanOrEqualTo(budget.EliteCap),
                        "Elite count must not exceed the elite cap.");
                }
            }
        }

        [Test]
        public void Compose_LowDifficultyBudgetBindsCountToOne()
        {
            // Difficulty budget (5) yields floor(5/10)=0 -> clamped to 1, so the
            // difficulty bound binds even though the concurrency cap is 5.
            var tight = new EncounterBudget(
                baseDifficulty: 5,
                depthDifficultyRamp: 0,
                activeEnemyCapBase: 5f,
                activeEnemyCapDepthRamp: 0f,
                activeEnemyCapMax: 5,
                minTypes: 1,
                maxTypes: 1,
                typeCountDepthRamp: 0f,
                minWaves: 1,
                maxWaves: 1,
                eliteCap: 0);

            for (int depth = 0; depth <= 10; depth++)
            {
                FloorEncounter encounter = Compose(tight, RoomKind.Normal, 99, 1, depth);
                Assert.That(tight.ActiveEnemyCapAt(depth), Is.EqualTo(5));
                Assert.That(encounter.EnemyCount, Is.EqualTo(1),
                    "A low difficulty budget must bind the count to one enemy.");
            }
        }

        // --- Depth ramp: composition grows (never shrinks) with depth ---

        [Test]
        public void Compose_EnemyCountRampsMonotonicallyWithDepth()
        {
            var budget = SampleBudget();

            // Same seed, deeper rooms: count is non-decreasing (both bounds ramp).
            FloorEncounter previous = Compose(budget, RoomKind.Normal, 7, 1, 0, EliteSlot);
            for (int depth = 1; depth <= 15; depth++)
            {
                FloorEncounter current = Compose(budget, RoomKind.Normal, 7, 1, depth, EliteSlot);
                Assert.That(current.EnemyCount, Is.GreaterThanOrEqualTo(previous.EnemyCount),
                    "Enemy count must ramp monotonically with depth.");
                previous = current;
            }

            // Spot-check the known ramp: min(cap, difficulty/10) = 3,3,4,4,5.
            Assert.That(Compose(budget, RoomKind.Normal, 7, 1, 0, EliteSlot).EnemyCount, Is.EqualTo(3));
            Assert.That(Compose(budget, RoomKind.Normal, 7, 1, 2, EliteSlot).EnemyCount, Is.EqualTo(4));
            Assert.That(Compose(budget, RoomKind.Normal, 7, 1, 4, EliteSlot).EnemyCount, Is.EqualTo(5));
        }

        // --- Room-kind edges: entrance / camp are the zero-budget path ---

        [Test]
        public void Compose_EntranceAndCampFieldNoEnemies()
        {
            var budget = SampleBudget();

            FloorEncounter entrance = Compose(budget, RoomKind.Entrance, 5, 0, 0, EliteSlot);
            FloorEncounter camp = Compose(budget, RoomKind.Camp, 5, 3, 3, EliteSlot);

            Assert.IsFalse(entrance.HasEncounter);
            Assert.AreEqual(0, entrance.EnemyCount);
            Assert.IsFalse(camp.HasEncounter);
            Assert.AreEqual(0, camp.EnemyCount);
        }

        [Test]
        public void Compose_BossRoomIsSingleBossRegardlessOfBudget()
        {
            var budget = SampleBudget();

            FloorEncounter boss = Compose(budget, RoomKind.Boss, 5, 4, 6, EliteSlot);

            Assert.IsTrue(boss.HasEncounter);
            Assert.IsTrue(boss.IsBoss);
            Assert.AreEqual(1, boss.EnemyCount);
            Assert.AreEqual(BossSlot, boss.EnemySlots[0].KindSlot);
        }

        // --- Override path: a resolved (overridden) budget feeds the combiner ---

        [Test]
        public void Compose_ResolvedOverrideChangesEliteComposition()
        {
            var table = new EncounterBudgetTable(SampleBudget());
            table.SetRoomKindOverride("Elite", new EncounterBudgetOverride { EliteCap = 3 });

            EncounterBudget baseResolved = table.Resolve("Forest", "Normal");
            EncounterBudget eliteResolved = table.Resolve("Forest", "Elite");

            // Depth 4 -> enemy count 5, so elite budgets of 1 vs 3 are visible.
            FloorEncounter baseEnc = FloorEncounterComposer.Compose(
                baseResolved, RoomKind.Normal, 42, 2, 4, BiomeId.Forest, Pool, BossSlot, EliteSlot);
            FloorEncounter eliteEnc = FloorEncounterComposer.Compose(
                eliteResolved, RoomKind.Normal, 42, 2, 4, BiomeId.Forest, Pool, BossSlot, EliteSlot);

            Assert.AreEqual(1, CountKind(baseEnc, EliteSlot), "Base elite cap is 1.");
            Assert.AreEqual(3, CountKind(eliteEnc, EliteSlot), "Overridden elite cap is 3.");
            // Same total count; only the elite share changed.
            Assert.AreEqual(baseEnc.EnemyCount, eliteEnc.EnemyCount);
        }

        [Test]
        public void Compose_NoEliteSlotProducesNoElites()
        {
            var budget = SampleBudget();

            FloorEncounter encounter = Compose(budget, RoomKind.Normal, 3, 1, 4);

            Assert.AreEqual(0, CountKind(encounter, EliteSlot));
            // All slots come from the normal pool.
            for (int i = 0; i < encounter.EnemySlots.Count; i++)
            {
                Assert.That(Pool, Contains.Item(encounter.EnemySlots[i].KindSlot));
            }
        }

        [Test]
        public void Compose_DistinctNormalTypesRespectTypeCount()
        {
            var budget = SampleBudget();

            // Depth 0 -> TypeCountAt = 1, so all normal slots share one kind.
            FloorEncounter shallow = Compose(budget, RoomKind.Normal, 8, 1, 0);
            HashSet<string> shallowKinds = new HashSet<string>();
            for (int i = 0; i < shallow.EnemySlots.Count; i++)
            {
                shallowKinds.Add(shallow.EnemySlots[i].KindSlot);
            }

            Assert.That(shallowKinds.Count, Is.LessThanOrEqualTo(budget.TypeCountAt(0)));
        }

        // --- Argument validation ---

        [Test]
        public void Compose_NullBudgetThrows()
        {
            Assert.That(
                () => FloorEncounterComposer.Compose(
                    null, RoomKind.Normal, 1, 0, 0, BiomeId.Forest, Pool, BossSlot),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Compose_NullEnemyKindSlotsThrows()
        {
            Assert.That(
                () => FloorEncounterComposer.Compose(
                    SampleBudget(), RoomKind.Normal, 1, 0, 0, BiomeId.Forest, null, BossSlot),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Compose_EmptyEnemyKindSlotsThrows()
        {
            Assert.That(
                () => FloorEncounterComposer.Compose(
                    SampleBudget(), RoomKind.Normal, 1, 0, 0, BiomeId.Forest, new string[0], BossSlot),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Compose_BlankBossSlotThrows()
        {
            Assert.That(
                () => FloorEncounterComposer.Compose(
                    SampleBudget(), RoomKind.Normal, 1, 0, 0, BiomeId.Forest, Pool, "  "),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Compose_NegativeRoomIdThrows()
        {
            Assert.That(
                () => FloorEncounterComposer.Compose(
                    SampleBudget(), RoomKind.Normal, 1, -1, 0, BiomeId.Forest, Pool, BossSlot),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }
    }
}
