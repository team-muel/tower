using NUnit.Framework;
using Tower.Floor;
using Tower.Gen;

namespace Tower.Tests.EditMode
{
    // T40b: pure trail-hit -> next-node resolution over a real FloorGraph.
    public sealed class TrailNavigatorTests
    {
        private static FloorGraph Graph(int seed = 4242, int nodes = 4)
        {
            FloorGenParams p = new FloorGenParams(
                seed, new IntRange(nodes, nodes), false, new IntRange(8, 14),
                new[] { "melee", "ranged" }, "boss",
                includeCamp: true, biomeId: BiomeId.Forest);
            return FloorGenerator.Generate(p);
        }

        [Test]
        public void EachNonExitNodeHasExactlyTwoForks()
        {
            FloorGraph g = Graph();
            for (int id = 0; id < g.ExitNodeId; id++)
            {
                Assert.AreEqual(2, TrailNavigator.ForksAt(g, id).Count,
                    $"Node {id} should expose exactly two fork trails.");
            }

            Assert.AreEqual(0, TrailNavigator.ForksAt(g, g.ExitNodeId).Count,
                "The exit node has no outgoing forks.");
        }

        [Test]
        public void ForkResolvesToTheNextNode()
        {
            FloorGraph g = Graph();
            TrailNavigator.ForkResolution res = TrailNavigator.ResolveByIndex(g, 0, 1);
            Assert.IsTrue(res.Found);
            Assert.AreEqual(0, res.FromNodeId);
            Assert.AreEqual(1, res.ToNodeId, "Both forks lead into the same next node (DD2 rule).");
        }

        [Test]
        public void BothForksLeadToSameNextNode()
        {
            FloorGraph g = Graph();
            var a = TrailNavigator.ResolveByIndex(g, 1, 0);
            var b = TrailNavigator.ResolveByIndex(g, 1, 1);
            Assert.AreEqual(a.ToNodeId, b.ToNodeId);
            Assert.AreNotEqual(a.RouteId, b.RouteId, "The two forks are distinct route edges.");
        }

        [Test]
        public void ResolveByRouteIdMatchesResolveByIndex()
        {
            FloorGraph g = Graph();
            var byIndex = TrailNavigator.ResolveByIndex(g, 2, 0);
            var byId = TrailNavigator.ResolveByRouteId(g, byIndex.RouteId);
            Assert.IsTrue(byId.Found);
            Assert.AreEqual(byIndex.ToNodeId, byId.ToNodeId);
            Assert.AreEqual(byIndex.RouteType, byId.RouteType);
        }

        [Test]
        public void ArrivesAtExitFlagsTheFinalStep()
        {
            FloorGraph g = Graph();
            int penultimate = g.ExitNodeId - 1;
            var res = TrailNavigator.ResolveByIndex(g, penultimate, 0);
            Assert.IsTrue(res.ArrivesAtExit, "Forks into the exit node must flag Ascend.");
            var early = TrailNavigator.ResolveByIndex(g, 0, 0);
            Assert.IsFalse(early.ArrivesAtExit);
        }

        [Test]
        public void UnknownRouteIdReturnsNotFound()
        {
            FloorGraph g = Graph();
            var res = TrailNavigator.ResolveByRouteId(g, 99999);
            Assert.IsFalse(res.Found);
        }

        [Test]
        public void CampNodeIsFlaggedAsEvent()
        {
            FloorGraph g = Graph();
            bool anyCamp = false;
            foreach (FloorNode n in g.Nodes)
            {
                if (n.Kind == RoomKind.Camp)
                {
                    anyCamp = true;
                    Assert.IsTrue(TrailNavigator.IsEventNode(n));
                }
            }

            Assert.IsTrue(anyCamp, "includeCamp graph should contain a Camp (event) node.");
        }
    }
}
