using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class TurnEngineTests
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
        public void InitiativeOrder_SortsBySpeedDescendingThenUnitId()
        {
            var engine = CreateEngine(
                Unit("z-tie", CombatTeam.Player, 5),
                Unit("b-fast", CombatTeam.Player, 10),
                Unit("a-tie", CombatTeam.Enemy, 5),
                Unit("enemy-slow", CombatTeam.Enemy, 1));

            Assert.That(engine.CurrentRoundOrder, Is.EqualTo(new[] { "b-fast", "a-tie", "z-tie", "enemy-slow" }));
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("b-fast"));
        }

        [Test]
        public void SpeedChange_AppliesOnNextRoundOnly()
        {
            var engine = CreateEngine(
                Unit("actor-a", CombatTeam.Player, 10),
                Unit("actor-b", CombatTeam.Enemy, 5));

            Assert.That(engine.CurrentRoundOrder, Is.EqualTo(new[] { "actor-a", "actor-b" }));

            var updated = CreateState("actor-b-updated", 20);
            Assert.That(engine.UpdateCombatantState("actor-b", updated).IsSuccess, Is.True);

            Assert.That(engine.CurrentRoundOrder, Is.EqualTo(new[] { "actor-a", "actor-b" }));
            Assert.That(engine.Submit(new SkipTurnCommand("actor-a")).IsSuccess, Is.True);
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("actor-b"));

            Assert.That(engine.Submit(new SkipTurnCommand("actor-b")).IsSuccess, Is.True);
            Assert.That(engine.RoundNumber, Is.EqualTo(2));
            Assert.That(engine.CurrentRoundOrder, Is.EqualTo(new[] { "actor-b", "actor-a" }));
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("actor-b"));
        }

        [Test]
        public void DefeatedUnit_IsRemovedFromCurrentRoundImmediately()
        {
            var engine = CreateEngine(
                Unit("actor-a", CombatTeam.Player, 10),
                Unit("actor-b", CombatTeam.Enemy, 9),
                Unit("actor-c", CombatTeam.Enemy, 8));

            Assert.That(engine.DefeatCombatant("actor-b").IsSuccess, Is.True);

            Assert.That(engine.IsAlive("actor-b"), Is.False);
            Assert.That(engine.CurrentRoundOrder, Does.Not.Contain("actor-b"));

            Assert.That(engine.Submit(new SkipTurnCommand("actor-a")).IsSuccess, Is.True);
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("actor-c"));
        }

        [Test]
        public void RoundProgression_RebuildsOrderAndResetsTurnBudget()
        {
            var engine = CreateEngine(
                Unit("actor-a", CombatTeam.Player, 10),
                Unit("actor-b", CombatTeam.Enemy, 5));

            Assert.That(engine.Submit(new MoveCommand("actor-a", TurnEngine.DefaultMovementPerTurn)).IsSuccess, Is.True);
            Assert.That(engine.CurrentTurn.RemainingMovement, Is.EqualTo(0));
            Assert.That(engine.Submit(new MoveCommand("actor-a", 1)).IsFailure, Is.True);

            Assert.That(engine.Submit(new UseAbilityCommand("actor-a", "placeholder")).IsSuccess, Is.True);
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("actor-b"));
            Assert.That(engine.RoundNumber, Is.EqualTo(1));

            Assert.That(engine.Submit(new SkipTurnCommand("actor-b")).IsSuccess, Is.True);
            Assert.That(engine.RoundNumber, Is.EqualTo(2));
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("actor-a"));
            Assert.That(engine.CurrentTurn.RemainingMovement, Is.EqualTo(TurnEngine.DefaultMovementPerTurn));
            Assert.That(engine.CurrentTurn.HasAction, Is.True);
        }

        [Test]
        public void TeamWipe_EndsCombatWithWinner()
        {
            var engine = CreateEngine(
                Unit("actor-a", CombatTeam.Player, 10),
                Unit("actor-b", CombatTeam.Enemy, 5));

            Assert.That(engine.DefeatCombatant("actor-b").IsSuccess, Is.True);

            Assert.That(engine.IsCombatEnded, Is.True);
            Assert.That(engine.WinningTeam, Is.EqualTo(CombatTeam.Player));
            Assert.That(engine.CurrentTurn, Is.Null);
            Assert.That(engine.Submit(new SkipTurnCommand("actor-a")).IsFailure, Is.True);
        }

        private TurnEngine CreateEngine(params CombatantRef[] combatants)
        {
            var result = TurnEngine.Create(combatants);
            Assert.That(result.IsSuccess, Is.True, result.Error);
            return result.Value;
        }

        private CombatantRef Unit(string unitId, CombatTeam team, int speed, int currentHp = 10)
        {
            var state = CreateState(unitId, speed, currentHp);
            var result = CombatantRef.Create(unitId, team, state);
            Assert.That(result.IsSuccess, Is.True, result.Error);
            return result.Value;
        }

        private CharacterState CreateState(string id, int speed, int currentHp = 10)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDef>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "id", id);
            SetPrivateField(definition, "displayName", id);
            SetPrivateField(definition, "maxHp", 10);
            SetPrivateField(definition, "speed", speed);

            var abilities = new[]
            {
                CreateAbility(id + "-a"),
                CreateAbility(id + "-b")
            };

            var result = CharacterState.Create(
                definition,
                currentHp,
                slotCount: AbilityLoadout.DefaultSlots,
                assignedAbilities: abilities);
            Assert.That(result.IsSuccess, Is.True, result.Error);
            return result.Value;
        }

        private AbilityDef CreateAbility(string id)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDef>();
            createdObjects.Add(ability);
            SetPrivateField(ability, "id", id);
            SetPrivateField(ability, "displayName", id);
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
