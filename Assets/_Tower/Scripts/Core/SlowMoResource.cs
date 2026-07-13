using System;

namespace Tower.Core
{
    /// <summary>
    /// Frame-rate-independent charge for the slow-motion preview. This class has
    /// no Unity dependency; callers decide whether its current charge is being
    /// drained or recharged from unscaled time.
    /// </summary>
    public sealed class SlowMoResource
    {
        private readonly float fullDrainSeconds;
        private readonly float fullRechargeSeconds;
        private readonly float minEngage;
        private float charge;

        public SlowMoResource(
            float initialCharge,
            float fullDrainSeconds,
            float fullRechargeSeconds,
            float minEngage)
        {
            if (fullDrainSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fullDrainSeconds));
            }

            if (fullRechargeSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(fullRechargeSeconds));
            }

            if (minEngage < 0f || minEngage > 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(minEngage));
            }

            this.fullDrainSeconds = fullDrainSeconds;
            this.fullRechargeSeconds = fullRechargeSeconds;
            this.minEngage = minEngage;
            charge = Clamp01(initialCharge);
        }

        public float Charge => charge;
        public float MinEngage => minEngage;
        public bool CanEngage => charge > 0f && charge >= minEngage;
        public bool IsDepleted => charge <= 0f;

        public void Drain(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            charge = Clamp01(charge - (deltaSeconds / fullDrainSeconds));
        }

        public void Recharge(float deltaSeconds)
        {
            if (deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            }

            charge = Clamp01(charge + (deltaSeconds / fullRechargeSeconds));
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }
    }
}
