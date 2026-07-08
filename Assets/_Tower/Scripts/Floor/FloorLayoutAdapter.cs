using System;
using System.Collections.Generic;
using Tower.Gen;
using UnityEngine;

namespace Tower.Floor
{
    // Thin harvest adapter: maps the Core FloorLayout (Tower.Gen, T40a) onto the
    // renderer's decoupled IFloorLayoutSource (T40b). Travel axis = world +Z,
    // cross axis = X, matching both sides' conventions. RouteId is preserved so
    // traversal (which resolves by route id) lines up with graph.Routes[].Id.
    public sealed class FloorLayoutAdapter : IFloorLayoutSource
    {
        private readonly FloorLayout _layout;
        private readonly Dictionary<int, List<FloorForkTrail>> _forksByFrom;

        public FloorLayoutAdapter(FloorLayout layout)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _forksByFrom = new Dictionary<int, List<FloorForkTrail>>();

            for (int i = 0; i < _layout.Trails.Count; i++)
            {
                FloorLayout.RouteTrail trail = _layout.Trails[i];
                if (!_forksByFrom.TryGetValue(trail.FromNodeId, out List<FloorForkTrail> list))
                {
                    list = new List<FloorForkTrail>(2);
                    _forksByFrom[trail.FromNodeId] = list;
                }

                list.Add(new FloorForkTrail(
                    trail.RouteId,
                    trail.FromNodeId,
                    trail.ToNodeId,
                    trail.RouteType,
                    ToVectors(trail.Waypoints)));
            }
        }

        public static FloorLayoutAdapter FromGraph(FloorGraph graph)
        {
            return new FloorLayoutAdapter(FloorLayout.Generate(graph));
        }

        public FloorFieldRect GetField(int nodeId)
        {
            FloorLayout.NodeLayout node = _layout.NodeById(nodeId);
            Vector3 center = new Vector3(node.Position.X, node.Position.Y, node.Position.Z);
            // Travel axis = +Z (SizeZ), cross axis = X (SizeX).
            return new FloorFieldRect(nodeId, center, node.FieldSize.SizeZ, node.FieldSize.SizeX);
        }

        public IReadOnlyList<FloorForkTrail> GetForks(int fromNodeId)
        {
            if (_forksByFrom.TryGetValue(fromNodeId, out List<FloorForkTrail> list))
            {
                return list;
            }

            return Array.Empty<FloorForkTrail>();
        }

        private static IReadOnlyList<Vector3> ToVectors(IReadOnlyList<FloorLayout.Vec3> source)
        {
            List<Vector3> result = new List<Vector3>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                result.Add(new Vector3(source[i].X, source[i].Y, source[i].Z));
            }

            return result;
        }
    }
}
