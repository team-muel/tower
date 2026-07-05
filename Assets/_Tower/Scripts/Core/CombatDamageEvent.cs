namespace Tower.Core
{
    public readonly struct CombatDamageEvent
    {
        public CombatDamageEvent(
            string sourceUnitId,
            string targetUnitId,
            string abilityId,
            int damage,
            bool targetDefeated)
        {
            SourceUnitId = sourceUnitId;
            TargetUnitId = targetUnitId;
            AbilityId = abilityId;
            Damage = damage;
            TargetDefeated = targetDefeated;
        }

        public string SourceUnitId { get; }
        public string TargetUnitId { get; }
        public string AbilityId { get; }
        public int Damage { get; }
        public bool TargetDefeated { get; }
    }
}
