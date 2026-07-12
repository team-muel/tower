using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class ActionScorerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private AnalogBattlefield battlefield;
        private StatusBoard statusBoard;
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
        public void ChooseAction_IsDeterministicAcrossIdenticalRuns()
        {
            var first = RunMarkedEnemyScenario();
            var second = RunMarkedEnemyScenario();

            Assert.That(second.Kind, Is.EqualTo(first.Kind));
            Assert.That(second.AbilityId, Is.EqualTo(first.AbilityId));
            Assert.That(second.TargetUnitId, Is.EqualTo(first.TargetUnitId));
            Assert.That(second.MovePosition, Is.EqualTo(first.MovePosition));
            Assert.That(second.MoveDistance, Is.EqualTo(first.MoveDistance));
            Assert.That(second.Score, Is.EqualTo(first.Score));
        }

        [Test]
        public void ChooseAction_PrefersConsumingMarkedEnemy()
        {
            var plan = RunMarkedEnemyScenario();

            Assert.That(plan.Kind, Is.EqualTo(AiPlanKind.Ability));
            Assert.That(plan.AbilityId, Is.EqualTo("detonate"));
            Assert.That(plan.TargetUnitId, Is.EqualTo("enemy-b"));
        }

        [Test]
        public void ChoosePendingAction_RestrictsAbilityCandidate()
        {
            InitScorer();
            var detonate = Ability("detonate", AbilityTag.Consume, 4, 8);
            var strike = Ability("strike", AbilityTag.Apply, 5, 8);
            var caster = Unit("caster", CombatTeam.Player, abilities: new[] { detonate, strike });
            var enemy = Unit("enemy", CombatTeam.Enemy);
            Place("caster", 2f, 2f);
            Place("enemy", 3f, 2f);
            var state = State(caster, enemy);

            var result = scorer.ChoosePendingAction(state, "caster", "strike");

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value.Kind, Is.EqualTo(AiPlanKind.Ability));
            Assert.That(result.Value.AbilityId, Is.EqualTo("strike"));
        }

        [Test]
        public void ChooseAction_PrefersKillableTarget()
        {
            InitScorer();
            var strike = Ability("strike", AbilityTag.Apply, 20, 8);
            var caster = Unit("caster", CombatTeam.Player, abilities: new[] { strike });
            var healthy = Unit("enemy-a", CombatTeam.Enemy, currentHp: 20);
            var wounded = Unit("enemy-b", CombatTeam.Enemy, currentHp: 4);
            Place("caster", 2f, 2f);
            Place("enemy-a", 3f, 2f);
            Place("enemy-b", 2f, 3f);
            var state = State(caster, healthy, wounded);

            var result = scorer.ChooseAction(state, "caster");

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value.TargetUnitId, Is.EqualTo("enemy-b"));
        }

        private AiPlan RunMarkedEnemyScenario()
        {
            InitScorer();
            var mark = Mark("burn");
            var detonate = Ability("detonate", AbilityTag.Consume, 4, 8, mark);
            var caster = Unit("caster", CombatTeam.Player, abilities: new[] { detonate });
            var enemyA = Unit("enemy-a", CombatTeam.Enemy);
            var enemyB = Unit("enemy-b", CombatTeam.Enemy);
            Place("caster", 2f, 2f);
            Place("enemy-a", 3f, 2f);
            Place("enemy-b", 2f, 3f);
            var state = State(caster, enemyA, enemyB);
            Assert.That(statusBoard.ApplyMark("enemy-b", mark, state.ElapsedSeconds).IsSuccess, Is.True);

            var result = scorer.ChooseAction(state, "caster");
            Assert.That(result.IsSuccess, Is.True, result.Error);
            return result.Value;
        }

        private void InitScorer()
        {
            battlefield = new AnalogBattlefield(8f, 8f);
            statusBoard = new StatusBoard();
            var created = ActionScorer.Create(battlefield, statusBoard);
            Assert.That(created.IsSuccess, Is.True, created.Error);
            scorer = created.Value;
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

        private AbilityDef Ability(string id, AbilityTag tag, int power, int range, MarkDef mark = null)
        {
            var ability = AbilityDef.CreateRuntime(id, tag, power, range, AbilityTargetType.Enemy, mark);
            createdObjects.Add(ability);
            return ability;
        }

        private MarkDef Mark(string id)
        {
            var mark = MarkDef.CreateRuntime(id, id, 2);
            createdObjects.Add(mark);
            return mark;
        }

        private CombatantRef Unit(
            string unitId,
            CombatTeam team,
            int currentHp = 20,
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

            var definition = CharacterDef.CreateRuntime(unitId, unitId, 20, 0, 0, 10, DispositionType.Aggressive, assignedAbilities);
            createdObjects.Add(definition);
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
    }
}
