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

        private T Track<T>(T createdObject) where T : Object
        {
            createdObjects.Add(createdObject);
            return createdObject;
        }
    }
}
