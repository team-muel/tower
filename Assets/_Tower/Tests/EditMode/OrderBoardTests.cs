using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    // Brief semantics (docs/tasks/T6.md + vault 13 §6 v0): orders are a per-combat
    // budget (default 2). Spending is not refunded on expiry; the budget refills
    // when a new combat starts / the combat ends.
    public sealed class OrderBoardTests
    {
        private OrderBoard orderBoard;

        [SetUp]
        public void SetUp()
        {
            orderBoard = new OrderBoard();
        }

        [Test]
        public void IssueFocus_ConsumesOrderSlot_AndRegistersRecord()
        {
            var result = orderBoard.IssueFocus("ally-red", expiresOnRound: 2);
            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(orderBoard.RemainingOrders(), Is.EqualTo(1));
            Assert.That(orderBoard.HasActiveOrders(), Is.True);
            Assert.That(orderBoard.HasFocus("ally-red"), Is.True);
            Assert.That(orderBoard.GetActiveOrders(), Has.Count.EqualTo(1));
            Assert.That(orderBoard.GetActiveOrders()[0].OrderType, Is.EqualTo("Focus"));
            Assert.That(orderBoard.GetActiveOrders()[0].TargetUnitId, Is.EqualTo("ally-red"));
            Assert.That(orderBoard.GetActiveOrders()[0].ExpiresAtRound, Is.EqualTo(2));
        }

        [Test]
        public void DuplicateFocus_UpdatesTargetInsteadOfAddingNewEntry()
        {
            var first = orderBoard.IssueFocus("ally-red", expiresOnRound: 1);
            Assert.That(first.IsSuccess, Is.True);

            var second = orderBoard.IssueFocus("ally-red", expiresOnRound: 4);
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(orderBoard.GetActiveOrders(), Has.Count.EqualTo(1));
            Assert.That(orderBoard.GetActiveOrders()[0].ExpiresAtRound, Is.EqualTo(4));
            Assert.That(orderBoard.RemainingOrders(), Is.EqualTo(1));
        }

        [Test]
        public void IssueFocus_AfterEmptyingRemainingOrders_ReturnsFailure()
        {
            Assert.That(orderBoard.IssueFocus("ally-red", expiresOnRound: 2).IsSuccess, Is.True);
            Assert.That(orderBoard.IssueFocus("ally-blue", expiresOnRound: 2).IsSuccess, Is.True);
            Assert.That(orderBoard.IssueFocus("ally-green", expiresOnRound: 2).IsFailure, Is.True);
            Assert.That(orderBoard.GetActiveOrders(), Has.Count.EqualTo(2));
        }

        [Test]
        public void AdvanceRound_ExpiresOrdersPastTheirRound_WithoutRefund()
        {
            Assert.That(orderBoard.IssueFocus("ally-red", expiresOnRound: 2).IsSuccess, Is.True);

            Assert.That(orderBoard.HasFocus("ally-red"), Is.True);
            Assert.That(orderBoard.AdvanceRound(2), Is.Empty);
            Assert.That(orderBoard.HasFocus("ally-red"), Is.False);
            Assert.That(orderBoard.HasActiveOrders(), Is.False);
            // Spent orders are NOT refunded by expiry.
            Assert.That(orderBoard.RemainingOrders(), Is.EqualTo(1));
        }

        [Test]
        public void StartNewCombat_ResetsEntries_AndRestoresCapacity()
        {
            Assert.That(orderBoard.IssueFocus("ally-red", expiresOnRound: 2).IsSuccess, Is.True);
            Assert.That(orderBoard.IssueFocus("ally-blue", expiresOnRound: 2).IsSuccess, Is.True);
            Assert.That(orderBoard.IssueFocus("ally-green", expiresOnRound: 2).IsFailure, Is.True);

            Assert.That(orderBoard.StartNewCombat().IsSuccess, Is.True);
            Assert.That(orderBoard.GetActiveOrders(), Is.Empty);
            Assert.That(orderBoard.RemainingOrders(), Is.EqualTo(2));
            Assert.That(orderBoard.HasActiveOrders(), Is.False);
            Assert.That(orderBoard.IssueFocus("ally-blue", expiresOnRound: 2).IsSuccess, Is.True);
        }

        [Test]
        public void EndCombat_ConsumesActiveOrders_AndRefillsCapacity()
        {
            Assert.That(orderBoard.IssueFocus("ally-red", expiresOnRound: 2).IsSuccess, Is.True);

            var finalOrders = orderBoard.EndCombat();
            Assert.That(finalOrders, Is.Empty);
            Assert.That(orderBoard.HasActiveOrders(), Is.False);
            Assert.That(orderBoard.GetActiveOrders(), Is.Empty);
            Assert.That(orderBoard.RemainingOrders(), Is.EqualTo(2));
            Assert.That(orderBoard.HasFocus("ally-red"), Is.False);
        }

        [Test]
        public void CustomCombatOrderLimit_IsPersistedAcrossReset()
        {
            var table = new OrderBoard(1);
            Assert.That(table.IssueFocus("ally-red", expiresOnRound: 2).IsSuccess, Is.True);
            Assert.That(table.IssueFocus("ally-blue", expiresOnRound: 2).IsFailure, Is.True);

            Assert.That(table.StartNewCombat().IsSuccess, Is.True);
            Assert.That(table.RemainingOrders(), Is.EqualTo(1));
            Assert.That(table.IssueFocus("ally-red", expiresOnRound: 2).IsSuccess, Is.True);
            Assert.That(table.IssueFocus("ally-blue", expiresOnRound: 2).IsFailure, Is.True);
        }

        [Test]
        public void AdvanceRound_PreservesLaterExpiringOrders()
        {
            Assert.That(orderBoard.IssueFocus("ally-red", expiresOnRound: 5).IsSuccess, Is.True);

            Assert.That(orderBoard.AdvanceRound(3), Has.Count.EqualTo(1));
            Assert.That(orderBoard.HasFocus("ally-red"), Is.True);
            Assert.That(orderBoard.GetActiveOrders()[0].ExpiresAtRound, Is.EqualTo(5));
            Assert.That(orderBoard.RemainingOrders(), Is.EqualTo(1));
        }
    }
}
