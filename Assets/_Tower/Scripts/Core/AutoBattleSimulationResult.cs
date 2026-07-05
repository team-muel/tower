using System;
using System.Collections.Generic;

namespace Tower.Core
{
    [Serializable]
    public sealed class AutoBattleSimulationResult
    {
        public int seed;
        public int battles;
        public int maxRounds;
        public int playerWins;
        public int enemyWins;
        public int draws;
        public int guardedBattles;
        public float playerWinRate;
        public float enemyWinRate;
        public float averageRounds;
        public float averagePlayerSurvivors;
        public float averageEnemySurvivors;
        public float averageWinningSurvivors;
        public List<AutoBattleUnitAggregate> unitStats = new List<AutoBattleUnitAggregate>();
    }
}
