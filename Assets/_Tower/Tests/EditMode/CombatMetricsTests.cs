using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class CombatMetricsTests
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
        public void Metrics_CountsRoundsActionsDamageAndKills()
        {
            var map = new GridMap(4, 1);
            var statusBoard = new StatusBoard();
            var metrics = new CombatMetrics();
            var strike = Track(AbilityDef.CreateRuntime("strike", AbilityTag.Apply, 5, 1, AbilityTargetType.Enemy));
            var caster = Unit("caster", CombatTeam.Player, 10, currentHp: 20, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, 1, currentHp: 6, abilities: new[] { strike });
            Assert.That(map.TrySetOccupant(new GridPos(0, 0), "caster"), Is.True);
            Assert.That(map.TrySetOccupant(new GridPos(1, 0), "enemy"), Is.True);
            var resolver = AbilityResolver.Create(map, statusBoard, metrics);
            Assert.That(resolver.IsSuccess, Is.True, resolver.Error);
            var engineResult = TurnEngine.Create(new[] { caster, enemy }, abilityExecutor: resolver.Value, combatObserver: metrics);
            Assert.That(engineResult.IsSuccess, Is.True, engineResult.Error);
            var engine = engineResult.Value;

            Assert.That(engine.Submit(new UseAbilityCommand("caster", "strike", "enemy")).IsSuccess, Is.True);
            Assert.That(engine.Submit(new SkipTurnCommand("enemy")).IsSuccess, Is.True);
            Assert.That(engine.Submit(new UseAbilityCommand("caster", "strike", "enemy")).IsSuccess, Is.True);

            Assert.That(engine.IsCombatEnded, Is.True);
            Assert.That(metrics.RoundCount, Is.EqualTo(2));
            Assert.That(metrics.ActionCount, Is.EqualTo(3));
            Assert.That(metrics.WinningTeam, Is.EqualTo(CombatTeam.Player));
            Assert.That(metrics.Units["caster"].ActionsTaken, Is.EqualTo(2));
            Assert.That(metrics.Units["caster"].DamageDealt, Is.EqualTo(6));
            Assert.That(metrics.Units["caster"].Kills, Is.EqualTo(1));
            Assert.That(metrics.Units["enemy"].ActionsTaken, Is.EqualTo(1));
            Assert.That(metrics.Units["enemy"].DamageTaken, Is.EqualTo(6));
            Assert.That(metrics.StartedAtUtc.HasValue, Is.True);
            Assert.That(metrics.EndedAtUtc.HasValue, Is.True);
        }

        private CombatantRef Unit(
            string unitId,
            CombatTeam team,
            int speed,
            int currentHp,
            AbilityDef[] abilities)
        {
            var definition = Track(CharacterDef.CreateRuntime(unitId, unitId, 20, 0, 0, speed, DispositionType.Aggressive, abilities));
            var state = CharacterState.Create(
                definition,
                currentHp,
                slotCount: abilities.Length,
                assignedAbilities: abilities);
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
