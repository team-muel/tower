using NUnit.Framework;
using Tower.Gen;
using Tower.UI;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace Tower.Tests.EditMode
{
    [TestFixture]
    public sealed class DungeonMapTests
    {
        [Test]
        public void DungeonMap_LayoutGeneration_CanBeMappedToCoordinates()
        {
            var seed = 42;
            var layout = FloorGenerator.Generate(new FloorGenParams(seed, false));
            
            Assert.That(layout.Rooms, Is.Not.Null);
            Assert.That(layout.Rooms.Count, Is.GreaterThan(0));

            int maxDepth = 0;
            foreach (var r in layout.Rooms)
            {
                if (r.Depth > maxDepth) maxDepth = r.Depth;
            }
            Assert.That(maxDepth, Is.GreaterThan(0));

            var depthGroups = new Dictionary<int, List<FloorRoom>>();
            foreach (var r in layout.Rooms)
            {
                if (!depthGroups.ContainsKey(r.Depth))
                {
                    depthGroups[r.Depth] = new List<FloorRoom>();
                }
                depthGroups[r.Depth].Add(r);
            }

            foreach (var r in layout.Rooms)
            {
                var siblings = depthGroups[r.Depth];
                int sibIndex = siblings.IndexOf(r);
                int sibCount = siblings.Count;

                float x = (float)r.Depth / maxDepth;
                float y = sibCount > 1 ? (float)sibIndex / (sibCount - 1) : 0.5f;

                Assert.That(x, Is.InRange(0f, 1f));
                Assert.That(y, Is.InRange(0f, 1f));
            }
        }
    }
}
