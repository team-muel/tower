using NUnit.Framework;
using Tower.Floor;
using Tower.Gen;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class ForestFloorPrefabFallbackTests
    {
        [Test]
        public void RendererWithoutPrefabsKeepsPlannerCounts()
        {
            GameObject host = new GameObject("forest-renderer");
            try
            {
                ForestFloorRenderer renderer = host.AddComponent<ForestFloorRenderer>();
                renderer.Rebuild();

                Transform root = host.transform.Find(ForestFloorRenderer.GeneratedRootName);
                Assert.IsNotNull(root);
                Assert.IsNotNull(renderer.Graph);

                LinearStubLayout layout = new LinearStubLayout(renderer.Graph);
                int expectedNodes = renderer.Graph.Nodes.Count;
                int expectedTrees = 0;
                int expectedRocks = 0;
                int expectedAnchors = 0;

                for (int i = 0; i < renderer.Graph.Nodes.Count; i++)
                {
                    FloorNode node = renderer.Graph.Nodes[i];
                    FloorFieldRect field = layout.GetField(node.Id);
                    ForestContentPlan content = ForestContentPlanner.Build(renderer.Graph.Seed, node.Id, field);
                    expectedTrees += content.Trees.Count;
                    expectedRocks += content.Rocks.Count;
                    expectedAnchors += NodeAnchorPlanner.Build(renderer.Graph.Seed, node, field, content.Clearing).Anchors.Count;
                }

                int actualNodes = 0;
                int actualTrees = 0;
                int actualRocks = 0;
                int actualAnchors = 0;
                for (int i = 0; i < root.childCount; i++)
                {
                    Transform nodeRoot = root.GetChild(i);
                    if (!nodeRoot.name.StartsWith("Node_")) continue;

                    actualNodes++;
                    actualTrees += ChildCount(nodeRoot, "Trees");
                    actualRocks += ChildCount(nodeRoot, "Rocks");
                    actualAnchors += ChildCount(nodeRoot, "Anchors");
                }

                Assert.AreEqual(expectedNodes, actualNodes);
                Assert.AreEqual(expectedTrees, actualTrees);
                Assert.AreEqual(expectedRocks, actualRocks);
                Assert.AreEqual(expectedAnchors, actualAnchors);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static int ChildCount(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child != null ? child.childCount : 0;
        }
    }
}
