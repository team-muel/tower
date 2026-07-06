using System.Linq;
using NUnit.Framework;
using Tower.Core;
using UnityEditor;

namespace Tower.Tests.EditMode
{
    public sealed class T1AssetIntegrityTests
    {
        [Test]
        public void SliceContent_HasRequiredAssetCounts()
        {
            Assert.That(LoadAssets<MarkDef>("Assets/_Tower/Data/Marks"), Has.Length.EqualTo(2));
            Assert.That(LoadAssets<AbilityDef>("Assets/_Tower/Data/Abilities"), Has.Length.EqualTo(10));
            Assert.That(LoadAssets<PassiveDef>("Assets/_Tower/Data/Passives"), Has.Length.EqualTo(3));
            Assert.That(LoadAssets<CharacterDef>("Assets/_Tower/Data/Characters"), Has.Length.EqualTo(4));
        }

        [Test]
        public void AbilityDefs_HaveTagSpecificRequiredFields()
        {
            var abilities = LoadAssets<AbilityDef>("Assets/_Tower/Data/Abilities");

            Assert.That(abilities.Count(ability => ability.Tag == AbilityTag.Apply), Is.EqualTo(3));
            Assert.That(abilities.Count(ability => ability.Tag == AbilityTag.Consume), Is.EqualTo(3));
            Assert.That(abilities.Count(ability => ability.Tag == AbilityTag.Amplify), Is.EqualTo(2));
            Assert.That(abilities.Count(ability => ability.Tag == AbilityTag.None), Is.EqualTo(2));

            // T18: a few slice abilities carry a 1-2 round cooldown.
            Assert.That(abilities.Count(ability => ability.CooldownRounds > 0), Is.EqualTo(3));

            foreach (var ability in abilities)
            {
                Assert.That(ability.Id, Is.Not.Empty, ability.name);
                Assert.That(ability.DisplayName, Is.Not.Empty, ability.name);
                Assert.That(ability.Range, Is.GreaterThanOrEqualTo(0), ability.name);
                Assert.That(ability.Cost, Is.GreaterThanOrEqualTo(0), ability.name);
                Assert.That(ability.CooldownRounds, Is.InRange(0, 2), ability.name);

                if (ability.Tag == AbilityTag.Apply || ability.Tag == AbilityTag.Consume)
                {
                    Assert.That(ability.TargetMark, Is.Not.Null, ability.name);
                }

                if (ability.Tag == AbilityTag.Amplify)
                {
                    Assert.That(ability.AmplificationMultiplier, Is.GreaterThan(1f), ability.name);
                }
            }
        }

        [Test]
        public void CharacterDefs_HaveValidDefaultsForVerticalSlice()
        {
            var characters = LoadAssets<CharacterDef>("Assets/_Tower/Data/Characters");

            Assert.That(characters.Count(character => character.IsReturner), Is.EqualTo(1));
            Assert.That(characters.Count(character => !character.IsReturner), Is.EqualTo(3));
            Assert.That(characters.Count(character => character.Disposition == DispositionType.Aggressive), Is.EqualTo(2));
            Assert.That(characters.Count(character => character.Disposition == DispositionType.Protective), Is.EqualTo(2));

            foreach (var character in characters)
            {
                Assert.That(character.Id, Is.Not.Empty, character.name);
                Assert.That(character.DisplayName, Is.Not.Empty, character.name);
                Assert.That(character.MaxHp, Is.GreaterThan(0), character.name);
                Assert.That(character.Speed, Is.GreaterThanOrEqualTo(0), character.name);
                Assert.That(character.Passive, Is.Not.Null, character.name);
                Assert.That(character.DefaultAbilities, Has.Length.EqualTo(AbilityLoadout.DefaultSlots), character.name);
                Assert.That(character.DefaultAbilities, Has.All.Not.Null, character.name);

                var state = CharacterState.Create(character);
                Assert.That(state.IsSuccess, Is.True, state.Error);
            }
        }

        private static T[] LoadAssets<T>(string folder) where T : UnityEngine.Object
        {
            return AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset != null)
                .ToArray();
        }
    }
}
