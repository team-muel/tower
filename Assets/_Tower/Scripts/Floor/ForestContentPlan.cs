using System.Collections.Generic;
using UnityEngine;

namespace Tower.Floor
{
    public enum ForestPropKind
    {
        Tree,
        Rock
    }

    // A single deterministic prop placement. Position is world XZ with Y at the field
    // baseline; the renderer grounds Y onto the terrain height field at spawn time.
    public readonly struct ForestProp
    {
        public ForestProp(ForestPropKind kind, Vector3 position, float radius, float height,
                          int canopyCount, float yawDegrees)
        {
            Kind = kind;
            Position = position;
            Radius = radius;
            Height = height;
            CanopyCount = canopyCount;
            YawDegrees = yawDegrees;
        }

        public ForestPropKind Kind { get; }
        public Vector3 Position { get; }
        public float Radius { get; }      // trunk radius / rock radius
        public float Height { get; }      // trunk height / rock height
        public int CanopyCount { get; }   // canopy spheres (trees only)
        public float YawDegrees { get; }
    }

    // A flat, tree-free clearing inside the field.
    public readonly struct ForestClearing
    {
        public ForestClearing(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public Vector3 Center { get; }
        public float Radius { get; }

        public bool Contains(float x, float z)
        {
            float dx = x - Center.x;
            float dz = z - Center.z;
            return dx * dx + dz * dz <= Radius * Radius;
        }
    }

    // The full deterministic content set for one node's forest segment.
    public sealed class ForestContentPlan
    {
        public ForestContentPlan(IReadOnlyList<ForestProp> trees, IReadOnlyList<ForestProp> rocks,
                                 ForestClearing clearing, IReadOnlyList<Vector3> pathWaypoints)
        {
            Trees = trees;
            Rocks = rocks;
            Clearing = clearing;
            PathWaypoints = pathWaypoints;
        }

        public IReadOnlyList<ForestProp> Trees { get; }
        public IReadOnlyList<ForestProp> Rocks { get; }
        public ForestClearing Clearing { get; }
        public IReadOnlyList<Vector3> PathWaypoints { get; }
    }
}
