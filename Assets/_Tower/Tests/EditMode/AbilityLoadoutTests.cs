using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class AbilityLoadoutTests
    {
        private AbilityDef[] abilities;

        [SetUp]
        public void SetUp()
        {
            abilities = new[]
            {
                ScriptableObject.CreateInstance<AbilityDef>(),
                ScriptableObject.CreateInstance<AbilityDef>(),
                ScriptableObject.CreateInstance<AbilityDef>(),
                ScriptableObject.CreateInstance<AbilityDef>()
            };
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var ability in abilities)
            {
                Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void Create_DefaultSlots_AcceptsExactlyTwoAbilities()
        {
            var result = AbilityLoadout.Create(new[] { abilities[0], abilities[1] });

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value.SlotCount, Is.EqualTo(AbilityLoadout.DefaultSlots));
            Assert.That(result.Value.Abilities, Has.Count.EqualTo(2));
        }

        [Test]
        public void Create_AcceptsSlotCountBoundaries()
        {
            var oneSlot = AbilityLoadout.Create(new[] { abilities[0] }, AbilityLoadout.MinSlots);
            var fourSlots = AbilityLoadout.Create(abilities, AbilityLoadout.MaxSlots);

            Assert.That(oneSlot.IsSuccess, Is.True, oneSlot.Error);
            Assert.That(fourSlots.IsSuccess, Is.True, fourSlots.Error);
        }

        [Test]
        public void Create_RejectsSlotCountOutsideOneToFour()
        {
            var below = AbilityLoadout.Create(new[] { abilities[0] }, 0);
            var above = AbilityLoadout.Create(abilities, 5);

            Assert.That(below.IsFailure, Is.True);
            Assert.That(above.IsFailure, Is.True);
        }

        [Test]
        public void Create_RejectsAbilityCountBelowOrAboveSlotCount()
        {
            var below = AbilityLoadout.Create(new[] { abilities[0] }, 2);
            var above = AbilityLoadout.Create(new[] { abilities[0], abilities[1], abilities[2] }, 2);

            Assert.That(below.IsFailure, Is.True);
            Assert.That(above.IsFailure, Is.True);
        }

        [Test]
        public void Create_RejectsNullAbilityEntries()
        {
            var result = AbilityLoadout.Create(new[] { abilities[0], null });

            Assert.That(result.IsFailure, Is.True);
        }
    }
}
