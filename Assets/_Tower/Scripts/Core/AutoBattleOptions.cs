using System;

namespace Tower.Core
{
    [Serializable]
    public sealed class AutoBattleOptions
    {
        public int seed = 1729;
        public int battles = 20;
        public int maxRounds = 12;
    }
}
