using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    // T18: unit-level cooldown bookkeeping on CharacterState.
    public sealed class CharacterStateCooldownTests
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
        public void RemainingCooldown_DefaultsToZero()
        {
            var state = CreateState("unit");

            Assert.That(state.RemainingCooldown("unit-a"), Is.EqualTo(0));
            Assert.That(state.RemainingCooldown("unknown"), Is.EqualTo(0));
            Assert.That(state.RemainingCooldown(null), Is.EqualTo(0));
        }

        [Test]
        public void WithAbilityCooldown_RecordsWithoutMutatingOriginal()
        {
            var state = CreateState("unit");

            var cooled = state.WithAbilityCooldown("unit-a", 2);

            Assert.That(cooled.IsSuccess, Is.True, cooled.Error);
            Assert.That(cooled.Value.RemainingCooldown("unit-a"), Is.EqualTo(2));
            Assert.That(state.RemainingCooldown("unit-a"), Is.EqualTo(0), "The original state must stay untouched.");
            Assert.That(cooled.Value.CurrentHp, Is.EqualTo(state.CurrentHp));
            Assert.That(cooled.Value.Loadout, Is.SameAs(state.Loadout));
        }

        [Test]
        public void WithAbilityCooldown_ZeroClearsTheEntry()
        {
            var state = CreateState("unit").WithAbilityCooldown("unit-a", 2).Value;

            var cleared = state.WithAbilityCooldown("unit-a", 0);

            Assert.That(cleared.IsSuccess, Is.True, cleared.Error);
            Assert.That(cleared.Value.RemainingCooldown("unit-a"), Is.EqualTo(0));
            Assert.That(cleared.Value.AbilityCooldowns, Is.Empty);
        }

        [Test]
        public void WithAbilityCooldown_RejectsInvalidInput()
        {
            var state = CreateState("unit");

            Assert.That(state.WithAbilityCooldown(null, 1).IsFailure, Is.True);
            Assert.That(state.WithAbilityCooldown(" ", 1).IsFailure, Is.True);
            Assert.That(state.WithAbilityCooldown("unit-a", -1).IsFailure, Is.True);
        }

        [Test]
        public void WithCooldownsTicked_DecrementsAndExpires()
        {
            var state = CreateState("unit").WithAbilityCooldown("unit-a", 2).Value;

            var afterOneRound = state.WithCooldownsTicked();
            Assert.That(afterOneRound.RemainingCooldown("unit-a"), Is.EqualTo(1));

            var afterTwoRounds = afterOneRound.WithCooldownsTicked();
            Assert.That(afterTwoRounds.RemainingCooldown("unit-a"), Is.EqualTo(0));
            Assert.That(afterTwoRounds.AbilityCooldowns, Is.Empty, "Expired cooldowns must be removed.");
        }

        [Test]
        public void WithCooldownsTicked_ReturnsSameInstanceWhenNothingIsCooling()
        {
            var state = CreateState("unit");

            Assert.That(state.WithCooldownsTicked(), Is.SameAs(state));
        }

        [Test]
        public void Create_PreservesProvidedCooldowns_AndDropsExpiredEntries()
        {
            var cooldowns = new Dictionary<string, int>
            {
                { "unit-a", 2 },
                { "unit-b", 0 }
            };

            var state = CreateState("unit", cooldowns);

            Assert.That(state.RemainingCooldown("unit-a"), Is.EqualTo(2));
            Assert.That(state.RemainingCooldown("unit-b"), Is.EqualTo(0));
            Assert.That(state.AbilityCooldowns.Count, Is.EqualTo(1));
        }

        [Test]
        public void Create_RejectsNegativeCooldowns()
        {
            var definition = CreateDefinition("unit");
            var abilities = CreateAbilities("unit");

            var state = CharacterState.Create(
                definition,
                slotCount: abilities.Length,
                assignedAbilities: abilities,
                abilityCooldowns: new Dictionary<string, int> { { "unit-a", -1 } });

            Assert.That(state.IsFailure, Is.True);
        }

        private CharacterState CreateState(string id, IReadOnlyDictionary<string, int> cooldowns = null)
        {
            var definition = CreateDefinition(id);
            var abilities = CreateAbilities(id);
            var result = CharacterState.Create(
                definition,
                slotCount: abilities.Length,
                assignedAbilities: abilities,
                abilityCooldowns: cooldowns);
            Assert.That(result.IsSuccess, Is.True, result.Error);
            return result.Value;
        }

        private CharacterDef CreateDefinition(string id)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDef>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "id", id);
            SetPrivateField(definition, "displayName", id);
            SetPrivateField(definition, "maxHp", 10);
            SetPrivateField(definition, "speed", 5);
            return definition;
        }

        private AbilityDef[] CreateAbilities(string id)
        {
            return new[] { CreateAbility(id + "-a"), CreateAbility(id + "-b") };
        }

        private AbilityDef CreateAbility(string id)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDef>();
            createdObjects.Add(ability);
            SetPrivateField(ability, "id", id);
            SetPrivateField(ability, "displayName", id);
            return ability;
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            var field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
