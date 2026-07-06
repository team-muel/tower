using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    public sealed class CharacterState
    {
        private readonly Dictionary<string, int> abilityCooldowns;

        private CharacterState(
            CharacterDef definition,
            int currentHp,
            int deathCount,
            int speedModifier,
            AbilityLoadout loadout,
            Dictionary<string, int> abilityCooldowns)
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

        // T18: remaining cooldown rounds per ability id. Combat-scoped state;
        // it is not persisted into expedition saves.
        public IReadOnlyDictionary<string, int> AbilityCooldowns => abilityCooldowns;

        public static Result<CharacterState> Create(
            CharacterDef definition,
            int? currentHp = null,
            int deathCount = 0,
            int speedModifier = 0,
            int slotCount = AbilityLoadout.DefaultSlots,
            AbilityDef[] assignedAbilities = null,
            IReadOnlyDictionary<string, int> abilityCooldowns = null)
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

            var cooldowns = new Dictionary<string, int>(StringComparer.Ordinal);
            if (abilityCooldowns != null)
            {
                foreach (var entry in abilityCooldowns)
                {
                    if (string.IsNullOrWhiteSpace(entry.Key))
                    {
                        return Result<CharacterState>.Failure("Cooldown ability id is required.");
                    }

                    if (entry.Value < 0)
                    {
                        return Result<CharacterState>.Failure("Cooldown rounds cannot be negative.");
                    }

                    if (entry.Value > 0)
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
        public int RemainingCooldown(string abilityId)
        {
            return !string.IsNullOrEmpty(abilityId) && abilityCooldowns.TryGetValue(abilityId, out var remaining)
                ? remaining
                : 0;
        }

        // T18: records a cooldown after a successful ability use. Zero clears
        // any tracked cooldown for the ability.
        public Result<CharacterState> WithAbilityCooldown(string abilityId, int cooldownRounds)
        {
            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return Result<CharacterState>.Failure("Ability id is required.");
            }

            if (cooldownRounds < 0)
            {
                return Result<CharacterState>.Failure("Cooldown rounds cannot be negative.");
            }

            var updated = new Dictionary<string, int>(abilityCooldowns, StringComparer.Ordinal);
            if (cooldownRounds == 0)
            {
                updated.Remove(abilityId);
            }
            else
            {
                updated[abilityId] = cooldownRounds;
            }

            return Result<CharacterState>.Success(
                new CharacterState(Definition, CurrentHp, DeathCount, SpeedModifier, Loadout, updated));
        }

        // T18: round-boundary tick — every tracked cooldown drops by one and
        // expired entries are removed. Returns the same instance when there is
        // nothing to tick.
        public CharacterState WithCooldownsTicked()
        {
            if (abilityCooldowns.Count == 0)
            {
                return this;
            }

            var updated = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var entry in abilityCooldowns)
            {
                if (entry.Value > 1)
                {
                    updated[entry.Key] = entry.Value - 1;
                }
            }

            return new CharacterState(Definition, CurrentHp, DeathCount, SpeedModifier, Loadout, updated);
        }
    }
}
