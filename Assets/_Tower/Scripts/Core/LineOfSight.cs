using System;
using UnityEngine;

namespace Tower.Core
{
    public static class LineOfSight
    {
        // v0: Bresenham line between the endpoints; any blocked cell strictly
        // between caster and target breaks line of sight. Endpoints never block.
        // Hybrid: If running inside Unity scene with valid physics, uses Raycast against Obstacle layer.
        public static bool IsClear(GridMap map, GridPos from, GridPos to)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (Application.isPlaying && Physics.defaultPhysicsScene.IsValid())
            {
                Vector3 start = new Vector3(from.X, 0.5f, from.Y);
                Vector3 end = new Vector3(to.X, 0.5f, to.Y);
                Vector3 direction = end - start;
                float distance = direction.magnitude;

                int mask = LayerMask.GetMask("Obstacle");
                if (mask <= 0)
                {
                    mask = 1 << 8; // fallback to layer 8
                }

                if (Physics.Raycast(start, direction.normalized, out RaycastHit hit, distance, mask))
                {
                    return false; // Blocked by physical obstacle
                }
            }

            var x = from.X;
            var y = from.Y;
            var deltaX = Math.Abs(to.X - from.X);
            var deltaY = -Math.Abs(to.Y - from.Y);
            var stepX = from.X < to.X ? 1 : -1;
            var stepY = from.Y < to.Y ? 1 : -1;
            var error = deltaX + deltaY;

            while (true)
            {
                var isEndpoint = (x == from.X && y == from.Y) || (x == to.X && y == to.Y);
                if (!isEndpoint && map.IsBlocked(new GridPos(x, y)))
                {
                    return false;
                }

                if (x == to.X && y == to.Y)
                {
                    return true;
                }

                var doubledError = 2 * error;
                if (doubledError >= deltaY)
                {
                    error += deltaY;
                    x += stepX;
                }

                if (doubledError <= deltaX)
                {
                    error += deltaX;
                    y += stepY;
                }
            }
        }
    }
}
