using System;
using System.Collections.Generic;
using System.Text;

namespace Tower.Gen
{
    // 층계 골격: 노드 + 갈림길(route). 층 진입 시 사전생성(DC-4=C). 결정적.
    public sealed class FloorGraph
    {
        public FloorGraph(int seed, bool isBossFloor, IReadOnlyList<FloorNode> nodes,
                          IReadOnlyList<RouteEdge> routes, int entranceNodeId, int exitNodeId, BiomeTheme biomeTheme)
        {
            if (nodes == null) throw new ArgumentNullException(nameof(nodes));
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            if (nodes.Count == 0) throw new ArgumentException("Floor graph requires at least one node.", nameof(nodes));
            if (biomeTheme == null) throw new ArgumentNullException(nameof(biomeTheme));

            Seed = seed; IsBossFloor = isBossFloor;
            Nodes = new List<FloorNode>(nodes); Routes = new List<RouteEdge>(routes);
            EntranceNodeId = entranceNodeId; ExitNodeId = exitNodeId; BiomeTheme = biomeTheme;
        }

        public int Seed { get; }
        public bool IsBossFloor { get; }
        public IReadOnlyList<FloorNode> Nodes { get; }
        public IReadOnlyList<RouteEdge> Routes { get; }
        public int EntranceNodeId { get; }
        public int ExitNodeId { get; }
        public BiomeTheme BiomeTheme { get; }

        // 노드의 갈림길(나가는 route) = outgoing routes.
        public IEnumerable<RouteEdge> RoutesFrom(int nodeId)
        {
            for (int i = 0; i < Routes.Count; i++)
                if (Routes[i].FromNodeId == nodeId) yield return Routes[i];
        }

        public FloorNode NodeById(int nodeId)
        {
            for (int i = 0; i < Nodes.Count; i++)
                if (Nodes[i].Id == nodeId) return Nodes[i];
            return null;
        }

        public string ToStableString()
        {
            StringBuilder b = new StringBuilder();
            b.Append("seed=").Append(Seed).Append(";boss=").Append(IsBossFloor)
             .Append(";entrance=").Append(EntranceNodeId).Append(";exit=").Append(ExitNodeId)
             .Append(";biome=").Append(BiomeTheme.Id);
            for (int i = 0; i < Nodes.Count; i++)
            {
                FloorNode n = Nodes[i];
                b.Append("|node:").Append(n.Id).Append(',').Append(n.Depth).Append(',')
                 .Append(n.Kind).Append(',').Append(n.RoomTemplateId).Append(',')
                 .Append(n.IsEntrance).Append(',').Append(n.IsExit).Append(',').Append(n.IsBossRoom);
            }
            for (int i = 0; i < Routes.Count; i++)
            {
                RouteEdge r = Routes[i];
                b.Append("|route:").Append(r.Id).Append(':').Append(r.FromNodeId).Append('-')
                 .Append(r.ToNodeId).Append(',').Append(r.RouteType);
            }
            return b.ToString();
        }
    }
}
