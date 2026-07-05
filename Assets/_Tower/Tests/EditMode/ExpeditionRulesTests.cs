using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class ExpeditionRulesTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void ClearFloor_IntermediateFloor_MovesUpWithoutSave()
        {
            var state = CreateExpedition("a", "b");

            var progress = ClearFloor(state);

            Assert.That(progress.Outcome, Is.EqualTo(ExpeditionOutcome.FloorCleared));
            Assert.That(progress.RequiresSave, Is.False);
            Assert.That(progress.State.FloorIndex, Is.EqualTo(2));
            Assert.That(progress.State.HasShortcut(1), Is.False);
        }

        [Test]
        public void Advance_LocksInDeadMembersAndGainsShortcut()
        {
            var state = CreateExpedition("a", "b");
            state = ClearFloor(state).State;
            state = ClearFloor(state).State;
            state = Kill(state, "b");

            var progress = ClearFloor(state);

            Assert.That(progress.Outcome, Is.EqualTo(ExpeditionOutcome.Advanced));
            Assert.That(progress.RequiresSave, Is.True);
            Assert.That(progress.ConfirmedDeadIds, Is.EqualTo(new[] { "b" }));
            Assert.That(progress.State.Roster.Select(member => member.UnitId), Is.EqualTo(new[] { "a" }));
            Assert.That(progress.State.FallenIds, Is.EqualTo(new[] { "b" }));
            Assert.That(progress.State.HasShortcut(1), Is.True);
            Assert.That(progress.State.IsComplete, Is.True);
        }

        [Test]
        public void Advance_WithMoreStairways_MovesToNextStairwayFloorOne()
        {
            var state = CreateExpedition(stairwayCount: 2, unitIds: new[] { "a" });
            state = ClearFloor(state).State;
            state = ClearFloor(state).State;

            var progress = ClearFloor(state);

            Assert.That(progress.Outcome, Is.EqualTo(ExpeditionOutcome.Advanced));
            Assert.That(progress.State.IsComplete, Is.False);
            Assert.That(progress.State.StairwayIndex, Is.EqualTo(2));
            Assert.That(progress.State.FloorIndex, Is.EqualTo(1));
            Assert.That(progress.State.HasShortcut(1), Is.True);
            Assert.That(progress.State.HasShortcut(2), Is.False);
        }

        [Test]
        public void Retreat_RollsBackAndRevivesDeadWithDeathCountPlusOne()
        {
            var checkpoint = CreateExpedition("a", "b");
            var current = ClearFloor(checkpoint).State;
            current = Kill(current, "b");

            var progress = Retreat(current, checkpoint);

            Assert.That(progress.Outcome, Is.EqualTo(ExpeditionOutcome.Retreated));
            Assert.That(progress.RequiresSave, Is.True);
            Assert.That(progress.State.FloorIndex, Is.EqualTo(1));
            Assert.That(progress.State.RetreatCount, Is.EqualTo(1));
            Assert.That(progress.RevivedIds, Is.EqualTo(new[] { "b" }));

            var revived = progress.State.FindMember("b");
            Assert.That(revived, Is.Not.Null);
            Assert.That(revived.IsDead, Is.False);
            Assert.That(revived.State.CurrentHp, Is.EqualTo(checkpoint.FindMember("b").State.CurrentHp));
            Assert.That(revived.State.DeathCount, Is.EqualTo(1));
            Assert.That(progress.State.FindMember("a").State.DeathCount, Is.EqualTo(0));
        }

        [Test]
        public void Retreat_ThirdDeath_MarksMemberMissingInsteadOfReviving()
        {
            var checkpoint = CreateExpedition(
                stairwayCount: 1,
                unitIds: new[] { "a", "doomed" },
                deathCounts: new Dictionary<string, int> { ["doomed"] = 2 });
            var current = Kill(checkpoint, "doomed");

            var progress = Retreat(current, checkpoint);

            Assert.That(progress.Outcome, Is.EqualTo(ExpeditionOutcome.Retreated));
            Assert.That(progress.NewlyMissingIds, Is.EqualTo(new[] { "doomed" }));
            Assert.That(progress.RevivedIds, Is.Empty);
            Assert.That(progress.State.MissingIds, Is.EqualTo(new[] { "doomed" }));
            Assert.That(progress.State.FindMember("doomed"), Is.Null);
            Assert.That(progress.State.Roster.Select(member => member.UnitId), Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void Retreat_ThirdRetreat_TriggersGreatRegression()
        {
            var regression = RunGreatRegressionScenario();

            Assert.That(regression.Outcome, Is.EqualTo(ExpeditionOutcome.GreatRegression));
            Assert.That(regression.RequiresSave, Is.True);
            Assert.That(regression.State.StairwayIndex, Is.EqualTo(1));
            Assert.That(regression.State.FloorIndex, Is.EqualTo(1));
            Assert.That(regression.State.RetreatCount, Is.EqualTo(0));

            // Roster resets from the initial template: the fallen member "b"
            // returns fresh, the missing member "c" stays excluded.
            Assert.That(
                regression.State.Roster.Select(member => member.UnitId),
                Is.EquivalentTo(new[] { "a", "b" }));
            Assert.That(regression.State.Roster.All(member => member.State.DeathCount == 0), Is.True);
            Assert.That(regression.State.Roster.All(member => !member.IsDead), Is.True);
            Assert.That(regression.State.MissingIds, Is.EqualTo(new[] { "c" }));
            Assert.That(regression.State.FallenIds, Is.Empty);

            // Shortcuts survive the great regression (v0 decision).
            Assert.That(regression.State.HasShortcut(1), Is.True);
        }

        [Test]
        public void ApplyShortcutGate_ConqueredStairway_SkipsToTopFloor()
        {
            var regressed = RunGreatRegressionScenario().State;
            Assert.That(regressed.HasShortcut(1), Is.True);

            var gated = ExpeditionRules.ApplyShortcutGate(regressed);

            Assert.That(gated.IsSuccess, Is.True, gated.Error);
            Assert.That(gated.Value.FloorIndex, Is.EqualTo(regressed.FloorCount));
            Assert.That(gated.Value.StairwayIndex, Is.EqualTo(1));
        }

        [Test]
        public void ApplyShortcutGate_WithoutShortcut_LeavesStateUnchanged()
        {
            var state = CreateExpedition("a");

            var gated = ExpeditionRules.ApplyShortcutGate(state);

            Assert.That(gated.IsSuccess, Is.True, gated.Error);
            Assert.That(gated.Value.FloorIndex, Is.EqualTo(1));
        }

        [Test]
        public void ClearFloor_WipedParty_Fails()
        {
            var state = CreateExpedition("a");
            state = Kill(state, "a");

            Assert.That(ExpeditionRules.IsPartyWiped(state), Is.True);
            Assert.That(ExpeditionRules.ClearFloor(state).IsFailure, Is.True);
        }

        [Test]
        public void UpdateMemberState_UnknownMember_Fails()
        {
            var state = CreateExpedition("a");

            var updated = ExpeditionRules.UpdateMemberState(state, "ghost", state.FindMember("a").State);

            Assert.That(updated.IsFailure, Is.True);
        }

        // Shared scenario: two stairways; "b" falls at the stairway 1 advance,
        // "c" (two prior deaths) goes missing on the first retreat, then two
        // more retreats trigger the great regression.
        private ExpeditionProgress RunGreatRegressionScenario()
        {
            var state = CreateExpedition(
                stairwayCount: 2,
                unitIds: new[] { "a", "b", "c" },
                deathCounts: new Dictionary<string, int> { ["c"] = 2 });

            state = ClearFloor(state).State;
            state = ClearFloor(state).State;
            state = Kill(state, "b");
            var advanced = ClearFloor(state);
            Assert.That(advanced.Outcome, Is.EqualTo(ExpeditionOutcome.Advanced));

            var checkpoint = advanced.State;
            var current = Kill(checkpoint, "c");
            var firstRetreat = Retreat(current, checkpoint);
            Assert.That(firstRetreat.Outcome, Is.EqualTo(ExpeditionOutcome.Retreated));
            Assert.That(firstRetreat.State.MissingIds, Does.Contain("c"));

            checkpoint = firstRetreat.State;
            var secondRetreat = Retreat(checkpoint, checkpoint);
            Assert.That(secondRetreat.Outcome, Is.EqualTo(ExpeditionOutcome.Retreated));

            checkpoint = secondRetreat.State;
            var thirdRetreat = Retreat(checkpoint, checkpoint);
            Assert.That(thirdRetreat.Outcome, Is.EqualTo(ExpeditionOutcome.GreatRegression));
            return thirdRetreat;
        }

        private ExpeditionState CreateExpedition(params string[] unitIds)
        {
            return CreateExpedition(1, unitIds);
        }

        private ExpeditionState CreateExpedition(
            int stairwayCount,
            string[] unitIds,
            Dictionary<string, int> deathCounts = null)
        {
            var members = unitIds
                .Select(unitId => CreateMember(
                    unitId,
                    deathCounts != null && deathCounts.TryGetValue(unitId, out var count) ? count : 0))
                .ToList();
            var state = ExpeditionState.CreateNew(members, stairwayCount);
            Assert.That(state.IsSuccess, Is.True, state.Error);
            return state.Value;
        }

        private ExpeditionMember CreateMember(string unitId, int deathCount)
        {
            var definition = CreateCharacter(unitId);
            var state = CharacterState.Create(definition, deathCount: deathCount, slotCount: 1);
            Assert.That(state.IsSuccess, Is.True, state.Error);
            var member = ExpeditionMember.Create(unitId, state.Value);
            Assert.That(member.IsSuccess, Is.True, member.Error);
            return member.Value;
        }

        private ExpeditionState Kill(ExpeditionState state, string unitId)
        {
            var member = state.FindMember(unitId);
            Assert.That(member, Is.Not.Null, unitId);
            var dead = CharacterState.Create(
                member.State.Definition,
                0,
                member.State.DeathCount,
                member.State.SpeedModifier,
                member.State.Loadout.SlotCount,
                member.State.Loadout.Abilities.ToArray());
            Assert.That(dead.IsSuccess, Is.True, dead.Error);
            var updated = ExpeditionRules.UpdateMemberState(state, unitId, dead.Value);
            Assert.That(updated.IsSuccess, Is.True, updated.Error);
            return updated.Value;
        }

        private static ExpeditionProgress ClearFloor(ExpeditionState state)
        {
            var progress = ExpeditionRules.ClearFloor(state);
            Assert.That(progress.IsSuccess, Is.True, progress.Error);
            return progress.Value;
        }

        private static ExpeditionProgress Retreat(ExpeditionState current, ExpeditionState checkpoint)
        {
            var progress = ExpeditionRules.Retreat(current, checkpoint);
            Assert.That(progress.IsSuccess, Is.True, progress.Error);
            return progress.Value;
        }

        private CharacterDef CreateCharacter(string id)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDef>();
            createdObjects.Add(ability);
            SetPrivateField(ability, "id", id + "-strike");
            SetPrivateField(ability, "displayName", id + "-strike");
            SetPrivateField(ability, "tag", AbilityTag.Apply);
            SetPrivateField(ability, "range", 1);
            SetPrivateField(ability, "basePower", 3);
            SetPrivateField(ability, "targetType", AbilityTargetType.Enemy);

            var definition = ScriptableObject.CreateInstance<CharacterDef>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "id", id);
            SetPrivateField(definition, "displayName", id);
            SetPrivateField(definition, "maxHp", 10);
            SetPrivateField(definition, "attack", 2);
            SetPrivateField(definition, "speed", 5);
            SetPrivateField(definition, "defaultAbilities", new[] { ability });
            return definition;
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            var field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
