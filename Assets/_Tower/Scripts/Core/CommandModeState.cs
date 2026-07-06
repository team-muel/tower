namespace Tower.Core
{
    // T19: bullet-time command mode state machine. Entering is only allowed
    // while combat runs; exiting is always allowed. Presentation slow-down is
    // exposed as a presenter-local playback factor - global Time.timeScale is
    // never touched (turn-based game: command mode is an overlay state, not a
    // time stop).
    public sealed class CommandModeState
    {
        public const float SlowPlaybackFactor = 0.25f;

        public bool IsActive { get; private set; }

        public float PlaybackFactor => IsActive ? SlowPlaybackFactor : 1f;

        public Result Toggle(bool combatActive)
        {
            if (IsActive)
            {
                IsActive = false;
                return Result.Success();
            }

            if (!combatActive)
            {
                return Result.Failure("Command mode requires an active combat.");
            }

            IsActive = true;
            return Result.Success();
        }

        // Combat ending force-exits command mode. Returns true when the call
        // actually deactivated the mode, so callers can refresh presentation.
        public bool SyncCombatActive(bool combatActive)
        {
            if (combatActive || !IsActive)
            {
                return false;
            }

            IsActive = false;
            return true;
        }
    }
}
