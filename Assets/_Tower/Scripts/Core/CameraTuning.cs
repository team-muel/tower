namespace Tower.Core
{
    // v0 camera defaults + clamp ranges. Concrete values are confirmed through
    // the in-game tuning mode (-devcam) and promoted back into these constants.
    public static class CameraTuning
    {
        public const float DefaultPitch = 52f;
        public const float MinPitch = 20f;
        public const float MaxPitch = 80f;

        public const float DefaultDistance = 14f;
        public const float MinDistance = 8f;
        public const float MaxDistance = 20f;

        public const float DefaultFov = 38f;
        public const float MinFov = 20f;
        public const float MaxFov = 70f;

        public const float DefaultFollowDamping = 0.12f;

        public static float ClampPitch(float value)
        {
            return Clamp(value, MinPitch, MaxPitch);
        }

        public static float ClampDistance(float value)
        {
            return Clamp(value, MinDistance, MaxDistance);
        }

        public static float ClampFov(float value)
        {
            return Clamp(value, MinFov, MaxFov);
        }

        public static CameraTuningState Clamp(CameraTuningState state)
        {
            return new CameraTuningState(
                ClampPitch(state.Pitch),
                ClampDistance(state.Distance),
                ClampFov(state.Fov),
                state.FollowDamping < 0f || float.IsNaN(state.FollowDamping) ? 0f : state.FollowDamping);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (float.IsNaN(value) || value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
