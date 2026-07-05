using System.Collections.Generic;

namespace Tower.Core
{
    public enum ExpeditionOutcome
    {
        // An intermediate floor was cleared; no checkpoint is written.
        FloorCleared,

        // The top floor of the stairway was cleared: deaths are locked in,
        // the shortcut is gained, and a checkpoint save is required.
        Advanced,

        // Rolled back to the last checkpoint; dead members return with an
        // extra death count (or go missing at three).
        Retreated,

        // Third retreat: back to stairway 1 floor 1 with a reset roster.
        GreatRegression
    }

    // Result payload for ExpeditionRules transitions: the new state plus what
    // happened, so callers (runner, UI, tests) can react without diffing.
    public sealed class ExpeditionProgress
    {
        private static readonly string[] Empty = new string[0];

        internal ExpeditionProgress(
            ExpeditionOutcome outcome,
            ExpeditionState state,
            IReadOnlyList<string> confirmedDeadIds = null,
            IReadOnlyList<string> revivedIds = null,
            IReadOnlyList<string> newlyMissingIds = null)
        {
            Outcome = outcome;
            State = state;
            ConfirmedDeadIds = confirmedDeadIds ?? Empty;
            RevivedIds = revivedIds ?? Empty;
            NewlyMissingIds = newlyMissingIds ?? Empty;
        }

        public ExpeditionOutcome Outcome { get; }
        public ExpeditionState State { get; }

        // Members permanently removed by an advance.
        public IReadOnlyList<string> ConfirmedDeadIds { get; }

        // Members revived by a retreat (death count incremented).
        public IReadOnlyList<string> RevivedIds { get; }

        // Members that hit the three-death threshold during this transition.
        public IReadOnlyList<string> NewlyMissingIds { get; }

        // Advance, retreat and the great regression all persist; clearing an
        // intermediate floor does not.
        public bool RequiresSave => Outcome != ExpeditionOutcome.FloorCleared;
    }
}
