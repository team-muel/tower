using System;
using System.Collections.Generic;

namespace Tower.Gen
{
    public sealed class FloorEncounter
    {
        private static readonly FloorEnemySlot[] EmptySlots = new FloorEnemySlot[0];

        internal FloorEncounter(bool isBoss, int enemyCount, IReadOnlyList<FloorEnemySlot> enemySlots)
        {
            if (enemyCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(enemyCount), "Enemy count cannot be negative.");
            }

            if (enemySlots == null)
            {
                throw new ArgumentNullException(nameof(enemySlots));
            }

            if (enemySlots.Count != enemyCount)
            {
                throw new ArgumentException("Enemy slots must match enemy count.", nameof(enemySlots));
            }

            IsBoss = isBoss;
            EnemyCount = enemyCount;
            EnemySlots = new List<FloorEnemySlot>(enemySlots);
        }

        public bool HasEncounter
        {
            get { return EnemyCount > 0; }
        }

        public bool IsBoss { get; }

        public int EnemyCount { get; }

        public IReadOnlyList<FloorEnemySlot> EnemySlots { get; }

        internal static FloorEncounter None()
        {
            return new FloorEncounter(false, 0, EmptySlots);
        }
    }
}
