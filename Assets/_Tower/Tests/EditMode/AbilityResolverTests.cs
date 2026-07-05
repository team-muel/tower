using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class AbilityResolverTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GridMap map;
        private StatusBoard statusBoard;
        private AbilityResolver resolver;

        [SetUp]
        public void SetUp()
        {
            map = new GridMap(8, 8);
            statusBoard = new StatusBoard();
            var created = AbilityResolver.Create(map, statusBoard);
            Assert.That(created.IsSuccess, Is.True, created.Error);
            resolver = created.Value;
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
        public void Apply_AppliesMarkAndDealsBasePowerDamage()
        {
            var mark = CreateMark("burn", durationTurns: 2, stackable: false);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5, mark: mark);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, attack: 3, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1, defense: 2);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, enemy);

            var result = resolver.Execute(engine, new UseAbilityCommand("caster", "strike", "enemy"));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - (5 + 3 - 2)));
            Assert.That(statusBoard.HasMark("enemy", "burn", engine.RoundNumber), Is.True);
        }

        [Test]
        public void Apply_WithZeroPower_AppliesMarkWithoutDamage()
        {
            var mark = CreateMark("hex", durationTurns: 2, stackable: false);
            var hex = CreateAbility("hex-ability", AbilityTag.Apply, power: 0, mark: mark);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, attack: 3, abilities: new[] { hex });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, enemy);

            var result = resolver.Execute(engine, new UseAbilityCommand("caster", "hex-ability", "enemy"));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20));
            Assert.That(statusBoard.HasMark("enemy", "hex", engine.RoundNumber), Is.True);
        }

        [Test]
        public void Consume_WithMark_RemovesMarkAndDealsBonusDamage()
        {
            var mark = CreateMark("burn", durationTurns: 2, stackable: false);
            var detonate = CreateAbility("detonate", AbilityTag.Consume, power: 4, mark: mark);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, attack: 3, abilities: new[] { detonate });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1, defense: 2);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, enemy);
            Assert.That(statusBoard.ApplyMark("enemy", mark, engine.RoundNumber).IsSuccess, Is.True);

            var result = resolver.Execute(engine, new UseAbilityCommand("caster", "detonate", "enemy"));

            var expectedDamage = Mathf.RoundToInt(4 * AbilityResolver.ConsumeBonusMultiplier) + 3 - 2;
            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - expectedDamage));
            Assert.That(statusBoard.HasMark("enemy", "burn", engine.RoundNumber), Is.False);
        }

        [Test]
        public void Consume_WithoutMark_DealsBasePowerOnly()
        {
            var mark = CreateMark("burn", durationTurns: 2, stackable: false);
            var detonate = CreateAbility("detonate", AbilityTag.Consume, power: 4, mark: mark);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, attack: 3, abilities: new[] { detonate });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1, defense: 2);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, enemy);

            var result = resolver.Execute(engine, new UseAbilityCommand("caster", "detonate", "enemy"));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - (4 + 3 - 2)));
        }

        [Test]
        public void Amplify_MultipliesAllyNextAbilityPowerOnce()
        {
            var amplify = CreateAbility("war-cry", AbilityTag.Amplify, targetType: AbilityTargetType.Ally, amplifyMultiplier: 2f);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { amplify });
            var ally = Unit("ally", CombatTeam.Player, speed: 5, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("ally", 1, 0);
            Place("enemy", 1, 1);
            var engine = CreateEngine(caster, ally, enemy);

            Assert.That(resolver.Execute(engine, new UseAbilityCommand("caster", "war-cry", "ally")).IsSuccess, Is.True);
            Assert.That(statusBoard.IsAmplified("ally", engine.RoundNumber), Is.True);

            Assert.That(resolver.Execute(engine, new UseAbilityCommand("ally", "strike", "enemy")).IsSuccess, Is.True);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - 10), "First strike should be amplified.");

            Assert.That(resolver.Execute(engine, new UseAbilityCommand("ally", "strike", "enemy")).IsSuccess, Is.True);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - 10 - 5), "Second strike should not be amplified.");
        }

        [Test]
        public void Amplify_ReapplyRefreshesInsteadOfStacking()
        {
            var amplify = CreateAbility("war-cry", AbilityTag.Amplify, targetType: AbilityTargetType.Ally, amplifyMultiplier: 2f);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { amplify });
            var ally = Unit("ally", CombatTeam.Player, speed: 5, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("ally", 1, 0);
            Place("enemy", 1, 1);
            var engine = CreateEngine(caster, ally, enemy);

            Assert.That(resolver.Execute(engine, new UseAbilityCommand("caster", "war-cry", "ally")).IsSuccess, Is.True);
            Assert.That(resolver.Execute(engine, new UseAbilityCommand("caster", "war-cry", "ally")).IsSuccess, Is.True);

            Assert.That(resolver.Execute(engine, new UseAbilityCommand("ally", "strike", "enemy")).IsSuccess, Is.True);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - 10), "Amplify must not stack multiplicatively.");
        }

        [Test]
        public void Amplify_ExpiresAfterItsRound()
        {
            var amplify = CreateAbility("war-cry", AbilityTag.Amplify, targetType: AbilityTargetType.Ally, amplifyMultiplier: 2f);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { amplify });
            var ally = Unit("ally", CombatTeam.Player, speed: 5, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("ally", 1, 0);
            Place("enemy", 2, 0);
            var engine = CreateEngine(caster, ally, enemy);

            Assert.That(engine.Submit(new UseAbilityCommand("caster", "war-cry", "ally")).IsSuccess, Is.True);
            Assert.That(statusBoard.IsAmplified("ally", engine.RoundNumber), Is.True);

            Assert.That(engine.Submit(new SkipTurnCommand("ally")).IsSuccess, Is.True);
            Assert.That(engine.Submit(new SkipTurnCommand("enemy")).IsSuccess, Is.True);
            Assert.That(engine.RoundNumber, Is.EqualTo(2));
            Assert.That(statusBoard.IsAmplified("ally", engine.RoundNumber), Is.False);

            Assert.That(engine.Submit(new SkipTurnCommand("caster")).IsSuccess, Is.True);
            Assert.That(engine.Submit(new UseAbilityCommand("ally", "strike", "enemy")).IsSuccess, Is.True);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - 5), "Expired amplify must not boost the strike.");
        }

        [Test]
        public void Consume_AfterMarkExpires_DealsBasePowerOnly()
        {
            var mark = CreateMark("burn", durationTurns: 1, stackable: false);
            var apply = CreateAbility("ignite", AbilityTag.Apply, power: 0, mark: mark);
            var detonate = CreateAbility("detonate", AbilityTag.Consume, power: 4, mark: mark);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { apply, detonate });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, enemy);

            Assert.That(engine.Submit(new UseAbilityCommand("caster", "ignite", "enemy")).IsSuccess, Is.True);
            Assert.That(statusBoard.HasMark("enemy", "burn", engine.RoundNumber), Is.True);

            Assert.That(engine.Submit(new SkipTurnCommand("enemy")).IsSuccess, Is.True);
            Assert.That(engine.RoundNumber, Is.EqualTo(2));
            Assert.That(statusBoard.HasMark("enemy", "burn", engine.RoundNumber), Is.False);

            Assert.That(engine.Submit(new UseAbilityCommand("caster", "detonate", "enemy")).IsSuccess, Is.True);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - 4), "Expired mark must not grant the consume bonus.");
        }

        [Test]
        public void Damage_IsClampedToMinimumOne()
        {
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 1);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1, defense: 50);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, enemy);

            var result = resolver.Execute(engine, new UseAbilityCommand("caster", "strike", "enemy"));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(19));
        }

        [Test]
        public void Execute_FailsWhenTargetOutOfRange()
        {
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5, range: 1);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 3, 0);
            var engine = CreateEngine(caster, enemy);

            var result = resolver.Execute(engine, new UseAbilityCommand("caster", "strike", "enemy"));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20));
        }

        [Test]
        public void Execute_FailsWhenLineOfSightBlocked()
        {
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5, range: 3);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 0, 2);
            map.SetBlocked(new GridPos(0, 1), true);
            var engine = CreateEngine(caster, enemy);

            var result = resolver.Execute(engine, new UseAbilityCommand("caster", "strike", "enemy"));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20));
        }

        [Test]
        public void Execute_FailsOnWrongTargetType()
        {
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var amplify = CreateAbility("war-cry", AbilityTag.Amplify, targetType: AbilityTargetType.Ally, amplifyMultiplier: 2f);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike, amplify });
            var ally = Unit("ally", CombatTeam.Player, speed: 5);
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("ally", 1, 0);
            Place("enemy", 0, 1);
            var engine = CreateEngine(caster, ally, enemy);

            Assert.That(resolver.Execute(engine, new UseAbilityCommand("caster", "strike", "ally")).IsFailure, Is.True);
            Assert.That(resolver.Execute(engine, new UseAbilityCommand("caster", "war-cry", "enemy")).IsFailure, Is.True);
            Assert.That(engine.GetCombatant("ally").State.CurrentHp, Is.EqualTo(20));
            Assert.That(statusBoard.IsAmplified("enemy", engine.RoundNumber), Is.False);
        }

        [Test]
        public void Execute_FailsOnDeadTarget()
        {
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var deadEnemy = Unit("dead-enemy", CombatTeam.Enemy, speed: 5, currentHp: 0);
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, deadEnemy, enemy);

            var result = resolver.Execute(engine, new UseAbilityCommand("caster", "strike", "dead-enemy"));

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public void Execute_FailsWhenCellTargetMissing()
        {
            var blast = CreateAbility("blast", AbilityTag.Apply, power: 5, targetType: AbilityTargetType.Cell);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { blast });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, enemy);

            var result = resolver.Execute(engine, new UseAbilityCommand("caster", "blast"));

            Assert.That(result.IsFailure, Is.True);
        }

        [Test]
        public void CellTargetedAbility_HitsCellOccupant()
        {
            var blast = CreateAbility("blast", AbilityTag.Apply, power: 5, targetType: AbilityTargetType.Cell);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { blast });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, enemy);

            var result = resolver.Execute(engine, new UseAbilityCommand("caster", "blast", targetCell: new GridPos(1, 0)));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - 5));
        }

        [Test]
        public void Kill_RemovesUnitFromTurnQueueAndGrid()
        {
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 5, currentHp: 3);
            var otherEnemy = Unit("other-enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            Place("other-enemy", 2, 0);
            var engine = CreateEngine(caster, enemy, otherEnemy);

            var result = resolver.Execute(engine, new UseAbilityCommand("caster", "strike", "enemy"));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.IsAlive("enemy"), Is.False);
            Assert.That(engine.CurrentRoundOrder, Does.Not.Contain("enemy"));
            Assert.That(map.GetOccupant(new GridPos(1, 0)), Is.Null.Or.Empty);
            Assert.That(engine.IsCombatEnded, Is.False);
        }

        [Test]
        public void Submit_FailedAbilityDoesNotConsumeAction()
        {
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5, range: 1);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 3, 0);
            var engine = CreateEngine(caster, enemy);

            var result = engine.Submit(new UseAbilityCommand("caster", "strike", "enemy"));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("caster"));
            Assert.That(engine.CurrentTurn.HasAction, Is.True);
        }

        [Test]
        public void Submit_AbilityConsumesActionAndAdvancesTurn()
        {
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, enemy);

            var result = engine.Submit(new UseAbilityCommand("caster", "strike", "enemy"));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - 5));
            Assert.That(engine.CurrentTurn.UnitId, Is.EqualTo("enemy"));
        }

        [Test]
        public void Submit_KillingLastEnemyEndsCombat()
        {
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1, currentHp: 1);
            Place("caster", 0, 0);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, enemy);

            var result = engine.Submit(new UseAbilityCommand("caster", "strike", "enemy"));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(engine.IsCombatEnded, Is.True);
            Assert.That(engine.WinningTeam, Is.EqualTo(CombatTeam.Player));
            Assert.That(engine.CurrentTurn, Is.Null);
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

        private MarkDef CreateMark(string id, int durationTurns, bool stackable)
        {
            var mark = ScriptableObject.CreateInstance<MarkDef>();
            createdObjects.Add(mark);
            SetPrivateField(mark, "id", id);
            SetPrivateField(mark, "displayName", id);
            SetPrivateField(mark, "durationTurns", durationTurns);
            SetPrivateField(mark, "stackable", stackable);
            return mark;
        }

        private static void SetPrivateField<T>(T target, string fieldName, object value)
        {
            var field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
