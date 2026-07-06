namespace Tower.Core
{
    // T20: battlefield space selector. Analog is the default; the grid
    // implementation stays behind the same IBattlefield seam as a one-flag
    // rollback path until the analog migration proves stable.
    public enum CombatSpaceMode
    {
        Grid = 0,
        Analog = 1
    }

    public static class CombatSpaceSettings
    {
        public const CombatSpaceMode DefaultMode = CombatSpaceMode.Analog;

        public static CombatSpaceMode Mode { get; set; } = DefaultMode;

        public static void Reset()
        {
            Mode = DefaultMode;
        }
    }
}
