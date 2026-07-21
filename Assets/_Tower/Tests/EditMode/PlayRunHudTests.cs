using NUnit.Framework;
using Tower.Combat;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class PlayRunHudTests
    {
        private static CombatantRef PlayerWith(out AbilityDef strike, out AbilityDef guard)
        {
            strike = AbilityDef.CreateRuntime(
                "hud-strike", AbilityTag.Apply, 5, 3, AbilityTargetType.Enemy,
                displayName: "Strike", cooldownSeconds: 4f);
            guard = AbilityDef.CreateRuntime(
                "hud-guard", AbilityTag.Apply, 2, 1, AbilityTargetType.Enemy,
                displayName: "Guard", cooldownSeconds: 0f);
            CharacterDef definition = CharacterDef.CreateRuntime(
                "hud-player", "Returner", 30, 5, 2, 10,
                DispositionType.Aggressive, new[] { strike, guard });
            CharacterState state = CharacterState.Create(
                definition, slotCount: 2, assignedAbilities: definition.DefaultAbilities).Value;
            return CombatantRef.Create("hud-player", CombatTeam.Player, state).Value;
        }

        [Test]
        public void Compose_RunOnlyShowsProgressAndRewards()
        {
            RunLifecycle run = RunLifecycle.CreateNew(4242);

            PlayRunHudModel model = PlayRunHudComposer.Compose(run, null);

            Assert.That(model.RunLine, Does.StartWith("Floor 1/10"));
            Assert.That(model.RunLine, Does.Contain($"Events 0/{run.Progress.Plan.Slots.Count}"));
            Assert.That(model.RewardLine, Does.Contain("Resource x0"));
            Assert.That(model.CombatVisible, Is.False);
            Assert.That(model.Slots, Is.Empty);
        }

        [Test]
        public void Compose_PlayerCombatantFillsHpAndSlots()
        {
            CombatantRef player = PlayerWith(out AbilityDef strike, out AbilityDef guard);
            try
            {
                PlayRunHudModel model = PlayRunHudComposer.Compose(null, player);

                Assert.That(model.CombatVisible, Is.True);
                Assert.That(model.PlayerHpLine, Does.Contain("30/30"));
                Assert.That(model.PlayerHpFraction, Is.EqualTo(1f).Within(0.001f));
                Assert.That(model.Slots, Has.Count.EqualTo(2));
                Assert.That(model.Slots[0].Label, Is.EqualTo("Strike"));
                Assert.That(model.Slots[0].Ready, Is.True);
                Assert.That(model.Slots[1].Ready, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(strike);
                Object.DestroyImmediate(guard);
            }
        }

        [Test]
        public void Compose_ReflectsCooldownFractionAfterUse()
        {
            CombatantRef player = PlayerWith(out AbilityDef strike, out AbilityDef guard);
            try
            {
                CharacterState cooled = player.State.WithAbilityCooldown("hud-strike", 4f).Value;
                cooled = cooled.WithCooldownsAdvanced(1f).Value;
                CombatantRef cooling = player.WithState(cooled);

                PlayRunHudModel model = PlayRunHudComposer.Compose(null, cooling);

                Assert.That(model.Slots[0].Ready, Is.False);
                Assert.That(model.Slots[0].CooldownFraction, Is.EqualTo(0.75f).Within(0.01f));
                Assert.That(model.Slots[1].Ready, Is.True, "zero-cooldown ability is always ready");
            }
            finally
            {
                Object.DestroyImmediate(strike);
                Object.DestroyImmediate(guard);
            }
        }

        [Test]
        public void Compose_PassesSlowMoChargeThrough()
        {
            PlayRunHudModel hidden = PlayRunHudComposer.Compose(null, null);
            PlayRunHudModel shown = PlayRunHudComposer.Compose(null, null, null, 0.42f);

            Assert.That(hidden.SlowMoCharge, Is.LessThan(0f), "gauge hidden by default");
            Assert.That(shown.SlowMoCharge, Is.EqualTo(0.42f).Within(0.001f));
        }

        [Test]
        public void Compose_ConqueredRunReadsAsConquered()
        {
            RunLifecycle run = RunLifecycle.CreateNew(4242);
            while (!run.IsConquered)
            {
                RunEventSlot slot = run.NextPendingEvent;
                while (run.FloorNumber < slot.FloorNumber)
                {
                    run.AdvanceFloor();
                }

                RewardType type = slot.Kind == RunEventKind.Boss ? RewardType.Ability : RewardType.Resource;
                run.ResolveEvent(slot.EventId, EncounterReward.Create(slot.EventId, type, 1, "R").Value);
                if (run.Progress.IsComplete)
                {
                    run.AdvanceFloor();
                }
                else if (run.NextPendingEvent.FloorNumber > run.FloorNumber)
                {
                    run.AdvanceFloor();
                }
            }

            PlayRunHudModel model = PlayRunHudComposer.Compose(run, null);

            Assert.That(model.RunLine, Does.StartWith("CONQUERED"));
        }
    }
}
