using System.Collections.Generic;
using Tower.Gen;
using UnityEngine;

namespace Tower.Floor
{
    // The renderer's own, minimal spatial-layout input contract. It is intentionally
    // DECOUPLED from any Core FloorLayout type (T40a): the orchestrator writes a thin
    // adapter that maps a Core FloorLayout onto this interface. A trivial standalone
    // implementation (LinearStubLayout) lets this layer compile and run on its own.
    public interface IFloorLayoutSource
    {
        // World-space elongated field for a node (center + travel/cross extents).
        FloorFieldRect GetField(int nodeId);

        // The two visible fork trails leaving a node (node i exit -> node i+1 entry),
        // in stable step order. Returns an empty list for the exit node.
        IReadOnlyList<FloorForkTrail> GetForks(int fromNodeId);
    }

    // Elongated, world-space battlefield rect for a node. Travel axis = local +Z,
    // cross axis = local X. Elongation = CrossWidth / TravelLength (< 1 => the field
    // is longer along the travel axis, per the T40 "길쭉한 필드" rule).
    public readonly struct FloorFieldRect
    {
        public FloorFieldRect(int nodeId, Vector3 center, float travelLength, float crossWidth)
        {
            NodeId = nodeId;
            Center = center;
            TravelLength = travelLength;
            CrossWidth = crossWidth;
            Elongation = travelLength > 0f ? crossWidth / travelLength : 1f;
        }

        public int NodeId { get; }
        public Vector3 Center { get; }
        public float TravelLength { get; }
        public float CrossWidth { get; }
        public float Elongation { get; }

        public float MinX => Center.x - CrossWidth * 0.5f;
        public float MaxX => Center.x + CrossWidth * 0.5f;
        public float MinZ => Center.z - TravelLength * 0.5f;
        public float MaxZ => Center.z + TravelLength * 0.5f;

        // Party walks the field from the entry edge (-Z) to the exit edge (+Z).
        public Vector3 EntryPoint => new Vector3(Center.x, Center.y, MinZ);
        public Vector3 ExitPoint => new Vector3(Center.x, Center.y, MaxZ);

        public bool ContainsXZ(float x, float z)
        {
            return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
        }
    }

    // One visible fork trail: the walkable path for a single RouteEdge, carrying the
    // RouteType so the renderer can tint/mark it in-world (the diegetic choice).
    public readonly struct FloorForkTrail
    {
        public FloorForkTrail(int routeId, int fromNodeId, int toNodeId, RouteType routeType,
                              IReadOnlyList<Vector3> waypoints)
        {
            RouteId = routeId;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            RouteType = routeType;
            Waypoints = waypoints;
        }

        public int RouteId { get; }
        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public RouteType RouteType { get; }

        // Ordered world waypoints: node i exit edge -> node i+1 entry edge.
        public IReadOnlyList<Vector3> Waypoints { get; }
    }
}
