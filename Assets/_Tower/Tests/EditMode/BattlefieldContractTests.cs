using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class BattlefieldContractTests
    {
        [Test]
        public void AnalogBattlefield_TracksOccupantsAndMovement()
        {
            var battlefield = new AnalogBattlefield(6f, 4f);

            Assert.That(battlefield.Width, Is.EqualTo(6f));
            Assert.That(battlefield.Height, Is.EqualTo(4f));
            Assert.That(battlefield.TryPlaceOccupant("unit-a", new BattlePos(1f, 1f)), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("unit-b", new BattlePos(3f, 1f)), Is.True);

            Assert.That(battlefield.FindOccupant("unit-a"), Is.EqualTo(new BattlePos(1f, 1f)));
            Assert.That(battlefield.GetOccupantAt(new BattlePos(3f, 1f)), Is.EqualTo("unit-b"));
            Assert.That(battlefield.TryMoveOccupant("unit-a", new BattlePos(1.5f, 1f)), Is.True);
            Assert.That(battlefield.RemoveOccupant("unit-b"), Is.True);
            Assert.That(battlefield.FindOccupant("unit-b").HasValue, Is.False);
        }

        [Test]
        public void ClampMove_RespectsBudgetAndBounds()
        {
            var battlefield = new AnalogBattlefield(6f, 4f);
            Assert.That(battlefield.TryPlaceOccupant("unit-a", new BattlePos(1f, 1f)), Is.True);

            BattlePos clamped = battlefield.ClampMove("unit-a", new BattlePos(1f, 1f), new BattlePos(5f, 1f), 2f);

            Assert.That(battlefield.Distance(new BattlePos(1f, 1f), clamped), Is.LessThanOrEqualTo(2.001f));
            Assert.That(battlefield.Contains(clamped), Is.True);
        }

        [Test]
        public void MoveCandidates_AlwaysIncludeStayPut()
        {
            var battlefield = new AnalogBattlefield(6f, 4f);
            Assert.That(battlefield.TryPlaceOccupant("unit-a", new BattlePos(1f, 1f)), Is.True);

            var candidates = battlefield.GetMoveCandidates("unit-a", new BattlePos(1f, 1f), 2f);

            Assert.That(candidates.Count, Is.GreaterThan(0));
            Assert.That(candidates[0].Position, Is.EqualTo(new BattlePos(1f, 1f)));
            Assert.That(candidates[0].Cost, Is.EqualTo(0f));
        }
    }
}
