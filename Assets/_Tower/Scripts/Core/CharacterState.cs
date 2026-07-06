using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    public sealed class CharacterState
    {
        private CharacterState(
            CharacterDef definition,
            int currentHp,
            int deathCount,
            int speedModifier,
            AbilityLoadout loadout,
            IReadOnlyDictionary<string, int> cooldowns)
        {
            Definition = definition;
            CurrentHp = currentHp;
            DeathCount = deathCount;
            SpeedModifier = speedModifier;
            Loadout = loadout;
            Cooldowns = cooldowns ?? new Dictionary<string, int>(StringComparer.Ordinal);
        }

        public CharacterDef Definition { get; }
        public int CurrentHp { get; }
        public int DeathCount { get; }
        public int SpeedModifier { get; }
        public int EffectiveSpeed => Math.Max(0, Definition.Speed + SpeedModifier);
        public AbilityLoadout Loadout { get; }
        public IReadOnlyDictionary<string, int> Cooldowns { get; }

        public static Result<CharacterState> Create(
            CharacterDef definition,
            int? currentHp = null,
            int deathCount = 0,
            int speedModifier = 0,
            int slotCount = AbilityLoadout.DefaultSlots,
            AbilityDef[] assignedAbilities = null,
            IReadOnlyDictionary<string, int> cooldowns = null)
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

            return Result<CharacterState>.Success(new CharacterState(definition, hp, deathCount, speedModifier, loadout.Value, cooldowns));
        }

        public CharacterState WithHp(int hp)
        {
            return new CharacterState(Definition, hp, DeathCount, SpeedModifier, Loadout, Cooldowns);
        }

        public CharacterState WithCooldown(string abilityId, int rounds)
        {
            var nextCooldowns = new Dictionary<string, int>(Cooldowns, StringComparer.Ordinal);
            if (rounds > 0)
            {
                nextCooldowns[abilityId] = rounds;
            }
            else
            {
                nextCooldowns.Remove(abilityId);
            }
            return new CharacterState(Definition, CurrentHp, DeathCount, SpeedModifier, Loadout, nextCooldowns);
        }

        public CharacterState AdvanceCooldowns()
        {
            var nextCooldowns = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var kvp in Cooldowns)
            {
                if (kvp.Value > 1)
                {
                    nextCooldowns[kvp.Key] = kvp.Value - 1;
                }
            }
            return new CharacterState(Definition, CurrentHp, DeathCount, SpeedModifier, Loadout, nextCooldowns);
        }
    }
}
