using System;
using System.Linq;

namespace Tower.Core
{
    public sealed class CharacterState
    {
        private CharacterState(CharacterDef definition, int currentHp, int deathCount, int speedModifier, AbilityLoadout loadout)
        {
            Definition = definition;
            CurrentHp = currentHp;
            DeathCount = deathCount;
            SpeedModifier = speedModifier;
            Loadout = loadout;
        }

        public CharacterDef Definition { get; }
        public int CurrentHp { get; }
        public int DeathCount { get; }
        public int SpeedModifier { get; }
        public int EffectiveSpeed => Math.Max(0, Definition.Speed + SpeedModifier);
        public AbilityLoadout Loadout { get; }

        public static Result<CharacterState> Create(
            CharacterDef definition,
            int? currentHp = null,
            int deathCount = 0,
            int speedModifier = 0,
            int slotCount = AbilityLoadout.DefaultSlots,
            AbilityDef[] assignedAbilities = null)
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

            return Result<CharacterState>.Success(new CharacterState(definition, hp, deathCount, speedModifier, loadout.Value));
        }
    }
}
