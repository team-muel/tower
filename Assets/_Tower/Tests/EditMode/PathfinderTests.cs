using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class PathfinderTests
    {
        [Test]
        public void FindsStraightPath()
        {
            GridMap map = new GridMap(5, 2);

            IReadOnlyList<GridPos> path = Pathfinder.FindPath(map, new GridPos(0, 0), new GridPos(3, 0));

            Assert.AreEqual(4, path.Count);
            Assert.AreEqual(new GridPos(0, 0), path[0]);
            Assert.AreEqual(new GridPos(1, 0), path[1]);
            Assert.AreEqual(new GridPos(2, 0), path[2]);
            Assert.AreEqual(new GridPos(3, 0), path[3]);
            AssertPathIsCardinal(path);
        }

        [Test]
        public void DetoursAroundBlockedCells()
        {
            GridMap map = new GridMap(4, 3);
            map.SetBlocked(new GridPos(1, 0), true);
            map.SetBlocked(new GridPos(1, 1), true);

            IReadOnlyList<GridPos> path = Pathfinder.FindPath(map, new GridPos(0, 0), new GridPos(3, 0));

            Assert.AreEqual(new GridPos(0, 0), path[0]);
            Assert.AreEqual(new GridPos(3, 0), path[path.Count - 1]);
            Assert.Greater(path.Count, GridDistance.Manhattan(new GridPos(0, 0), new GridPos(3, 0)) + 1);
            AssertPathIsCardinal(path);
            AssertPathDoesNotContain(path, new GridPos(1, 0));
            AssertPathDoesNotContain(path, new GridPos(1, 1));
        }

        [Test]
        public void AvoidsOccupiedCells()
        {
            GridMap map = new GridMap(4, 3);
            Assert.IsTrue(map.TrySetOccupant(new GridPos(1, 0), "unit-a"));
            Assert.IsTrue(map.TrySetOccupant(new GridPos(1, 1), "unit-b"));

            IReadOnlyList<GridPos> path = Pathfinder.FindPath(map, new GridPos(0, 0), new GridPos(3, 0));

            Assert.AreEqual(new GridPos(0, 0), path[0]);
            Assert.AreEqual(new GridPos(3, 0), path[path.Count - 1]);
            AssertPathDoesNotContain(path, new GridPos(1, 0));
            AssertPathDoesNotContain(path, new GridPos(1, 1));
        }

        [Test]
        public void ReturnsEmptyPathWhenGoalIsBlocked()
        {
            GridMap map = new GridMap(3, 1);
            map.SetBlocked(new GridPos(2, 0), true);

            IReadOnlyList<GridPos> path = Pathfinder.FindPath(map, new GridPos(0, 0), new GridPos(2, 0));

            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void ReturnsEmptyPathWhenNoRouteExists()
        {
            GridMap map = new GridMap(3, 3);
            map.SetBlocked(new GridPos(1, 0), true);
            map.SetBlocked(new GridPos(1, 1), true);
            map.SetBlocked(new GridPos(1, 2), true);

            IReadOnlyList<GridPos> path = Pathfinder.FindPath(map, new GridPos(0, 0), new GridPos(2, 0));

            Assert.AreEqual(0, path.Count);
        }

        [Test]
        public void ManhattanDistanceMatchesGridRange()
        {
            Assert.AreEqual(7, GridDistance.Manhattan(new GridPos(-1, 4), new GridPos(3, 1)));
        }

        private static void AssertPathIsCardinal(IReadOnlyList<GridPos> path)
        {
            for (int i = 1; i < path.Count; i++)
            {
                Assert.AreEqual(1, GridDistance.Manhattan(path[i - 1], path[i]));
            }
        }

        private static void AssertPathDoesNotContain(IReadOnlyList<GridPos> path, GridPos blocked)
        {
            for (int i = 0; i < path.Count; i++)
            {
                Assert.AreNotEqual(blocked, path[i]);
            }
        }
    }
}
