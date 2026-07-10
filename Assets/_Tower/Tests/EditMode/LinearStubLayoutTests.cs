using System.Collections.Generic;
using NUnit.Framework;
using Tower.Floor;
using Tower.Gen;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    // T40b: the standalone IFloorLayoutSource stub the renderer runs on until the
    // Core FloorLayout adapter is wired. Validates the interface contract shape.
    public sealed class LinearStubLayoutTests
    {
        private static FloorGraph Graph(int seed = 2024, int nodes = 4)
        {
            FloorGenParams p = new FloorGenParams(
                seed, new IntRange(nodes, nodes), false, new IntRange(8, 14),
                new[] { "melee", "ranged" }, "boss",
                includeCamp: false, biomeId: BiomeId.Forest);
            return FloorGenerator.Generate(p);
        }

        [Test]
        public void EachStepHasExactlyTwoForkTrails()
        {
            FloorGraph g = Graph();
            IFloorLayoutSource layout = new LinearStubLayout(g);
            for (int id = 0; id < g.ExitNodeId; id++)
            {
                Assert.AreEqual(2, layout.GetForks(id).Count);
            }

            Assert.AreEqual(0, layout.GetForks(g.ExitNodeId).Count);
        }

        [Test]
        public void TrailsConnectNodeIToNodeIPlusOne()
        {
            FloorGraph g = Graph();
            IFloorLayoutSource layout = new LinearStubLayout(g);
            IReadOnlyList<FloorForkTrail> forks = layout.GetForks(1);
            foreach (FloorForkTrail trail in forks)
            {
                Assert.AreEqual(1, trail.FromNodeId);
                Assert.AreEqual(2, trail.ToNodeId);
                Assert.GreaterOrEqual(trail.Waypoints.Count, 2);

                // First waypoint sits at node 1's exit edge, last at node 2's entry edge.
                FloorFieldRect from = layout.GetField(1);
                FloorFieldRect to = layout.GetField(2);
                Assert.AreEqual(from.ExitPoint.z, trail.Waypoints[0].z, 0.001f);
                Assert.AreEqual(to.EntryPoint.z, trail.Waypoints[trail.Waypoints.Count - 1].z, 0.001f);
            }
        }

        [Test]
        public void ForkTrailsPreserveRouteIdentity()
        {
            FloorGraph g = Graph();
            IFloorLayoutSource layout = new LinearStubLayout(g);
            List<RouteEdge> edges = new List<RouteEdge>(g.RoutesFrom(0));
            IReadOnlyList<FloorForkTrail> trails = layout.GetForks(0);
            Assert.AreEqual(edges.Count, trails.Count);
            for (int i = 0; i < edges.Count; i++)
            {
                Assert.AreEqual(edges[i].Id, trails[i].RouteId);
                Assert.AreEqual(edges[i].RouteType, trails[i].RouteType);
            }
        }

        [Test]
        public void TwoForksSplitLaterally()
        {
            FloorGraph g = Graph();
            IFloorLayoutSource layout = new LinearStubLayout(g, forkBow: 6f);
            IReadOnlyList<FloorForkTrail> forks = layout.GetForks(0);
            int mid = forks[0].Waypoints.Count / 2;
            float xa = forks[0].Waypoints[mid].x;
            float xb = forks[1].Waypoints[mid].x;
            Assert.Less(xa, xb, "The first fork should bow left of the second.");
        }

        [Test]
        public void FieldsAreElongatedAlongTravelAxis()
        {
            FloorGraph g = Graph();
            IFloorLayoutSource layout = new LinearStubLayout(g, travelLength: 26f, crossWidth: 15f);
            FloorFieldRect field = layout.GetField(0);
            Assert.Greater(field.TravelLength, field.CrossWidth);
            Assert.Less(field.Elongation, 1f);
        }

        [Test]
        public void NodesAreSpacedAlongTheTravelAxis()
        {
            FloorGraph g = Graph();
            IFloorLayoutSource layout = new LinearStubLayout(g, travelLength: 26f, crossWidth: 15f, gap: 10f);
            float z0 = layout.GetField(0).Center.z;
            float z1 = layout.GetField(1).Center.z;
            Assert.AreEqual(36f, z1 - z0, 0.001f, "Stride = travelLength + gap.");
        }
    }
}
