using System;
using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;

namespace Tower.Tests.EditMode
{
    // T26: Core-pure, deterministic encounter-budget coverage. No UnityEngine,
    // no RNG: every assertion is a pure function of (budget, depth). Mirrors
    // the CampZoneDefTests style (NUnit, Tower.Tests.EditMode namespace).
    public sealed class EncounterBudgetTests
    {
        private static EncounterBudget SampleBudget()
        {
            return new EncounterBudget(
                baseDifficulty: 30,
                depthDifficultyRamp: 10,
                activeEnemyCapBase: 3f,
                activeEnemyCapDepthRamp: 0.5f,
                activeEnemyCapMax: 5,
                minTypes: 1,
                maxTypes: 3,
                typeCountDepthRamp: 0.5f,
                minWaves: 1,
                maxWaves: 3,
                eliteCap: 1);
        }

        // --- Deterministic depth ramp (same budget + depth => same numbers) ---

        [Test]
        public void DifficultyAt_IsDeterministicAndRampsWithDepth()
        {
            var budget = SampleBudget();

            // Repeated calls at the same depth are identical (no hidden RNG).
            Assert.That(budget.DifficultyAt(0), Is.EqualTo(budget.DifficultyAt(0)));
            Assert.That(budget.DifficultyAt(0), Is.EqualTo(30));
            Assert.That(budget.DifficultyAt(1), Is.EqualTo(40));
            Assert.That(budget.DifficultyAt(4), Is.EqualTo(70));

            // Strictly non-decreasing as depth climbs.
            for (int depth = 1; depth <= 10; depth++)
            {
                Assert.That(
                    budget.DifficultyAt(depth),
                    Is.GreaterThanOrEqualTo(budget.DifficultyAt(depth - 1)),
                    "Difficulty must ramp monotonically with depth.");
            }
        }

        [Test]
        public void DifficultyAt_NegativeDepthClampsToZero()
        {
            var budget = SampleBudget();

            Assert.That(budget.DifficultyAt(-5), Is.EqualTo(budget.DifficultyAt(0)));
        }

        // --- Active enemy cap: ramps but never exceeds the max ---

        [Test]
        public void ActiveEnemyCapAt_RampsThenClampsToMax()
        {
            var budget = SampleBudget();

            // floor(3 + 0.5 * depth), clamped to [1, 5].
            Assert.That(budget.ActiveEnemyCapAt(0), Is.EqualTo(3));
            Assert.That(budget.ActiveEnemyCapAt(1), Is.EqualTo(3)); // 3.5 -> 3
            Assert.That(budget.ActiveEnemyCapAt(2), Is.EqualTo(4)); // 4.0 -> 4
            Assert.That(budget.ActiveEnemyCapAt(4), Is.EqualTo(5)); // 5.0 -> 5
            Assert.That(budget.ActiveEnemyCapAt(20), Is.EqualTo(5)); // clamped
        }

        [Test]
        public void ActiveEnemyCapAt_NeverExceedsMaxAndAtLeastOne()
        {
            var budget = SampleBudget();

            for (int depth = 0; depth <= 50; depth++)
            {
                int cap = budget.ActiveEnemyCapAt(depth);
                Assert.That(cap, Is.GreaterThanOrEqualTo(1));
                Assert.That(cap, Is.LessThanOrEqualTo(budget.ActiveEnemyCapMax));
            }
        }

        // --- Type count stays inside [MinTypes, MaxTypes] ---

        [Test]
        public void TypeCountAt_StaysWithinMinMaxRange()
        {
            var budget = SampleBudget();

            // MinTypes(1) + floor(0.5 * depth), clamped to [1, 3].
            Assert.That(budget.TypeCountAt(0), Is.EqualTo(1));
            Assert.That(budget.TypeCountAt(2), Is.EqualTo(2));
            Assert.That(budget.TypeCountAt(4), Is.EqualTo(3));
            Assert.That(budget.TypeCountAt(100), Is.EqualTo(3)); // clamped to max

            for (int depth = 0; depth <= 50; depth++)
            {
                int types = budget.TypeCountAt(depth);
                Assert.That(types, Is.GreaterThanOrEqualTo(budget.MinTypes));
                Assert.That(types, Is.LessThanOrEqualTo(budget.MaxTypes));
            }
        }

        // --- Default budget honours the v0 slice invariants ---

