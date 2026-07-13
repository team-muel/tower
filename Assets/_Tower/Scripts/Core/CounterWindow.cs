using System;

namespace Tower.Core
{
    public enum CounterInstantResult
    {
        Early,
        Clean,
        Late,
        Missed
    }

    public enum CounterCoverageResult
    {
        Clean,
        InsufficientCoverage,
        Missed
    }

    /// <summary>
    /// Pure timing measurement for the v0 counter preview. The supplied times
    /// are scaled game-time values; results intentionally cause no gameplay.
    /// </summary>
    public sealed class CounterWindow
    {
        private const float BoundaryEpsilon = 0.00001f;
        private readonly float earlyBoundary;
        private readonly float cleanBoundary;
        private readonly float coverageThreshold;

        public CounterWindow(float earlyBoundary, float cleanBoundary, float coverageThreshold)
        {
            if (earlyBoundary < 0f || earlyBoundary > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(earlyBoundary));
            }

            if (cleanBoundary < earlyBoundary || cleanBoundary > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(cleanBoundary));
            }

            if (coverageThreshold < 0f || coverageThreshold > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(coverageThreshold));
            }

            this.earlyBoundary = earlyBoundary;
            this.cleanBoundary = cleanBoundary;
            this.coverageThreshold = coverageThreshold;
        }

        public CounterInstantResult ClassifyInstant(float? pressTime, TelegraphState telegraph)
        {
            if (!pressTime.HasValue || telegraph == null || telegraph.Phase != TelegraphPhase.Windup)
            {
                return CounterInstantResult.Missed;
            }

            return ClassifyInstant(pressTime.Value, telegraph.WindupStartedAt, telegraph.Durations.WindupSeconds);
        }

        public CounterInstantResult ClassifyInstant(float pressTime, float windupStartedAt, float windupSeconds)
        {
            if (windupSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(windupSeconds));
            }

            var normalized = (pressTime - windupStartedAt) / windupSeconds;
            if (normalized < 0f || normalized > 1f)
            {
                return CounterInstantResult.Missed;
            }

            if (normalized < earlyBoundary - BoundaryEpsilon)
            {
                return CounterInstantResult.Early;
            }

            if (normalized <= cleanBoundary + BoundaryEpsilon)
            {
                return CounterInstantResult.Clean;
            }

            return CounterInstantResult.Late;
        }

        public CounterCoverageResult ClassifyCoverage(
            float? holdStart,
            float? holdEnd,
            float windupStartedAt,
            float commitStartedAt)
        {
            if (!holdStart.HasValue || !holdEnd.HasValue || holdEnd.Value < holdStart.Value || commitStartedAt < windupStartedAt)
            {
                return CounterCoverageResult.Missed;
            }

            if (holdStart.Value > commitStartedAt || holdEnd.Value < commitStartedAt)
            {
                return CounterCoverageResult.Missed;
            }

            var windupSeconds = commitStartedAt - windupStartedAt;
            if (windupSeconds <= 0f)
            {
                return CounterCoverageResult.Missed;
            }

            var lateStart = windupStartedAt + (cleanBoundary * windupSeconds);
            var lateDuration = commitStartedAt - lateStart;
            var overlapStart = Math.Max(holdStart.Value, lateStart);
            var overlapEnd = Math.Min(holdEnd.Value, commitStartedAt);
            var coveredSeconds = Math.Max(0f, overlapEnd - overlapStart);
            var coverage = lateDuration <= 0f ? 1f : coveredSeconds / lateDuration;
            return coverage >= coverageThreshold
                ? CounterCoverageResult.Clean
                : CounterCoverageResult.InsufficientCoverage;
        }
    }
}
