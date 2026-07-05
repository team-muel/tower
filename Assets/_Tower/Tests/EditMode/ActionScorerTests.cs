using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class ActionScorerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private GridMap map;
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
        public void ChooseAction_IsDeterministic_AcrossIdenticalRuns()
        {
            var first = RunMarkedEnemyScenario();
            var second = RunMarkedEnemyScenario();

            Assert.That(second.Kind, Is.EqualTo(first.Kind));
            Assert.That(second.AbilityId, Is.EqualTo(first.AbilityId));
            Assert.That(second.TargetUnitId, Is.EqualTo(first.TargetUnitId));
            Assert.That(second.MoveDestination, Is.EqualTo(first.MoveDestination));
            Assert.That(second.MoveDistance, Is.EqualTo(first.MoveDistance));
            Assert.That(second.Score, Is.EqualTo(first.Score));
        }

        [Test]
        public void ChooseAction_TieBreaksByTargetIdThenDestination()
        {
            InitScorer(5, 5);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemyA = Unit("enemy-a", CombatTeam.Enemy, speed: 2);
            var enemyB = Unit("enemy-b", CombatTeam.Enemy, speed: 1);
            Place("caster", 2, 2);
            Place("enemy-a", 1, 2);
            Place("enemy-b", 3, 2);
            var engine = CreateEngine(caster, enemyA, enemyB);

            var plan = Choose(engine, "caster");

            // Both enemies are interchangeable; the tie must break on target
            // id ("enemy-a" < "enemy-b") and then on destination (Y, then X).
            Assert.That(plan.Kind, Is.EqualTo(AiPlanKind.Ability));
            Assert.That(plan.AbilityId, Is.EqualTo("strike"));
            Assert.That(plan.TargetUnitId, Is.EqualTo("enemy-a"));
            Assert.That(plan.MoveDestination, Is.EqualTo(new GridPos(1, 1)));
        }

        [Test]
        public void ChooseAction_PrefersConsumingMarkedEnemy()
        {
            var plan = RunMarkedEnemyScenario();

            // "enemy-b" carries the mark; without combo awareness the
            // deterministic tie-break would have picked "enemy-a".
            Assert.That(plan.Kind, Is.EqualTo(AiPlanKind.Ability));
            Assert.That(plan.AbilityId, Is.EqualTo("detonate"));
            Assert.That(plan.TargetUnitId, Is.EqualTo("enemy-b"));
        }

        [Test]
        public void ChooseAction_PrefersKillableTarget()
        {
            InitScorer(5, 5);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { strike });
            var enemyA = Unit("enemy-a", CombatTeam.Enemy, speed: 2);
            var enemyB = Unit("enemy-b", CombatTeam.Enemy, speed: 1, currentHp: 3);
            Place("caster", 2, 2);
            Place("enemy-a", 1, 2);
            Place("enemy-b", 3, 2);
            var engine = CreateEngine(caster, enemyA, enemyB);

            var plan = Choose(engine, "caster");

            Assert.That(plan.Kind, Is.EqualTo(AiPlanKind.Ability));
            Assert.That(plan.TargetUnitId, Is.EqualTo("enemy-b"), "Kill bonus must outweigh the target-id tie-break.");
        }

        [Test]
        public void ChooseAction_Apply_PrefersMarkWhenTeammateCanConsume()
        {
            InitScorer(8, 8);
            var mark = CreateMark("burn", durationTurns: 2, stackable: false);
            var smite = CreateAbility("smite", AbilityTag.Apply, power: 3);
            var brand = CreateAbility("brand", AbilityTag.Apply, power: 2, mark: mark);
            var detonate = CreateAbility("detonate", AbilityTag.Consume, power: 4, mark: mark);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { smite, brand });
            var ally = Unit("ally", CombatTeam.Player, speed: 5, abilities: new[] { detonate });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("ally", 0, 1);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, ally, enemy);

            var plan = Choose(engine, "caster");

            Assert.That(plan.Kind, Is.EqualTo(AiPlanKind.Ability));
            Assert.That(plan.AbilityId, Is.EqualTo("brand"), "Setting up an ally's consume must beat slightly higher raw damage.");
        }

        [Test]
        public void ChooseAction_Apply_PrefersDamageWhenNoTeammateCanConsume()
        {
            InitScorer(8, 8);
            var mark = CreateMark("burn", durationTurns: 2, stackable: false);
            var smite = CreateAbility("smite", AbilityTag.Apply, power: 3);
            var brand = CreateAbility("brand", AbilityTag.Apply, power: 2, mark: mark);
            var jab = CreateAbility("jab", AbilityTag.Apply, power: 1);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { smite, brand });
            var ally = Unit("ally", CombatTeam.Player, speed: 5, abilities: new[] { jab });
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("ally", 0, 1);
            Place("enemy", 1, 0);
            var engine = CreateEngine(caster, ally, enemy);

            var plan = Choose(engine, "caster");

            Assert.That(plan.Kind, Is.EqualTo(AiPlanKind.Ability));
            Assert.That(plan.AbilityId, Is.EqualTo("smite"), "Without a consumer on the team the mark set-up loses to raw damage.");
        }

        [Test]
        public void ChooseAction_Amplify_TargetsNextActingAlly()
        {
            InitScorer(12, 12);
            var warCry = CreateAbility("war-cry", AbilityTag.Amplify, range: 3, targetType: AbilityTargetType.Ally, amplifyMultiplier: 2f);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 5);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { warCry, strike });
            var allyNext = Unit("ally-next", CombatTeam.Player, speed: 8, abilities: new[] { CreateAbility("heavy-blow", AbilityTag.Apply, power: 6) });
            var allyLater = Unit("ally-later", CombatTeam.Player, speed: 5);
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("ally-next", 1, 0);
            Place("ally-later", 0, 1);
            Place("enemy", 11, 11);
            var engine = CreateEngine(caster, allyNext, allyLater, enemy);

            var plan = Choose(engine, "caster");

            // No enemy is reachable, so buffing the ally who acts next (speed
            // order) must beat pure repositioning and buffing anyone else.
            Assert.That(plan.Kind, Is.EqualTo(AiPlanKind.Ability));
            Assert.That(plan.AbilityId, Is.EqualTo("war-cry"));
            Assert.That(plan.TargetUnitId, Is.EqualTo("ally-next"));
        }

        [Test]
        public void ChooseAction_DispositionsDiverge_InTheSameSituation()
        {
            var aggressivePlan = RunDispositionScenario(DispositionType.Aggressive);
            var protectivePlan = RunDispositionScenario(DispositionType.Protective);

            Assert.That(aggressivePlan.Kind, Is.EqualTo(AiPlanKind.Ability), "Aggressive should trade danger for damage.");
            Assert.That(protectivePlan.Kind, Is.Not.EqualTo(AiPlanKind.Ability), "Protective should decline the risky attack.");
            Assert.That(
                GridDistance.Manhattan(protectivePlan.MoveDestination, new GridPos(0, 0)),
                Is.EqualTo(1),
                "Protective should stay next to the wounded ally.");
        }

        [Test]
        public void ChooseAction_Protective_MovesTowardWoundedAlly()
        {
            InitScorer(12, 2);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 2);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, disposition: DispositionType.Protective, abilities: new[] { strike });
            var ally = Unit("ally", CombatTeam.Player, speed: 5, currentHp: 3);
            var enemy = Unit("enemy", CombatTeam.Enemy, speed: 1);
            Place("caster", 0, 0);
            Place("ally", 5, 0);
            Place("enemy", 11, 0);
            var engine = CreateEngine(caster, ally, enemy);

            var plan = Choose(engine, "caster");

            Assert.That(plan.Kind, Is.EqualTo(AiPlanKind.Move));
            Assert.That(plan.MoveDestination, Is.EqualTo(new GridPos(4, 0)), "Protective should close the gap to the wounded ally.");
        }

        private AiPlan RunMarkedEnemyScenario()
        {
            InitScorer(8, 8);
            var mark = CreateMark("burn", durationTurns: 2, stackable: false);
            var detonate = CreateAbility("detonate", AbilityTag.Consume, power: 4, mark: mark);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, abilities: new[] { detonate });
            var enemyA = Unit("enemy-a", CombatTeam.Enemy, speed: 2);
            var enemyB = Unit("enemy-b", CombatTeam.Enemy, speed: 1);
            Place("caster", 2, 2);
            Place("enemy-a", 1, 2);
            Place("enemy-b", 3, 2);
            var engine = CreateEngine(caster, enemyA, enemyB);
            Assert.That(statusBoard.ApplyMark("enemy-b", mark, engine.RoundNumber).IsSuccess, Is.True);

            return Choose(engine, "caster");
        }

        private AiPlan RunDispositionScenario(DispositionType disposition)
        {
            InitScorer(8, 2);
            var strike = CreateAbility("strike", AbilityTag.Apply, power: 2);
            var caster = Unit("caster", CombatTeam.Player, speed: 10, disposition: disposition, abilities: new[] { strike });
            var ally = Unit("ally", CombatTeam.Player, speed: 5, currentHp: 5);
            var enemyA = Unit("enemy-a", CombatTeam.Enemy, speed: 2);
            var enemyB = Unit("enemy-b", CombatTeam.Enemy, speed: 1);
            Place("ally", 0, 0);
            Place("caster", 1, 0);
            Place("enemy-a", 5, 0);
            Place("enemy-b", 4, 1);
            var engine = CreateEngine(caster, ally, enemyA, enemyB);

            return Choose(engine, "caster");
        }

        private AiPlan Choose(TurnEngine engine, string unitId)
        {
            var plan = scorer.ChooseAction(engine, unitId);
            Assert.That(plan.IsSuccess, Is.True, plan.Error);
            return plan.Value;
        }

        private void InitScorer(int width, int height)
        {
            map = new GridMap(width, height);
            statusBoard = new StatusBoard();
            var created = ActionScorer.Create(map, statusBoard);
            Assert.That(created.IsSuccess, Is.True, created.Error);
            scorer = created.Value;
        }

        private TurnEngine CreateEngine(params CombatantRef[] combatants)
        {
            var result = TurnEngine.Create(combatants);
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
