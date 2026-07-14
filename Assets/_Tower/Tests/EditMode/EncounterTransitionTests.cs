using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class EncounterTransitionTests
    {
        [TestCase(0f, 0.4f)]
        [TestCase(7f, 0f)]
        [TestCase(float.NaN, 0.4f)]
        [TestCase(7f, float.PositiveInfinity)]
        public void Create_RejectsInvalidTuning(float radius, float hold)
        {
            Assert.That(EncounterTransition.Create(radius, hold).IsFailure, Is.True);
        }

        [Test]
        public void CrossingTrigger_HoldsBeforeActivatingRealTimeCombat()
        {
            var transition = CreateTransition(7f, 0.45f);

            Assert.That(transition.TryBegin(7.01f), Is.False);
            Assert.That(transition.Phase, Is.EqualTo(EncounterPhase.Exploring));
            Assert.That(transition.TryBegin(7f), Is.True);
            Assert.That(transition.IsPlayerHeld, Is.True);
            Assert.That(transition.IsCombatActive, Is.False);

            Assert.That(transition.Tick(0.44f), Is.False);
            Assert.That(transition.IsPlayerHeld, Is.True);
            Assert.That(transition.Tick(0.01f), Is.True);
            Assert.That(transition.IsPlayerHeld, Is.False);
            Assert.That(transition.IsCombatActive, Is.True);
        }

        [Test]
        public void HoldTiming_IsFrameRateIndependent()
        {
            var single = CreateTransition(7f, 0.5f);
            var stepped = CreateTransition(7f, 0.5f);
            single.TryBegin(1f);
            stepped.TryBegin(1f);

            single.Tick(0.5f);
            for (var index = 0; index < 50; index++)
            {
                stepped.Tick(0.01f);
            }

            Assert.That(single.Phase, Is.EqualTo(EncounterPhase.Active));
            Assert.That(stepped.Phase, Is.EqualTo(EncounterPhase.Active));
            Assert.That(stepped.HoldProgress, Is.EqualTo(single.HoldProgress));
        }

        [Test]
        public void Encounter_CannotRetriggerAndResolvesOnlyFromActive()
        {
            var transition = CreateTransition(7f, 0.2f);

            Assert.That(transition.Resolve().IsFailure, Is.True);
            transition.TryBegin(2f);
            transition.Tick(0.2f);

            Assert.That(transition.TryBegin(1f), Is.False);
            Assert.That(transition.Resolve().IsSuccess, Is.True);
            Assert.That(transition.Phase, Is.EqualTo(EncounterPhase.Resolved));
            Assert.That(transition.TryBegin(1f), Is.False);
        }

        private static EncounterTransition CreateTransition(float radius, float hold)
        {
            var created = EncounterTransition.Create(radius, hold);
            Assert.That(created.IsSuccess, Is.True, created.Error);
            return created.Value;
        }
    }
}
