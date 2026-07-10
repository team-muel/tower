namespace Tower.Core
{
    public sealed class UseAbilityCommand
    {
        public UseAbilityCommand(
            string unitId,
            string abilityId,
            string targetUnitId = null,
            BattlePos? targetPoint = null)
        {
            UnitId = unitId;
            AbilityId = abilityId;
            TargetUnitId = targetUnitId;
            TargetPoint = targetPoint;
        }

        public string UnitId { get; }
        public string AbilityId { get; }
        public string TargetUnitId { get; }
        public BattlePos? TargetPoint { get; }
    }
}
