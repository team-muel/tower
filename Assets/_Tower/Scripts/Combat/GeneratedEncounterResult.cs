using Tower.Core;

namespace Tower.Combat
{
    public sealed class GeneratedEncounterResult
    {
        public GeneratedEncounterResult(
            string eventId,
            CombatTeam winningTeam,
            int actionCount,
            float durationSeconds)
        {
            EventId = eventId;
            WinningTeam = winningTeam;
            ActionCount = actionCount;
            DurationSeconds = durationSeconds;
        }

        public string EventId { get; }
        public CombatTeam WinningTeam { get; }
        public int ActionCount { get; }
        public float DurationSeconds { get; }
    }
}
