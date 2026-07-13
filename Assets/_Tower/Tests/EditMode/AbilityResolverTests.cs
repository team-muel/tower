using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class AbilityResolverTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private AnalogBattlefield battlefield;
        private StatusBoard statusBoard;
        private AbilityResolver resolver;

        [SetUp]
        public void SetUp()
        {
            battlefield = new AnalogBattlefield(8f, 8f);
            statusBoard = new StatusBoard();
            var created = AbilityResolver.Create(battlefield, statusBoard);
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
            var mark = Mark("burn", durationTurns: 2);
            var strike = Ability("strike", AbilityTag.Apply, power: 5, range: 2, mark: mark);
            var caster = Unit("caster", CombatTeam.Player, attack: 3, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy, defense: 2);
            Place("caster", 1f, 1f);
            Place("enemy", 2f, 1f);
            var state = State(caster, enemy);

            var result = resolver.Execute(state, new UseAbilityCommand("caster", "strike", "enemy"));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(state.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - (5 + 3 - 2)));
            Assert.That(statusBoard.HasMark("enemy", "burn", state.ElapsedSeconds), Is.True);
        }

        [Test]
        public void Consume_WithMark_RemovesMarkAndDealsBonusDamage()
        {
            var mark = Mark("burn", durationTurns: 2);
            var detonate = Ability("detonate", AbilityTag.Consume, power: 4, range: 2, mark: mark);
            var caster = Unit("caster", CombatTeam.Player, attack: 3, abilities: new[] { detonate });
            var enemy = Unit("enemy", CombatTeam.Enemy, defense: 2);
            Place("caster", 1f, 1f);
            Place("enemy", 2f, 1f);
            var state = State(caster, enemy);
            Assert.That(statusBoard.ApplyMark("enemy", mark, state.ElapsedSeconds).IsSuccess, Is.True);

            var result = resolver.Execute(state, new UseAbilityCommand("caster", "detonate", "enemy"));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(statusBoard.HasMark("enemy", "burn", state.ElapsedSeconds), Is.False);
            Assert.That(state.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - (int)System.Math.Round((4 * AbilityResolver.ConsumeBonusMultiplier) + 3 - 2)));
        }

        [Test]
        public void Amplify_IsConsumedByNextDamage()
        {
            var amplify = Ability("war-cry", AbilityTag.Amplify, power: 0, range: 2, targetType: AbilityTargetType.Ally, amplification: 2f);
            var strike = Ability("strike", AbilityTag.Apply, power: 4, range: 2);
            var caster = Unit("caster", CombatTeam.Player, abilities: new[] { amplify });
            var ally = Unit("ally", CombatTeam.Player, attack: 2, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy);
            Place("caster", 1f, 1f);
            Place("ally", 2f, 1f);
            Place("enemy", 3f, 1f);
            var state = State(caster, ally, enemy);

            Assert.That(resolver.Execute(state, new UseAbilityCommand("caster", "war-cry", "ally")).IsSuccess, Is.True);
            Assert.That(statusBoard.IsAmplified("ally", state.ElapsedSeconds), Is.True);
            Assert.That(resolver.Execute(state, new UseAbilityCommand("ally", "strike", "enemy")).IsSuccess, Is.True);

            Assert.That(statusBoard.IsAmplified("ally", state.ElapsedSeconds), Is.False);
            Assert.That(state.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20 - ((4 * 2) + 2)));
        }

        [Test]
        public void PointTarget_ResolvesOccupiedPoint()
        {
            var blast = Ability("blast", AbilityTag.Apply, power: 3, range: 3, targetType: AbilityTargetType.Cell);
            var caster = Unit("caster", CombatTeam.Player, abilities: new[] { blast });
            var enemy = Unit("enemy", CombatTeam.Enemy);
            Place("caster", 1f, 1f);
            Place("enemy", 2f, 1f);
            var state = State(caster, enemy);

            var result = resolver.Execute(state, new UseAbilityCommand("caster", "blast", targetPoint: new BattlePos(2f, 1f)));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(state.GetCombatant("enemy").State.CurrentHp, Is.LessThan(20));
        }

        [Test]
        public void Execute_FailsWhenAbilityIsOnCooldown()
        {
            var strike = Ability("strike", AbilityTag.Apply, power: 3, range: 2, cooldown: 2);
            var casterState = CharacterState.Create(
                Character("caster", attack: 0, defense: 0, abilities: new[] { strike }),
                currentHp: 20,
                slotCount: 1,
                assignedAbilities: new[] { strike });
            Assert.That(casterState.IsSuccess, Is.True, casterState.Error);
            var cooled = casterState.Value.WithAbilityCooldown("strike", 1);
            Assert.That(cooled.IsSuccess, Is.True, cooled.Error);
            var caster = CombatantRef.Create("caster", CombatTeam.Player, cooled.Value).Value;
            var enemy = Unit("enemy", CombatTeam.Enemy);
            Place("caster", 1f, 1f);
            Place("enemy", 2f, 1f);
            var state = State(caster, enemy);

            var result = resolver.Execute(state, new UseAbilityCommand("caster", "strike", "enemy"));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("cooldown"));
        }

        [Test]
        public void LethalDamageEndsCombatAndRemovesOccupant()
        {
            var strike = Ability("strike", AbilityTag.Apply, power: 50, range: 2);
            var caster = Unit("caster", CombatTeam.Player, abilities: new[] { strike });
            var enemy = Unit("enemy", CombatTeam.Enemy);
            Place("caster", 1f, 1f);
            Place("enemy", 2f, 1f);
            var state = State(caster, enemy);

            var result = resolver.Execute(state, new UseAbilityCommand("caster", "strike", "enemy"));

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(state.IsCombatEnded, Is.True);
            Assert.That(state.WinningTeam, Is.EqualTo(CombatTeam.Player));
            Assert.That(battlefield.FindOccupant("enemy").HasValue, Is.False);
        }

        private CombatState State(params CombatantRef[] combatants)
        {
            var result = CombatState.Create(combatants, statusBoard);
            Assert.That(result.IsSuccess, Is.True, result.Error);
            return result.Value;
        }

        private void Place(string unitId, float x, float y)
        {
            Assert.That(battlefield.TryPlaceOccupant(unitId, new BattlePos(x, y)), Is.True, unitId);
        }

        private AbilityDef Ability(
            string id,
            AbilityTag tag,
            int power,
            int range,
            AbilityTargetType targetType = AbilityTargetType.Enemy,
            MarkDef mark = null,
            float amplification = 1f,
            int cooldown = 0)
        {
            var ability = AbilityDef.CreateRuntime(
                id,
                tag,
                power,
                range,
                targetType,
                mark,
                amplification,
                cooldownRounds: cooldown);
            createdObjects.Add(ability);
            return ability;
        }

        private MarkDef Mark(string id, int durationTurns)
        {
            var mark = MarkDef.CreateRuntime(id, id, durationTurns);
            createdObjects.Add(mark);
            return mark;
        }

        private CombatantRef Unit(
            string unitId,
            CombatTeam team,
            int attack = 0,
            int defense = 0,
            AbilityDef[] abilities = null)
        {
            var assignedAbilities = abilities;
            if (assignedAbilities == null || assignedAbilities.Length == 0)
            {
                assignedAbilities = new[]
                {
                    AbilityDef.CreateRuntime(unitId + "-noop", AbilityTag.None, 0, 0, AbilityTargetType.Enemy)
                };
                createdObjects.Add(assignedAbilities[0]);
            }

            var definition = Character(unitId, attack, defense, assignedAbilities);
            var state = CharacterState.Create(
                definition,
                currentHp: 20,
                slotCount: assignedAbilities.Length,
                assignedAbilities: assignedAbilities);
            Assert.That(state.IsSuccess, Is.True, state.Error);
            var combatant = CombatantRef.Create(unitId, team, state.Value);
            Assert.That(combatant.IsSuccess, Is.True, combatant.Error);
            return combatant.Value;
        }

        private CharacterDef Character(string id, int attack, int defense, AbilityDef[] abilities)
        {
            var definition = CharacterDef.CreateRuntime(id, id, 20, attack, defense, 10, DispositionType.Aggressive, abilities);
            createdObjects.Add(definition);
            return definition;
        }
    }
}
