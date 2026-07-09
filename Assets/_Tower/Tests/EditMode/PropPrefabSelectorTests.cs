using System.Collections.Generic;
using NUnit.Framework;
using Tower.Floor;

namespace Tower.Tests.EditMode
{
    public sealed class PropPrefabSelectorTests
    {
        [Test]
        public void SameInputsAlwaysReturnSameIndex()
        {
            int baseline = PropPrefabSelector.PickIndex(777, 3, 11, 8);
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(baseline, PropPrefabSelector.PickIndex(777, 3, 11, 8));
            }
        }

        [Test]
        public void DifferentNodeIdsTouchMultipleIndices()
        {
            HashSet<int> seen = new HashSet<int>();
            for (int nodeId = 0; nodeId < 200; nodeId++)
            {
                seen.Add(PropPrefabSelector.PickIndex(1234, nodeId, 0, 5));
            }

            Assert.Greater(seen.Count, 1, "Selector should not collapse all node ids to one prefab.");
        }

        [Test]
        public void ZeroCountReturnsFallbackSignal()
        {
            Assert.AreEqual(-1, PropPrefabSelector.PickIndex(777, 3, 11, 0));
        }

        [Test]
        public void SingleCountAlwaysReturnsZero()
        {
            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(0, PropPrefabSelector.PickIndex(777 + i, i, i * 3, 1));
            }
        }
    }
}
