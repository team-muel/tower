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
        public void RemainingCooldownSeconds_DefaultsToZero()
        {
            var state = CreateState("unit");

            Assert.That(state.RemainingCooldownSeconds("unit-a"), Is.EqualTo(0f));
            Assert.That(state.RemainingCooldownSeconds("unknown"), Is.EqualTo(0f));
            Assert.That(state.RemainingCooldownSeconds(null), Is.EqualTo(0f));
        }

        [Test]
        public void WithAbilityCooldown_RecordsWithoutMutatingOriginal()
        {
            var state = CreateState("unit");

            var cooled = state.WithAbilityCooldown("unit-a", 2.5f);

            Assert.That(cooled.IsSuccess, Is.True, cooled.Error);
            Assert.That(cooled.Value.RemainingCooldownSeconds("unit-a"), Is.EqualTo(2.5f));
            Assert.That(state.RemainingCooldownSeconds("unit-a"), Is.EqualTo(0f), "The original state must stay untouched.");
            Assert.That(cooled.Value.CurrentHp, Is.EqualTo(state.CurrentHp));
            Assert.That(cooled.Value.Loadout, Is.SameAs(state.Loadout));
        }

        [Test]
        public void WithAbilityCooldown_ZeroClearsTheEntry()
        {
            var state = CreateState("unit").WithAbilityCooldown("unit-a", 2f).Value;

            var cleared = state.WithAbilityCooldown("unit-a", 0f);

            Assert.That(cleared.IsSuccess, Is.True, cleared.Error);
            Assert.That(cleared.Value.RemainingCooldownSeconds("unit-a"), Is.EqualTo(0f));
            Assert.That(cleared.Value.AbilityCooldowns, Is.Empty);
        }

        [Test]
        public void WithAbilityCooldown_RejectsInvalidInput()
        {
            var state = CreateState("unit");

            Assert.That(state.WithAbilityCooldown(null, 1f).IsFailure, Is.True);
            Assert.That(state.WithAbilityCooldown(" ", 1f).IsFailure, Is.True);
            Assert.That(state.WithAbilityCooldown("unit-a", -1f).IsFailure, Is.True);
            Assert.That(state.WithAbilityCooldown("unit-a", float.NaN).IsFailure, Is.True);
            Assert.That(state.WithAbilityCooldown("unit-a", float.PositiveInfinity).IsFailure, Is.True);
        }

        [Test]
        public void WithCooldownsAdvanced_DecrementsByFractionalSecondsAndExpires()
        {
            var state = CreateState("unit").WithAbilityCooldown("unit-a", 2f).Value;

            var afterFraction = state.WithCooldownsAdvanced(0.75f);
            Assert.That(afterFraction.IsSuccess, Is.True, afterFraction.Error);
            Assert.That(afterFraction.Value.RemainingCooldownSeconds("unit-a"), Is.EqualTo(1.25f).Within(0.0001f));

            var expired = afterFraction.Value.WithCooldownsAdvanced(1.25f);
            Assert.That(expired.IsSuccess, Is.True, expired.Error);
            Assert.That(expired.Value.RemainingCooldownSeconds("unit-a"), Is.EqualTo(0f));
            Assert.That(expired.Value.AbilityCooldowns, Is.Empty, "Expired cooldowns must be removed.");
        }

        [Test]
        public void WithCooldownsAdvanced_ValidatesDeltaAndReturnsSameInstanceForNoWork()
        {
            var state = CreateState("unit");

            Assert.That(state.WithCooldownsAdvanced(0.5f).Value, Is.SameAs(state));
            Assert.That(state.WithCooldownsAdvanced(0f).Value, Is.SameAs(state));
            Assert.That(state.WithCooldownsAdvanced(-0.1f).IsFailure, Is.True);
            Assert.That(state.WithCooldownsAdvanced(float.NaN).IsFailure, Is.True);
            Assert.That(state.WithCooldownsAdvanced(float.PositiveInfinity).IsFailure, Is.True);
        }

        [Test]
        public void Create_PreservesProvidedCooldowns_AndDropsExpiredEntries()
        {
            var cooldowns = new Dictionary<string, float>
            {
                { "unit-a", 2.5f },
                { "unit-b", 0f }
            };

            var state = CreateState("unit", cooldowns);

            Assert.That(state.RemainingCooldownSeconds("unit-a"), Is.EqualTo(2.5f));
            Assert.That(state.RemainingCooldownSeconds("unit-b"), Is.EqualTo(0f));
            Assert.That(state.AbilityCooldowns.Count, Is.EqualTo(1));
        }

        [Test]
        public void Create_RejectsInvalidCooldowns()
        {
            var definition = CreateDefinition("unit");
            var abilities = CreateAbilities("unit");

            var state = CharacterState.Create(
                definition,
                slotCount: abilities.Length,
                assignedAbilities: abilities,
                abilityCooldowns: new Dictionary<string, float> { { "unit-a", -1f } });

            Assert.That(state.IsFailure, Is.True);

            state = CharacterState.Create(
                definition,
                slotCount: abilities.Length,
                assignedAbilities: abilities,
                abilityCooldowns: new Dictionary<string, float> { { "unit-a", float.NaN } });

            Assert.That(state.IsFailure, Is.True);
        }

        private CharacterState CreateState(string id, IReadOnlyDictionary<string, float> cooldowns = null)
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
