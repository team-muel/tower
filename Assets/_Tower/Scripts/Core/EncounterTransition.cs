namespace Tower.Core
{
    public enum EncounterPhase
    {
        Exploring,
        IntroHold,
        Active,
        Resolved
    }

    /// <summary>
    /// Engine-independent encounter entry state. Crossing the trigger starts a
    /// short local player hold; combat becomes active after real elapsed time.
    /// It never changes global time scale and cannot retrigger once consumed.
    /// </summary>
    public sealed class EncounterTransition
    {
        private const float CompletionEpsilon = 0.0001f;
        private readonly float triggerRadius;
        private readonly float holdSeconds;
        private float phaseElapsed;

        private EncounterTransition(float triggerRadius, float holdSeconds)
        {
            this.triggerRadius = triggerRadius;
            this.holdSeconds = holdSeconds;
            Phase = EncounterPhase.Exploring;
        }

        public EncounterPhase Phase { get; private set; }
        public float TriggerRadius => triggerRadius;
        public float HoldSeconds => holdSeconds;
        public float PhaseElapsed => phaseElapsed;
        public float HoldProgress => Phase == EncounterPhase.IntroHold
            ? Clamp01(phaseElapsed / holdSeconds)
            : Phase == EncounterPhase.Exploring ? 0f : 1f;
        public bool IsPlayerHeld => Phase == EncounterPhase.IntroHold;
        public bool IsCombatActive => Phase == EncounterPhase.Active;

        public static Result<EncounterTransition> Create(float triggerRadius, float holdSeconds)
        {
            if (!IsFinite(triggerRadius) || triggerRadius <= 0f)
            {
                return Result<EncounterTransition>.Failure(
                    "Encounter trigger radius must be finite and greater than zero.");
            }

            if (!IsFinite(holdSeconds) || holdSeconds <= 0f)
            {
                return Result<EncounterTransition>.Failure(
                    "Encounter intro hold must be finite and greater than zero.");
            }

            return Result<EncounterTransition>.Success(
                new EncounterTransition(triggerRadius, holdSeconds));
        }

        public bool TryBegin(float planarDistance)
        {
            if (Phase != EncounterPhase.Exploring
                || !IsFinite(planarDistance)
                || planarDistance < 0f
                || planarDistance > triggerRadius)
            {
                return false;
            }

            Phase = EncounterPhase.IntroHold;
            phaseElapsed = 0f;
            return true;
        }

        /// <summary>Returns true only on the frame combat becomes active.</summary>
        public bool Tick(float realDeltaSeconds)
        {
            if (Phase != EncounterPhase.IntroHold
                || !IsFinite(realDeltaSeconds)
                || realDeltaSeconds < 0f)
            {
                return false;
            }

            phaseElapsed += realDeltaSeconds;
            if (phaseElapsed + CompletionEpsilon < holdSeconds)
            {
                return false;
            }

            phaseElapsed = holdSeconds;
            Phase = EncounterPhase.Active;
            return true;
        }

        public Result Resolve()
        {
            if (Phase != EncounterPhase.Active)
            {
                return Result.Failure("Only an active encounter can be resolved.");
            }

            Phase = EncounterPhase.Resolved;
            phaseElapsed = 0f;
            return Result.Success();
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
