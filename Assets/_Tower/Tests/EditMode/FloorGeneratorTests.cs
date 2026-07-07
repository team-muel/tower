using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;
using Tower.Gen;

namespace Tower.Tests.EditMode
{
    // T30: FloorGenerator now emits a FloorGraph (node+route). Encounters and
    // battlefields are lazily bound through FloorNodeBinder (grid removed from
    // the skeleton), so encounter assertions go through the binder.
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
                new[] { "melee", "ranged" },
                "boss",
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
                new IntRange(8, 14),
                new[] { "melee", "ranged" },
                "boss");

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
            FloorGraph graph = FloorGenerator.Generate(new FloorGenParams(4242, new IntRange(5, 5), false,
                new IntRange(8, 8), new[] { "melee", "ranged" }, "boss"));

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
        public void EntranceNodeHasNoEncounter()
        {
            FloorGenParams parameters = new FloorGenParams(29);
            FloorGraph graph = FloorGenerator.Generate(parameters);

            FloorNode entrance = graph.NodeById(graph.EntranceNodeId);

            Assert.IsTrue(entrance.IsEntrance);
            Assert.AreEqual(RoomKind.Entrance, entrance.Kind);

            FloorNodeContent content = FloorNodeBinder.Bind(graph, entrance, parameters);
            Assert.IsFalse(content.Encounter.HasEncounter);
            Assert.AreEqual(0, content.Encounter.EnemyCount);
        }

        [Test]
        public void BossFloorMakesExitNodeSingleBossEncounter()
        {
            FloorGenParams parameters = new FloorGenParams(64, true);
            FloorGraph graph = FloorGenerator.Generate(parameters);

            FloorNode exit = graph.NodeById(graph.ExitNodeId);

            Assert.IsTrue(graph.IsBossFloor);
            Assert.IsTrue(exit.IsExit);
            Assert.IsTrue(exit.IsBossRoom);
            Assert.AreEqual(RoomKind.Boss, exit.Kind);

            FloorNodeContent content = FloorNodeBinder.Bind(graph, exit, parameters);
            Assert.IsTrue(content.Encounter.HasEncounter);
            Assert.IsTrue(content.Encounter.IsBoss);
            Assert.AreEqual(1, content.Encounter.EnemyCount);
            Assert.AreEqual("boss", content.Encounter.EnemySlots[0].KindSlot);
        }

        [Test]
        public void NonEntranceCombatEncountersFollowResolvedBudget()
        {
            FloorGenParams parameters = new FloorGenParams(
                90210,
                new IntRange(5, 5),
                false,
                new IntRange(8, 8),
                new[] { "melee", "ranged" },
                "boss");

            FloorGraph graph = FloorGenerator.Generate(parameters);

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                FloorNode node = graph.Nodes[i];
                if (node.IsEntrance || node.Kind == RoomKind.Camp || node.IsBossRoom)
                {
                    continue;
                }

                FloorNodeContent content = FloorNodeBinder.Bind(graph, node, parameters);
                int expected = ExpectedBudgetCount(parameters, node);
                Assert.AreEqual(expected, content.Encounter.EnemyCount);
                Assert.AreEqual(content.Encounter.EnemyCount, content.Encounter.EnemySlots.Count);
            }
        }

        [Test]
        public void BinderUsesResolvedEncounterBudget()
        {
            var baseBudget = new EncounterBudget(
                baseDifficulty: 10,
                depthDifficultyRamp: 0,
                activeEnemyCapBase: 1f,
                activeEnemyCapDepthRamp: 0f,
                activeEnemyCapMax: 1,
                minTypes: 1,
                maxTypes: 1,
                typeCountDepthRamp: 0f,
                minWaves: 1,
                maxWaves: 1,
                eliteCap: 0);
            var table = new EncounterBudgetTable(baseBudget);
            table.SetRoomKindOverride("Normal", new EncounterBudgetOverride
            {
                BaseDifficulty = 40,
                ActiveEnemyCapBase = 4f,
                ActiveEnemyCapMax = 4,
                MaxTypes = 2
            });

            FloorGenParams parameters = new FloorGenParams(
                614,
                new IntRange(4, 4),
                false,
                new IntRange(8, 8),
                new[] { "melee", "ranged" },
                "boss",
                encounterBudgetTable: table);
            FloorGraph graph = FloorGenerator.Generate(parameters);
            FloorNode normal = FindFirstNormal(graph);

            FloorNodeContent content = FloorNodeBinder.Bind(graph, normal, parameters);

            Assert.AreEqual(4, content.Encounter.EnemyCount);
        }

        [Test]
        public void IncludeCampMarksNodeBeforeExitWithNoEncounter()
        {
            FloorGenParams parameters = new FloorGenParams(
                451,
                new IntRange(5, 5),
                true,
                new IntRange(8, 8),
                new[] { "melee", "ranged" },
                "boss",
                includeCamp: true);

            FloorGraph graph = FloorGenerator.Generate(parameters);
            FloorNode camp = FindCamp(graph);
            FloorNode exit = graph.NodeById(graph.ExitNodeId);

            Assert.AreEqual(RoomKind.Camp, camp.Kind);
            Assert.IsFalse(camp.IsEntrance);
            Assert.IsFalse(camp.IsExit);
            Assert.IsFalse(camp.IsBossRoom);

            FloorNodeContent content = FloorNodeBinder.Bind(graph, camp, parameters);
            Assert.IsFalse(content.Encounter.HasEncounter);
            Assert.AreEqual(0, content.Encounter.EnemyCount);
            Assert.IsTrue(AreConnected(graph, camp.Id, exit.Id));
            Assert.AreEqual(RoomKind.Boss, exit.Kind);
        }

        [Test]
        public void IncludeCampRemainsDeterministicForSameSeed()
        {
            FloorGenParams parameters = new FloorGenParams(
                912,
                new IntRange(4, 5),
                false,
                new IntRange(8, 14),
                new[] { "melee", "ranged", "elite" },
                "boss",
                includeCamp: true);

            FloorGraph first = FloorGenerator.Generate(parameters);
            FloorGraph second = FloorGenerator.Generate(parameters);

            Assert.AreEqual(first.ToStableString(), second.ToStableString());
            Assert.AreEqual(FindCamp(first).Id, FindCamp(second).Id);
        }

        [Test]
        public void BiomeIdFlowsToGraphTheme()
        {
            FloorGenParams parameters = new FloorGenParams(
                77,
                new IntRange(3, 3),
                false,
                new IntRange(8, 8),
                new[] { "melee" },
                "boss",
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

        private static int ExpectedBudgetCount(FloorGenParams parameters, FloorNode node)
        {
            EncounterBudget budget = parameters.EncounterBudgetTable.Resolve(
                parameters.BiomeId.ToString(),
                node.Kind.ToString());
            int cap = budget.ActiveEnemyCapAt(node.Depth);
            int difficultyCount = budget.DifficultyAt(node.Depth) / FloorEncounterComposer.DifficultyPerEnemy;
            if (difficultyCount < 1)
            {
                difficultyCount = 1;
            }

            int count = cap < difficultyCount ? cap : difficultyCount;
            return count < 1 ? 1 : count;
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
    }
}
