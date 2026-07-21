using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class RealtimeCommandBoardTests
    {
        [Test]
        public void DefaultAssignment_FollowsDispositionUntilOwnerOverridesIt()
        {
            var board = new RealtimeCommandBoard();

            Assert.That(
                board.GetAssignment("ember", DispositionType.Aggressive).Stance,
                Is.EqualTo(CommandStance.Assault));
            Assert.That(
                board.GetAssignment("ward", DispositionType.Protective).Stance,
                Is.EqualTo(CommandStance.Guard));

            Assert.That(board.SetStance("ember", CommandStance.Guard).IsSuccess, Is.True);
            Assert.That(
                board.GetAssignment("ember", DispositionType.Aggressive).Stance,
                Is.EqualTo(CommandStance.Guard));
        }

        [Test]
        public void FocusStance_RequiresAndRemembersItsTarget()
        {
            var board = new RealtimeCommandBoard();

            Assert.That(board.SetStance("ember", CommandStance.Focus).IsFailure, Is.True);
            Assert.That(board.SetStance("ember", CommandStance.Focus, "pillbug-0").IsSuccess, Is.True);

            var assignment = board.GetAssignment("ember", DispositionType.Aggressive);
            Assert.That(assignment.Stance, Is.EqualTo(CommandStance.Focus));
            Assert.That(assignment.FocusTargetId, Is.EqualTo("pillbug-0"));
        }

        [Test]
        public void SetStance_RejectsUndefinedEnumValues()
        {
            var board = new RealtimeCommandBoard();

            Assert.That(board.SetStance("ember", (CommandStance)99).IsFailure, Is.True);
            Assert.That(board.Assignments, Is.Empty);
        }

        [Test]
        public void PreciseOrder_IsRejectedOutsideBulletTime()
        {
            var board = new RealtimeCommandBoard();

            var result = board.IssuePreciseOrder(
                "ember",
                "thermal-break",
                "pillbug-0",
                null,
                commandWindowActive: false,
                issuedAtSeconds: 0f);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(board.PreciseOrders, Is.Empty);
        }

        [Test]
        public void PreciseOrder_ReplacesPerCompanionAndExpiresByCombatTime()
        {
            var board = new RealtimeCommandBoard();

            Assert.That(board.IssuePreciseOrder(
                "ember", "strike", "pillbug-a", null, true, 1f, lifetimeSeconds: 3f).IsSuccess, Is.True);
            Assert.That(board.IssuePreciseOrder(
                "ember", "thermal-break", "pillbug-b", null, true, 2f, lifetimeSeconds: 3f).IsSuccess, Is.True);
            Assert.That(board.PreciseOrders, Has.Count.EqualTo(1));

            Assert.That(board.TryGetPreciseOrder("ember", 2.5f, out PreciseOrder order), Is.True);
            Assert.That(order.AbilityId, Is.EqualTo("thermal-break"));
            Assert.That(order.TargetUnitId, Is.EqualTo("pillbug-b"));

            board.Advance(5f);
            Assert.That(board.TryGetPreciseOrder("ember", 5f, out _), Is.False);
        }

        [Test]
        public void PreciseOrder_RejectsNegativeCombatTime()
        {
            var board = new RealtimeCommandBoard();

            Assert.That(board.IssuePreciseOrder(
                "ember", "strike", "pillbug", null, true, -0.1f).IsFailure, Is.True);
            Assert.That(board.PreciseOrders, Is.Empty);
        }
    }
}
