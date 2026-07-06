using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    // T19: pure binding data for the command-mode pending-ability popup -
    // slot numbering, cooldown gating (UX rule 2), pending flag, labels.
    public sealed class PendingAbilityBindingTests
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
        public void BuildOptions_NumbersSlotsFromOne_InLoadoutOrder()
        {
            var state = CreateState(new[] { "strike", "guard", "heal" });

            var options = PendingAbilityBinding.BuildOptions(state, null);

            Assert.That(options.Count, Is.EqualTo(3));
            Assert.That(options[0].SlotNumber, Is.EqualTo(1));
            Assert.That(options[0].AbilityId, Is.EqualTo("strike"));
            Assert.That(options[1].SlotNumber, Is.EqualTo(2));
            Assert.That(options[1].AbilityId, Is.EqualTo("guard"));
            Assert.That(options[2].SlotNumber, Is.EqualTo(3));
            Assert.That(options[2].AbilityId, Is.EqualTo("heal"));
        }

        [Test]
        public void BuildOptions_CooldownEntries_AreNotSelectable_AndKeepRemainingRounds()
        {
            var cooldowns = new Dictionary<string, int> { { "guard", 2 } };
            var state = CreateState(new[] { "strike", "guard" }, cooldowns);

            var options = PendingAbilityBinding.BuildOptions(state, null);

            Assert.That(options[0].IsSelectable, Is.True);
            Assert.That(options[0].RemainingCooldown, Is.EqualTo(0));
            Assert.That(options[1].IsSelectable, Is.False);
            Assert.That(options[1].RemainingCooldown, Is.EqualTo(2));
        }

        [Test]
        public void BuildOptions_MarksThePendingAbility()
        {
            var state = CreateState(new[] { "strike", "guard" });

            var options = PendingAbilityBinding.BuildOptions(state, "guard");

            Assert.That(options[0].IsPending, Is.False);
            Assert.That(options[1].IsPending, Is.True);
        }

        [Test]
        public void BuildOptions_NullState_ReturnsEmpty()
        {
            var options = PendingAbilityBinding.BuildOptions(null, "strike");

            Assert.That(options, Is.Empty);
        }

        [Test]
        public void FormatOptionLabel_ReadyAbility_IsSlotAndName()
        {
            var option = new PendingAbilityOption(1, "strike", "강타", 0, false);

            Assert.That(PendingAbilityBinding.FormatOptionLabel(option), Is.EqualTo("1. 강타"));
        }

        [Test]
        public void FormatOptionLabel_CoolingAbility_AppendsRemainingRounds()
        {
            var option = new PendingAbilityOption(2, "heal", "회복", 2, false);

            Assert.That(PendingAbilityBinding.FormatOptionLabel(option), Is.EqualTo("2. 회복 (쿨 2라운드)"));
        }

        [Test]
        public void FormatOptionLabel_PendingAbility_IsMarked()
        {
            var option = new PendingAbilityOption(1, "strike", "강타", 0, true);

            Assert.That(PendingAbilityBinding.FormatOptionLabel(option), Does.Contain("예비"));
        }

        private CharacterState CreateState(string[] abilityIds, IReadOnlyDictionary<string, int> cooldowns = null)
        {
            var abilities = new AbilityDef[abilityIds.Length];
            for (var index = 0; index < abilityIds.Length; index++)
            {
                abilities[index] = CreateAbility(abilityIds[index]);
            }

            var definition = ScriptableObject.CreateInstance<CharacterDef>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "id", "unit");
            SetPrivateField(definition, "displayName", "unit");
            SetPrivateField(definition, "maxHp", 20);
            SetPrivateField(definition, "speed", 5);

            var state = CharacterState.Create(
                definition,
                slotCount: abilities.Length,
                assignedAbilities: abilities,
                abilityCooldowns: cooldowns);
            Assert.That(state.IsSuccess, Is.True, state.Error);
            return state.Value;
        }

        private AbilityDef CreateAbility(string id)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDef>();
            createdObjects.Add(ability);
            SetPrivateField(ability, "id", id);
            SetPrivateField(ability, "displayName", id);
            SetPrivateField(ability, "tag", AbilityTag.Apply);
            SetPrivateField(ability, "basePower", 1);
            SetPrivateField(ability, "range", 1);
            SetPrivateField(ability, "targetType", AbilityTargetType.Enemy);
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
