using UnityEngine;

namespace Tower.Core
{
    public static class PlayerLocomotion
    {
        public static float PlanarSpeed(Vector3 previous, Vector3 current, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return 0f;
            }

            Vector3 delta = current - previous;
            delta.y = 0f;
            return delta.magnitude / deltaTime;
        }

        public static float SpeedFactor(float planarSpeed, float moveSpeed)
        {
            if (moveSpeed <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(planarSpeed / moveSpeed);
        }
    }
}
