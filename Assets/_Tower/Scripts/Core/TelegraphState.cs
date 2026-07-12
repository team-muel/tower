using System;

namespace Tower.Core
{
    public enum TelegraphPhase
    {
        Idle,
        Windup,
        Commit,
        Recover
    }

    public readonly struct TelegraphDurations
    {
        public TelegraphDurations(float windupSeconds, float commitSeconds, float recoverSeconds)
        {
            if (windupSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(windupSeconds));
            }

            if (commitSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(commitSeconds));
            }

            if (recoverSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(recoverSeconds));
            }

            WindupSeconds = windupSeconds;
            CommitSeconds = commitSeconds;
            RecoverSeconds = recoverSeconds;
        }

        public float WindupSeconds { get; }
        public float CommitSeconds { get; }
        public float RecoverSeconds { get; }
        public float CycleSeconds => WindupSeconds + CommitSeconds + RecoverSeconds;
    }

    /// <summary>
    /// A deterministic one-commit telegraph cycle. Only Idle can begin a new
    /// Windup, so Commit can never be entered directly or re-entered mid-cycle.
    /// </summary>
    public sealed class TelegraphState
    {
        private readonly TelegraphDurations durations;
        private float phaseElapsed;
        private float elapsedSeconds;

        public TelegraphState(TelegraphDurations durations)
        {
            this.durations = durations;
            Phase = TelegraphPhase.Idle;
            WindupStartedAt = -1f;
            CommitStartedAt = -1f;
        }

        public TelegraphPhase Phase { get; private set; }
        public float ElapsedSeconds => elapsedSeconds;
        public float PhaseElapsed => phaseElapsed;
        public float WindupStartedAt { get; private set; }
        public float CommitStartedAt { get; private set; }
        public TelegraphDurations Durations => durations;

        public bool TryBeginWindup()
        {
            if (Phase != TelegraphPhase.Idle)
            {
                return false;
            }

            Phase = TelegraphPhase.Windup;
            phaseElapsed = 0f;
            WindupStartedAt = elapsedSeconds;
            CommitStartedAt = -1f;
            return true;
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            elapsedSeconds += deltaSeconds;
            var remaining = deltaSeconds;
            while (remaining > 0f && Phase != TelegraphPhase.Idle)
            {
                var duration = DurationFor(Phase);
                var untilTransition = duration - phaseElapsed;
                if (remaining < untilTransition)
                {
                    phaseElapsed += remaining;
                    return;
                }

                remaining -= untilTransition;
                phaseElapsed = 0f;
                AdvancePhase(remaining);
            }
        }

        private float DurationFor(TelegraphPhase phase)
        {
            switch (phase)
            {
                case TelegraphPhase.Windup:
                    return durations.WindupSeconds;
                case TelegraphPhase.Commit:
                    return durations.CommitSeconds;
                case TelegraphPhase.Recover:
                    return durations.RecoverSeconds;
                default:
                    return 0f;
            }
        }

        private void AdvancePhase(float remainingAfterTransition)
        {
            switch (Phase)
            {
                case TelegraphPhase.Windup:
                    Phase = TelegraphPhase.Commit;
                    CommitStartedAt = elapsedSeconds - remainingAfterTransition;
                    break;
                case TelegraphPhase.Commit:
                    Phase = TelegraphPhase.Recover;
                    break;
                case TelegraphPhase.Recover:
                    Phase = TelegraphPhase.Idle;
                    break;
            }
        }
    }
}
