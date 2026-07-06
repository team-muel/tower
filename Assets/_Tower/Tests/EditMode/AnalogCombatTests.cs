using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    // T20: end-to-end combat behaviour on the analog battlefield — the AI
    // pipeline (scorer + driver) works through IBattlefield and the T9
    // simulator stays deterministic in Analog mode (same seed = same result).
    public sealed class AnalogCombatTests
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

        [Test]
        public void AiTurnDriver_Analog_MovesIntoRangeAndAttacks()
        {
            var battlefield = new AnalogBattlefield(8f, 3f);
            var statusBoard = new StatusBoard();
            var strike = Track(AbilityDef.CreateRuntime("strike", AbilityTag.Apply, basePower: 5, range: 1, targetType: AbilityTargetType.Enemy));
            var caster = CreateCombatant("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemy = CreateCombatant("enemy", CombatTeam.Enemy, speed: 1, abilities: new[] { strike });
            Assert.That(battlefield.TryPlaceOccupant("caster", new BattlePos(0.5f, 1.5f)), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("enemy", new BattlePos(4.5f, 1.5f)), Is.True);

            var resolver = AbilityResolver.Create(battlefield, statusBoard);
            Assert.That(resolver.IsSuccess, Is.True, resolver.Error);
            var engine = TurnEngine.Create(new[] { caster, enemy }, abilityExecutor: resolver.Value);
            Assert.That(engine.IsSuccess, Is.True, engine.Error);
            var scorer = ActionScorer.Create(battlefield, statusBoard);
            Assert.That(scorer.IsSuccess, Is.True, scorer.Error);
            var driver = AiTurnDriver.Create(engine.Value, battlefield, scorer.Value);
            Assert.That(driver.IsSuccess, Is.True, driver.Error);

            var result = driver.Value.TakeTurn();

            Assert.That(result.IsSuccess, Is.True, result.Error);
            var casterPosition = battlefield.FindOccupant("caster");
            Assert.That(casterPosition.HasValue, Is.True);
            var enemyPosition = battlefield.FindOccupant("enemy");
            Assert.That(enemyPosition.HasValue, Is.True);
            // The caster closed to strike range (1.0) without overlapping the
            // enemy's circle, and the strike landed.
            Assert.That(battlefield.Distance(casterPosition.Value, enemyPosition.Value), Is.LessThanOrEqualTo(1f + 0.001f));
            Assert.That(battlefield.Distance(casterPosition.Value, enemyPosition.Value), Is.GreaterThanOrEqualTo(0.9f - 0.001f));
            Assert.That(engine.Value.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - 5));
        }

        [Test]
        public void ActionScorer_Analog_IsDeterministicAcrossIdenticalRuns()
        {
            var first = RunAnalogScorerScenario();
            var second = RunAnalogScorerScenario();

            Assert.That(second.Kind, Is.EqualTo(first.Kind));
            Assert.That(second.AbilityId, Is.EqualTo(first.AbilityId));
            Assert.That(second.TargetUnitId, Is.EqualTo(first.TargetUnitId));
            Assert.That(second.MovePosition, Is.EqualTo(first.MovePosition));
            Assert.That(second.MoveDistance, Is.EqualTo(first.MoveDistance));
            Assert.That(second.Score, Is.EqualTo(first.Score));
        }

        [Test]
        public void Simulator_AnalogMode_SameSeedSameAggregateResult()
        {
            var simulator = new AutoBattleSimulator();
            var options = new AutoBattleOptions
            {
                seed = 404,
                battles = 5,
                maxRounds = 12,
                spaceMode = CombatSpaceMode.Analog
            };

            var first = simulator.Run(options);
            var second = simulator.Run(options);

            Assert.That(first.IsSuccess, Is.True, first.Error);
            Assert.That(second.IsSuccess, Is.True, second.Error);
            Assert.That(JsonUtility.ToJson(first.Value), Is.EqualTo(JsonUtility.ToJson(second.Value)));
        }

        [Test]
        public void Simulator_GridMode_StaysAvailableAsRollback()
        {
            var simulator = new AutoBattleSimulator();
            var options = new AutoBattleOptions
            {
                seed = 511,
                battles = 3,
                maxRounds = 10,
                spaceMode = CombatSpaceMode.Grid
            };

            var first = simulator.Run(options);
            var second = simulator.Run(options);

            Assert.That(first.IsSuccess, Is.True, first.Error);
            Assert.That(second.IsSuccess, Is.True, second.Error);
            Assert.That(JsonUtility.ToJson(first.Value), Is.EqualTo(JsonUtility.ToJson(second.Value)));
        }

        [Test]
        public void CombatSpaceSettings_DefaultsToAnalog()
        {
            Assert.That(CombatSpaceSettings.DefaultMode, Is.EqualTo(CombatSpaceMode.Analog));
            Assert.That(new AutoBattleOptions().spaceMode, Is.EqualTo(CombatSpaceMode.Analog));
        }

        private AiPlan RunAnalogScorerScenario()
        {
            var battlefield = new AnalogBattlefield(8f, 8f);
            var statusBoard = new StatusBoard();
            var strike = Track(AbilityDef.CreateRuntime("strike", AbilityTag.Apply, basePower: 5, range: 1, targetType: AbilityTargetType.Enemy));
            var caster = CreateCombatant("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemyA = CreateCombatant("enemy-a", CombatTeam.Enemy, speed: 2, abilities: new[] { strike });
            var enemyB = CreateCombatant("enemy-b", CombatTeam.Enemy, speed: 1, abilities: new[] { strike });
            Assert.That(battlefield.TryPlaceOccupant("caster", new BattlePos(2.5f, 2.5f)), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("enemy-a", new BattlePos(1.5f, 2.5f)), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("enemy-b", new BattlePos(3.5f, 2.5f)), Is.True);

            var engine = TurnEngine.Create(new[] { caster, enemyA, enemyB });
            Assert.That(engine.IsSuccess, Is.True, engine.Error);
            var scorer = ActionScorer.Create(battlefield, statusBoard);
            Assert.That(scorer.IsSuccess, Is.True, scorer.Error);

            var plan = scorer.Value.ChooseAction(engine.Value, "caster");
            Assert.That(plan.IsSuccess, Is.True, plan.Error);
            return plan.Value;
        }

        private CombatantRef CreateCombatant(string unitId, CombatTeam team, int speed, AbilityDef[] abilities)
        {
            var definition = Track(CharacterDef.CreateRuntime(
                unitId, unitId, 20, 0, 0, speed, DispositionType.Aggressive, abilities));
            var state = CharacterState.Create(definition, slotCount: abilities.Length, assignedAbilities: definition.DefaultAbilities);
            Assert.That(state.IsSuccess, Is.True, state.Error);
            var combatant = CombatantRef.Create(unitId, team, state.Value);
            Assert.That(combatant.IsSuccess, Is.True, combatant.Error);
            return combatant.Value;
        }

        private T Track<T>(T createdObject) where T : Object
        {
            createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
