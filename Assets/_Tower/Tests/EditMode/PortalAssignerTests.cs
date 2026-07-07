using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Tower.Core;
using Tower.Gen;

namespace Tower.Tests.EditMode
{
    // T30: PortalAssigner now assigns per-node RouteOffer/RouteOption over a
    // FloorGraph's outgoing routes (the DD2 fork), replacing the old per-door
    // PortalDef assignment. New model types have public constructors, so the
    // hand-built graph cases no longer need reflection.
    public sealed class PortalAssignerTests
    {
        [Test]
        public void AssignForNode_SameSeedProducesDeterministicOffer()
        {
            var firstGraph = FloorGenerator.Generate(FixedParams(250125));
            var secondGraph = FloorGenerator.Generate(FixedParams(250125));
            var firstNode = FirstNodeWithRoutes(firstGraph);
            var secondNode = secondGraph.NodeById(firstNode.Id);

            var firstOffer = PortalAssigner.AssignForNode(firstGraph, firstNode);
            var secondOffer = PortalAssigner.AssignForNode(secondGraph, secondNode);

            Assert.That(Signature(firstOffer), Is.EqualTo(Signature(secondOffer)));
        }

        [Test]
        public void AssignForNode_OfferCoversEveryOutgoingRoute()
        {
            int[] seeds = { 250125, 252525, 640064, 770077 };
            for (var seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
            {
                var graph = FloorGenerator.Generate(FixedParams(seeds[seedIndex], isBossFloor: seedIndex == 2));
                for (var nodeIndex = 0; nodeIndex < graph.Nodes.Count; nodeIndex++)
                {
                    var node = graph.Nodes[nodeIndex];
                    var outgoing = new List<RouteEdge>(graph.RoutesFrom(node.Id));
                    var offer = PortalAssigner.AssignForNode(graph, node);

                    Assert.That(offer.NodeId, Is.EqualTo(node.Id));
                    Assert.That(offer.Count, Is.EqualTo(outgoing.Count));
                    for (var i = 0; i < outgoing.Count; i++)
                    {
                        var option = offer.ForRoute(outgoing[i].Id);
                        Assert.That(option, Is.Not.Null, "Missing option for route " + outgoing[i].Id);
                        Assert.That(option.RouteId, Is.EqualTo(outgoing[i].Id));
                        Assert.That(option.ToNodeId, Is.EqualTo(outgoing[i].ToNodeId));
                    }
                }
            }
        }

        [Test]
        public void AssignForNode_SuppressesConsecutiveRewardTypesWhenAlternativesExist()
        {
            var graph = FloorGenerator.Generate(FixedParams(12345));
            var node = graph.NodeById(0);
            var offer = PortalAssigner.AssignForNode(graph, node);

            Assert.That(offer.Count, Is.GreaterThanOrEqualTo(2));
            for (var index = 1; index < offer.Count; index++)
            {
                Assert.That(
                    offer.Options[index].RewardType,
                    Is.Not.EqualTo(offer.Options[index - 1].RewardType),
                    Signature(offer));
            }
        }

        [Test]
        public void AssignForNode_RerollAllowedOnlyForUnlockedHealOrResource()
        {
            var checkedOptions = 0;
            int[] seeds = { 20260706, 250125, 451451, 90210, 770077 };
            for (var seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
            {
                var graph = FloorGenerator.Generate(FixedParams(seeds[seedIndex], includeCamp: seedIndex % 2 == 0));
                for (var nodeIndex = 0; nodeIndex < graph.Nodes.Count; nodeIndex++)
                {
                    var offer = PortalAssigner.AssignForNode(graph, graph.Nodes[nodeIndex]);
                    for (var optionIndex = 0; optionIndex < offer.Options.Count; optionIndex++)
                    {
                        var option = offer.Options[optionIndex];
                        var expected = !option.IsLocked
                            && (option.RewardType == RewardType.Heal || option.RewardType == RewardType.Resource);

                        Assert.That(option.RerollAllowed, Is.EqualTo(expected), Signature(option));
                        checkedOptions++;
                    }
                }
            }

            Assert.That(checkedOptions, Is.GreaterThan(0));
        }

        [Test]
        public void AssignForNode_BossRouteIsBossGated()
        {
            var graph = FloorGenerator.Generate(FixedParams(640064, isBossFloor: true));
            var node = graph.NodeById(graph.ExitNodeId - 1);
            var offer = PortalAssigner.AssignForNode(graph, node);

            RouteOption bossOption = null;
            for (var i = 0; i < offer.Options.Count; i++)
            {
                if (offer.Options[i].ToKind == RoomKind.Boss)
                {
                    bossOption = offer.Options[i];
                    break;
                }
            }

            Assert.That(bossOption, Is.Not.Null, "Expected a route to the boss node.");
            Assert.That(bossOption.LockReason, Is.EqualTo(PortalLockReason.BossGated));
            Assert.That(bossOption.IsLocked, Is.True);
        }

        [Test]
        public void AssignForNode_AbilityRewardRequiresKey()
        {
            RouteOption ability = null;
            int[] seeds = { 12345, 250125, 90210, 451, 777, 20260706 };
            for (var s = 0; s < seeds.Length && ability == null; s++)
            {
                var graph = FloorGenerator.Generate(FixedParams(seeds[s]));
                for (var nodeIndex = 0; nodeIndex < graph.Nodes.Count && ability == null; nodeIndex++)
                {
                    var offer = PortalAssigner.AssignForNode(graph, graph.Nodes[nodeIndex]);
                    for (var i = 0; i < offer.Options.Count; i++)
                    {
                        if (offer.Options[i].RewardType == RewardType.Ability)
                        {
                            ability = offer.Options[i];
                            break;
                        }
                    }
                }
            }

            Assert.That(ability, Is.Not.Null, "Expected at least one Ability route option.");
            Assert.That(ability.LockReason, Is.EqualTo(PortalLockReason.RequiresKey));
            Assert.That(ability.IsLocked, Is.True);
        }

        [Test]
        public void AssignForRoute_DeepRouteIsDepthGated()
        {
            var from = new FloorNode(0, 0, RoomKind.Normal, "test/normal/0", true, false, false);
            var deep = new FloorNode(1, 3, RoomKind.Normal, "test/normal/1", false, false, false);
            var route = new RouteEdge(0, 0, 1, RouteType.Combat);
            var graph = new FloorGraph(
                404040,
                false,
                new[] { from, deep },
                new[] { route },
                0,
                1,
                BiomeTheme.For(BiomeId.Forest));

            var option = PortalAssigner.AssignForRoute(graph, from, route, 0);

            Assert.That(option.ToDepth, Is.EqualTo(3));
            Assert.That(option.LockReason, Is.EqualTo(PortalLockReason.DepthGated));
            Assert.That(option.IsLocked, Is.True);
        }

        private static FloorGenParams FixedParams(int seed, bool isBossFloor = false, bool includeCamp = false)
        {
            return new FloorGenParams(
                seed,
                new IntRange(5, 5),
                isBossFloor,
                new IntRange(8, 8),
                new[] { "melee", "ranged", "elite" },
                "boss",
                includeCamp);
        }

        private static FloorNode FirstNodeWithRoutes(FloorGraph graph)
        {
            for (var index = 0; index < graph.Nodes.Count; index++)
            {
                foreach (var route in graph.RoutesFrom(graph.Nodes[index].Id))
                {
                    return graph.Nodes[index];
                }
            }

            Assert.Fail("Expected at least one node with routes.");
            return null;
        }

        private static string Signature(RouteOffer offer)
        {
            var parts = new List<string>();
            for (var index = 0; index < offer.Options.Count; index++)
            {
                parts.Add(Signature(offer.Options[index]));
            }

            return string.Join("|", parts);
        }

        private static string Signature(RouteOption option)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}:{1}:{2}:{3}:{4}:{5}:{6}:{7}:{8}:{9}:{10}",
                option.RouteId,
                option.RouteType,
                option.ToNodeId,
                option.ToKind,
                option.ToBiome,
                option.ToDepth,
                option.RewardType,
                option.RewardMagnitude,
                option.RiskTags,
                option.LockReason,
                option.RerollAllowed);
        }
    }
}
