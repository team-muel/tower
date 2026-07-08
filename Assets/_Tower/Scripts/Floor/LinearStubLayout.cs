using System;
using System.Collections.Generic;
using Tower.Gen;
using UnityEngine;

namespace Tower.Floor
{
    // Trivial, deterministic IFloorLayoutSource built directly from a FloorGraph so
    // the render/traversal layer compiles and can be exercised standalone (no Core
    // FloorLayout dependency). Nodes are laid out in a straight line along +Z, each
    // with an elongated field; the two fork trails per step bow left/right and are
    // taken verbatim from the graph's outgoing RouteEdges (id + RouteType preserved).
    public sealed class LinearStubLayout : IFloorLayoutSource
    {
        private readonly Dictionary<int, FloorFieldRect> _fields = new Dictionary<int, FloorFieldRect>();
        private readonly Dictionary<int, List<FloorForkTrail>> _forks = new Dictionary<int, List<FloorForkTrail>>();
        private static readonly List<FloorForkTrail> Empty = new List<FloorForkTrail>();

        public LinearStubLayout(FloorGraph graph, float travelLength = 26f, float crossWidth = 15f,
                                float gap = 10f, float forkBow = 5.5f, int waypointsPerTrail = 5)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (travelLength <= 0f) throw new ArgumentOutOfRangeException(nameof(travelLength));

            float stride = travelLength + gap;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                FloorNode node = graph.Nodes[i];
                Vector3 center = new Vector3(0f, 0f, node.Depth * stride);
                _fields[node.Id] = new FloorFieldRect(node.Id, center, travelLength, crossWidth);
            }

            foreach (FloorNode node in graph.Nodes)
            {
                List<RouteEdge> outgoing = new List<RouteEdge>();
                foreach (RouteEdge e in graph.RoutesFrom(node.Id)) outgoing.Add(e);
                if (outgoing.Count == 0) continue;

                List<FloorForkTrail> trails = new List<FloorForkTrail>(outgoing.Count);
                for (int k = 0; k < outgoing.Count; k++)
                {
                    // First fork bows left (-X), second bows right (+X); extra forks fan out.
                    float side = outgoing.Count == 1 ? 0f : (k == 0 ? -1f : 1f) * (1f + k / 2);
                    trails.Add(BuildTrail(outgoing[k], _fields[node.Id], _fields[outgoing[k].ToNodeId],
                                          side * forkBow, waypointsPerTrail));
                }

                _forks[node.Id] = trails;
            }
        }

        private static FloorForkTrail BuildTrail(RouteEdge edge, FloorFieldRect from, FloorFieldRect to,
                                                 float lateralBow, int waypointCount)
        {
            int count = Mathf.Max(2, waypointCount);
            Vector3 start = from.ExitPoint;
            Vector3 end = to.EntryPoint;
            List<Vector3> pts = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)(count - 1);
                // Straight interpolation plus a sine bow so the two forks visibly split.
                Vector3 p = Vector3.Lerp(start, end, t);
                p.x += lateralBow * Mathf.Sin(t * Mathf.PI);
                pts.Add(p);
            }

            return new FloorForkTrail(edge.Id, edge.FromNodeId, edge.ToNodeId, edge.RouteType, pts);
        }

        public FloorFieldRect GetField(int nodeId)
        {
            if (_fields.TryGetValue(nodeId, out FloorFieldRect rect)) return rect;
            throw new ArgumentOutOfRangeException(nameof(nodeId), nodeId, "Unknown node id.");
        }

        public IReadOnlyList<FloorForkTrail> GetForks(int fromNodeId)
        {
            return _forks.TryGetValue(fromNodeId, out List<FloorForkTrail> list) ? list : Empty;
        }
    }
}
