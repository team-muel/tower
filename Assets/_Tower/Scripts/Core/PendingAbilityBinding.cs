using System;
using System.Collections.Generic;
using System.Globalization;

namespace Tower.Core
{
    // T19: pure binding data for the command-mode pending-ability popup.
    // Builds slot entries (1..4) from a unit's loadout plus its cooldown
    // state so the UI layer only renders what this returns.
    public static class PendingAbilityBinding
    {
        public static IReadOnlyList<PendingAbilityOption> BuildOptions(CharacterState state, string pendingAbilityId)
        {
            var options = new List<PendingAbilityOption>();
            if (state == null)
            {
                return options;
            }

            var abilities = state.Loadout.Abilities;
            for (var index = 0; index < abilities.Count; index++)
            {
                var ability = abilities[index];
                if (ability == null)
                {
                    continue;
                }

                var remaining = state.RemainingCooldown(ability.Id);
                var isPending = !string.IsNullOrEmpty(pendingAbilityId)
                    && StringComparer.Ordinal.Equals(ability.Id, pendingAbilityId);
                options.Add(new PendingAbilityOption(index + 1, ability.Id, ability.DisplayName, remaining, isPending));
            }

            return options;
        }

        // UX rule 2: cooling entries render grayed with the remaining rounds.
        public static string FormatOptionLabel(PendingAbilityOption option)
        {
            var label = string.Format(CultureInfo.InvariantCulture, "{0}. {1}", option.SlotNumber, option.DisplayName);
            if (option.RemainingCooldown > 0)
            {
                label += string.Format(CultureInfo.InvariantCulture, " (쿨 {0}라운드)", option.RemainingCooldown);
            }

            if (option.IsPending)
            {
                label += " — 예비";
            }

            return label;
        }
    }
}
