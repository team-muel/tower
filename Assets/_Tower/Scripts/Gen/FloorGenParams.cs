using System;
using System.Collections.Generic;

namespace Tower.Gen
{
    public sealed class FloorGenParams
    {
        private static readonly string[] DefaultEnemyKindSlots =
        {
            "melee",
            "ranged",
            "elite"
        };

        public FloorGenParams(int seed)
            : this(seed, new IntRange(3, 5), false, new IntRange(8, 14), DefaultEnemyKindSlots, "boss")
        {
        }

        public FloorGenParams(int seed, bool isBossFloor)
            : this(seed, new IntRange(3, 5), isBossFloor, new IntRange(8, 14), DefaultEnemyKindSlots, "boss")
        {
        }

        public FloorGenParams(
            int seed,
            IntRange roomCountRange,
            bool isBossFloor,
            IntRange roomSizeRange,
            IEnumerable<string> enemyKindSlots,
            string bossKindSlot)
        {
            if (roomCountRange.Min < 3)
            {
                throw new ArgumentOutOfRangeException(nameof(roomCountRange), "Floor v0 requires at least 3 rooms.");
            }

            if (roomCountRange.Max > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(roomCountRange), "Floor v0 supports at most 5 rooms.");
            }

            if (roomSizeRange.Min < 8)
            {
                throw new ArgumentOutOfRangeException(nameof(roomSizeRange), "Floor v0 rooms must be at least 8x8.");
            }

            if (roomSizeRange.Max > 14)
            {
                throw new ArgumentOutOfRangeException(nameof(roomSizeRange), "Floor v0 rooms must be at most 14x14.");
            }

            if (enemyKindSlots == null)
            {
                throw new ArgumentNullException(nameof(enemyKindSlots));
            }

            if (string.IsNullOrWhiteSpace(bossKindSlot))
            {
                throw new ArgumentException("Boss kind slot is required.", nameof(bossKindSlot));
            }

            List<string> slots = new List<string>();
            foreach (string slot in enemyKindSlots)
            {
                if (string.IsNullOrWhiteSpace(slot))
                {
                    throw new ArgumentException("Enemy kind slots cannot contain blank values.", nameof(enemyKindSlots));
                }

                slots.Add(slot);
            }

            if (slots.Count == 0)
            {
                throw new ArgumentException("At least one enemy kind slot is required.", nameof(enemyKindSlots));
            }

            Seed = seed;
            RoomCountRange = roomCountRange;
            IsBossFloor = isBossFloor;
            RoomSizeRange = roomSizeRange;
            EnemyKindSlots = slots;
            BossKindSlot = bossKindSlot;
        }

        public int Seed { get; }

        public IntRange RoomCountRange { get; }

        public bool IsBossFloor { get; }

        public IntRange RoomSizeRange { get; }

        public IReadOnlyList<string> EnemyKindSlots { get; }

        public string BossKindSlot { get; }
    }
}
