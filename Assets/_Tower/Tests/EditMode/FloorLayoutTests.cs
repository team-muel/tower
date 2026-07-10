using System.Collections.Generic;
using NUnit.Framework;
using Tower.Gen;

namespace Tower.Tests.EditMode
{
    public sealed class FloorLayoutTests
    {
        [Test]
        public void SameSeedAndGraphProduceStableLayout()
        {
            FloorGraph graph = FixedGraph(24680);

            FloorLayout first = FloorLayout.Generate(graph);
            FloorLayout second = FloorLayout.Generate(graph);

            Assert.AreEqual(first.ToStableString(), second.ToStableString());
        }

        [Test]
        public void NodeCountMatchesGraph()
        {
            FloorGraph graph = FixedGraph(13579);
            FloorLayout layout = FloorLayout.Generate(graph);

            Assert.AreEqual(graph.Nodes.Count, layout.Nodes.Count);
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                Assert.AreEqual(graph.Nodes[i].Id, layout.Nodes[i].NodeId);
                Assert.AreEqual(i, layout.Nodes[i].StepIndex);
            }
        }

        [Test]
        public void EveryProgressionStepHasExactlyTwoForkTrails()
        {
            FloorGraph graph = FixedGraph(777);
            FloorLayout layout = FloorLayout.Generate(graph);

            for (int step = 0; step < graph.Nodes.Count - 1; step++)
            {
                List<FloorLayout.RouteTrail> trails = layout.TrailsForStep(step);

                Assert.AreEqual(2, trails.Count);
                Assert.AreEqual(TrailSide.Left, trails[0].Side);
                Assert.AreEqual(TrailSide.Right, trails[1].Side);
            }
        }

        [Test]
        public void TrailsConnectNodeExitToNextNodeEntry()
        {
            FloorGraph graph = FixedGraph(424242);
            FloorLayout layout = FloorLayout.Generate(graph);

            for (int step = 0; step < graph.Nodes.Count - 1; step++)
            {
                FloorLayout.NodeLayout from = layout.Nodes[step];
                FloorLayout.NodeLayout to = layout.Nodes[step + 1];
                List<FloorLayout.RouteTrail> trails = layout.TrailsForStep(step);

                for (int i = 0; i < trails.Count; i++)
                {
                    FloorLayout.RouteTrail trail = trails[i];
                    Assert.AreEqual(from.NodeId, trail.FromNodeId);
                    Assert.AreEqual(to.NodeId, trail.ToNodeId);
                    Assert.AreEqual(from.ExitPoint, trail.Waypoints[0]);
                    Assert.AreEqual(to.EntryPoint, trail.Waypoints[trail.Waypoints.Count - 1]);
                    Assert.AreEqual(graph.Routes[(step * 2) + i].RouteType, trail.RouteType);
                    Assert.Greater(trail.Waypoints[1].Z, trail.Waypoints[0].Z);
                    Assert.Greater(trail.Waypoints[2].Z, trail.Waypoints[1].Z);
                    Assert.Greater(trail.Waypoints[3].Z, trail.Waypoints[2].Z);
                }
            }
        }

        [Test]
        public void FieldDimensionsReflectElongation()
        {
            FloorGraph graph = FixedGraph(98765);
            FloorLayout layout = FloorLayout.Generate(graph);

            for (int i = 0; i < layout.Nodes.Count; i++)
            {
                FloorLayout.FieldSize field = layout.Nodes[i].FieldSize;

                Assert.Greater(field.ElongationFactor, 1f);
                if (field.LongAxis == FieldAxis.X)
                {
                    Assert.Greater(field.SizeX, field.SizeZ);
                    Assert.AreEqual(field.ElongationFactor, field.SizeX / field.SizeZ);
                }
                else
                {
                    Assert.Greater(field.SizeZ, field.SizeX);
                    Assert.AreEqual(field.ElongationFactor, field.SizeZ / field.SizeX);
                }
            }
        }

        private static FloorGraph FixedGraph(int seed)
        {
            FloorGenParams parameters = new FloorGenParams(
                seed,
                new IntRange(5, 5),
                false,
                new IntRange(8, 8));
            return FloorGenerator.Generate(parameters);
        }
    }
}
