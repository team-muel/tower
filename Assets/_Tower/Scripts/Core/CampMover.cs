using System;

namespace Tower.Core
{
    // Pure move-to-point math for the camp regressor (right-click movement).
    // Kept engine-free so arrival behavior is unit-testable.
    public static class CampMover
    {
        // Close enough to snap to the destination and stop.
        public const float ArrivalEpsilon = 0.05f;

        public static CampMoveStep StepTowards(float x, float z, float destX, float destZ, float speed, float deltaTime)
        {
            float dx = destX - x;
            float dz = destZ - z;
            float distance = (float)Math.Sqrt((dx * dx) + (dz * dz));
            if (distance <= ArrivalEpsilon)
            {
                return new CampMoveStep(destX, destZ, true);
            }

            float step = speed * deltaTime;
            if (step <= 0f)
            {
                return new CampMoveStep(x, z, false);
            }

            if (step >= distance)
            {
                // Never overshoot: clamp the final step onto the destination.
                return new CampMoveStep(destX, destZ, true);
            }

            float scale = step / distance;
            return new CampMoveStep(x + (dx * scale), z + (dz * scale), false);
        }
    }
}
