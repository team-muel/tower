using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // T26: budget lookup with biome and room-kind overrides. Keys are plain
    // ordinal strings (e.g. BiomeId/RoomKind enum names) so Tower.Core stays
    // independent of Tower.Gen's enums. Resolution order: base budget, then
    // the biome override, then the room-kind override.
    public sealed class EncounterBudgetTable
    {
        private readonly Dictionary<string, EncounterBudgetOverride> biomeOverrides =
            new Dictionary<string, EncounterBudgetOverride>(StringComparer.Ordinal);

        private readonly Dictionary<string, EncounterBudgetOverride> roomKindOverrides =
            new Dictionary<string, EncounterBudgetOverride>(StringComparer.Ordinal);

        public EncounterBudgetTable(EncounterBudget baseBudget)
        {
            if (baseBudget == null)
            {
                throw new ArgumentNullException(nameof(baseBudget));
            }

            BaseBudget = baseBudget;
        }

        public EncounterBudget BaseBudget { get; }

        public void SetBiomeOverride(string biomeKey, EncounterBudgetOverride budgetOverride)
        {
            SetOverride(biomeOverrides, biomeKey, budgetOverride, nameof(biomeKey));
        }

        public void SetRoomKindOverride(string roomKindKey, EncounterBudgetOverride budgetOverride)
        {
            SetOverride(roomKindOverrides, roomKindKey, budgetOverride, nameof(roomKindKey));
        }

        // Null keys are allowed and simply skip that override layer.
        public EncounterBudget Resolve(string biomeKey, string roomKindKey)
        {
            EncounterBudget resolved = BaseBudget;
            EncounterBudgetOverride budgetOverride;
            if (biomeKey != null && biomeOverrides.TryGetValue(biomeKey, out budgetOverride))
            {
                resolved = budgetOverride.Apply(resolved);
            }

            if (roomKindKey != null && roomKindOverrides.TryGetValue(roomKindKey, out budgetOverride))
            {
                resolved = budgetOverride.Apply(resolved);
            }

            return resolved;
        }

        private static void SetOverride(
            Dictionary<string, EncounterBudgetOverride> target,
            string key,
            EncounterBudgetOverride budgetOverride,
            string keyParamName)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Override key is required.", keyParamName);
            }

            if (budgetOverride == null)
            {
                throw new ArgumentNullException(nameof(budgetOverride));
            }

            target[key] = budgetOverride;
        }
    }
}
