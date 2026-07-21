using NUnit.Framework;
using Tower.Combat;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class EncounterRewardProfileTests
    {
        private EncounterRewardProfile profile;

        [SetUp]
        public void SetUp()
        {
            profile = EncounterRewardProfile.CreateRuntime(
                RewardType.Resource,
                1,
                "Run resource",
                RewardType.Ability,
                1,
                "Ability draft");
        }

        [TearDown]
        public void TearDown()
        {
            if (profile != null) Object.DestroyImmediate(profile);
        }

        [Test]
        public void CreateReward_UsesAuthoredOrdinaryAndBossEntries()
        {
            RunEventPlan plan = RunEventPlan.Create(17);

            EncounterReward ordinary = profile.CreateReward(plan.Slots[0]).Value;
            EncounterReward boss = profile.CreateReward(plan.Slots[plan.Slots.Count - 1]).Value;

            Assert.That(profile.Validate().IsSuccess, Is.True);
            Assert.That(ordinary.Type, Is.EqualTo(RewardType.Resource));
            Assert.That(ordinary.EventId, Is.EqualTo(plan.Slots[0].EventId));
            Assert.That(boss.Type, Is.EqualTo(RewardType.Ability));
            Assert.That(boss.EventId, Is.EqualTo(plan.Slots[plan.Slots.Count - 1].EventId));
        }
    }
}
