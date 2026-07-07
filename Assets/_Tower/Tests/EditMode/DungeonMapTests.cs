using NUnit.Framework;
using Tower.Gen;
using System.Collections.Generic;

namespace Tower.Tests.EditMode
{
    [TestFixture]
    public sealed class DungeonMapTests
    {
        [Test]
        public void DungeonMap_GraphGeneration_CanBeMappedToCoordinates()
        {
            var seed = 42;
            var graph = FloorGenerator.Generate(new FloorGenParams(seed, false));

            Assert.That(graph.Nodes, Is.Not.Null);
            Assert.That(graph.Nodes.Count, Is.GreaterThan(0));

            int maxDepth = 0;
            foreach (var node in graph.Nodes)
            {
                if (node.Depth > maxDepth) maxDepth = node.Depth;
            }
            Assert.That(maxDepth, Is.GreaterThan(0));

            var depthGroups = new Dictionary<int, List<FloorNode>>();
            foreach (var node in graph.Nodes)
            {
                if (!depthGroups.ContainsKey(node.Depth))
                {
                    depthGroups[node.Depth] = new List<FloorNode>();
                }
                depthGroups[node.Depth].Add(node);
            }

            foreach (var node in graph.Nodes)
            {
                var siblings = depthGroups[node.Depth];
                int sibIndex = siblings.IndexOf(node);
                int sibCount = siblings.Count;

                float x = (float)node.Depth / maxDepth;
                float y = sibCount > 1 ? (float)sibIndex / (sibCount - 1) : 0.5f;

                Assert.That(x, Is.InRange(0f, 1f));
                Assert.That(y, Is.InRange(0f, 1f));
            }
        }
    }
}
