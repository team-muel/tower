using System;
using System.Collections.Generic;
using Tower.Core;

namespace Tower.Combat
{
    public sealed class AbilityFeelCatalog
    {
        private readonly Dictionary<string, AbilityDef> abilities = new Dictionary<string, AbilityDef>(StringComparer.Ordinal);
        private readonly Dictionary<string, AbilityFeelDef> overrides = new Dictionary<string, AbilityFeelDef>(StringComparer.Ordinal);

        public static AbilityFeelCatalog Empty { get; } = new AbilityFeelCatalog();

        public static AbilityFeelCatalog FromCombatants(IEnumerable<CombatantRef> combatants)
        {
            var catalog = new AbilityFeelCatalog();
            if (combatants == null)
            {
                return catalog;
            }

            foreach (var combatant in combatants)
            {
                if (combatant?.State?.Loadout?.Abilities == null)
                {
                    continue;
                }

                foreach (var ability in combatant.State.Loadout.Abilities)
                {
                    catalog.RegisterAbility(ability);
                }
            }

            return catalog;
        }

        public void RegisterAbility(AbilityDef ability)
        {
            if (ability == null || string.IsNullOrWhiteSpace(ability.Id))
            {
                return;
            }

            abilities[ability.Id] = ability;
        }

        public void RegisterOverride(string abilityId, AbilityFeelDef feel)
        {
            if (string.IsNullOrWhiteSpace(abilityId) || feel == null)
            {
                return;
            }

            overrides[abilityId] = feel;
        }

        public AbilityDef FindAbility(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return null;
            }

            return abilities.TryGetValue(abilityId, out var ability) ? ability : null;
        }

        public AbilityFeelDef FindOverride(string abilityId)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return null;
            }

            return overrides.TryGetValue(abilityId, out var feel) ? feel : null;
        }
    }
}
