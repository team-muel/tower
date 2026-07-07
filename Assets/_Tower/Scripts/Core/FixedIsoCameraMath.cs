using System;

namespace Tower.Core
{
    // Pure math for the fixed-angle combat follow camera. The yaw is data, not
    // player input: camera movement is follow + zoom only.
    public static class FixedIsoCameraMath
    {
        public const float DefaultYaw = 45f;
        public const float DefaultPitch = 50f;
        public const float MinPitch = 20f;
        public const float MaxPitch = 70f;

        public const float DefaultDistance = 14f;
        public const float MinDistance = 6f;
        public const float MaxDistance = 22f;

        public const float DefaultZoomStep = 1.5f;
        public const float DefaultFocusDamping = 0.12f;

        private const double DegreesToRadians = Math.PI / 180.0;

        public static float ClampPitch(float pitchDegrees, float minPitch = MinPitch, float maxPitch = MaxPitch)
        {
            return Clamp(pitchDegrees, minPitch, maxPitch);
        }

        public static float ClampDistance(float distance, float minDistance = MinDistance, float maxDistance = MaxDistance)
        {
            return Clamp(distance, minDistance, maxDistance);
        }

        public static CameraOffset ComputeOffset(float fixedYawDegrees, float pitchDegrees, float distance)
        {
            float yawRadians = fixedYawDegrees * (float)DegreesToRadians;
            float pitchRadians = pitchDegrees * (float)DegreesToRadians;
            float cosPitch = (float)Math.Cos(pitchRadians);
            float sinPitch = (float)Math.Sin(pitchRadians);
            float cosYaw = (float)Math.Cos(yawRadians);
            float sinYaw = (float)Math.Sin(yawRadians);

            return new CameraOffset(
                -distance * cosPitch * sinYaw,
                distance * sinPitch,
                -distance * cosPitch * cosYaw);
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
