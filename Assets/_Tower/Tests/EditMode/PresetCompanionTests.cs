using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tower.Tests.EditMode
{
    // T12: preset companions secretly ignore the permanent three-death rule.
    // Externally (MissingIds, roster, progress payloads) they look exactly
    // like generated companions going missing; the great regression brings
    // them back with their death count reset (v0).
    public sealed class PresetCompanionTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private readonly Dictionary<string, CharacterDef> characterSource =
            new Dictionary<string, CharacterDef>(StringComparer.Ordinal);

        private string tempDirectory;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "tower-t12-tests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
            characterSource.Clear();

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Test]
        public void Retreat_ThirdDeath_PresetLooksMissingExternally()
        {
            var checkpoint = CreateExpedition(
                CreateMember("a"),
                CreateMember("p", isPreset: true, factionId: 1, deathCount: 2));
            var current = Kill(checkpoint, "p");

            var progress = Retreat(current, checkpoint);

            // Externally identical to a generated companion going missing.
            Assert.That(progress.Outcome, Is.EqualTo(ExpeditionOutcome.Retreated));
            Assert.That(progress.NewlyMissingIds, Is.EqualTo(new[] { "p" }));
            Assert.That(progress.RevivedIds, Is.Empty);
            Assert.That(progress.State.MissingIds, Is.EqualTo(new[] { "p" }));
            Assert.That(progress.State.FindMember("p"), Is.Null);
            Assert.That(progress.State.Roster.Select(member => member.UnitId), Is.EqualTo(new[] { "a" }));

            // Internal record only: the preset is flagged for return.
            Assert.That(progress.State.HiddenMissingIds, Is.EqualTo(new[] { "p" }));
        }

        [Test]
        public void Retreat_ThirdDeath_GeneratedCompanionIsNotHidden()
        {
            var checkpoint = CreateExpedition(
                CreateMember("a"),
                CreateMember("g", isPreset: false, factionId: 1, deathCount: 2));
            var current = Kill(checkpoint, "g");

            var progress = Retreat(current, checkpoint);

            Assert.That(progress.State.MissingIds, Is.EqualTo(new[] { "g" }));
            Assert.That(progress.State.HiddenMissingIds, Is.Empty);
        }

        [Test]
        public void GreatRegression_HiddenMissingPresetReturnsWithDeathCountReset()
        {
            var regression = RunGreatRegressionScenario(includeGenerated: false);

            Assert.That(regression.Outcome, Is.EqualTo(ExpeditionOutcome.GreatRegression));
            Assert.That(
                regression.State.Roster.Select(member => member.UnitId),
                Is.EquivalentTo(new[] { "a", "p" }));

            var returned = regression.State.FindMember("p");
            Assert.That(returned, Is.Not.Null);
            Assert.That(returned.IsDead, Is.False);
            Assert.That(returned.State.DeathCount, Is.EqualTo(0), "death count resets on return (v0)");

            // The pretence leaves no trace: neither missing nor hidden.
            Assert.That(regression.State.MissingIds, Is.Empty);
            Assert.That(regression.State.HiddenMissingIds, Is.Empty);
        }

        [Test]
        public void GreatRegression_GeneratedCompanionStaysPermanentlyMissing()
        {
            var regression = RunGreatRegressionScenario(includeGenerated: true);

            Assert.That(
                regression.State.Roster.Select(member => member.UnitId),
                Is.EquivalentTo(new[] { "a", "p" }));
            Assert.That(regression.State.MissingIds, Is.EqualTo(new[] { "g" }));
            Assert.That(regression.State.HiddenMissingIds, Is.Empty);
            Assert.That(regression.State.FindMember("g"), Is.Null);
        }

        [Test]
        public void SaveRoundTrip_PreservesHiddenMissing_AndPresetStillReturns()
        {
            var checkpoint = CreateExpedition(
                CreateMember("a"),
                CreateMember("p", isPreset: true, factionId: 2, deathCount: 2));
            var current = Kill(checkpoint, "p");
            var hiddenState = Retreat(current, checkpoint).State;
            Assert.That(hiddenState.HiddenMissingIds, Is.EqualTo(new[] { "p" }));

            var repository = SaveRepository.Create(Path.Combine(tempDirectory, "save.json"));
            Assert.That(repository.IsSuccess, Is.True, repository.Error);

            var save = ExpeditionSaveMapper.ToSave(hiddenState);
            Assert.That(save.IsSuccess, Is.True, save.Error);
            Assert.That(repository.Value.Save(save.Value).IsSuccess, Is.True);

            var loaded = repository.Value.Load();
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error);
            var restored = ExpeditionSaveMapper.ToState(loaded.Value, FindCharacter);
            Assert.That(restored.IsSuccess, Is.True, restored.Error);

            Assert.That(restored.Value.MissingIds, Is.EqualTo(hiddenState.MissingIds));
            Assert.That(restored.Value.HiddenMissingIds, Is.EqualTo(new[] { "p" }));

            // The reloaded state still honours the hidden-missing rule.
            var second = Retreat(restored.Value, restored.Value).State;
            var third = Retreat(second, second);
            Assert.That(third.Outcome, Is.EqualTo(ExpeditionOutcome.GreatRegression));
            Assert.That(third.State.FindMember("p"), Is.Not.Null);
            Assert.That(third.State.MissingIds, Is.Empty);
        }

        [Test]
        public void ToState_SaveWithoutHiddenMissingIds_DefaultsToEmpty()
        {
            var state = CreateExpedition(CreateMember("a"));
            var save = ExpeditionSaveMapper.ToSave(state).Value;
            save.hiddenMissingIds = null;

            var restored = ExpeditionSaveMapper.ToState(save, FindCharacter);

            Assert.That(restored.IsSuccess, Is.True, restored.Error);
            Assert.That(restored.Value.HiddenMissingIds, Is.Empty);
        }

        // Scenario: "p" (preset, two prior deaths) and optionally "g"
        // (generated, two prior deaths) die and go missing on the first
        // retreat; two more retreats trigger the great regression.
        private ExpeditionProgress RunGreatRegressionScenario(bool includeGenerated)
        {
            var members = new List<ExpeditionMember>
            {
                CreateMember("a"),
                CreateMember("p", isPreset: true, factionId: 1, deathCount: 2)
            };
            if (includeGenerated)
            {
                members.Add(CreateMember("g", isPreset: false, factionId: 1, deathCount: 2));
            }

            var checkpoint = CreateExpedition(members.ToArray());
            var current = Kill(checkpoint, "p");
            if (includeGenerated)
            {
                current = Kill(current, "g");
            }

            var firstRetreat = Retreat(current, checkpoint);
            Assert.That(firstRetreat.Outcome, Is.EqualTo(ExpeditionOutcome.Retreated));
            Assert.That(firstRetreat.State.MissingIds, Does.Contain("p"));

            var secondRetreat = Retreat(firstRetreat.State, firstRetreat.State);
            Assert.That(secondRetreat.Outcome, Is.EqualTo(ExpeditionOutcome.Retreated));

            var thirdRetreat = Retreat(secondRetreat.State, secondRetreat.State);
            Assert.That(thirdRetreat.Outcome, Is.EqualTo(ExpeditionOutcome.GreatRegression));
            return thirdRetreat;
        }

        private ExpeditionState CreateExpedition(params ExpeditionMember[] members)
        {
            var state = ExpeditionState.CreateNew(members);
            Assert.That(state.IsSuccess, Is.True, state.Error);
            return state.Value;
        }

        private ExpeditionMember CreateMember(
            string unitId,
            bool isPreset = false,
            int factionId = CharacterDef.NoFactionId,
            int deathCount = 0)
        {
            var ability = AbilityDef.CreateRuntime(
                unitId + "-strike",
                AbilityTag.Apply,
                basePower: 3,
                range: 1,
                targetType: AbilityTargetType.Enemy);
            createdObjects.Add(ability);

            var definition = CharacterDef.CreateRuntime(
                unitId,
                unitId,
                maxHp: 10,
                attack: 2,
                defense: 0,
                speed: 5,
                disposition: DispositionType.Aggressive,
                defaultAbilities: new[] { ability },
                isPreset: isPreset,
                factionId: factionId);
            createdObjects.Add(definition);
            characterSource[unitId] = definition;

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

        private static ExpeditionProgress Retreat(ExpeditionState current, ExpeditionState checkpoint)
        {
            var progress = ExpeditionRules.Retreat(current, checkpoint);
            Assert.That(progress.IsSuccess, Is.True, progress.Error);
            return progress.Value;
        }

        private CharacterDef FindCharacter(string characterId)
        {
            return characterSource.TryGetValue(characterId, out var definition) ? definition : null;
        }
    }
}
