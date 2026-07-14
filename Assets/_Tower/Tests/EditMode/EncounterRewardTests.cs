using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class EncounterRewardTests
    {
        [Test]
        public void Grant_IsExactlyOnceForAnIdenticalEventReward()
        {
            EncounterReward reward = EncounterReward.Create(
                "event-01",
                RewardType.Resource,
                2,
                "Run resource").Value;
            var inventory = new RunRewardInventory();

            Result<bool> first = inventory.Grant(reward);
            Result<bool> repeated = inventory.Grant(reward);

            Assert.That(first.IsSuccess, Is.True);
            Assert.That(first.Value, Is.True);
            Assert.That(repeated.IsSuccess, Is.True);
            Assert.That(repeated.Value, Is.False);
            Assert.That(inventory.ClaimCount, Is.EqualTo(1));
            Assert.That(inventory.AmountOf(RewardType.Resource), Is.EqualTo(2));
        }

        [Test]
        public void Grant_RejectsConflictingRetryWithoutChangingTotals()
        {
            var inventory = new RunRewardInventory();
            EncounterReward first = EncounterReward.Create(
                "event-01",
                RewardType.Resource,
                1,
                "Run resource").Value;
            EncounterReward conflict = EncounterReward.Create(
                "event-01",
                RewardType.Ability,
                1,
                "Ability draft").Value;

            Assert.That(inventory.Grant(first).IsSuccess, Is.True);
            Assert.That(inventory.Grant(conflict).IsFailure, Is.True);
            Assert.That(inventory.AmountOf(RewardType.Resource), Is.EqualTo(1));
            Assert.That(inventory.AmountOf(RewardType.Ability), Is.Zero);
        }

        [TestCase(RewardType.None, 1, "Reward")]
        [TestCase(RewardType.Resource, 0, "Reward")]
        [TestCase(RewardType.Resource, 1, "")]
        public void Create_RejectsInvalidRewardPayload(
            RewardType type,
            int amount,
            string displayName)
        {
            Assert.That(
                EncounterReward.Create("event-01", type, amount, displayName).IsFailure,
                Is.True);
        }
    }
}
