using NUnit.Framework;
using Tower.Floor;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class FloorGraphTerrainPreviewTests
    {
        [Test]
        public void RebuildCreatesOneGeneratedSegmentPerGraphNode()
        {
            GameObject host = new GameObject("preview");
            try
            {
                FloorGraphTerrainPreview preview = host.AddComponent<FloorGraphTerrainPreview>();
                preview.Rebuild();

                Transform root = host.transform.Find(FloorGraphTerrainPreview.GeneratedRootName);

                Assert.IsNotNull(root);
                Assert.IsNotNull(preview.LastGraph);
                Assert.AreEqual(preview.LastGraph.Nodes.Count, root.childCount);

                for (int i = 0; i < root.childCount; i++)
                {
                    Transform child = root.GetChild(i);
                    Assert.IsNotNull(child.GetComponent<MeshFilter>().sharedMesh);
                    Assert.IsNotNull(child.GetComponent<MeshRenderer>());
                    Assert.IsNotNull(child.GetComponent<MeshCollider>().sharedMesh);
                    Assert.IsTrue(child.name.StartsWith("Node_"));
                }
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void RebuildReplacesPriorGeneratedRoot()
        {
            GameObject host = new GameObject("preview");
            try
            {
                FloorGraphTerrainPreview preview = host.AddComponent<FloorGraphTerrainPreview>();
                preview.Rebuild();
                Transform firstRoot = host.transform.Find(FloorGraphTerrainPreview.GeneratedRootName);
                int firstRootId = firstRoot.GetInstanceID();

                preview.Rebuild();
                Transform secondRoot = host.transform.Find(FloorGraphTerrainPreview.GeneratedRootName);

                Assert.IsNotNull(secondRoot);
                Assert.AreNotEqual(firstRootId, secondRoot.GetInstanceID());
                Assert.AreEqual(1, CountGeneratedRoots(host.transform));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private static int CountGeneratedRoots(Transform host)
        {
            int count = 0;
            for (int i = 0; i < host.childCount; i++)
            {
                if (host.GetChild(i).name == FloorGraphTerrainPreview.GeneratedRootName)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
