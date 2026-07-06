using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    // One weighted option inside a bias table.
    public readonly struct WeightedOption<T>
    {
        public WeightedOption(T value, int weight)
        {
            Value = value;
            Weight = weight;
        }

        public T Value { get; }
        public int Weight { get; }
    }

    // Generic weighted roll table. Pure data + a deterministic roll, so
    // faction flavour lives in table entries rather than code branches.
    public sealed class WeightedTable<T>
    {
        private readonly List<WeightedOption<T>> options;
        private readonly int totalWeight;

        private WeightedTable(List<WeightedOption<T>> options, int totalWeight)
        {
            this.options = options;
            this.totalWeight = totalWeight;
        }

        public IReadOnlyList<WeightedOption<T>> Options => options;

        public static Result<WeightedTable<T>> Create(IEnumerable<WeightedOption<T>> options)
        {
            if (options == null)
            {
                return Result<WeightedTable<T>>.Failure("Weighted table options are required.");
            }

            var entries = options.ToList();
            if (entries.Count == 0)
            {
                return Result<WeightedTable<T>>.Failure("Weighted table requires at least one option.");
            }

            if (entries.Any(entry => entry.Weight <= 0))
            {
                return Result<WeightedTable<T>>.Failure("Weighted table weights must be positive.");
            }

            return Result<WeightedTable<T>>.Success(new WeightedTable<T>(
                entries,
                entries.Sum(entry => entry.Weight)));
        }

        public T Roll(Random rng)
        {
            if (rng == null)
            {
                throw new ArgumentNullException(nameof(rng));
            }

            var roll = rng.Next(totalWeight);
            foreach (var option in options)
            {
                roll -= option.Weight;
                if (roll < 0)
                {
                    return option.Value;
                }
            }

            return options[options.Count - 1].Value;
        }
    }

    // Per-faction generation bias: disposition and ability tag weights.
    // Adding or tuning a faction means editing table data, not code.
    public sealed class FactionBias
    {
        private FactionBias(
            int factionId,
            WeightedTable<DispositionType> dispositions,
            WeightedTable<AbilityTag> abilityTags)
        {
            FactionId = factionId;
            Dispositions = dispositions;
            AbilityTags = abilityTags;
        }

        public int FactionId { get; }
        public WeightedTable<DispositionType> Dispositions { get; }
        public WeightedTable<AbilityTag> AbilityTags { get; }

        public static Result<FactionBias> Create(
            int factionId,
            IEnumerable<WeightedOption<DispositionType>> dispositionWeights,
            IEnumerable<WeightedOption<AbilityTag>> abilityTagWeights)
        {
            var dispositions = WeightedTable<DispositionType>.Create(dispositionWeights);
            if (dispositions.IsFailure)
            {
                return Result<FactionBias>.Failure(dispositions.Error);
            }

            var abilityTags = WeightedTable<AbilityTag>.Create(abilityTagWeights);
            if (abilityTags.IsFailure)
            {
                return Result<FactionBias>.Failure(abilityTags.Error);
            }

            return Result<FactionBias>.Success(new FactionBias(factionId, dispositions.Value, abilityTags.Value));
        }
    }

    // Lookup of faction id -> bias. Data-driven: v0 ships three placeholder
    // factions; a fourth faction is a new table entry, not a new branch.
    public sealed class FactionBiasTable
    {
        private readonly Dictionary<int, FactionBias> biases;

        private FactionBiasTable(Dictionary<int, FactionBias> biases)
        {
            this.biases = biases;
        }

        public IReadOnlyCollection<int> FactionIds => biases.Keys;

        public static Result<FactionBiasTable> Create(IEnumerable<FactionBias> factionBiases)
        {
            if (factionBiases == null)
            {
                return Result<FactionBiasTable>.Failure("Faction biases are required.");
            }

            var map = new Dictionary<int, FactionBias>();
            foreach (var bias in factionBiases)
            {
                if (bias == null)
                {
                    return Result<FactionBiasTable>.Failure("Faction bias entries cannot be null.");
                }

                if (map.ContainsKey(bias.FactionId))
                {
                    return Result<FactionBiasTable>.Failure($"Duplicate faction id {bias.FactionId}.");
                }

                map[bias.FactionId] = bias;
            }

            if (map.Count == 0)
            {
                return Result<FactionBiasTable>.Failure("Faction bias table requires at least one faction.");
            }

            return Result<FactionBiasTable>.Success(new FactionBiasTable(map));
        }

        public Result<FactionBias> Find(int factionId)
        {
            return biases.TryGetValue(factionId, out var bias)
                ? Result<FactionBias>.Success(bias)
                : Result<FactionBias>.Failure($"Unknown faction id {factionId}.");
        }

        // v0 placeholder factions (design brief T12):
        //   1 — offense / Consume-leaning
        //   2 — protection / Apply-leaning
        //   3 — opportunist / Amplify-leaning (dispositions balanced until an
        //       opportunist disposition exists)
        public static FactionBiasTable CreateDefaultV0()
        {
            var faction1 = FactionBias.Create(
                1,
                new[]
                {
                    new WeightedOption<DispositionType>(DispositionType.Aggressive, 80),
                    new WeightedOption<DispositionType>(DispositionType.Protective, 20)
                },
                new[]
                {
                    new WeightedOption<AbilityTag>(AbilityTag.Consume, 70),
                    new WeightedOption<AbilityTag>(AbilityTag.Apply, 20),
                    new WeightedOption<AbilityTag>(AbilityTag.Amplify, 10)
                });

            var faction2 = FactionBias.Create(
                2,
                new[]
                {
                    new WeightedOption<DispositionType>(DispositionType.Aggressive, 20),
                    new WeightedOption<DispositionType>(DispositionType.Protective, 80)
                },
                new[]
                {
                    new WeightedOption<AbilityTag>(AbilityTag.Apply, 70),
                    new WeightedOption<AbilityTag>(AbilityTag.Amplify, 20),
                    new WeightedOption<AbilityTag>(AbilityTag.Consume, 10)
                });

            var faction3 = FactionBias.Create(
                3,
                new[]
                {
                    new WeightedOption<DispositionType>(DispositionType.Aggressive, 50),
                    new WeightedOption<DispositionType>(DispositionType.Protective, 50)
                },
                new[]
                {
                    new WeightedOption<AbilityTag>(AbilityTag.Amplify, 70),
                    new WeightedOption<AbilityTag>(AbilityTag.Apply, 15),
                    new WeightedOption<AbilityTag>(AbilityTag.Consume, 15)
                });

            var table = Create(new[] { faction1.Value, faction2.Value, faction3.Value });
            return table.Value;
        }
    }
}
