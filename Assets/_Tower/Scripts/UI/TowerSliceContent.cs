using System;
using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.UI
{
    internal sealed class TowerSliceContent
    {
        private readonly Dictionary<string, CharacterDef> characters = new Dictionary<string, CharacterDef>(StringComparer.Ordinal);

        private TowerSliceContent()
        {
        }

        public IReadOnlyDictionary<string, CharacterDef> Characters => characters;

        public static TowerSliceContent Create()
        {
            var content = new TowerSliceContent();

            var quickSlash = AbilityDef.CreateRuntime("quick-slash", AbilityTag.Consume, 4, 1, AbilityTargetType.Enemy, displayName: "Quick Slash");
            var focusStrike = AbilityDef.CreateRuntime("focus-strike", AbilityTag.Apply, 3, 2, AbilityTargetType.Enemy, displayName: "Focus Strike");
            var burningBrand = AbilityDef.CreateRuntime("burning-brand", AbilityTag.Apply, 3, 3, AbilityTargetType.Enemy, displayName: "Burning Brand");
            var thermalBreak = AbilityDef.CreateRuntime("thermal-break", AbilityTag.Consume, 5, 1, AbilityTargetType.Enemy, displayName: "Thermal Break");
            var holdLine = AbilityDef.CreateRuntime("hold-line", AbilityTag.Amplify, 0, 3, AbilityTargetType.Ally, amplificationMultiplier: 1.5f, displayName: "Hold Line");
            var guardedSurge = AbilityDef.CreateRuntime("guarded-surge", AbilityTag.Apply, 2, 1, AbilityTargetType.Enemy, displayName: "Guarded Surge");
            var frostBolt = AbilityDef.CreateRuntime("frost-bolt", AbilityTag.Apply, 2, 4, AbilityTargetType.Enemy, displayName: "Frost Bolt");
            var shatterFrost = AbilityDef.CreateRuntime("shatter-frost", AbilityTag.Consume, 4, 3, AbilityTargetType.Enemy, displayName: "Shatter Frost");

            content.Add(CharacterDef.CreateRuntime("regressor", "Regressor", 28, 4, 1, 6, DispositionType.Aggressive, new[] { quickSlash, focusStrike }, isReturner: true));
            content.Add(CharacterDef.CreateRuntime("ember", "Ember Vanguard", 22, 4, 1, 5, DispositionType.Aggressive, new[] { burningBrand, thermalBreak }));
            content.Add(CharacterDef.CreateRuntime("ward", "Ward Bearer", 26, 2, 2, 3, DispositionType.Protective, new[] { holdLine, guardedSurge }));
            content.Add(CharacterDef.CreateRuntime("glass", "Glass Breaker", 18, 3, 0, 4, DispositionType.Aggressive, new[] { frostBolt, shatterFrost }));
            content.Add(CharacterDef.CreateRuntime("enemy-melee", "Tower Husk", 12, 2, 0, 3, DispositionType.Aggressive, new[] { quickSlash, focusStrike }));
            content.Add(CharacterDef.CreateRuntime("enemy-ranged", "Ash Sight", 10, 2, 0, 4, DispositionType.Aggressive, new[] { frostBolt, shatterFrost }));
            content.Add(CharacterDef.CreateRuntime("enemy-elite", "Gate Guard", 18, 3, 1, 4, DispositionType.Protective, new[] { guardedSurge, thermalBreak }));
            content.Add(CharacterDef.CreateRuntime("boss", "Stair Anchor", 30, 4, 1, 5, DispositionType.Aggressive, new[] { burningBrand, shatterFrost }));

            return content;
        }

        public static string[] PartyIds => new[] { "regressor", "ember", "ward", "glass" };

        public static int GetSpeedModifier(string characterId)
        {
            return PlayerPrefs.GetInt("tower.speed." + characterId, 0);
        }

        public static void SetSpeedModifier(string characterId, int modifier)
        {
            PlayerPrefs.SetInt("tower.speed." + characterId, Mathf.Clamp(modifier, -2, 2));
            PlayerPrefs.Save();
        }

        public List<ExpeditionMember> CreateRosterFromLoadout()
        {
            var roster = new List<ExpeditionMember>();
            foreach (var id in PartyIds)
            {
                var definition = characters[id];
                var state = CharacterState.Create(
                    definition,
                    speedModifier: GetSpeedModifier(id),
                    slotCount: 2,
                    assignedAbilities: definition.DefaultAbilities);
                Debug.Assert(state.IsSuccess, state.Error);

                var member = ExpeditionMember.Create(id, state.Value);
                Debug.Assert(member.IsSuccess, member.Error);
                roster.Add(member.Value);
            }

            return roster;
        }

        public CharacterDef ResolveCharacter(string characterId)
        {
            characters.TryGetValue(characterId, out var definition);
            return definition;
        }

        private void Add(CharacterDef definition)
        {
            characters.Add(definition.Id, definition);
        }
    }
}
