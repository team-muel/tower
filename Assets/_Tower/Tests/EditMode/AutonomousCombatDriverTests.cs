using System.Collections.Generic;
using NUnit.Framework;
using Tower.Core;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class AutonomousCombatDriverTests
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
        public void Step_BoundsMovementAndDefersProjectedRangeAbility()
        {
            var fixture = CreateFixture(playerHp: 20, enemyHp: 20, playerPosition: new BattlePos(2f, 4f), enemyPosition: new BattlePos(10f, 4f));

            var stepped = fixture.Driver.Step();

            Assert.That(stepped.IsSuccess, Is.True, stepped.Error);
            Assert.That(fixture.State.ElapsedSeconds, Is.EqualTo(1f));
            Assert.That(fixture.Battlefield.FindOccupant("player"), Is.EqualTo(new BattlePos(4f, 4f)));
            Assert.That(fixture.Battlefield.FindOccupant("enemy"), Is.EqualTo(new BattlePos(8f, 4f)));
            Assert.That(fixture.State.GetCombatant("player").State.CurrentHp, Is.EqualTo(20));
            Assert.That(fixture.State.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20));
            Assert.That(stepped.Value.Events, Has.All.Matches<AutonomousCombatEvent>(entry => !entry.AbilityResolved));
        }

        [Test]
        public void Step_RepeatsAutonomousPlansUntilCombatEnds()
        {
            var fixture = CreateFixture(playerHp: 15, enemyHp: 15, playerPosition: new BattlePos(2f, 4f), enemyPosition: new BattlePos(10f, 4f));

            var steps = 0;
            while (!fixture.State.IsCombatEnded && steps < 8)
            {
                var stepped = fixture.Driver.Step();
                Assert.That(stepped.IsSuccess, Is.True, stepped.Error);
                steps++;
            }

            Assert.That(fixture.State.IsCombatEnded, Is.True);
            Assert.That(fixture.State.WinningTeam, Is.EqualTo(CombatTeam.Player));
            Assert.That(fixture.Metrics.ActionCount, Is.EqualTo(5));
            Assert.That(fixture.Metrics.Units["player"].Kills, Is.EqualTo(1));
            Assert.That(fixture.State.ElapsedSeconds, Is.EqualTo(5f));
        }

        [Test]
        public void Step_IsDeterministicAcrossIdenticalFixtures()
        {
            var first = CreateFixture(playerHp: 15, enemyHp: 15, playerPosition: new BattlePos(2f, 4f), enemyPosition: new BattlePos(10f, 4f));
            var second = CreateFixture(playerHp: 15, enemyHp: 15, playerPosition: new BattlePos(2f, 4f), enemyPosition: new BattlePos(10f, 4f));

            for (var tick = 0; tick < 5; tick++)
            {
                var firstStep = first.Driver.Step();
                var secondStep = second.Driver.Step();

                Assert.That(firstStep.IsSuccess, Is.True, firstStep.Error);
                Assert.That(secondStep.IsSuccess, Is.True, secondStep.Error);
                Assert.That(secondStep.Value.ElapsedSeconds, Is.EqualTo(firstStep.Value.ElapsedSeconds));
                Assert.That(secondStep.Value.CombatEnded, Is.EqualTo(firstStep.Value.CombatEnded));
                Assert.That(secondStep.Value.Events.Count, Is.EqualTo(firstStep.Value.Events.Count));
                for (var eventIndex = 0; eventIndex < firstStep.Value.Events.Count; eventIndex++)
                {
                    var expected = firstStep.Value.Events[eventIndex];
                    var actual = secondStep.Value.Events[eventIndex];
                    Assert.That(actual.UnitId, Is.EqualTo(expected.UnitId));
                    Assert.That(actual.PlannedKind, Is.EqualTo(expected.PlannedKind));
                    Assert.That(actual.FromPosition, Is.EqualTo(expected.FromPosition));
                    Assert.That(actual.ToPosition, Is.EqualTo(expected.ToPosition));
                    Assert.That(actual.AbilityResolved, Is.EqualTo(expected.AbilityResolved));
                }
            }

            Assert.That(first.State.WinningTeam, Is.EqualTo(CombatTeam.Player));
            Assert.That(second.State.WinningTeam, Is.EqualTo(first.State.WinningTeam));
            Assert.That(second.Metrics.ActionCount, Is.EqualTo(first.Metrics.ActionCount));
        }

        [Test]
        public void Step_UsesEffectiveSpeedForIndependentRealTimeCadence()
        {
            var fixture = CreateFixture(
                playerHp: 20,
                enemyHp: 20,
                playerPosition: new BattlePos(2f, 4f),
                enemyPosition: new BattlePos(3f, 4f),
                playerSpeed: 20,
                enemySpeed: 5,
                tickSeconds: 0.25f);

            var playerEvents = 0;
            var enemyEvents = 0;
            for (var tick = 0; tick < 4; tick++)
            {
                var stepped = fixture.Driver.Step();
                Assert.That(stepped.IsSuccess, Is.True, stepped.Error);
                foreach (var entry in stepped.Value.Events)
                {
                    if (entry.UnitId == "player") playerEvents++;
                    if (entry.UnitId == "enemy") enemyEvents++;
                }
            }

            Assert.That(fixture.State.ElapsedSeconds, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(playerEvents, Is.EqualTo(2));
            Assert.That(enemyEvents, Is.EqualTo(0));
            Assert.That(fixture.Driver.SecondsUntilNextAction("player"), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(fixture.Driver.SecondsUntilNextAction("enemy"), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ActionIntervalSeconds_UsesSpeedTenAsOneSecondBaseline()
        {
            Assert.That(AutonomousCombatDriver.ActionIntervalSeconds(20), Is.EqualTo(0.5f));
            Assert.That(AutonomousCombatDriver.ActionIntervalSeconds(10), Is.EqualTo(1f));
            Assert.That(AutonomousCombatDriver.ActionIntervalSeconds(5), Is.EqualTo(2f));
            Assert.That(AutonomousCombatDriver.ActionIntervalSeconds(0), Is.EqualTo(10f));
        }

        [Test]
        public void Step_ExternallyPositionedUnitDoesNotAutoMoveButCanAttackInRange()
        {
            var fixture = CreateFixture(
                playerHp: 20,
                enemyHp: 20,
                playerPosition: new BattlePos(2f, 4f),
                enemyPosition: new BattlePos(10f, 4f),
                externallyPositionedUnitIds: new[] { "player" });

            Assert.That(fixture.Driver.Step().IsSuccess, Is.True);
            Assert.That(fixture.Battlefield.FindOccupant("player"), Is.EqualTo(new BattlePos(2f, 4f)));
            Assert.That(fixture.State.GetCombatant("enemy").State.CurrentHp, Is.EqualTo(20));

            Assert.That(fixture.Battlefield.TryMoveOccupant("player", new BattlePos(7f, 4f)), Is.True);
            Assert.That(fixture.Driver.Step().IsSuccess, Is.True);
            Assert.That(fixture.Battlefield.FindOccupant("player"), Is.EqualTo(new BattlePos(7f, 4f)));
            Assert.That(fixture.State.GetCombatant("enemy").State.CurrentHp, Is.LessThan(20));
        }

        [Test]
        public void Create_RejectsNonPositiveFixedTickConfiguration()
        {
            var fixture = CreateFixture(playerHp: 20, enemyHp: 20, playerPosition: new BattlePos(2f, 4f), enemyPosition: new BattlePos(10f, 4f));

            var created = AutonomousCombatDriver.Create(
                fixture.State,
                fixture.Battlefield,
                fixture.Scorer,
                fixture.Resolver,
                tickSeconds: 0f,
                movementUnitsPerSecond: 2f);

            Assert.That(created.IsFailure, Is.True);
            Assert.That(created.Error, Does.Contain("Tick seconds"));
        }

        private Fixture CreateFixture(
            int playerHp,
            int enemyHp,
            BattlePos playerPosition,
            BattlePos enemyPosition,
            int playerSpeed = 10,
            int enemySpeed = 10,
            float tickSeconds = 1f,
            IEnumerable<string> externallyPositionedUnitIds = null)
        {
            var battlefield = new AnalogBattlefield(14f, 8f);
            var statusBoard = new StatusBoard();
            var metrics = new CombatMetrics();
            var scorer = ActionScorer.Create(battlefield, statusBoard);
            Assert.That(scorer.IsSuccess, Is.True, scorer.Error);
            var resolver = AbilityResolver.Create(battlefield, statusBoard, metrics);
            Assert.That(resolver.IsSuccess, Is.True, resolver.Error);

            var playerStrike = Ability("player-strike", 5);
            var enemyStrike = Ability("enemy-strike", 5);
            var player = Unit("player", CombatTeam.Player, playerHp, playerStrike, playerSpeed);
            var enemy = Unit("enemy", CombatTeam.Enemy, enemyHp, enemyStrike, enemySpeed);
            Assert.That(battlefield.TryPlaceOccupant("player", playerPosition), Is.True);
            Assert.That(battlefield.TryPlaceOccupant("enemy", enemyPosition), Is.True);
            var state = CombatState.Create(new[] { player, enemy }, statusBoard, metrics);
            Assert.That(state.IsSuccess, Is.True, state.Error);
            var driver = AutonomousCombatDriver.Create(
                state.Value,
                battlefield,
                scorer.Value,
                resolver.Value,
                tickSeconds: tickSeconds,
                movementUnitsPerSecond: 2f,
                externallyPositionedUnitIds: externallyPositionedUnitIds);
            Assert.That(driver.IsSuccess, Is.True, driver.Error);

            return new Fixture(state.Value, battlefield, scorer.Value, resolver.Value, metrics, driver.Value);
        }

        private AbilityDef Ability(string id, int power)
        {
            var ability = AbilityDef.CreateRuntime(id, AbilityTag.Apply, power, 2, AbilityTargetType.Enemy);
            createdObjects.Add(ability);
            return ability;
        }

        private CombatantRef Unit(string unitId, CombatTeam team, int currentHp, AbilityDef ability, int speed)
        {
            var definition = CharacterDef.CreateRuntime(
                unitId,
                unitId,
                20,
                0,
                0,
                speed,
                DispositionType.Aggressive,
                new[] { ability });
            createdObjects.Add(definition);
            var state = CharacterState.Create(definition, currentHp, slotCount: 1, assignedAbilities: new[] { ability });
            Assert.That(state.IsSuccess, Is.True, state.Error);
            var combatant = CombatantRef.Create(unitId, team, state.Value);
            Assert.That(combatant.IsSuccess, Is.True, combatant.Error);
            return combatant.Value;
        }

        private sealed class Fixture
        {
            public Fixture(
                CombatState state,
                AnalogBattlefield battlefield,
                ActionScorer scorer,
                AbilityResolver resolver,
                CombatMetrics metrics,
                AutonomousCombatDriver driver)
            {
                State = state;
                Battlefield = battlefield;
                Scorer = scorer;
                Resolver = resolver;
                Metrics = metrics;
                Driver = driver;
            }

            public CombatState State { get; }
            public AnalogBattlefield Battlefield { get; }
            public ActionScorer Scorer { get; }
            public AbilityResolver Resolver { get; }
            public CombatMetrics Metrics { get; }
            public AutonomousCombatDriver Driver { get; }
        }
    }
}
