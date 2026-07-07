using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // T26: partial override for an EncounterBudget. Only the fields that are
    // set replace the base budget; everything else flows through. Used by
    // EncounterBudgetTable for biome / room-kind specific tuning.
    public sealed class EncounterBudgetOverride
    {
        public int? BaseDifficulty { get; set; }

        public int? DepthDifficultyRamp { get; set; }

        public float? ActiveEnemyCapBase { get; set; }

        public float? ActiveEnemyCapDepthRamp { get; set; }

        public int? ActiveEnemyCapMax { get; set; }

        public int? MinTypes { get; set; }

        public int? MaxTypes { get; set; }

        public float? TypeCountDepthRamp { get; set; }

        public int? MinWaves { get; set; }

        public int? MaxWaves { get; set; }

        public int? EliteCap { get; set; }

        public IReadOnlyList<IReadOnlyList<int>> ManualWaveTemplates { get; set; }

        // Returns a new budget with the set fields replacing the base
        // values. The EncounterBudget constructor re-validates the merged
        // result, so an override cannot produce an inconsistent budget.
        public EncounterBudget Apply(EncounterBudget baseBudget)
        {
            if (baseBudget == null)
            {
                throw new ArgumentNullException(nameof(baseBudget));
            }

            return new EncounterBudget(
                BaseDifficulty ?? baseBudget.BaseDifficulty,
                DepthDifficultyRamp ?? baseBudget.DepthDifficultyRamp,
                ActiveEnemyCapBase ?? baseBudget.ActiveEnemyCapBase,
                ActiveEnemyCapDepthRamp ?? baseBudget.ActiveEnemyCapDepthRamp,
                ActiveEnemyCapMax ?? baseBudget.ActiveEnemyCapMax,
                MinTypes ?? baseBudget.MinTypes,
                MaxTypes ?? baseBudget.MaxTypes,
                TypeCountDepthRamp ?? baseBudget.TypeCountDepthRamp,
                MinWaves ?? baseBudget.MinWaves,
                MaxWaves ?? baseBudget.MaxWaves,
                EliteCap ?? baseBudget.EliteCap,
                ManualWaveTemplates ?? baseBudget.ManualWaveTemplates);
        }
    }
}
