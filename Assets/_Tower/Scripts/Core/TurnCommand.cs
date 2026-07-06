namespace Tower.Core
{
    public abstract class TurnCommand
    {
        protected TurnCommand(string unitId)
        {
            UnitId = unitId;
        }

        public string UnitId { get; }
    }

    public sealed class MoveCommand : TurnCommand
    {
        public MoveCommand(string unitId, float distance) : this(unitId, distance, null)
        {
        }

        // T20: analog moves carry their continuous destination alongside the
        // spent distance. The battlefield applies the actual position change
        // (as it did for the grid); the engine only spends the budget.
        public MoveCommand(string unitId, float distance, BattlePos? destination) : base(unitId)
        {
            Distance = distance;
            Destination = destination;
        }

        public float Distance { get; }
        public BattlePos? Destination { get; }
    }

    public sealed class SkipTurnCommand : TurnCommand
    {
        public SkipTurnCommand(string unitId) : base(unitId)
        {
        }
    }

    public sealed class UseAbilityCommand : TurnCommand
    {
        public UseAbilityCommand(
            string unitId,
            string abilityId,
            string targetUnitId = null,
            GridPos? targetCell = null,
            BattlePos? targetPoint = null) : base(unitId)
        {
            AbilityId = abilityId;
            TargetUnitId = targetUnitId;
            TargetCell = targetCell;
            TargetPoint = targetPoint;
        }

        public string AbilityId { get; }
        public string TargetUnitId { get; }
        public GridPos? TargetCell { get; }

        // T20: continuous target for point-targeted abilities; when absent,
        // TargetCell (legacy grid target) is converted at the resolver seam.
        public BattlePos? TargetPoint { get; }
    }
}
