using System.Collections.Generic;

namespace Tower.Core
{
    // T5: per-disposition scoring weights for the companion/enemy AI.
    // Dispositions are pure data: adding a new disposition means adding a new
    // weight set to the table, never a new branch inside ActionScorer.
    public sealed class DispositionWeights
    {
        public DispositionWeights(
            float damageWeight,
            float killBonus,
            float rangeKeepingWeight,
            float dangerPenalty,
            float allyProtectionWeight,
            float consumeMarkedBonus,
            float applyComboBonus,
            float amplifyNextAllyBonus)
        {
            DamageWeight = damageWeight;
            KillBonus = killBonus;
            RangeKeepingWeight = rangeKeepingWeight;
            DangerPenalty = dangerPenalty;
            AllyProtectionWeight = allyProtectionWeight;
            ConsumeMarkedBonus = consumeMarkedBonus;
            ApplyComboBonus = applyComboBonus;
            AmplifyNextAllyBonus = amplifyNextAllyBonus;
        }

        // Score gained per point of expected damage.
        public float DamageWeight { get; }

        // Flat bonus when the expected damage would defeat the target.
        public float KillBonus { get; }

        // Penalty per cell of |distance to nearest enemy - preferred range|.
        public float RangeKeepingWeight { get; }

        // Penalty per enemy adjacent to the destination cell.
        public float DangerPenalty { get; }

        // Penalty per cell of distance to the protect target (lowest-HP teammate).
        public float AllyProtectionWeight { get; }

        // Combo: Consume ability used on a target carrying the matching mark.
        public float ConsumeMarkedBonus { get; }

        // Combo: Apply ability whose mark a living teammate can consume.
        public float ApplyComboBonus { get; }

        // Combo: Amplify targeting the next-acting teammate in the round order.
        public float AmplifyNextAllyBonus { get; }

        // v0 tuning. Aggressive leans into damage and kills and shrugs at
        // danger; Protective hugs wounded allies and avoids being surrounded.
        public static IReadOnlyDictionary<DispositionType, DispositionWeights> CreateDefaultTable()
        {
            return new Dictionary<DispositionType, DispositionWeights>
            {
                [DispositionType.Aggressive] = new DispositionWeights(
                    damageWeight: 10f,
                    killBonus: 60f,
                    rangeKeepingWeight: 2f,
                    dangerPenalty: 2f,
                    allyProtectionWeight: 0.5f,
                    consumeMarkedBonus: 40f,
                    applyComboBonus: 15f,
                    amplifyNextAllyBonus: 25f),
                [DispositionType.Protective] = new DispositionWeights(
                    damageWeight: 6f,
                    killBonus: 40f,
                    rangeKeepingWeight: 2f,
                    dangerPenalty: 8f,
                    allyProtectionWeight: 6f,
                    consumeMarkedBonus: 40f,
                    applyComboBonus: 15f,
                    amplifyNextAllyBonus: 25f)
            };
        }
    }
}