        [Test]
        public void Default_ExposesConsistentV0Values()
        {
            var budget = EncounterBudget.Default;

            Assert.That(budget.MinWaves, Is.LessThanOrEqualTo(budget.MaxWaves));
            Assert.That(budget.MinTypes, Is.LessThanOrEqualTo(budget.MaxTypes));
            Assert.That(budget.EliteCap, Is.GreaterThanOrEqualTo(0));
            Assert.That(budget.ActiveEnemyCapAt(0), Is.LessThanOrEqualTo(budget.ActiveEnemyCapMax));
            Assert.That(budget.ManualWaveTemplates, Is.Not.Null);
            Assert.That(budget.ManualWaveTemplates.Count, Is.EqualTo(0));
        }

        // --- Constructor validation ---

        [Test]
        public void Constructor_RejectsMaxTypesBelowMinTypes()
        {
            Assert.That(
                () => new EncounterBudget(30, 10, 3f, 0.5f, 5, 3, 2, 0.5f, 1, 3, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_RejectsMaxWavesBelowMinWaves()
        {
            Assert.That(
                () => new EncounterBudget(30, 10, 3f, 0.5f, 5, 1, 3, 0.5f, 3, 2, 1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Constructor_RejectsNegativeEliteCap()
        {
            Assert.That(
                () => new EncounterBudget(30, 10, 3f, 0.5f, 5, 1, 3, 0.5f, 1, 3, -1),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        // --- Manual wave templates are defensively copied and validated ---

        [Test]
        public void Constructor_CopiesManualWaveTemplatesDefensively()
        {
            var inner = new List<int> { 2, 3 };
            var templates = new List<IReadOnlyList<int>> { inner };

            var budget = new EncounterBudget(30, 10, 3f, 0.5f, 5, 1, 3, 0.5f, 1, 3, 1, templates);

            // Mutating the source list must not affect the stored copy.
            inner.Add(99);
            Assert.That(budget.ManualWaveTemplates.Count, Is.EqualTo(1));
            Assert.That(budget.ManualWaveTemplates[0].Count, Is.EqualTo(2));
            Assert.That(budget.ManualWaveTemplates[0][0], Is.EqualTo(2));
            Assert.That(budget.ManualWaveTemplates[0][1], Is.EqualTo(3));
        }

        [Test]
        public void Constructor_RejectsEmptyManualWaveTemplateEntry()
        {
            var templates = new List<IReadOnlyList<int>> { new List<int>() };

            Assert.That(
                () => new EncounterBudget(30, 10, 3f, 0.5f, 5, 1, 3, 0.5f, 1, 3, 1, templates),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Constructor_RejectsNonPositiveWaveCountInTemplate()
        {
            var templates = new List<IReadOnlyList<int>> { new List<int> { 2, 0 } };

            Assert.That(
                () => new EncounterBudget(30, 10, 3f, 0.5f, 5, 1, 3, 0.5f, 1, 3, 1, templates),
                Throws.TypeOf<ArgumentException>());
        }

        // --- Override application (partial merge, re-validated) ---

        [Test]
        public void Override_AppliesOnlySetFields()
        {
            var baseBudget = SampleBudget();
            var over = new EncounterBudgetOverride
            {
                BaseDifficulty = 50,
                EliteCap = 2,
            };

            var merged = over.Apply(baseBudget);

            // Overridden fields change...
            Assert.That(merged.BaseDifficulty, Is.EqualTo(50));
            Assert.That(merged.EliteCap, Is.EqualTo(2));
            // ...everything else flows through unchanged.
            Assert.That(merged.DepthDifficultyRamp, Is.EqualTo(baseBudget.DepthDifficultyRamp));
            Assert.That(merged.MaxTypes, Is.EqualTo(baseBudget.MaxTypes));
            Assert.That(merged.ActiveEnemyCapMax, Is.EqualTo(baseBudget.ActiveEnemyCapMax));

            // Determinism preserved on the merged budget.
            Assert.That(merged.DifficultyAt(2), Is.EqualTo(50 + (10 * 2)));
        }

        [Test]
        public void Override_EmptyOverrideIsIdentity()
        {
            var baseBudget = SampleBudget();
            var merged = new EncounterBudgetOverride().Apply(baseBudget);

            Assert.That(merged.BaseDifficulty, Is.EqualTo(baseBudget.BaseDifficulty));
            Assert.That(merged.DifficultyAt(3), Is.EqualTo(baseBudget.DifficultyAt(3)));
            Assert.That(merged.ActiveEnemyCapAt(3), Is.EqualTo(baseBudget.ActiveEnemyCapAt(3)));
            Assert.That(merged.TypeCountAt(3), Is.EqualTo(baseBudget.TypeCountAt(3)));
        }

        [Test]
        public void Override_ReValidatesMergedResult()
        {
            var baseBudget = SampleBudget();
            // MaxTypes(2) would fall below MinTypes(3) once merged.
            var over = new EncounterBudgetOverride { MinTypes = 3, MaxTypes = 2 };

            Assert.That(() => over.Apply(baseBudget), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void Override_ApplyNullBaseThrows()
        {
            Assert.That(
                () => new EncounterBudgetOverride().Apply(null),
                Throws.TypeOf<ArgumentNullException>());
        }

        // --- Table lookup: resolution order and edge cases ---

        [Test]
        public void Table_ResolveNoOverridesReturnsBase()
        {
            var baseBudget = SampleBudget();
            var table = new EncounterBudgetTable(baseBudget);

            var resolved = table.Resolve("Crypt", "Combat");

            Assert.That(resolved, Is.SameAs(baseBudget));
        }

        [Test]
        public void Table_BiomeOverrideApplies()
        {
            var table = new EncounterBudgetTable(SampleBudget());
            table.SetBiomeOverride("Crypt", new EncounterBudgetOverride { EliteCap = 3 });

            var resolved = table.Resolve("Crypt", null);

            Assert.That(resolved.EliteCap, Is.EqualTo(3));
        }

        [Test]
        public void Table_RoomKindLayersOverBiome()
        {
            var table = new EncounterBudgetTable(SampleBudget());
            table.SetBiomeOverride("Crypt", new EncounterBudgetOverride { BaseDifficulty = 40, EliteCap = 2 });
            table.SetRoomKindOverride("Elite", new EncounterBudgetOverride { EliteCap = 4 });

            var resolved = table.Resolve("Crypt", "Elite");

            // Biome sets base difficulty; room-kind wins on the shared field.
            Assert.That(resolved.BaseDifficulty, Is.EqualTo(40));
            Assert.That(resolved.EliteCap, Is.EqualTo(4));
        }

        [Test]
        public void Table_UnknownKeysFallThroughToBase()
        {
            var baseBudget = SampleBudget();
            var table = new EncounterBudgetTable(baseBudget);
            table.SetBiomeOverride("Crypt", new EncounterBudgetOverride { EliteCap = 3 });

            var resolved = table.Resolve("Forest", "Unknown");

            Assert.That(resolved.EliteCap, Is.EqualTo(baseBudget.EliteCap));
            Assert.That(resolved, Is.SameAs(baseBudget));
        }

        [Test]
        public void Table_NullKeysSkipTheirOverrideLayer()
        {
            var table = new EncounterBudgetTable(SampleBudget());
            table.SetBiomeOverride("Crypt", new EncounterBudgetOverride { EliteCap = 3 });
            table.SetRoomKindOverride("Elite", new EncounterBudgetOverride { EliteCap = 4 });

            // Null room-kind => only the biome layer applies.
            var biomeOnly = table.Resolve("Crypt", null);
            Assert.That(biomeOnly.EliteCap, Is.EqualTo(3));

            // Null biome => only the room-kind layer applies.
            var roomOnly = table.Resolve(null, "Elite");
            Assert.That(roomOnly.EliteCap, Is.EqualTo(4));
        }

        [Test]
        public void Table_ResolveIsDeterministicAcrossCalls()
        {
            var table = new EncounterBudgetTable(SampleBudget());
            table.SetBiomeOverride("Crypt", new EncounterBudgetOverride { BaseDifficulty = 55 });

            var first = table.Resolve("Crypt", "Combat");
            var second = table.Resolve("Crypt", "Combat");

            Assert.That(first.BaseDifficulty, Is.EqualTo(second.BaseDifficulty));
            Assert.That(first.DifficultyAt(3), Is.EqualTo(second.DifficultyAt(3)));
        }

        [Test]
        public void Table_LatestOverrideForKeyWins()
        {
            var table = new EncounterBudgetTable(SampleBudget());
            table.SetBiomeOverride("Crypt", new EncounterBudgetOverride { EliteCap = 2 });
            table.SetBiomeOverride("Crypt", new EncounterBudgetOverride { EliteCap = 5 });

            Assert.That(table.Resolve("Crypt", null).EliteCap, Is.EqualTo(5));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  ")]
        public void Table_SetOverrideRejectsBlankKey(string key)
        {
            var table = new EncounterBudgetTable(SampleBudget());

            Assert.That(
                () => table.SetBiomeOverride(key, new EncounterBudgetOverride()),
                Throws.TypeOf<ArgumentException>());
        }

        [Test]
        public void Table_SetOverrideRejectsNullOverride()
        {
            var table = new EncounterBudgetTable(SampleBudget());

            Assert.That(
                () => table.SetRoomKindOverride("Elite", null),
                Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void Table_ConstructorRejectsNullBaseBudget()
        {
            Assert.That(() => new EncounterBudgetTable(null), Throws.TypeOf<ArgumentNullException>());
        }
    }
}
