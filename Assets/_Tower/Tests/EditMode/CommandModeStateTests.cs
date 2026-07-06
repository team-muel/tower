using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    // T19: command mode state machine - combat-gated entry, always-exit,
    // presenter-local playback factor (never Time.timeScale).
    public sealed class CommandModeStateTests
    {
        [Test]
        public void StartsInactive_WithNormalPlayback()
        {
            var state = new CommandModeState();

            Assert.That(state.IsActive, Is.False);
            Assert.That(state.PlaybackFactor, Is.EqualTo(1f));
        }

        [Test]
        public void Toggle_OutsideCombat_FailsAndStaysInactive()
        {
            var state = new CommandModeState();

            var result = state.Toggle(combatActive: false);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("combat"));
            Assert.That(state.IsActive, Is.False);
        }

        [Test]
        public void Toggle_InCombat_Enters_WithSlowPlayback()
        {
            var state = new CommandModeState();

            var result = state.Toggle(combatActive: true);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(state.IsActive, Is.True);
            Assert.That(state.PlaybackFactor, Is.EqualTo(CommandModeState.SlowPlaybackFactor));
        }

        [Test]
        public void Toggle_Twice_ExitsAndRestoresPlayback()
        {
            var state = new CommandModeState();
            Assert.That(state.Toggle(combatActive: true).IsSuccess, Is.True);

            var result = state.Toggle(combatActive: true);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(state.IsActive, Is.False);
            Assert.That(state.PlaybackFactor, Is.EqualTo(1f));
        }

        [Test]
        public void Toggle_WhileActive_AlwaysExits_EvenAfterCombatEnds()
        {
            var state = new CommandModeState();
            Assert.That(state.Toggle(combatActive: true).IsSuccess, Is.True);

            var result = state.Toggle(combatActive: false);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(state.IsActive, Is.False);
        }

        [Test]
        public void SyncCombatActive_CombatEnded_ForcesExitOnce()
        {
            var state = new CommandModeState();
            Assert.That(state.Toggle(combatActive: true).IsSuccess, Is.True);

            Assert.That(state.SyncCombatActive(false), Is.True, "First sync must deactivate.");
            Assert.That(state.IsActive, Is.False);
            Assert.That(state.SyncCombatActive(false), Is.False, "Second sync is a no-op.");
        }

        [Test]
        public void SyncCombatActive_CombatRunning_KeepsMode()
        {
            var state = new CommandModeState();
            Assert.That(state.Toggle(combatActive: true).IsSuccess, Is.True);

            Assert.That(state.SyncCombatActive(true), Is.False);
            Assert.That(state.IsActive, Is.True);
        }
    }
}
