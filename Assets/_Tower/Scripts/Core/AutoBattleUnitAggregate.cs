using System;

namespace Tower.Core
{
    [Serializable]
    public sealed class AutoBattleUnitAggregate
    {
        public string unitId;
        public string team;
        public int battles;
        public int kills;
        public int damageDealt;
        public int damageTaken;
        public int actionsTaken;
    }
}
