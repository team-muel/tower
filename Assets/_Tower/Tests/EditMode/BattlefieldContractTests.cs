using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    // T20: shared IBattlefield contract suite. Runs against BOTH
    // implementations (GridBattlefieldAdapter and AnalogBattlefield) so any
    // behavioural drift between the rollback grid path and the analog
    // default is caught at the seam.
    [TestFixture(CombatSpaceMode.Grid)]
    [TestFixture(CombatSpaceMode.Analog)]
    public sealed class BattlefieldContractTests
    {
        private readonly CombatSpaceMode mode;

        public BattlefieldContractTests(CombatSpaceMode mode)
        {
            this.mode = mode;
        }

        private IBattlefield CreateBattlefield(int width = 8, int height = 8)
        {
            if (mode == CombatSpaceMode.Grid)
            {
                return new GridBattlefieldAdapter(new GridMap(width, height));
            }

            return AnalogBattlefield.FromRoom(width, height);
        }

        private static BattlePos Center(int cellX, int cellY)
        {
            return BattleScale.ToBattlePos(new GridPos(cellX, cellY));
        }

        [Test]
        public void Mode_ReportsConstructedMode()
        {
            Assert.That(CreateBattlefield().Mode, Is.EqualTo(mode));
        }

        [Test]
        public void Dimensions_MatchRoomScale()
        {
            var battlefield = CreateBattlefield(6, 4);

            Assert.That(battlefield.Width, Is.EqualTo(6f * BattleScale.UnitsPerCell));
            Assert.That(battlefield.Height, Is.EqualTo(4f * BattleScale.UnitsPerCell));
        }

        [Test]
        public void Contains_InsideTrue_OutsideFalse()
        {
            var battlefield = CreateBattlefield();

            Assert.That(battlefield.Contains(Center(4, 4)), Is.True);
            Assert.That(battlefield.Contains(new BattlePos(-1f, -1f)), Is.False);
            Assert.That(battlefield.Contains(new BattlePos(9.5f, 9.5f)), Is.False);
        }

        [Test]
        public void Distance_ZeroOnSelf_SymmetricAndUnitScaled()
        {
            var battlefield = CreateBattlefield();
            var a = Center(1, 1);
            var b = Center(2, 1);

            Assert.That(battlefield.Distance(a, a), Is.EqualTo(0f));
            Assert.That(battlefield.Distance(a, b), Is.EqualTo(battlefield.Distance(b, a)));
            // One cell apart = exactly one analog unit in both modes.
            Assert.That(battlefield.Distance(a, b), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void AreAdjacent_NeighborsTrue_FarApartFalse()
        {
            var battlefield = CreateBattlefield();

            Assert.That(battlefield.AreAdjacent(Center(1, 1), Center(2, 1)), Is.True);
            Assert.That(battlefield.AreAdjacent(Center(1, 1), Center(5, 5)), Is.False);
        }

        [Test]
        public void HasLineOfSight_OpenField_IsClear()
        {
            var battlefield = CreateBattlefield();

            Assert.That(battlefield.HasLineOfSight(Center(0, 0), Center(7, 7)), Is.True);
        }

        [Test]
        public void Occupancy_PlaceFindRemove_RoundTrips()
        {
            var battlefield = CreateBattlefield();
            var spot = Center(2, 2);

            Assert.That(battlefield.TryPlaceOccupant("a", spot), Is.True);
            Assert.That(battlefield.FindOccupant("a"), Is.EqualTo((BattlePos?)spot));
            Assert.That(battlefield.IsOccupied(spot), Is.True);
            Assert.That(battlefield.GetOccupantAt(spot), Is.EqualTo("a"));

            Assert.That(battlefield.RemoveOccupant("a"), Is.True);
            Assert.That(battlefield.FindOccupant("a"), Is.Null);
            Assert.That(battlefield.IsOccupied(spot), Is.False);
        }

        [Test]
        public void TryPlaceOccupant_RejectsTakenSpot()
        {
            var battlefield = CreateBattlefield();
            var spot = Center(3, 3);

            Assert.That(battlefield.TryPlaceOccupant("a", spot), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("b", spot), Is.False);
        }

        [Test]
        public void TryMoveOccupant_FreeSpotSucceeds_TakenSpotFails()
        {
            var battlefield = CreateBattlefield();
            Assert.That(battlefield.TryPlaceOccupant("a", Center(2, 2)), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("b", Center(5, 5)), Is.True);

            Assert.That(battlefield.TryMoveOccupant("a", Center(5, 5)), Is.False);
            Assert.That(battlefield.TryMoveOccupant("a", Center(3, 2)), Is.True);
            Assert.That(battlefield.FindOccupant("a"), Is.EqualTo((BattlePos?)Center(3, 2)));
        }

        [Test]
        public void RemoveOccupant_UnknownUnit_Fails()
        {
            var battlefield = CreateBattlefield();

            Assert.That(battlefield.RemoveOccupant("ghost"), Is.False);
        }

        [Test]
        public void ClampMove_NeverExceedsBudget_AndMakesProgress()
        {
            var battlefield = CreateBattlefield();
            var from = Center(0, 0);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);

            var result = battlefield.ClampMove("a", from, Center(7, 0), 3f);

            Assert.That(battlefield.Distance(from, result), Is.LessThanOrEqualTo(3f + 0.01f));
            Assert.That(battlefield.Distance(from, result), Is.GreaterThan(0f));
        }

        [Test]
        public void ClampMove_ZeroBudget_ReturnsFrom()
        {
            var battlefield = CreateBattlefield();
            var from = Center(1, 1);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);

            Assert.That(battlefield.ClampMove("a", from, Center(6, 6), 0f), Is.EqualTo(from));
        }

        [Test]
        public void ClampMove_IsDeterministic()
        {
            var battlefield = CreateBattlefield();
            var from = Center(0, 0);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("b", Center(4, 0)), Is.True);

            var first = battlefield.ClampMove("a", from, Center(7, 0), 4f);
            var second = battlefield.ClampMove("a", from, Center(7, 0), 4f);

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void GetMoveCandidates_StartsWithStay_AndStaysWithinBudget()
        {
            var battlefield = CreateBattlefield();
            var from = Center(4, 4);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);

            var candidates = battlefield.GetMoveCandidates("a", from, 2f);

            Assert.That(candidates.Count, Is.GreaterThan(0));
            Assert.That(candidates[0].Position, Is.EqualTo(from));
            Assert.That(candidates[0].Cost, Is.EqualTo(0f));
            foreach (var candidate in candidates)
            {
                Assert.That(candidate.Cost, Is.LessThanOrEqualTo(2f + 0.001f));
                Assert.That(battlefield.Contains(candidate.Position), Is.True);
            }
        }

        [Test]
        public void GetMoveCandidates_IsDeterministic()
        {
            var battlefield = CreateBattlefield();
            var from = Center(4, 4);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("b", Center(6, 4)), Is.True);

            var first = battlefield.GetMoveCandidates("a", from, 3f);
            var second = battlefield.GetMoveCandidates("a", from, 3f);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (var index = 0; index < first.Count; index++)
            {
                Assert.That(second[index].Position, Is.EqualTo(first[index].Position), "position " + index);
                Assert.That(second[index].Cost, Is.EqualTo(first[index].Cost), "cost " + index);
            }
        }
    }
}
