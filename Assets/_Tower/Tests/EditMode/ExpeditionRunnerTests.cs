using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tower.Combat;
using Tower.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tower.Tests.EditMode
{
    // Integration: floor generation + AI-vs-AI combat + advance/retreat rules
    // + checkpoint persistence, end to end through ExpeditionRunner.
    public sealed class ExpeditionRunnerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
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

            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        [Test]
        public void StrongParty_ClearsAllFloorsAndAdvances()
        {
            var repository = CreateRepository();
            var state = CreateExpedition(
                CreateMember("regressor", maxHp: 200, attack: 10, speed: 8),
                CreateMember("ally", maxHp: 200, attack: 10, speed: 7));
            var factory = new TestEnemyFactory(this, maxHp: 2, attack: 0, speed: 1);

            var runner = ExpeditionRunner.Create(state, repository, factory, baseSeed: 424242);
            Assert.That(runner.IsSuccess, Is.True, runner.Error);
            Assert.That(repository.HasSave, Is.True, "Creation writes the initial checkpoint.");

            var outcomes = new List<ExpeditionOutcome>();
            var safety = 0;
            while (!runner.Value.State.IsComplete && safety++ < 10)
            {
                var progress = runner.Value.PlayCurrentFloor();
                Assert.That(progress.IsSuccess, Is.True, progress.Error);
                outcomes.Add(progress.Value.Outcome);
            }

            Assert.That(outcomes, Is.EqualTo(new[]
            {
                ExpeditionOutcome.FloorCleared,
                ExpeditionOutcome.FloorCleared,
                ExpeditionOutcome.Advanced
            }));
            Assert.That(runner.Value.State.IsComplete, Is.True);
            Assert.That(runner.Value.State.HasShortcut(1), Is.True);

            var saved = repository.Load();
            Assert.That(saved.IsSuccess, Is.True, saved.Error);
            Assert.That(saved.Value.isComplete, Is.True);
        }

        [Test]
        public void WeakParty_WipesAndRetreatsToCheckpoint()
        {
            var repository = CreateRepository();
            var state = CreateExpedition(
                CreateMember("regressor", maxHp: 2, attack: 0, speed: 1),
                CreateMember("ally", maxHp: 2, attack: 0, speed: 1));
            var factory = new TestEnemyFactory(this, maxHp: 80, attack: 12, speed: 9);

            var runner = ExpeditionRunner.Create(state, repository, factory, baseSeed: 424242);
            Assert.That(runner.IsSuccess, Is.True, runner.Error);

            var progress = runner.Value.PlayCurrentFloor();
            Assert.That(progress.IsSuccess, Is.True, progress.Error);
            Assert.That(progress.Value.Outcome, Is.EqualTo(ExpeditionOutcome.Retreated));

            var after = runner.Value.State;
            Assert.That(after.FloorIndex, Is.EqualTo(1));
            Assert.That(after.RetreatCount, Is.EqualTo(1));
            Assert.That(after.Roster.All(member => !member.IsDead), Is.True, "The wiped party returns alive.");
            Assert.That(progress.Value.RevivedIds, Is.Not.Empty);
            Assert.That(after.Roster.Any(member => member.State.DeathCount == 1), Is.True);
        }

        private SaveRepository CreateRepository()
        {
            var repository = SaveRepository.Create(Path.Combine(tempDirectory, "save.json"));
            Assert.That(repository.IsSuccess, Is.True, repository.Error);
            return repository.Value;
        }

        private ExpeditionState CreateExpedition(params ExpeditionMember[] members)
        {
            var state = ExpeditionState.CreateNew(members);
            Assert.That(state.IsSuccess, Is.True, state.Error);
            return state.Value;
        }

        private ExpeditionMember CreateMember(string unitId, int maxHp, int attack, int speed)
        {
            var definition = CreateCharacter(unitId, maxHp, attack, speed);
            var state = CharacterState.Create(definition, slotCount: 1);
            Assert.That(state.IsSuccess, Is.True, state.Error);
            var member = ExpeditionMember.Create(unitId, state.Value);
            Assert.That(member.IsSuccess, Is.True, member.Error);
            return member.Value;
        }

        private CharacterDef CreateCharacter(string id, int maxHp, int attack, int speed)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDef>();
            createdObjects.Add(ability);
            SetPrivateField(ability, "id", id + "-strike");
            SetPrivateField(ability, "displayName", id + "-strike");
            SetPrivateField(ability, "tag", AbilityTag.Apply);
            SetPrivateField(ability, "range", 1);
            SetPrivateField(ability, "basePower", 4);
            SetPrivateField(ability, "targetType", AbilityTargetType.Enemy);

            var definition = ScriptableObject.CreateInstance<CharacterDef>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "id", id);
            SetPrivateField(definition, "displayName", id);
            SetPrivateField(definition, "maxHp", maxHp);
            SetPrivateField(definition, "attack", attack);
            SetPrivateField(definition, "speed", speed);
            SetPrivateField(definition, "defaultAbilities", new[] { ability });
            return definition;
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            var field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }

        private sealed class TestEnemyFactory : IExpeditionEnemyFactory
        {
            private readonly ExpeditionRunnerTests owner;
            private readonly int maxHp;
            private readonly int attack;
            private readonly int speed;
            private readonly Dictionary<string, CharacterDef> definitions =
                new Dictionary<string, CharacterDef>(StringComparer.Ordinal);

            public TestEnemyFactory(ExpeditionRunnerTests owner, int maxHp, int attack, int speed)
            {
                this.owner = owner;
                this.maxHp = maxHp;
                this.attack = attack;
                this.speed = speed;
            }

            public Result<CharacterState> Create(string kindSlot, int stairwayIndex, int floorIndex)
            {
                if (!definitions.TryGetValue(kindSlot, out var definition))
                {
                    definition = owner.CreateCharacter("enemy-" + kindSlot, maxHp, attack, speed);
                    definitions[kindSlot] = definition;
                }

                return CharacterState.Create(definition, slotCount: 1);
            }
        }
    }
}
