namespace Tower.Core
{
    public sealed class UnitCombatMetrics
    {
        public UnitCombatMetrics(string unitId)
        {
            UnitId = unitId;
        }

        public string UnitId { get; }
        public int ActionsTaken { get; internal set; }
        public int DamageDealt { get; internal set; }
        public int DamageTaken { get; internal set; }
        public int Kills { get; internal set; }
    }
}
