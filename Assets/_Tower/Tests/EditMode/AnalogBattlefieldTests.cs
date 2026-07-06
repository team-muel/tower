using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    // T20: analog-specific behaviour — euclidean distance, straight-line
    // ClampMove, circular collision, deterministic sampling and the
    // grid-to-analog scale contract.
    public sealed class AnalogBattlefieldTests
    {
        [Test]
        public void Scale_OneCellIsOneAnalogUnit()
        {
            var battlefield = AnalogBattlefield.FromRoom(6, 4);

            Assert.That(battlefield.Width, Is.EqualTo(6f));
            Assert.That(battlefield.Height, Is.EqualTo(4f));
            Assert.That(BattleScale.ToBattlePos(new GridPos(2, 3)), Is.EqualTo(new BattlePos(2.5f, 3.5f)));
            Assert.That(BattleScale.ToGridPos(new BattlePos(2.5f, 3.5f)), Is.EqualTo(new GridPos(2, 3)));
        }

        [Test]
        public void Distance_IsEuclidean()
        {
            var battlefield = new AnalogBattlefield(10f, 10f);

            var distance = battlefield.Distance(new BattlePos(0.5f, 0.5f), new BattlePos(3.5f, 4.5f));

            Assert.That(distance, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void HasLineOfSight_IsAlwaysTrueInV0()
        {
            var battlefield = new AnalogBattlefield(10f, 10f);
            Assert.That(battlefield.TryPlaceOccupant("wall", new BattlePos(5f, 5f)), Is.True);

            // Even straight through another unit: no obstacles in v0.
            Assert.That(battlefield.HasLineOfSight(new BattlePos(1f, 5f), new BattlePos(9f, 5f)), Is.True);
        }

        [Test]
        public void ClampMove_StopsAtBudgetAlongStraightLine()
        {
            var battlefield = new AnalogBattlefield(10f, 10f);
            var from = new BattlePos(1f, 1f);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);

            var result = battlefield.ClampMove("a", from, new BattlePos(1f, 7f), 2.5f);

            Assert.That(result.X, Is.EqualTo(1f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(3.5f).Within(0.001f));
        }

        [Test]
        public void ClampMove_ReachesTargetWhenWithinBudget()
        {
            var battlefield = new AnalogBattlefield(10f, 10f);
            var from = new BattlePos(1f, 1f);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);

            var result = battlefield.ClampMove("a", from, new BattlePos(2f, 2f), 4f);

            Assert.That(battlefield.Distance(result, new BattlePos(2f, 2f)), Is.LessThanOrEqualTo(0.001f));
        }

        [Test]
        public void ClampMove_BacksOffBeforeCollidingUnit()
        {
            var battlefield = new AnalogBattlefield(10f, 10f);
            var from = new BattlePos(1f, 1f);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("b", new BattlePos(4f, 1f)), Is.True);

            var result = battlefield.ClampMove("a", from, new BattlePos(4f, 1f), 5f);

            // Two circles of radius 0.45 may not overlap: the mover stops at
            // least 0.9 away from the blocker (back-off step is 0.05).
            var separation = battlefield.Distance(result, new BattlePos(4f, 1f));
            Assert.That(separation, Is.GreaterThanOrEqualTo(0.9f - 0.001f));
            Assert.That(separation, Is.LessThanOrEqualTo(0.9f + 0.06f));
        }

        [Test]
        public void ClampMove_ClampsInsideAreaBounds()
        {
            var battlefield = new AnalogBattlefield(6f, 6f);
            var from = new BattlePos(5f, 5f);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);

            var result = battlefield.ClampMove("a", from, new BattlePos(9f, 9f), 4f);

            Assert.That(result.X, Is.LessThanOrEqualTo(6f - 0.45f + 0.001f));
            Assert.That(result.Y, Is.LessThanOrEqualTo(6f - 0.45f + 0.001f));
        }

        [Test]
        public void TryMoveOccupant_RejectsOverlappingPosition()
        {
            var battlefield = new AnalogBattlefield(10f, 10f);
            Assert.That(battlefield.TryPlaceOccupant("a", new BattlePos(1f, 1f)), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("b", new BattlePos(4f, 1f)), Is.True);

            Assert.That(battlefield.TryMoveOccupant("a", new BattlePos(3.6f, 1f)), Is.False);
            Assert.That(battlefield.TryMoveOccupant("a", new BattlePos(3f, 1f)), Is.True);
        }

        [Test]
        public void TryPlaceOccupant_RejectsPositionsOutsideRadiusMargin()
        {
            var battlefield = new AnalogBattlefield(6f, 6f);

            Assert.That(battlefield.TryPlaceOccupant("a", new BattlePos(0.2f, 0.2f)), Is.False);
            Assert.That(battlefield.TryPlaceOccupant("a", new BattlePos(0.5f, 0.5f)), Is.True);
        }

        [Test]
        public void GetMoveCandidates_SamplesStayPlusEightDirectionsAtTwoRadii()
        {
            var battlefield = new AnalogBattlefield(20f, 20f);
            var from = new BattlePos(10f, 10f);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);

            var candidates = battlefield.GetMoveCandidates("a", from, 4f);

            // Open field: stay + 8 directions x 2 radii = 17 candidates.
            Assert.That(candidates.Count, Is.EqualTo(17));
            Assert.That(candidates[0].Position, Is.EqualTo(from));
            Assert.That(candidates[0].Cost, Is.EqualTo(0f));
        }

        [Test]
        public void GetMoveCandidates_IsDeterministicAcrossCalls()
        {
            var battlefield = new AnalogBattlefield(10f, 10f);
            var from = new BattlePos(2f, 2f);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("b", new BattlePos(5f, 2f)), Is.True);

            var first = battlefield.GetMoveCandidates("a", from, 4f);
            var second = battlefield.GetMoveCandidates("a", from, 4f);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (var index = 0; index < first.Count; index++)
            {
                Assert.That(second[index].Position, Is.EqualTo(first[index].Position), "position " + index);
                Assert.That(second[index].Cost, Is.EqualTo(first[index].Cost), "cost " + index);
            }
        }

        [Test]
        public void GetMoveCandidates_CostsNeverExceedBudget()
        {
            var battlefield = new AnalogBattlefield(10f, 10f);
            var from = new BattlePos(5f, 5f);
            Assert.That(battlefield.TryPlaceOccupant("a", from), Is.True);

            foreach (var candidate in battlefield.GetMoveCandidates("a", from, 4f))
            {
                Assert.That(candidate.Cost, Is.LessThanOrEqualTo(4f + 0.001f));
            }
        }
    }
}
