using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class AiTurnDriverTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GridMap map;
        private StatusBoard statusBoard;
        private AbilityResolver resolver;
        private ActionScorer scorer;

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
        public void TakeTurn_MovesIntoRangeAndAttacks()
        {
            InitGrid(8, 1);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 3, 0);
            var engine = CreateEngine(caster, enemy);
            var driver = CreateDriver(engine);

            var result = driver.TakeTurn();

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(map.GetOccupant(new GridPos(2, 0)), Is.EqualTo("caster"), "AI should close to melee range.");
            Assert.That(map.GetOccupant(new GridPos(0, 0)), Is.Null.Or.Empty);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - 5));
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("enemy"), "The AI turn must end after the ability.");
        }

        [Test]
        public void TakeTurn_KillsTargetAndEndsCombat()
        {
            InitGrid(8, 1);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1, currentHp: 3);
            Place("caster", 0, 0);
            Place("enemy", 3, 0);
            var engine = CreateEngine(caster, enemy);
            var driver = CreateDriver(engine);

            var result = driver.TakeTurn();

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.IsAlive("enemy"), Is.False);
            Assert.That(engine.IsCombatEnded, Is.True);
            Assert.That(engine.WinningTeam, Is.EqualTo(CombatTeam.Player));
        }

        [Test]
        public void TakeTurn_RepositionsAndSkipsWhenNoAbilityIsUsable()
        {
            InitGrid(12, 1);
            var caster = Unit("caster", CombatTeam.Player, speed: 10);
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 11, 0);
            var engine = CreateEngine(caster, enemy);
            var driver = CreateDriver(engine);

            var result = driver.TakeTurn();

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(map.GetOccupant(new GridPos(4, 0)), Is.EqualTo("caster"), "AI should advance toward the enemy.");
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20));
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("enemy"), "The turn must end with a skip.");
        }

        [Test]
        public void TakeTurn_DrivesEnemyUnitsWithTheSameScorer()
        {
            InitGrid(8, 1);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var hero = Unit("hero", CombatTeam.Player, speed: 1);
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 10, abilities: new[] { strike });
            Place("hero", 0, 0);
            Place("enemy", 3, 0);
            var engine = CreateEngine(hero, enemy);
            var driver = CreateDriver(engine);
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("enemy"));

            var result = driver.TakeTurn();

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.GetCombatant("hero").State.CurrentHp, Is.EqualTo(20 - 5), "Enemy AI must use the shared scorer to attack.");
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("hero"));
        }

        private void InitGrid(int width, int height)
        {
            map = new GridMap(width, height);
            statusBoard = new StatusBoard();
            var createdResolver = AbilityResolver.Create(map, statusBoard);
            Assert.That(createdResolver.IsSuccess, Is.True, createdResolver.Error);
            resolver = createdResolver.Value;
            var createdScorer = ActionScorer.Create(map, statusBoard);
            Assert.That(createdScorer.IsSuccess, Is.True, createdScorer.Error);
            scorer = createdScorer.Value;
        }

        private AiTurnDriver CreateDriver(TurnEngine engine)
        {
            var created = AiTurnDriver.Create(engine, map, scorer);
            Assert.That(created.IsSuccess, Is.True, created.Error);
            return created.Value;
        }

        private TurnEngine CreateEngine(params CombatantRef[] combatants)
        {
            var result = TurnEngine.Create(combatants, abilityExecutor: resolver);
            Assert.That(result.IsSuccess, Is.True, result.Error);
            return result.Value;
        }

        private void Place(string unitId, int x, int y)
        {
            Assert.That(map.TrySetOccupant(new GridPos(x, y), unitId), Is.True, unitId);
        }

        private CombatantRef Unit(
            string unitId,
            CombatTeam team,
            int speed,
            DispositionType disposition = DispositionType.Aggressive,
            int attack = 0,
            int defense = 0,
            int currentHp = 20,
            AbilityDef[] abilities = null)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDef>();
            createdObjects.Add(definition);
            SetPrivateField(definition, "id", unitId);
            SetPrivateField(definition, "displayName", unitId);
            SetPrivateField(definition, "maxHp", 20);
            SetPrivateField(definition, "attack", attack);
            SetPrivateField(definition, "defense", defense);
            SetPrivateField(definition, "speed", speed);
            SetPrivateField(definition, "disposition", disposition);

            var assigned = new List<AbilityDef>(abilities ?? new AbilityDef[0]);
            while (assigned.Count < AbilityLoadout.DefaultSlots)
            {
                assigned.Add(CreateAbility(unitId + "-filler-" + assigned.Count));
            }

            var state = CharacterState.Create(
                definition,
                currentHp,
                slotCount: assigned.Count,
                assignedAbilities: assigned.ToArray());
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
            MarkDef mark = null,
            AbilityTargetType targetType = AbilityTargetType.Enemy,
            float amplifyMultiplier = 1f)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDef>();
            createdObjects.Add(ability);
            SetPrivateField(ability, "id", id);
            SetPrivateField(ability, "displayName", id);
            SetPrivateField(ability, "tag", tag);
            SetPrivateField(ability, "basePower", power);
            SetPrivateField(ability, "range", range);
            SetPrivateField(ability, "targetMark", mark);
            SetPrivateField(ability, "targetType", targetType);
            SetPrivateField(ability, "amplificationMultiplier", amplifyMultiplier);
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
