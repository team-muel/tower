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
        public MoveCommand(string unitId, int distance) : base(unitId)
        {
            Distance = distance;
        }

        public int Distance { get; }
    }

    public sealed class SkipTurnCommand : TurnCommand
    {
        public SkipTurnCommand(string unitId) : base(unitId)
        {
        }
    }

    public sealed class UseAbilityCommand : TurnCommand
    {
        public UseAbilityCommand(string unitId, string abilityId, string targetUnitId = null) : base(unitId)
        {
            AbilityId = abilityId;
            TargetUnitId = targetUnitId;
        }

        public string AbilityId { get; }
        public string TargetUnitId { get; }
    }
}
