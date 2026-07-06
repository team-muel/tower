using System;

namespace Tower.Core
{
    [Serializable]
    public sealed class AutoBattleOptions
    {
        public int seed = 1729;
        public int battles = 20;
        public int maxRounds = 12;

        // T20: which battlefield implementation the simulator runs on.
        // Analog is the game default; Grid remains for rollback comparison.
        public CombatSpaceMode spaceMode = CombatSpaceMode.Analog;
    }
}
