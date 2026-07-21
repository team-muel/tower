using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    public sealed class CharacterState
    {
        private readonly Dictionary<string, float> abilityCooldowns;

        private CharacterState(
            CharacterDef definition,
            int currentHp,
            int deathCount,
            int speedModifier,
            AbilityLoadout loadout,
            Dictionary<string, float> abilityCooldowns)
        {
            Definition = definition;
            CurrentHp = currentHp;
            DeathCount = deathCount;
            SpeedModifier = speedModifier;
            Loadout = loadout;
            this.abilityCooldowns = abilityCooldowns;
        }

        public CharacterDef Definition { get; }
        public int CurrentHp { get; }
        public int DeathCount { get; }
        public int SpeedModifier { get; }
        public int EffectiveSpeed => Math.Max(0, Definition.Speed + SpeedModifier);
        public AbilityLoadout Loadout { get; }

        // Remaining cooldown seconds per ability id. Combat-scoped state;
        // it is not persisted into expedition saves.
        public IReadOnlyDictionary<string, float> AbilityCooldowns => abilityCooldowns;

        public static Result<CharacterState> Create(
            CharacterDef definition,
            int? currentHp = null,
            int deathCount = 0,
            int speedModifier = 0,
            int slotCount = AbilityLoadout.DefaultSlots,
            AbilityDef[] assignedAbilities = null,
            IReadOnlyDictionary<string, float> abilityCooldowns = null)
        {
            if (definition == null)
            {
                return Result<CharacterState>.Failure("Character definition is required.");
            }

            if (definition.MaxHp <= 0)
            {
                return Result<CharacterState>.Failure("Character max HP must be greater than zero.");
            }

            var hp = currentHp ?? definition.MaxHp;
            if (hp < 0 || hp > definition.MaxHp)
            {
                return Result<CharacterState>.Failure("Current HP must be between zero and max HP.");
            }

            if (deathCount < 0)
            {
                return Result<CharacterState>.Failure("Death count cannot be negative.");
            }

            var abilities = assignedAbilities ?? definition.DefaultAbilities?.Take(slotCount).ToArray();
            var loadout = AbilityLoadout.Create(abilities, slotCount);
            if (loadout.IsFailure)
            {
                return Result<CharacterState>.Failure(loadout.Error);
            }

            var cooldowns = new Dictionary<string, float>(StringComparer.Ordinal);
            if (abilityCooldowns != null)
            {
                foreach (var entry in abilityCooldowns)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key))
                    {
                        return Result<CharacterState>.Failure("Cooldown ability id is required.");
                    }

                    if (entry.Value < 0f || float.IsNaN(entry.Value) || float.IsInfinity(entry.Value))
                    {
                        return Result<CharacterState>.Failure("Cooldown seconds must be finite and non-negative.");
                    }

                    if (entry.Value > 0f)
                    {
                        cooldowns[entry.Key] = entry.Value;
                    }
                }
            }

            return Result<CharacterState>.Success(
                new CharacterState(definition, hp, deathCount, speedModifier, loadout.Value, cooldowns));
        }

        // T18: remaining cooldown for an ability; zero when the ability is
        // ready (or unknown).
        public float RemainingCooldownSeconds(string abilityId)
        {
            return !string.IsNullOrEmpty(abilityId) && abilityCooldowns.TryGetValue(abilityId, out var remaining)
                ? remaining
                : 0f;
        }

        // T18: records a cooldown after a successful ability use. Zero clears
        // any tracked cooldown for the ability.
        public Result<CharacterState> WithAbilityCooldown(string abilityId, float cooldownSeconds)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return Result<CharacterState>.Failure("Ability id is required.");
            }

            if (cooldownSeconds < 0f || float.IsNaN(cooldownSeconds) || float.IsInfinity(cooldownSeconds))
            {
                return Result<CharacterState>.Failure("Cooldown seconds must be finite and non-negative.");
            }

            var updated = new Dictionary<string, float>(abilityCooldowns, StringComparer.Ordinal);
            if (cooldownSeconds == 0f)
            {
                updated.Remove(abilityId);
            }
            else
            {
                updated[abilityId] = cooldownSeconds;
            }

            return Result<CharacterState>.Success(
                new CharacterState(Definition, CurrentHp, DeathCount, SpeedModifier, Loadout, updated));
        }

        // Advances every tracked cooldown by real elapsed time. Expired entries
        // are removed; zero delta or an empty map returns the same instance.
        public Result<CharacterState> WithCooldownsAdvanced(float deltaSeconds)
        {
            if (deltaSeconds < 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds))
            {
                return Result<CharacterState>.Failure("Cooldown delta must be finite and non-negative.");
            }

            if (abilityCooldowns.Count == 0 || deltaSeconds == 0f)
            {
                return Result<CharacterState>.Success(this);
            }

            var updated = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (var entry in abilityCooldowns)
            {
                float remaining = entry.Value - deltaSeconds;
                if (remaining > 0.0001f)
                {
                    updated[entry.Key] = remaining;
                }
            }

            return Result<CharacterState>.Success(
                new CharacterState(Definition, CurrentHp, DeathCount, SpeedModifier, Loadout, updated));
        }
    }
}
