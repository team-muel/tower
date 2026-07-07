namespace Tower.Core
{
    // v0 reward palette previewed on a door before the player commits.
    // Kept intentionally small (Hades-style "what is behind this door").
    public enum RewardType
    {
        // No reward preview available (e.g. entrance/exit passthrough).
        None,

        // Restores party health at the next room.
        Heal,

        // Grants a run resource (currency / crafting stock).
        Resource,

        // Offers a new or upgraded ability.
        Ability,

        // Opens a shortcut / skips ahead.
        Shortcut
    }
}
