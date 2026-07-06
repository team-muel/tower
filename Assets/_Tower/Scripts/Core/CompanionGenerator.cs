using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Tower.Core
{
    // Inclusive integer range for generated stats.
    public readonly struct StatRange
    {
        public StatRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public int Min { get; }
        public int Max { get; }
        public bool IsValid => Min <= Max;

        public int Roll(Random rng)
        {
            return rng.Next(Min, Max + 1);
        }
    }

    // Generator input data: name pool + stat ranges. Pure data so pools and
    // tuning grow without code changes.
    public sealed class CompanionGeneratorConfig
    {
        private CompanionGeneratorConfig(
            IReadOnlyList<string> namePool,
            StatRange maxHp,
            StatRange attack,
            StatRange defense,
            StatRange speed)
        {
            NamePool = namePool;
            MaxHp = maxHp;
            Attack = attack;
            Defense = defense;
            Speed = speed;
        }

        public IReadOnlyList<string> NamePool { get; }
        public StatRange MaxHp { get; }
        public StatRange Attack { get; }
        public StatRange Defense { get; }
        public StatRange Speed { get; }

        public static Result<CompanionGeneratorConfig> Create(
            IReadOnlyList<string> namePool,
            StatRange maxHp,
            StatRange attack,
            StatRange defense,
            StatRange speed)
        {
            if (namePool == null || namePool.Count == 0)
            {
                return Result<CompanionGeneratorConfig>.Failure("Companion name pool is required.");
            }

            if (namePool.Any(string.IsNullOrWhiteSpace))
            {
                return Result<CompanionGeneratorConfig>.Failure("Companion names cannot be blank.");
            }

            if (!maxHp.IsValid || maxHp.Min < 1)
            {
                return Result<CompanionGeneratorConfig>.Failure("Max HP range must be valid and at least one.");
            }

            if (!attack.IsValid || attack.Min < 0 || !defense.IsValid || defense.Min < 0
                || !speed.IsValid || speed.Min < 0)
            {
                return Result<CompanionGeneratorConfig>.Failure("Stat ranges must be valid and non-negative.");
            }

            return Result<CompanionGeneratorConfig>.Success(
                new CompanionGeneratorConfig(namePool.ToList(), maxHp, attack, defense, speed));
        }

        // v0 tuning: a small neutral name pool and stat ranges roughly around
        // the hand-authored slice characters.
        public static CompanionGeneratorConfig CreateDefaultV0()
        {
            return Create(
                new[]
                {
                    "Arin", "Borin", "Cael", "Dara", "Edan", "Fenn",
                    "Gwyn", "Hale", "Iris", "Jorn", "Kade", "Lys"
                },
                new StatRange(8, 14),
                new StatRange(1, 4),
                new StatRange(0, 3),
                new StatRange(3, 7)).Value;
        }
    }

    // T12: seed-deterministic companion generation. The same seed, faction,
    // bias table, config and ability pool always produce the same companion.
    // Faction flavour comes exclusively from FactionBiasTable data.
    public static class CompanionGenerator
    {
        public const int AbilityCount = 2;

        public static Result<CharacterDef> Generate(
            int seed,
            int factionId,
            FactionBiasTable biasTable,
            CompanionGeneratorConfig config,
            IReadOnlyList<AbilityDef> abilityPool)
        {
            if (biasTable == null)
            {
                return Result<CharacterDef>.Failure("Faction bias table is required.");
            }

            if (config == null)
            {
                return Result<CharacterDef>.Failure("Companion generator config is required.");
            }

            if (abilityPool == null || abilityPool.Count == 0)
            {
                return Result<CharacterDef>.Failure("Ability pool is required.");
            }

            if (abilityPool.Any(ability => ability == null))
            {
                return Result<CharacterDef>.Failure("Ability pool cannot contain null entries.");
            }

            var bias = biasTable.Find(factionId);
            if (bias.IsFailure)
            {
                return Result<CharacterDef>.Failure(bias.Error);
            }

            var rng = new Random(seed);
            var displayName = config.NamePool[rng.Next(config.NamePool.Count)];
            var maxHp = config.MaxHp.Roll(rng);
            var attack = config.Attack.Roll(rng);
            var defense = config.Defense.Roll(rng);
            var speed = config.Speed.Roll(rng);
            var disposition = bias.Value.Dispositions.Roll(rng);
            var abilities = RollAbilities(rng, bias.Value.AbilityTags, abilityPool);

            var id = string.Format(
                CultureInfo.InvariantCulture,
                "gen-{0}-{1:x8}",
                factionId,
                unchecked((uint)seed));

            var definition = CharacterDef.CreateRuntime(
                id,
                displayName,
                maxHp,
                attack,
                defense,
                speed,
                disposition,
                abilities,
                passive: null,
                isReturner: false,
                isPreset: false,
                factionId: factionId);
            return Result<CharacterDef>.Success(definition);
        }

        // Rolls a tag from the faction's weighted table, then picks uniformly
        // among pool abilities with that tag. Falls back to the remaining
        // pool when the tag has no (unpicked) candidates, and only repeats an
        // ability when the pool is smaller than the requested count.
        private static AbilityDef[] RollAbilities(
            Random rng,
            WeightedTable<AbilityTag> tagTable,
            IReadOnlyList<AbilityDef> abilityPool)
        {
            var picked = new List<AbilityDef>(AbilityCount);
            for (var index = 0; index < AbilityCount; index++)
            {
                var tag = tagTable.Roll(rng);
                var candidates = abilityPool
                    .Where(ability => ability.Tag == tag && !picked.Contains(ability))
                    .ToList();
                if (candidates.Count == 0)
                {
                    candidates = abilityPool.Where(ability => !picked.Contains(ability)).ToList();
                }

                if (candidates.Count == 0)
                {
                    candidates = abilityPool.ToList();
                }

                picked.Add(candidates[rng.Next(candidates.Count)]);
            }

            return picked.ToArray();
        }
    }
}
