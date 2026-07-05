using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tower.Tests.EditMode
{
    public sealed class ExpeditionSaveTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private readonly Dictionary<string, CharacterDef> characterSource =
            new Dictionary<string, CharacterDef>(StringComparer.Ordinal);

        private string tempDirectory;

        [SetUp]
        public void SetUp()
        {
            tempDirectory = Path.Combine(Path.GetTempPath(), "tower-t8-tests", Guid.NewGuid().ToString("N"));
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
        public void SaveThenLoad_RoundTripsExpeditionState()
        {
            // A state with everything populated: a conquered stairway
            // (shortcut + fallen member) and a retreat (retreat count +
            // wounded death count).
            var state = BuildEventfulState();
            var repository = CreateRepository();

            var save = ExpeditionSaveMapper.ToSave(state);
            Assert.That(save.IsSuccess, Is.True, save.Error);
            Assert.That(repository.Save(save.Value).IsSuccess, Is.True);
            Assert.That(repository.HasSave, Is.True);

            var loaded = repository.Load();
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error);
            var restored = ExpeditionSaveMapper.ToState(loaded.Value, FindCharacter);
            Assert.That(restored.IsSuccess, Is.True, restored.Error);

            AssertStatesEqual(state, restored.Value);
        }

        [Test]
        public void Advance_SaveThenLoad_DeadMemberStaysRemoved()
        {
            var state = CreateExpedition("a", "b");
            state = Kill(state, "b");
            state = ClearToTop(state);

            var advanced = ExpeditionRules.ClearFloor(state);
            Assert.That(advanced.IsSuccess, Is.True, advanced.Error);
            Assert.That(advanced.Value.Outcome, Is.EqualTo(ExpeditionOutcome.Advanced));
            Assert.That(advanced.Value.RequiresSave, Is.True);

            var repository = CreateRepository();
            var save = ExpeditionSaveMapper.ToSave(advanced.Value.State);
            Assert.That(save.IsSuccess, Is.True, save.Error);
            Assert.That(repository.Save(save.Value).IsSuccess, Is.True);

            var loaded = ExpeditionSaveMapper.ToState(repository.Load().Value, FindCharacter);
            Assert.That(loaded.IsSuccess, Is.True, loaded.Error);
            Assert.That(loaded.Value.FindMember("b"), Is.Null);
            Assert.That(loaded.Value.FallenIds, Is.EqualTo(new[] { "b" }));
            Assert.That(loaded.Value.Roster.Select(member => member.UnitId), Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void Load_WithoutSaveFile_Fails()
        {
            var repository = CreateRepository();

            Assert.That(repository.HasSave, Is.False);
            Assert.That(repository.Load().IsFailure, Is.True);
        }

        [Test]
        public void Create_WithBlankPath_Fails()
        {
            Assert.That(SaveRepository.Create(" ").IsFailure, Is.True);
        }

        [Test]
        public void ToState_WithUnknownCharacterId_Fails()
        {
            var state = CreateExpedition("a");
            var save = ExpeditionSaveMapper.ToSave(state).Value;

            var restored = ExpeditionSaveMapper.ToState(save, _ => null);

            Assert.That(restored.IsFailure, Is.True);
        }

        private ExpeditionState BuildEventfulState()
        {
            var state = CreateExpedition(stairwayCount: 2, unitIds: new[] { "a", "b", "c" });

            state = ClearToTop(state);
            state = Kill(state, "b");
            var advanced = ExpeditionRules.ClearFloor(state);
            Assert.That(advanced.IsSuccess, Is.True, advanced.Error);

            var checkpoint = advanced.Value.State;
            var current = Kill(checkpoint, "c");
            var retreated = ExpeditionRules.Retreat(current, checkpoint);
            Assert.That(retreated.IsSuccess, Is.True, retreated.Error);
            return retreated.Value.State;
        }

        private static void AssertStatesEqual(ExpeditionState expected, ExpeditionState actual)
        {
            Assert.That(actual.StairwayCount, Is.EqualTo(expected.StairwayCount));
            Assert.That(actual.StairwayIndex, Is.EqualTo(expected.StairwayIndex));
            Assert.That(actual.FloorCount, Is.EqualTo(expected.FloorCount));
            Assert.That(actual.FloorIndex, Is.EqualTo(expected.FloorIndex));
            Assert.That(actual.RetreatCount, Is.EqualTo(expected.RetreatCount));
            Assert.That(actual.IsComplete, Is.EqualTo(expected.IsComplete));
            Assert.That(actual.MissingIds, Is.EqualTo(expected.MissingIds));
            Assert.That(actual.FallenIds, Is.EqualTo(expected.FallenIds));
            Assert.That(actual.ShortcutStairways, Is.EquivalentTo(expected.ShortcutStairways));
            AssertRostersEqual(expected.Roster, actual.Roster);
            AssertRostersEqual(expected.InitialRoster, actual.InitialRoster);
        }

        private static void AssertRostersEqual(
            IReadOnlyList<ExpeditionMember> expected,
            IReadOnlyList<ExpeditionMember> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (var index = 0; index < expected.Count; index++)
            {
                Assert.That(actual[index].UnitId, Is.EqualTo(expected[index].UnitId));
                Assert.That(actual[index].State.Definition.Id, Is.EqualTo(expected[index].State.Definition.Id));
                Assert.That(actual[index].State.CurrentHp, Is.EqualTo(expected[index].State.CurrentHp));
                Assert.That(actual[index].State.DeathCount, Is.EqualTo(expected[index].State.DeathCount));
                Assert.That(
                    actual[index].State.Loadout.SlotCount,
                    Is.EqualTo(expected[index].State.Loadout.SlotCount));
            }
        }

        private SaveRepository CreateRepository()
        {
            var repository = SaveRepository.Create(Path.Combine(tempDirectory, "save.json"));
            Assert.That(repository.IsSuccess, Is.True, repository.Error);
            return repository.Value;
        }

        private ExpeditionState ClearToTop(ExpeditionState state)
        {
            while (state.FloorIndex < state.FloorCount)
            {
                var progress = ExpeditionRules.ClearFloor(state);
                Assert.That(progress.IsSuccess, Is.True, progress.Error);
                state = progress.Value.State;
            }

            return state;
        }

        private ExpeditionState CreateExpedition(params string[] unitIds)
        {
            return CreateExpedition(1, unitIds);
        }

        private ExpeditionState CreateExpedition(int stairwayCount, string[] unitIds)
        {
            var members = unitIds.Select(unitId => CreateMember(unitId)).ToList();
            var state = ExpeditionState.CreateNew(members, stairwayCount);
            Assert.That(state.IsSuccess, Is.True, state.Error);
            return state.Value;
        }

        private ExpeditionMember CreateMember(string unitId)
        {
            var definition = CreateCharacter(unitId);
            var state = CharacterState.Create(definition, slotCount: 1);
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

        private CharacterDef FindCharacter(string characterId)
        {
            return characterSource.TryGetValue(characterId, out var definition) ? definition : null;
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
            characterSource[id] = definition;
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
