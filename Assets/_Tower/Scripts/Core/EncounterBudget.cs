using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // T26: encounter difficulty as a budget object instead of a single flat
    // number. Pure data plus pure depth-ramp helpers: no UnityEngine and no
    // RNG state, so Tower.Gen can compose encounters deterministically from
    // (budget, depth, seed).
    //
    // Reference pacing numbers (Hades): activeEnemyCapBase 2.3, capMax 8,
    // capDepthRamp 0.35, types 1-2, waves 1-3, baseDifficulty 30,
    // depthDifficultyRamp 10. The v0 defaults below intentionally deviate
    // from that reference so composed output stays near the current slice
    // balance (1..5 enemies across slice depths; wave data stays metadata
    // until runtime wave spawning lands).
    public sealed class EncounterBudget
    {
        private static readonly IReadOnlyList<int>[] EmptyTemplates = new IReadOnlyList<int>[0];

        public EncounterBudget(
            int baseDifficulty,
            int depthDifficultyRamp,
            float activeEnemyCapBase,
            float activeEnemyCapDepthRamp,
            int activeEnemyCapMax,
            int minTypes,
            int maxTypes,
            float typeCountDepthRamp,
            int minWaves,
            int maxWaves,
            int eliteCap,
            IReadOnlyList<IReadOnlyList<int>> manualWaveTemplates = null)
        {
            if (baseDifficulty < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDifficulty), "Base difficulty must be at least 1.");
            }

            if (depthDifficultyRamp < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(depthDifficultyRamp), "Depth difficulty ramp cannot be negative.");
            }

            if (activeEnemyCapBase < 1f)
            {
                throw new ArgumentOutOfRangeException(nameof(activeEnemyCapBase), "Active enemy cap base must be at least 1.");
            }

            if (activeEnemyCapDepthRamp < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(activeEnemyCapDepthRamp), "Active enemy cap depth ramp cannot be negative.");
            }

            if (activeEnemyCapMax < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(activeEnemyCapMax), "Active enemy cap max must be at least 1.");
            }

            if (minTypes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minTypes), "Min types must be at least 1.");
            }

            if (maxTypes < minTypes)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTypes), "Max types cannot be below min types.");
            }

            if (typeCountDepthRamp < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(typeCountDepthRamp), "Type count depth ramp cannot be negative.");
            }

            if (minWaves < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(minWaves), "Min waves must be at least 1.");
            }

            if (maxWaves < minWaves)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWaves), "Max waves cannot be below min waves.");
            }

            if (eliteCap < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(eliteCap), "Elite cap cannot be negative.");
            }

            BaseDifficulty = baseDifficulty;
            DepthDifficultyRamp = depthDifficultyRamp;
            ActiveEnemyCapBase = activeEnemyCapBase;
            ActiveEnemyCapDepthRamp = activeEnemyCapDepthRamp;
            ActiveEnemyCapMax = activeEnemyCapMax;
            MinTypes = minTypes;
            MaxTypes = maxTypes;
            TypeCountDepthRamp = typeCountDepthRamp;
            MinWaves = minWaves;
            MaxWaves = maxWaves;
            EliteCap = eliteCap;
            ManualWaveTemplates = CopyTemplates(manualWaveTemplates);
        }

        // v0 defaults tuned so composed encounters stay near the current
        // slice balance (see the class comment for the Hades reference
        // numbers this will migrate toward once wave spawning is runtime).
        public static EncounterBudget Default { get; } = new EncounterBudget(
            baseDifficulty: 30,
            depthDifficultyRamp: 30,
            activeEnemyCapBase: 3f,
            activeEnemyCapDepthRamp: 0.5f,
            activeEnemyCapMax: 5,
            minTypes: 1,
            maxTypes: 2,
            typeCountDepthRamp: 0.5f,
            minWaves: 1,
            maxWaves: 3,
            eliteCap: 1);

        public int BaseDifficulty { get; }

        public int DepthDifficultyRamp { get; }

        public float ActiveEnemyCapBase { get; }

        public float ActiveEnemyCapDepthRamp { get; }

        public int ActiveEnemyCapMax { get; }

        public int MinTypes { get; }

        public int MaxTypes { get; }

        public float TypeCountDepthRamp { get; }

        public int MinWaves { get; }

        public int MaxWaves { get; }

        public int EliteCap { get; }

        // Optional hand-authored wave splits (enemy count per wave). When
        // non-empty the composer uses templates instead of the ramped split.
        // Never null; empty when unset.
        public IReadOnlyList<IReadOnlyList<int>> ManualWaveTemplates { get; }

        // Total difficulty budget for a room at the given depth.
        public int DifficultyAt(int depth)
        {
            return BaseDifficulty + (DepthDifficultyRamp * ClampDepth(depth));
        }

        // Concurrent active enemy ceiling for a room at the given depth.
        public int ActiveEnemyCapAt(int depth)
        {
            int cap = (int)Math.Floor(ActiveEnemyCapBase + (ActiveEnemyCapDepthRamp * ClampDepth(depth)));
            if (cap < 1)
            {
                cap = 1;
            }

            return cap > ActiveEnemyCapMax ? ActiveEnemyCapMax : cap;
        }

        // How many distinct (non-elite) enemy types a room may mix at the
        // given depth.
        public int TypeCountAt(int depth)
        {
            int types = MinTypes + (int)Math.Floor(TypeCountDepthRamp * ClampDepth(depth));
            if (types < MinTypes)
            {
                types = MinTypes;
            }

            return types > MaxTypes ? MaxTypes : types;
        }

        private static int ClampDepth(int depth)
        {
            return depth < 0 ? 0 : depth;
        }

        private static IReadOnlyList<IReadOnlyList<int>> CopyTemplates(IReadOnlyList<IReadOnlyList<int>> templates)
        {
            if (templates == null || templates.Count == 0)
            {
                return EmptyTemplates;
            }

            List<IReadOnlyList<int>> copy = new List<IReadOnlyList<int>>(templates.Count);
            for (int templateIndex = 0; templateIndex < templates.Count; templateIndex++)
            {
                IReadOnlyList<int> template = templates[templateIndex];
                if (template == null || template.Count == 0)
                {
                    throw new ArgumentException("Manual wave templates cannot be null or empty.", nameof(templates));
                }

                List<int> waves = new List<int>(template.Count);
                for (int waveIndex = 0; waveIndex < template.Count; waveIndex++)
                {
                    if (template[waveIndex] < 1)
                    {
                        throw new ArgumentException("Manual wave template entries must be at least 1.", nameof(templates));
                    }

                    waves.Add(template[waveIndex]);
                }

                copy.Add(waves);
            }

            return copy;
        }
    }
}
