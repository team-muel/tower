using System.Collections.Generic;
using UnityEngine;

namespace Tower.Core
{
    // 89 Party Traversal Vision v2 movement contract, rule 1-2: the leader's
    // actual trajectory is recorded as a distance-based breadcrumb stream and
    // each roster slot consumes the same stream at its own distance delay.
    // Deterministic given the same Record calls; no fixed world-offset
    // formation targets live here (rule 3 belongs to the consumer's small
    // path-normal offsets).
    public sealed class BreadcrumbTrail
    {
        public const float DefaultSpacing = 0.25f;
        public const int DefaultCapacity = 256;

        private readonly List<Vector3> points = new List<Vector3>();
        private readonly float spacing;
        private readonly int capacity;

        public BreadcrumbTrail(float pointSpacing = DefaultSpacing, int pointCapacity = DefaultCapacity)
        {
            spacing = Mathf.Max(0.01f, pointSpacing);
            capacity = Mathf.Max(8, pointCapacity);
        }

        public int Count => points.Count;
        public float Spacing => spacing;

        public Vector3 Head => points.Count == 0 ? Vector3.zero : points[points.Count - 1];

        // Appends the leader position once it has moved at least one spacing
        // step from the previous breadcrumb. Returns true when recorded.
        public bool Record(Vector3 leaderPosition)
        {
            if (points.Count == 0)
            {
                points.Add(leaderPosition);
                return true;
            }

            if (Vector3.Distance(points[points.Count - 1], leaderPosition) < spacing)
            {
                return false;
            }

            points.Add(leaderPosition);
            if (points.Count > capacity)
            {
                points.RemoveAt(0);
            }

            return true;
        }

        // Walks backwards from the head along the recorded polyline and
        // returns the point `distanceBehind` ago, displaced by `lateralOffset`
        // along the local path normal (89 rule 3: small alternating offsets,
        // not a formation). Distances beyond the recorded tail clamp to the
        // oldest breadcrumb, so followers can never overtake the leader
        // (89 rule 5).
        public Vector3 Sample(float distanceBehind, float lateralOffset, Vector3 fallback)
        {
            if (points.Count == 0)
            {
                return fallback;
            }

            if (points.Count == 1 || distanceBehind <= 0f)
            {
                return points[points.Count - 1];
            }

            float remaining = distanceBehind;
            for (int index = points.Count - 1; index > 0; index--)
            {
                Vector3 segmentEnd = points[index];
                Vector3 segmentStart = points[index - 1];
                float segmentLength = Vector3.Distance(segmentStart, segmentEnd);
                if (segmentLength <= 0.0001f)
                {
                    continue;
                }

                if (remaining <= segmentLength)
                {
                    float t = 1f - (remaining / segmentLength);
                    Vector3 onPath = Vector3.Lerp(segmentStart, segmentEnd, t);
                    return onPath + (Normal(segmentStart, segmentEnd) * lateralOffset);
                }

                remaining -= segmentLength;
            }

            Vector3 tailStart = points[0];
            Vector3 tailEnd = points[1];
            return tailStart + (Normal(tailStart, tailEnd) * lateralOffset);
        }

        public void Clear()
        {
            points.Clear();
        }

        private static Vector3 Normal(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            return Vector3.Cross(Vector3.up, direction.normalized);
        }
    }
}
