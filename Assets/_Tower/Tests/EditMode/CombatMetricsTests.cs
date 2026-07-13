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
        public void Metrics_CountsActionsDamageAndKills()
        {
            var battlefield = new AnalogBattlefield(4f, 2f);
            var statusBoard = new StatusBoard();
            var metrics = new CombatMetrics();
            // A single real-time resolution must be lethal so combat-end metrics are observable without a turn loop.
            var strike = Track(AbilityDef.CreateRuntime("strike", AbilityTag.Apply, 6, 2, AbilityTargetType.Enemy));
            var caster = Unit("caster", CombatTeam.Player, currentHp: 20, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, currentHp: 6, abilities: new[] { strike });
            Assert.That(battlefield.TryPlaceOccupant("caster", new BattlePos(1f, 1f)), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("enemy", new BattlePos(2f, 1f)), Is.True);
            var resolver = AbilityResolver.Create(battlefield, statusBoard, metrics);
            Assert.That(resolver.IsSuccess, Is.True, resolver.Error);
            var stateResult = CombatState.Create(new[] { caster, enemy }, statusBoard, metrics);
            Assert.That(stateResult.IsSuccess, Is.True, stateResult.Error);
            var state = stateResult.Value;

            Assert.That(resolver.Value.Execute(state, new UseAbilityCommand("caster", "strike", "enemy")).IsSuccess, Is.True);

            Assert.That(state.IsCombatEnded, Is.True);
            Assert.That(metrics.ActionCount, Is.EqualTo(1));
            Assert.That(metrics.WinningTeam, Is.EqualTo(CombatTeam.Player));
            Assert.That(metrics.Units["caster"].ActionsTaken, Is.EqualTo(1));
            Assert.That(metrics.Units["caster"].DamageDealt, Is.EqualTo(6));
            Assert.That(metrics.Units["caster"].Kills, Is.EqualTo(1));
            Assert.That(metrics.Units["enemy"].DamageTaken, Is.EqualTo(6));
            Assert.That(metrics.StartedAtUtc.HasValue, Is.True);
            Assert.That(metrics.EndedAtUtc.HasValue, Is.True);
        }

        private CombatantRef Unit(
            string unitId,
            CombatTeam team,
            int currentHp,
            AbilityDef[] abilities)
        {
            var assignedAbilities = abilities;
            if (assignedAbilities == null || assignedAbilities.Length == 0)
            {
                assignedAbilities = new[]
                {
                    Track(AbilityDef.CreateRuntime(unitId + "-noop", AbilityTag.None, 0, 0, AbilityTargetType.Enemy))
                };
            }

            var definition = Track(CharacterDef.CreateRuntime(unitId, unitId, 20, 0, 0, 10, DispositionType.Aggressive, assignedAbilities));
            var state = CharacterState.Create(
                definition,
                currentHp,
                slotCount: assignedAbilities.Length,
                assignedAbilities: assignedAbilities);
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
