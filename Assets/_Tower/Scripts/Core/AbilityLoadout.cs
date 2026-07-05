using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    public sealed class AbilityLoadout
    {
        public const int MinSlots = 1;
        public const int DefaultSlots = 2;
        public const int MaxSlots = 4;

        private readonly List<AbilityDef> abilities;

        private AbilityLoadout(int slotCount, List<AbilityDef> abilities)
        {
            SlotCount = slotCount;
            this.abilities = abilities;
        }

        public int SlotCount { get; }
        public IReadOnlyList<AbilityDef> Abilities => abilities;

        public static Result<AbilityLoadout> Create(IEnumerable<AbilityDef> assignedAbilities, int slotCount = DefaultSlots)
        {
            var validation = ValidateSlotCount(slotCount);
            if (validation.IsFailure)
            {
                return Result<AbilityLoadout>.Failure(validation.Error);
            }

            if (assignedAbilities == null)
            {
                return Result<AbilityLoadout>.Failure("Assigned abilities are required.");
            }

            var abilityList = assignedAbilities.ToList();
            if (abilityList.Count < slotCount)
            {
                return Result<AbilityLoadout>.Failure($"Ability loadout has {abilityList.Count} abilities, below required slot count {slotCount}.");
            }

            if (abilityList.Count > slotCount)
            {
                return Result<AbilityLoadout>.Failure($"Ability loadout has {abilityList.Count} abilities, above allowed slot count {slotCount}.");
            }

            if (abilityList.Any(ability => ability == null))
            {
                return Result<AbilityLoadout>.Failure("Assigned abilities cannot contain null entries.");
            }

            return Result<AbilityLoadout>.Success(new AbilityLoadout(slotCount, abilityList));
        }

        public Result<AbilityLoadout> WithSlotCount(int slotCount)
        {
            return Create(abilities, slotCount);
        }

        public Result<AbilityLoadout> WithAbilities(IEnumerable<AbilityDef> assignedAbilities)
        {
            return Create(assignedAbilities, SlotCount);
        }

        public static Result ValidateSlotCount(int slotCount)
        {
            if (slotCount < MinSlots)
            {
                return Result.Failure($"Ability slot count must be at least {MinSlots}.");
            }

            if (slotCount > MaxSlots)
            {
                return Result.Failure($"Ability slot count cannot exceed {MaxSlots}.");
            }

            return Result.Success();
        }
    }
}
