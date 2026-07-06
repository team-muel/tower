using System;

namespace Tower.Core
{
    // T20: scale contract between legacy grid cells and analog units.
    // 1 grid cell = 1.0 analog unit, so integer ability ranges and movement
    // budgets carry over to the analog battlefield without rebalancing.
    public static class BattleScale
    {
        public const float UnitsPerCell = 1f;

        // A cell maps to its center point.
        public static BattlePos ToBattlePos(GridPos cell)
        {
            return new BattlePos((cell.X + 0.5f) * UnitsPerCell, (cell.Y + 0.5f) * UnitsPerCell);
        }

        // The cell containing the point (floor); the inverse of ToBattlePos.
        public static GridPos ToGridPos(BattlePos pos)
        {
            return new GridPos(FloorToCell(pos.X), FloorToCell(pos.Y));
        }

        private static int FloorToCell(float value)
        {
            return (int)Math.Floor(value / UnitsPerCell);
        }
    }
}
