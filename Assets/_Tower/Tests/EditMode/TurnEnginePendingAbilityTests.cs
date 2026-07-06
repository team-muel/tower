using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    // T18: pending-ability pick (seed-deterministic slot random), cooldown
    // recording/decrement at round boundaries, SetPendingAbility contract,
    // and the AiTurnDriver lock onto the pending ability.
    public sealed class TurnEnginePendingAbilityTests
    {
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        // --- pending pick ---

        [Test]
        public void PendingPick_SameSeed_ProducesSameSequence()
        {
            var firstSequence = CollectPendingSequence(seed: 1234, turns: 8);
            var secondSequence = CollectPendingSequence(seed: 1234, turns: 8);

            Assert.That(secondSequence, Is.EqualTo(firstSequence));
        }

        [Test]
        public void PendingPick_IsAnEquippedTaggedAbility()
        {
            var engine = CreateEngine(
                seed: 7,
                TaggedUnit("actor", CombatTeam.Player, 10, "a1", "a2", "a3", "a4"),
                TaggedUnit("enemy", CombatTeam.Enemy, 1, "e1", "e2"));

            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("actor"));
            Assert.That(engine.PendingAbilityId, Is.EqualTo("a1").Or.EqualTo("a2").Or.EqualTo("a3").Or.EqualTo("a4"));
        }

        [Test]
        public void PendingPick_SkipsAbilitiesOnCooldown()
        {
            var cooldowns = new Dictionary<string, int> { { "a1", 2 }, { "a3", 1 }, { "a4", 3 } };
            var engine = CreateEngine(
                seed: 7,
                TaggedUnit("actor", CombatTeam.Player, 10, cooldowns, "a1", "a2", "a3", "a4"),
                TaggedUnit("enemy", CombatTeam.Enemy, 1, "e1", "e2"));

            Assert.That(engine.PendingAbilityId, Is.EqualTo("a2"), "The only off-cooldown tagged ability must be picked.");
        }

        [Test]
        public void PendingPick_FallsBackToUntagged_WhenAllTaggedAreCooling()
        {
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 3);
            var basic = CreateAbility("basic", AbilityTag.None);
            var cooldowns = new Dictionary<string, int> { { "strike", 2 } };
            var engine = CreateEngine(
                seed: 7,
                Unit("actor", CombatTeam.Player, 10, new[] { strike, basic }, cooldowns),
                TaggedUnit("enemy", CombatTeam.Enemy, 1, "e1", "e2"));

            Assert.That(engine.PendingAbilityId, Is.EqualTo("basic"), "Untagged basic action is the fallback.");
        }

        [Test]
        public void PendingPick_IsNull_WhenEveryEquippedAbilityIsCooling()
        {
            var cooldowns = new Dictionary<string, int> { { "a1", 1 }, { "a2", 2 } };
            var engine = CreateEngine(
                seed: 7,
                TaggedUnit("actor", CombatTeam.Player, 10, cooldowns, "a1", "a2"),
                TaggedUnit("enemy", CombatTeam.Enemy, 1, "e1", "e2"));

            Assert.That(engine.PendingAbilityId, Is.Null);
        }

        [Test]
        public void PendingPick_RefreshesForEachTurn()
        {
            var engine = CreateEngine(
                seed: 7,
                TaggedUnit("actor", CombatTeam.Player, 10, "a1", "a2"),
                TaggedUnit("enemy", CombatTeam.Enemy, 1, "e1", "e2"));

            Assert.That(engine.PendingAbilityId, Is.EqualTo("a1").Or.EqualTo("a2"));
            Assert.That(engine.Submit(new SkipTurnCommand("actor")).IsSuccess, Is.True);
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("enemy"));
            Assert.That(engine.PendingAbilityId, Is.EqualTo("e1").Or.EqualTo("e2"), "The pick must follow the active unit.");
        }

        // --- SetPendingAbility ---

        [Test]
        public void SetPendingAbility_SwapsToAnEquippedOffCooldownAbility()
        {
            var engine = CreateEngine(
                seed: 7,
                TaggedUnit("actor", CombatTeam.Player, 10, "a1", "a2"),
                TaggedUnit("enemy", CombatTeam.Enemy, 1, "e1", "e2"));
            var other = engine.PendingAbilityId == "a1" ? "a2" : "a1";

            var result = engine.SetPendingAbility("actor", other);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.PendingAbilityId, Is.EqualTo(other));
        }

        [Test]
        public void SetPendingAbility_FailsForUnknownUnit()
        {
            var engine = CreateSimplePendingEngine();

            var result = engine.SetPendingAbility("nobody", "a1");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("Unknown combatant"));
        }

        [Test]
        public void SetPendingAbility_FailsForInactiveUnit()
        {
            var engine = CreateSimplePendingEngine();

            var result = engine.SetPendingAbility("enemy", "e1");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("not the active"));
        }

        [Test]
        public void SetPendingAbility_FailsForUnequippedAbility()
        {
            var engine = CreateSimplePendingEngine();

            var result = engine.SetPendingAbility("actor", "not-mine");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("not equipped"));
        }

        [Test]
        public void SetPendingAbility_FailsForAbilityOnCooldown()
        {
            var cooldowns = new Dictionary<string, int> { { "a1", 2 } };
            var engine = CreateEngine(
                seed: 7,
                TaggedUnit("actor", CombatTeam.Player, 10, cooldowns, "a1", "a2"),
                TaggedUnit("enemy", CombatTeam.Enemy, 1, "e1", "e2"));

            var result = engine.SetPendingAbility("actor", "a1");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("on cooldown"));
        }

        [Test]
        public void SetPendingAbility_FailsForEmptyAbilityId()
        {
            var engine = CreateSimplePendingEngine();

            var result = engine.SetPendingAbility("actor", " ");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("Ability id is required"));
        }

        [Test]
        public void SetPendingAbility_FailsAfterCombatEnds()
        {
            var engine = CreateSimplePendingEngine();
            Assert.That(engine.DefeatCombatant("enemy").IsSuccess, Is.True);
            Assert.That(engine.IsCombatEnded, Is.True);
            Assert.That(engine.PendingAbilityId, Is.Null, "Pending must clear when combat ends.");

            var result = engine.SetPendingAbility("actor", "a1");

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("Combat has ended"));
        }

        // --- cooldown record / decrement / expiry ---

        [Test]
        public void UseAbility_RecordsCooldown_TicksPerRound_AndExpires()
        {
            // vault 18 section 4 scenario: a 2-round cooldown ability is used
            // in round 1, stays unavailable in round 2, and returns in round 3.
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 3, cooldownRounds: 2);
            var basic = CreateAbility("basic", AbilityTag.None);
            var engine = CreateEngine(
                seed: 7,
                Unit("actor", CombatTeam.Player, 10, new[] { strike, basic }),
                TaggedUnit("enemy", CombatTeam.Enemy, 1, "e1", "e2"));

            // Round 1: strike is pending and usable (no executor configured).
            Assert.That(engine.PendingAbilityId, Is.EqualTo("strike"));
            Assert.That(engine.Submit(new UseAbilityCommand("actor", "strike")).IsSuccess, Is.True);
            Assert.That(engine.GetCombatant("actor").State.RemainingCooldown("strike"), Is.EqualTo(2));

            Assert.That(engine.Submit(new SkipTurnCommand("enemy")).IsSuccess, Is.True);

            // Round 2: cooldown ticked to 1 - strike is not pickable or usable.
            Assert.That(engine.RoundNumber, Is.EqualTo(2));
            Assert.That(engine.GetCombatant("actor").State.RemainingCooldown("strike"), Is.EqualTo(1));
            Assert.That(engine.PendingAbilityId, Is.EqualTo("basic"), "Cooling ability must not be picked.");

            var blocked = engine.Submit(new UseAbilityCommand("actor", "strike"));
            Assert.That(blocked.IsFailure, Is.True);
            Assert.That(blocked.Error, Does.Contain("on cooldown"));

            Assert.That(engine.Submit(new SkipTurnCommand("actor")).IsSuccess, Is.True);
            Assert.That(engine.Submit(new SkipTurnCommand("enemy")).IsSuccess, Is.True);

            // Round 3: cooldown expired - strike is pending and usable again.
            Assert.That(engine.RoundNumber, Is.EqualTo(3));
            Assert.That(engine.GetCombatant("actor").State.RemainingCooldown("strike"), Is.EqualTo(0));
            Assert.That(engine.PendingAbilityId, Is.EqualTo("strike"));
            Assert.That(engine.Submit(new UseAbilityCommand("actor", "strike")).IsSuccess, Is.True);
            Assert.That(engine.GetCombatant("actor").State.RemainingCooldown("strike"), Is.EqualTo(2));
        }

        [Test]
        public void UseAbility_WithoutCooldownRounds_RecordsNothing()
        {
            var engine = CreateSimplePendingEngine();
            var pending = engine.PendingAbilityId;

            Assert.That(engine.Submit(new UseAbilityCommand("actor", pending)).IsSuccess, Is.True);
            Assert.That(engine.GetCombatant("actor").State.AbilityCooldowns, Is.Empty);
        }

        // --- AiTurnDriver integration ---

        [Test]
        public void Driver_UsesOnlyThePendingAbility()
        {
            var map = new GridMap(2, 1);
            var statusBoard = new StatusBoard();
            var resolver = AbilityResolver.Create(map, statusBoard);
            Assert.That(resolver.IsSuccess, Is.True, resolver.Error);
            var scorer = ActionScorer.Create(map, statusBoard);
            Assert.That(scorer.IsSuccess, Is.True, scorer.Error);

            var heavy = CreateAbility("heavy", AbilityTag.Apply, power: 5);
            var light = CreateAbility("light", AbilityTag.Apply, power: 3);
            var caster = Unit("caster", CombatTeam.Player, 10, new[] { heavy, light });
            var enemy = TaggedUnit("enemy", CombatTeam.Enemy, 1, "e1", "e2");
            Assert.That(map.TrySetOccupant(new GridPos(0, 0), "caster"), Is.True);
            Assert.That(map.TrySetOccupant(new GridPos(1, 0), "enemy"), Is.True);

            // Find a seed whose deterministic pick is the weaker ability; an
            // unrestricted scorer would always prefer the heavy one.
            TurnEngine engine = null;
            for (var seed = 0; seed < 64; seed++)
            {
                var candidate = TurnEngine.Create(new[] { caster, enemy }, abilityExecutor: resolver.Value, seed: seed);
                Assert.That(candidate.IsSuccess, Is.True, candidate.Error);
                if (candidate.Value.PendingAbilityId == "light")
                {
                    engine = candidate.Value;
                    break;
                }
            }

            Assert.That(engine, Is.Not.Null, "No seed in range picked the weaker ability.");

            var driver = AiTurnDriver.Create(engine, map, scorer.Value);
            Assert.That(driver.IsSuccess, Is.True, driver.Error);
            var turn = driver.Value.TakeTurn();

            Assert.That(turn.IsSuccess, Is.True, turn.Error);
            Assert.That(
                engine.GetCombatant("enemy").State.CurrentHp,
                Is.EqualTo(20 - 3),
                "The driver must use the pending (weaker) ability, not the best-scoring one.");
        }

        [Test]
        public void Driver_MovesOrSkips_WhenEveryAbilityIsCooling()
        {
            var map = new GridMap(2, 1);
            var statusBoard = new StatusBoard();
            var resolver = AbilityResolver.Create(map, statusBoard);
            Assert.That(resolver.IsSuccess, Is.True, resolver.Error);
            var scorer = ActionScorer.Create(map, statusBoard);
            Assert.That(scorer.IsSuccess, Is.True, scorer.Error);

            var heavy = CreateAbility("heavy", AbilityTag.Apply, power: 5);
            var light = CreateAbility("light", AbilityTag.Apply, power: 3);
            var cooldowns = new Dictionary<string, int> { { "heavy", 2 }, { "light", 1 } };
            var caster = Unit("caster", CombatTeam.Player, 10, new[] { heavy, light }, cooldowns);
            var enemy = TaggedUnit("enemy", CombatTeam.Enemy, 1, "e1", "e2");
            Assert.That(map.TrySetOccupant(new GridPos(0, 0), "caster"), Is.True);
            Assert.That(map.TrySetOccupant(new GridPos(1, 0), "enemy"), Is.True);

            var engine = TurnEngine.Create(new[] { caster, enemy }, abilityExecutor: resolver.Value, seed: 7);
            Assert.That(engine.IsSuccess, Is.True, engine.Error);
            Assert.That(engine.Value.PendingAbilityId, Is.Null);

            var driver = AiTurnDriver.Create(engine.Value, map, scorer.Value);
            Assert.That(driver.IsSuccess, Is.True, driver.Error);
            var turn = driver.Value.TakeTurn();

            Assert.That(turn.IsSuccess, Is.True, turn.Error);
            Assert.That(engine.Value.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20), "No ability may be used while everything cools down.");
            Assert.That(engine.Value.CurrentTurn.UnitId, Is.EqualTo("enemy"), "The turn must still end.");
        }

        // --- helpers ---

        private List<string> CollectPendingSequence(int seed, int turns)
        {
            var engine = CreateEngine(
                seed,
                TaggedUnit("alpha", CombatTeam.Player, 10, "p1", "p2", "p3", "p4"),
                TaggedUnit("beta", CombatTeam.Enemy, 5, "q1", "q2", "q3"));

            var sequence = new List<string>();
            for (var index = 0; index < turns; index++)
            {
                sequence.Add(engine.PendingAbilityId);
                Assert.That(engine.Submit(new SkipTurnCommand(engine.CurrentTurn.UnitId)).IsSuccess, Is.True);
            }

            return sequence;
        }

        private TurnEngine CreateSimplePendingEngine()
        {
            return CreateEngine(
                seed: 7,
                TaggedUnit("actor", CombatTeam.Player, 10, "a1", "a2"),
                TaggedUnit("enemy", CombatTeam.Enemy, 1, "e1", "e2"));
        }

        private TurnEngine CreateEngine(int seed, params CombatantRef[] combatants)
        {
            var result = TurnEngine.Create(combatants, seed: seed);
            Assert.That(result.IsSuccess, Is.True, result.Error);
            return result.Value;
        }

        private CombatantRef TaggedUnit(string unitId, CombatTeam team, int speed, params string[] abilityIds)
        {
            return TaggedUnit(unitId, team, speed, null, abilityIds);
        }

        private CombatantRef TaggedUnit(
            string unitId,
            CombatTeam team,
            int speed,
            IReadOnlyDictionary<string, int> cooldowns,
            params string[] abilityIds)
        {
            var abilities = abilityIds
                .Select(abilityId => CreateAbility(abilityId, AbilityTag.Apply, power: 1))
                .ToArray();
            return Unit(unitId, team, speed, abilities, cooldowns);
        }

        private CombatantRef Unit(
            string unitId,
            CombatTeam team,
            int speed,
            AbilityDef[] abilities,
            IReadOnlyDictionary<string, int> cooldowns = null)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDef>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "id", unitId);
            SetPrivateField(definition, "displayName", unitId);
            SetPrivateField(definition, "maxHp", 20);
            SetPrivateField(definition, "speed", speed);

            var state = CharacterState.Create(
                definition,
                slotCount: abilities.Length,
                assignedAbilities: abilities,
                abilityCooldowns: cooldowns);
            Assert.That(state.IsSuccess, Is.True, state.Error);

            var combatant = CombatantRef.Create(unitId, team, state.Value);
            Assert.That(combatant.IsSuccess, Is.True, combatant.Error);
            return combatant.Value;
        }

        private AbilityDef CreateAbility(
            string id,
            AbilityTag tag = AbilityTag.None,
            int power = 0,
            int range = 1,
            int cooldownRounds = 0)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDef>();
            createdObjects.Add(ability);
            SetPrivateField(ability, "id", id);
            SetPrivateField(ability, "displayName", id);
            SetPrivateField(ability, "tag", tag);
            SetPrivateField(ability, "basePower", power);
            SetPrivateField(ability, "range", range);
            SetPrivateField(ability, "targetType", AbilityTargetType.Enemy);
            SetPrivateField(ability, "cooldownRounds", cooldownRounds);
            return ability;
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            var field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
