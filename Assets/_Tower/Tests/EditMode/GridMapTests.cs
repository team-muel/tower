using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class GridMapTests
    {
        [Test]
        public void NewMapCellsArePassableAndEmpty()
        {
            GridMap map = new GridMap(3, 2);
            GridPos pos = new GridPos(2, 1);

            Assert.IsTrue(map.InBounds(pos));
            Assert.IsTrue(map.IsPassable(pos));
            Assert.IsFalse(map.IsOccupied(pos));
            Assert.IsNull(map.GetOccupant(pos));
        }

        [Test]
        public void BlockedCellCannotReceiveOccupant()
        {
            GridMap map = new GridMap(3, 3);
            GridPos pos = new GridPos(1, 1);

            map.SetBlocked(pos, true);

            Assert.IsTrue(map.IsBlocked(pos));
            Assert.IsFalse(map.TrySetOccupant(pos, "unit-a"));
            Assert.IsFalse(map.IsOccupied(pos));
        }

        [Test]
        public void OccupiedCellRejectsDifferentOccupant()
        {
            GridMap map = new GridMap(3, 3);
            GridPos pos = new GridPos(1, 1);

            Assert.IsTrue(map.TrySetOccupant(pos, "unit-a"));

            Assert.IsTrue(map.IsOccupied(pos));
            Assert.AreEqual("unit-a", map.GetOccupant(pos));
            Assert.IsFalse(map.TrySetOccupant(pos, "unit-b"));
            Assert.AreEqual("unit-a", map.GetOccupant(pos));
        }

        [Test]
        public void MoveOccupantRequiresMatchingSourceAndOpenDestination()
        {
            GridMap map = new GridMap(3, 3);
            GridPos from = new GridPos(0, 0);
            GridPos blocked = new GridPos(1, 0);
            GridPos to = new GridPos(2, 0);

            map.SetBlocked(blocked, true);
            Assert.IsTrue(map.TrySetOccupant(from, "unit-a"));

            Assert.IsFalse(map.TryMoveOccupant(from, blocked, "unit-a"));
            Assert.AreEqual("unit-a", map.GetOccupant(from));

            Assert.IsTrue(map.TryMoveOccupant(from, to, "unit-a"));
            Assert.IsFalse(map.IsOccupied(from));
            Assert.AreEqual("unit-a", map.GetOccupant(to));
        }

        [Test]
        public void BlockingOccupiedCellClearsOccupant()
        {
            GridMap map = new GridMap(2, 2);
            GridPos pos = new GridPos(1, 1);

            Assert.IsTrue(map.TrySetOccupant(pos, "unit-a"));
            map.SetBlocked(pos, true);

            Assert.IsTrue(map.IsBlocked(pos));
            Assert.IsFalse(map.IsOccupied(pos));
            Assert.IsNull(map.GetOccupant(pos));
        }
    }
}
