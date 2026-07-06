using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tower.Tests.EditMode
{
    // T12: seed-deterministic companion generation with data-driven faction
    // bias (no per-faction code branches).
    public sealed class CompanionGeneratorTests
    {
        private const int SampleCount = 300;

        private readonly List<Object> createdObjects = new List<Object>();

        private FactionBiasTable biasTable;
        private CompanionGeneratorConfig config;
        private List<AbilityDef> abilityPool;

        [SetUp]
        public void SetUp()
        {
            biasTable = FactionBiasTable.CreateDefaultV0();
            config = CompanionGeneratorConfig.CreateDefaultV0();

            // Two abilities per tag so tag rolls always find a distinct
            // candidate and the sampled tag distribution stays undiluted.
            abilityPool = new List<AbilityDef>();
            foreach (var tag in new[] { AbilityTag.Apply, AbilityTag.Consume, AbilityTag.Amplify })
            {
                for (var variant = 0; variant < 2; variant++)
                {
                    var ability = AbilityDef.CreateRuntime(
                        $"{tag}-{variant}".ToLowerInvariant(),
                        tag,
                        basePower: 3,
                        range: 1,
                        targetType: AbilityTargetType.Enemy);
                    createdObjects.Add(ability);
                    abilityPool.Add(ability);
                }
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void Generate_SameSeed_ProducesIdenticalCompanion()
        {
            var first = Generate(seed: 1234, factionId: 1);
            var second = Generate(seed: 1234, factionId: 1);

            Assert.That(second.Id, Is.EqualTo(first.Id));
            Assert.That(second.DisplayName, Is.EqualTo(first.DisplayName));
            Assert.That(second.MaxHp, Is.EqualTo(first.MaxHp));
            Assert.That(second.Attack, Is.EqualTo(first.Attack));
            Assert.That(second.Defense, Is.EqualTo(first.Defense));
            Assert.That(second.Speed, Is.EqualTo(first.Speed));
            Assert.That(second.Disposition, Is.EqualTo(first.Disposition));
            Assert.That(
                second.DefaultAbilities.Select(ability => ability.Id),
                Is.EqualTo(first.DefaultAbilities.Select(ability => ability.Id)));
        }

        [Test]
        public void Generate_SetsGeneratedCompanionMetadata()
        {
            var companion = Generate(seed: 42, factionId: 2);

            Assert.That(companion.IsPreset, Is.False, "generated companions are never presets");
            Assert.That(companion.FactionId, Is.EqualTo(2));
            Assert.That(companion.IsReturner, Is.False);
            Assert.That(companion.DefaultAbilities, Has.Length.EqualTo(CompanionGenerator.AbilityCount));
            Assert.That(companion.DefaultAbilities.Distinct().Count(), Is.EqualTo(CompanionGenerator.AbilityCount));
            Assert.That(companion.MaxHp, Is.InRange(config.MaxHp.Min, config.MaxHp.Max));
            Assert.That(companion.Attack, Is.InRange(config.Attack.Min, config.Attack.Max));
            Assert.That(companion.Defense, Is.InRange(config.Defense.Min, config.Defense.Max));
            Assert.That(companion.Speed, Is.InRange(config.Speed.Min, config.Speed.Max));
            Assert.That(config.NamePool, Does.Contain(companion.DisplayName));
        }

        [Test]
        public void Generate_UnknownFaction_Fails()
        {
            var unknown = CompanionGenerator.Generate(1, 99, biasTable, config, abilityPool);
            Assert.That(unknown.IsFailure, Is.True);

            var unaffiliated = CompanionGenerator.Generate(1, CharacterDef.NoFactionId, biasTable, config, abilityPool);
            Assert.That(unaffiliated.IsFailure, Is.True, "v0 generation is always faction-pool based");
        }

        [Test]
        public void Generate_FactionBias_ShiftsDispositionAndTagDistributions()
        {
            var aggressiveCounts = new Dictionary<int, int>();
            var tagCounts = new Dictionary<int, Dictionary<AbilityTag, int>>();

            foreach (var factionId in new[] { 1, 2, 3 })
            {
                aggressiveCounts[factionId] = 0;
                tagCounts[factionId] = new Dictionary<AbilityTag, int>
                {
                    [AbilityTag.Apply] = 0,
                    [AbilityTag.Consume] = 0,
                    [AbilityTag.Amplify] = 0
                };

                for (var seed = 0; seed < SampleCount; seed++)
                {
                    var companion = Generate(seed, factionId);
                    if (companion.Disposition == DispositionType.Aggressive)
                    {
                        aggressiveCounts[factionId]++;
                    }

                    foreach (var ability in companion.DefaultAbilities)
                    {
                        tagCounts[factionId][ability.Tag]++;
                    }
                }
            }

            // Faction 1 (offense/Consume) vs faction 2 (protection/Apply):
            // with 80/20 weights over 300 samples the gap is enormous, so
            // these comparisons are deterministic in practice.
            Assert.That(aggressiveCounts[1], Is.GreaterThan(aggressiveCounts[2]));
            Assert.That(tagCounts[1][AbilityTag.Consume], Is.GreaterThan(tagCounts[2][AbilityTag.Consume]));
            Assert.That(tagCounts[2][AbilityTag.Apply], Is.GreaterThan(tagCounts[1][AbilityTag.Apply]));

            // Faction 3 (opportunist/Amplify) out-amplifies both.
            Assert.That(tagCounts[3][AbilityTag.Amplify], Is.GreaterThan(tagCounts[1][AbilityTag.Amplify]));
            Assert.That(tagCounts[3][AbilityTag.Amplify], Is.GreaterThan(tagCounts[2][AbilityTag.Amplify]));
        }

        [Test]
        public void WeightedTable_RejectsEmptyOrNonPositiveWeights()
        {
            Assert.That(WeightedTable<int>.Create(new WeightedOption<int>[0]).IsFailure, Is.True);
            Assert.That(
                WeightedTable<int>.Create(new[] { new WeightedOption<int>(1, 0) }).IsFailure,
                Is.True);
        }

        [Test]
        public void GeneratorConfig_RejectsInvalidInput()
        {
            Assert.That(
                CompanionGeneratorConfig.Create(
                    new string[0],
                    new StatRange(1, 2),
                    new StatRange(0, 1),
                    new StatRange(0, 1),
                    new StatRange(0, 1)).IsFailure,
                Is.True);
            Assert.That(
                CompanionGeneratorConfig.Create(
                    new[] { "Arin" },
                    new StatRange(5, 2),
                    new StatRange(0, 1),
                    new StatRange(0, 1),
                    new StatRange(0, 1)).IsFailure,
                Is.True);
        }

        private CharacterDef Generate(int seed, int factionId)
        {
            var companion = CompanionGenerator.Generate(seed, factionId, biasTable, config, abilityPool);
            Assert.That(companion.IsSuccess, Is.True, companion.Error);
            createdObjects.Add(companion.Value);
            return companion.Value;
        }
    }
}
