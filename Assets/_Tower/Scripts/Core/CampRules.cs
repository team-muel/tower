using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    public static class CampRules
    {
        public const float RecoveryRatio = 0.3f;

        public static Result<IReadOnlyList<ExpeditionMember>> EnterCamp(
            IReadOnlyList<ExpeditionMember> party,
            ICampEventHook eventHook = null)
        {
            if (party == null)
            {
                return Result<IReadOnlyList<ExpeditionMember>>.Failure("Party is required.");
            }

            eventHook?.OnCampEntered(party);

            List<ExpeditionMember> rested = new List<ExpeditionMember>(party.Count);
            for (int i = 0; i < party.Count; i++)
            {
                ExpeditionMember member = party[i];
                if (member == null)
                {
                    return Result<IReadOnlyList<ExpeditionMember>>.Failure("Party cannot contain null members.");
                }

                if (member.IsDead)
                {
                    rested.Add(member);
                    continue;
                }

                int recovery = CalculateRecovery(member.State.Definition.MaxHp);
                int hp = Math.Min(member.State.Definition.MaxHp, member.State.CurrentHp + recovery);
                Result<CharacterState> healed = CharacterState.Create(
                    member.State.Definition,
                    hp,
                    member.State.DeathCount,
                    member.State.SpeedModifier,
                    member.State.Loadout.SlotCount,
                    member.State.Loadout.Abilities.ToArray());
                if (healed.IsFailure)
                {
                    return Result<IReadOnlyList<ExpeditionMember>>.Failure(healed.Error);
                }

                rested.Add(member.WithState(healed.Value));
            }

            return Result<IReadOnlyList<ExpeditionMember>>.Success(rested);
        }

        public static int CalculateRecovery(int maxHp)
        {
            if (maxHp <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHp), "Max HP must be greater than zero.");
            }

            return (int)Math.Floor((maxHp * RecoveryRatio) + 0.5f);
        }
    }
}
