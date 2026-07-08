using System;
using System.Collections.Generic;
using Tower.Gen;

namespace Tower.Floor
{
    // Pure, engine-agnostic resolution of "player entered a trail" -> "which node do
    // we arrive at". Kept out of the MonoBehaviour so the traversal rule is unit
    // testable. Never mutates the graph; exploration state is marked by the caller.
    public static class TrailNavigator
    {
        public readonly struct ForkResolution
        {
            public ForkResolution(bool found, int routeId, int fromNodeId, int toNodeId,
                                  RouteType routeType, bool arrivesAtExit)
            {
                Found = found;
                RouteId = routeId;
                FromNodeId = fromNodeId;
                ToNodeId = toNodeId;
                RouteType = routeType;
                ArrivesAtExit = arrivesAtExit;
            }

            public bool Found { get; }
            public int RouteId { get; }
            public int FromNodeId { get; }
            public int ToNodeId { get; }
            public RouteType RouteType { get; }
            public bool ArrivesAtExit { get; }
        }

        // Outgoing fork edges from a node, in stable Routes order (two per step in v0).
        public static IReadOnlyList<RouteEdge> ForksAt(FloorGraph graph, int fromNodeId)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            List<RouteEdge> forks = new List<RouteEdge>();
            foreach (RouteEdge e in graph.RoutesFrom(fromNodeId)) forks.Add(e);
            return forks;
        }

        // Resolve by ordinal fork index (0 = first/left, 1 = second/right).
        public static ForkResolution ResolveByIndex(FloorGraph graph, int fromNodeId, int forkIndex)
        {
            IReadOnlyList<RouteEdge> forks = ForksAt(graph, fromNodeId);
            if (forkIndex < 0 || forkIndex >= forks.Count) return default;
            return Resolve(graph, forks[forkIndex]);
        }

        // Resolve by the RouteEdge id that a trail carries (what a trigger stores).
        public static ForkResolution ResolveByRouteId(FloorGraph graph, int routeId)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            for (int i = 0; i < graph.Routes.Count; i++)
            {
                if (graph.Routes[i].Id == routeId) return Resolve(graph, graph.Routes[i]);
            }

            return default;
        }

        private static ForkResolution Resolve(FloorGraph graph, RouteEdge edge)
        {
            bool arrivesAtExit = edge.ToNodeId == graph.ExitNodeId;
            return new ForkResolution(true, edge.Id, edge.FromNodeId, edge.ToNodeId, edge.RouteType, arrivesAtExit);
        }

        public static bool IsExit(FloorGraph graph, int nodeId)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            return nodeId == graph.ExitNodeId;
        }

        // Event-field placeholder rule (v0): the hand-crafted Camp room stands in for
        // the future hand-authored event field. Marker only, no interaction yet.
        public static bool IsEventNode(FloorNode node)
        {
            return node != null && node.Kind == RoomKind.Camp;
        }
    }
}
