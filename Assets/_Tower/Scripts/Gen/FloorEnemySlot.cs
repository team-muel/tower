using System;

namespace Tower.Gen
{
    public sealed class FloorEnemySlot
    {
        internal FloorEnemySlot(int index, string kindSlot)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), "Slot index cannot be negative.");
            }

            if (string.IsNullOrWhiteSpace(kindSlot))
            {
                throw new ArgumentException("Enemy kind slot is required.", nameof(kindSlot));
            }

            Index = index;
            KindSlot = kindSlot;
        }

        public int Index { get; }

        public string KindSlot { get; }
    }
}
