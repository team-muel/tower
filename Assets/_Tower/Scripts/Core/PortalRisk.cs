namespace Tower.Core
{
    // Risk hints shown on a door before the player commits. Multiple risks can
    // apply to a single portal; they are stored as a flags set so the preview
    // can list every trade-off at once.
    [System.Flags]
    public enum PortalRisk
    {
        None = 0,

        // The next room contains a stronger-than-usual fight.
        Elite = 1 << 0,

        // The next room is a boss encounter.
        Boss = 1 << 1,

        // The next room applies an environmental hazard.
        Hazard = 1 << 2,

        // The reward is high value but the fight is riskier to reach it.
        HighStakes = 1 << 3
    }
}
