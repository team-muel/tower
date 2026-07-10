using System.Collections.Generic;
using NUnit.Framework;
using Tower.Gen;

namespace Tower.Tests.EditMode
{
    public sealed class FloorGeneratorTests
    {
        [Test]
        public void SameSeedAndParamsProduceStableGraph()
        {
            FloorGenParams parameters = new FloorGenParams(12345);

            FloorGraph first = FloorGenerator.Generate(parameters);
            FloorGraph second = FloorGenerator.Generate(parameters);

            Assert.AreEqual(first.ToStableString(), second.ToStableString());
        }

        [Test]
        public void ToStableStringIsDeterministicAndCoversNodesAndRoutes()
        {
            FloorGenParams parameters = new FloorGenParams(
                9001,
                new IntRange(5, 5),
                false,
                new IntRange(8, 8),
                includeCamp: true,
                biomeId: BiomeId.GhostManor);

            string first = FloorGenerator.Generate(parameters).ToStableString();
            string second = FloorGenerator.Generate(parameters).ToStableString();

            Assert.AreEqual(first, second);
            Assert.IsTrue(first.Contains("|node:"));
            Assert.IsTrue(first.Contains("|route:"));
        }

        [Test]
        public void NodeCountStaysWithinConfiguredRange()
        {
            FloorGenParams parameters = new FloorGenParams(
                13,
                new IntRange(4, 5),
                false,
                new IntRange(8, 14));

            FloorGraph graph = FloorGenerator.Generate(parameters);

            Assert.That(graph.Nodes.Count, Is.InRange(4, 5));
        }

        [Test]
        public void RoutesConnectEveryNode()
        {
            FloorGraph graph = FloorGenerator.Generate(new FloorGenParams(777));

            HashSet<int> reached = Traverse(graph);

            Assert.AreEqual(graph.Nodes.Count, reached.Count);
        }

        [Test]
        public void EveryNonExitNodeOffersAtLeastTwoRoutes()
        {
            FloorGraph graph = FloorGenerator.Generate(new FloorGenParams(
                4242,
                new IntRange(5, 5),
                false,
                new IntRange(8, 8)));

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                FloorNode node = graph.Nodes[i];
                List<RouteEdge> outgoing = new List<RouteEdge>(graph.RoutesFrom(node.Id));
                if (node.Id == graph.ExitNodeId)
                {
                    Assert.AreEqual(0, outgoing.Count, "Exit node has no outgoing routes.");
                }
                else
                {
                    Assert.That(outgoing.Count, Is.GreaterThanOrEqualTo(2), "Each fork offers at least two routes.");
                }
            }
        }

        [Test]
        public void BossFloorMarksExitNodeAsBoss()
        {
            FloorGraph graph = FloorGenerator.Generate(new FloorGenParams(64, true));
            FloorNode exit = graph.NodeById(graph.ExitNodeId);

            Assert.IsTrue(graph.IsBossFloor);
            Assert.IsTrue(exit.IsExit);
            Assert.IsTrue(exit.IsBossRoom);
            Assert.AreEqual(RoomKind.Boss, exit.Kind);
        }

        [Test]
        public void BinderProducesDeterministicRoomDimensions()
        {
            FloorGenParams parameters = new FloorGenParams(
                614,
                new IntRange(4, 4),
                false,
                new IntRange(8, 8));
            FloorGraph graph = FloorGenerator.Generate(parameters);
            FloorNode normal = FindFirstNormal(graph);

            FloorNodeContent first = FloorNodeBinder.Bind(graph, normal, parameters);
            FloorNodeContent second = FloorNodeBinder.Bind(graph, normal, parameters);

            Assert.AreEqual(normal.Id, first.NodeId);
            Assert.AreEqual(8, first.Width);
            Assert.AreEqual(8, first.Height);
            Assert.AreEqual(first.Width, second.Width);
            Assert.AreEqual(first.Height, second.Height);
        }

        [Test]
        public void IncludeCampMarksNodeBeforeExit()
        {
            FloorGenParams parameters = new FloorGenParams(
                451,
                new IntRange(5, 5),
                true,
                new IntRange(8, 8),
                includeCamp: true);

            FloorGraph graph = FloorGenerator.Generate(parameters);
            FloorNode camp = FindCamp(graph);
            FloorNode exit = graph.NodeById(graph.ExitNodeId);

            Assert.AreEqual(RoomKind.Camp, camp.Kind);
            Assert.IsFalse(camp.IsEntrance);
            Assert.IsFalse(camp.IsExit);
            Assert.IsFalse(camp.IsBossRoom);
            Assert.IsTrue(AreConnected(graph, camp.Id, exit.Id));
            Assert.AreEqual(RoomKind.Boss, exit.Kind);
        }

        [Test]
        public void BiomeIdFlowsToGraphTheme()
        {
            FloorGenParams parameters = new FloorGenParams(
                77,
                new IntRange(3, 3),
                false,
                new IntRange(8, 8),
                biomeId: BiomeId.CrystalMine);

            FloorGraph graph = FloorGenerator.Generate(parameters);

            Assert.AreEqual(BiomeId.CrystalMine, graph.BiomeTheme.Id);
            Assert.AreEqual(BiomeTheme.For(BiomeId.CrystalMine), graph.BiomeTheme);
            Assert.Greater(graph.BiomeTheme.FogDensity, 0f);
            Assert.Greater(graph.BiomeTheme.DirectionalLightIntensity, 0f);
        }

        private static HashSet<int> Traverse(FloorGraph graph)
        {
            Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                adjacency[graph.Nodes[i].Id] = new List<int>();
            }

            for (int i = 0; i < graph.Routes.Count; i++)
            {
                RouteEdge route = graph.Routes[i];
                adjacency[route.FromNodeId].Add(route.ToNodeId);
                adjacency[route.ToNodeId].Add(route.FromNodeId);
            }

            HashSet<int> reached = new HashSet<int>();
            Queue<int> open = new Queue<int>();
            open.Enqueue(graph.EntranceNodeId);
            reached.Add(graph.EntranceNodeId);

            while (open.Count > 0)
            {
                int current = open.Dequeue();
                List<int> neighbors = adjacency[current];
                for (int i = 0; i < neighbors.Count; i++)
                {
                    if (reached.Add(neighbors[i]))
                    {
                        open.Enqueue(neighbors[i]);
                    }
                }
            }

            return reached;
        }

        private static FloorNode FindCamp(FloorGraph graph)
        {
            FloorNode camp = null;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i].Kind == RoomKind.Camp)
                {
                    Assert.IsNull(camp, "Only one camp node is expected.");
                    camp = graph.Nodes[i];
                }
            }

            Assert.IsNotNull(camp, "Expected a camp node.");
            return camp;
        }

        private static FloorNode FindFirstNormal(FloorGraph graph)
        {
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i].Kind == RoomKind.Normal)
                {
                    return graph.Nodes[i];
                }
            }

            Assert.Fail("Expected at least one normal node.");
            return null;
        }

        private static bool AreConnected(FloorGraph graph, int nodeAId, int nodeBId)
        {
            for (int i = 0; i < graph.Routes.Count; i++)
            {
                RouteEdge route = graph.Routes[i];
                if ((route.FromNodeId == nodeAId && route.ToNodeId == nodeBId) ||
                    (route.FromNodeId == nodeBId && route.ToNodeId == nodeAId))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
