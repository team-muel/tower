using NUnit.Framework;
using Tower.Core;
using Tower.Gen;

namespace Tower.Tests.EditMode
{
    // T31: FloorNode -> HeightField binding (74 <-> 75). Per-node seed via FNV-1a.
    public sealed class HeightFieldFactoryTests
    {
        private static FloorNode Node(int id)
        {
            return new FloorNode(id, id, RoomKind.Normal, "template.road", false, false, false);
        }

        [Test]
        public void ForNodeIsDeterministicForSameGraphSeedAndNode()
        {
            FloorNode node = Node(3);

            HeightField a = HeightFieldFactory.ForNode(12345, node, BiomeDef.For(BiomeId.Forest));
            HeightField b = HeightFieldFactory.ForNode(12345, node, BiomeDef.For(BiomeId.Forest));

            Assert.AreEqual(a.Seed, b.Seed, "Same graph seed + node id must derive the same node seed.");

            for (int i = 0; i < 30; i++)
            {
                float x = -20f + i * 1.3f;
                float z = 15f - i * 0.9f;
                Assert.AreEqual(a.Sample(x, z), b.Sample(x, z), 0f);
            }
        }

        [Test]
        public void ForNodeDiffersAcrossNodeIds()
        {
            HeightField a = HeightFieldFactory.ForNode(12345, Node(1));
            HeightField b = HeightFieldFactory.ForNode(12345, Node(2));

            Assert.AreNotEqual(a.Seed, b.Seed, "Different node ids must derive different seeds.");
        }

        [Test]
        public void ForNodeDiffersAcrossGraphSeeds()
        {
            HeightField a = HeightFieldFactory.ForNode(1, Node(5));
            HeightField b = HeightFieldFactory.ForNode(2, Node(5));

            Assert.AreNotEqual(a.Seed, b.Seed, "Different graph seeds must derive different seeds.");
        }

        [Test]
        public void ForNodeWithoutBiomeUsesDefaultParams()
        {
            HeightField hf = HeightFieldFactory.ForNode(42, Node(0));

            Assert.AreSame(HeightFieldParams.Default, hf.Parameters,
                "A null biome should fall back to the default terrain params.");
        }

        [Test]
        public void ForNodeProducesFiniteSamples()
        {
            HeightField hf = HeightFieldFactory.ForNode(777, Node(9), BiomeDef.For(BiomeId.Desert));

            for (int i = 0; i < 60; i++)
            {
                float x = -40f + i * 1.1f;
                float z = 25f - i * 0.83f;
                float h = hf.Sample(x, z);
                Assert.IsFalse(float.IsNaN(h));
                Assert.IsFalse(float.IsInfinity(h));
            }
        }
    }
}
