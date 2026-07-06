using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class AutoBattleSimulatorTests
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
        public void Run_WithSameSeed_ReturnsSameAggregateResult()
        {
            var simulator = new AutoBattleSimulator();
            var options = new AutoBattleOptions { seed = 404, battles = 5, maxRounds = 12 };

            var first = simulator.Run(options);
            var second = simulator.Run(options);

            Assert.That(first.IsSuccess, Is.True, first.Error);
            Assert.That(second.IsSuccess, Is.True, second.Error);
            Assert.That(JsonUtility.ToJson(first.Value), Is.EqualTo(JsonUtility.ToJson(second.Value)));
        }

        [Test]
        public void Run_WithCooldownAbilities_IsDeterministicForSameSeed()
        {
            // T18: pending-ability picks and cooldown ticks must flow from the
            // seed, so a scenario full of cooldown abilities stays reproducible.
            var scenario = CreateCooldownScenario();
            var simulator = new AutoBattleSimulator();
            var options = new AutoBattleOptions { seed = 777, battles = 6, maxRounds = 15 };

            var first = simulator.Run(scenario, options);
            var second = simulator.Run(scenario, options);

            Assert.That(first.IsSuccess, Is.True, first.Error);
            Assert.That(second.IsSuccess, Is.True, second.Error);
            Assert.That(JsonUtility.ToJson(first.Value), Is.EqualTo(JsonUtility.ToJson(second.Value)));
        }

        [Test]
        public void Run_StopsAtMaxRoundGuard()
        {
            var wait = Track(AbilityDef.CreateRuntime("wait", AbilityTag.None, 0, 1, AbilityTargetType.Enemy));
            var player = new AutoBattleUnitSpec(
                "player",
                CombatTeam.Player,
                Track(CharacterDef.CreateRuntime("player", "Player", 20, 0, 0, 10, DispositionType.Aggressive, new[] { wait })));
            var enemy = new AutoBattleUnitSpec(
                "enemy",
                CombatTeam.Enemy,
                Track(CharacterDef.CreateRuntime("enemy", "Enemy", 20, 0, 0, 1, DispositionType.Aggressive, new[] { wait })));
            var scenario = new AutoBattleScenario(4, 2, new[] { player }, new[] { enemy });
            var simulator = new AutoBattleSimulator();

            var result = simulator.Run(scenario, new AutoBattleOptions { seed = 7, battles = 1, maxRounds = 1 });

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value.guardedBattles, Is.EqualTo(1));
            Assert.That(result.Value.draws, Is.EqualTo(1));
            Assert.That(result.Value.playerWins, Is.EqualTo(0));
            Assert.That(result.Value.enemyWins, Is.EqualTo(0));
            Assert.That(result.Value.averageRounds, Is.EqualTo(1f));
        }

        private AutoBattleScenario CreateCooldownScenario()
        {
            var strike = Track(AbilityDef.CreateRuntime(
                "cd-strike", AbilityTag.Apply, basePower: 5, range: 1, targetType: AbilityTargetType.Enemy));
            var burst = Track(AbilityDef.CreateRuntime(
                "cd-burst", AbilityTag.Apply, basePower: 8, range: 2, targetType: AbilityTargetType.Enemy, cooldownRounds: 2));
            var rally = Track(AbilityDef.CreateRuntime(
                "cd-rally", AbilityTag.Amplify, basePower: 0, range: 3, targetType: AbilityTargetType.Ally,
                amplificationMultiplier: 1.5f, cooldownRounds: 1));

            var playerUnits = new[]
            {
                new AutoBattleUnitSpec(
                    "player-striker",
                    CombatTeam.Player,
                    Track(CharacterDef.CreateRuntime("cd-player-striker", "Striker", 24, 2, 1, 8, DispositionType.Aggressive, new[] { strike, burst }))),
                new AutoBattleUnitSpec(
                    "player-support",
                    CombatTeam.Player,
                    Track(CharacterDef.CreateRuntime("cd-player-support", "Support", 26, 1, 2, 5, DispositionType.Protective, new[] { strike, rally })))
            };

            var enemyUnits = new[]
            {
                new AutoBattleUnitSpec(
                    "enemy-striker",
                    CombatTeam.Enemy,
                    Track(CharacterDef.CreateRuntime("cd-enemy-striker", "Enemy Striker", 24, 2, 1, 8, DispositionType.Aggressive, new[] { strike, burst }))),
                new AutoBattleUnitSpec(
                    "enemy-support",
                    CombatTeam.Enemy,
                    Track(CharacterDef.CreateRuntime("cd-enemy-support", "Enemy Support", 26, 1, 2, 5, DispositionType.Protective, new[] { strike, rally })))
            };

            return new AutoBattleScenario(8, 5, playerUnits, enemyUnits);
        }

        private T Track<T>(T createdObject) where T : Object
        {
            createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
