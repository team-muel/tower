using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    public sealed class TelegraphStateTests
    {
        [Test]
        public void Cycle_AlwaysEntersWindupBeforeCommit()
        {
            var state = NewState();

            Assert.That(state.Phase, Is.EqualTo(TelegraphPhase.Idle));
            Assert.That(state.TryBeginWindup(), Is.True);
            Assert.That(state.Phase, Is.EqualTo(TelegraphPhase.Windup));
            state.Advance(0.9f);

            Assert.That(state.Phase, Is.EqualTo(TelegraphPhase.Commit));
            Assert.That(state.CommitStartedAt, Is.EqualTo(0.9f).Within(0.0001f));
        }

        [Test]
        public void Commit_CannotBeReenteredMidCycle()
        {
            var state = NewState();
            state.TryBeginWindup();
            state.Advance(0.9f);

            Assert.That(state.TryBeginWindup(), Is.False);
            state.Advance(0.1f);
            Assert.That(state.Phase, Is.EqualTo(TelegraphPhase.Commit));
        }

        [Test]
        public void CycleDuration_EqualsInjectedPhaseSum()
        {
            var state = NewState();
            state.TryBeginWindup();
            state.Advance(1.75f);

            Assert.That(state.Phase, Is.EqualTo(TelegraphPhase.Idle));
            Assert.That(state.ElapsedSeconds, Is.EqualTo(1.75f).Within(0.0001f));
        }

        private static TelegraphState NewState()
        {
            return new TelegraphState(new TelegraphDurations(0.9f, 0.25f, 0.6f));
        }
    }
}
