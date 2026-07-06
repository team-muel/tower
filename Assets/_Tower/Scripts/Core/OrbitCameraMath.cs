using System;

namespace Tower.Core
{
    // T19: pure orbit-camera math for the combat orbit rig. Kept engine-free
    // so the angle clamps and orbital position stay unit-testable. Defaults
    // follow the T19 brief: pitch 20-70 degrees, zoom 6-22 meters.
    public static class OrbitCameraMath
    {
        public const float DefaultYaw = 45f;
        public const float DefaultPitch = 50f;
        public const float MinPitch = 20f;
        public const float MaxPitch = 70f;

        public const float DefaultDistance = 14f;
        public const float MinDistance = 6f;
        public const float MaxDistance = 22f;

        public const float YawStepDegrees = 45f;

        // Degrees of orbit per mouse-axis unit while right-dragging.
        public const float DefaultOrbitSensitivity = 3f;

        // Meters of zoom per scroll notch; seconds of focus follow damping.
        public const float DefaultZoomStep = 1.5f;
        public const float DefaultFocusDamping = 0.12f;

        private const double DegreesToRadians = Math.PI / 180.0;

        // Yaw wraps into [0, 360). NaN falls back to zero.
        public static float NormalizeYaw(float yawDegrees)
        {
            if (float.IsNaN(yawDegrees))
            {
                return 0f;
            }

            var wrapped = yawDegrees % 360f;
            return wrapped < 0f ? wrapped + 360f : wrapped;
        }

        public static float ClampPitch(float pitchDegrees, float minPitch = MinPitch, float maxPitch = MaxPitch)
        {
            return Clamp(pitchDegrees, minPitch, maxPitch);
        }

        public static float ClampDistance(float distance, float minDistance = MinDistance, float maxDistance = MaxDistance)
        {
            return Clamp(distance, minDistance, maxDistance);
        }

        // Offset from the focus point for a camera at the given yaw/pitch and
        // distance. Matches the Unity convention: position = focus + offset
        // and rotation = Euler(pitch, yaw, 0) makes the camera face the focus.
        public static OrbitOffset ComputeOffset(float yawDegrees, float pitchDegrees, float distance)
        {
            var yawRadians = yawDegrees * DegreesToRadians;
            var pitchRadians = pitchDegrees * DegreesToRadians;
            var cosPitch = (float)Math.Cos(pitchRadians);
            var sinPitch = (float)Math.Sin(pitchRadians);
            var cosYaw = (float)Math.Cos(yawRadians);
            var sinYaw = (float)Math.Sin(yawRadians);

            return new OrbitOffset(
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
