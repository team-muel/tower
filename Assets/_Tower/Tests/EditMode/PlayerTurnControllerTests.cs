using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using Tower.Core;
using Tower.Combat;
using UnityEngine;

namespace Tower.Tests.EditMode
{
    public sealed class PlayerTurnControllerTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private TurnEngine engine;
        private TileHighlighter highlighter;
        private GridView gridView;
        private UnitToken playerToken;
        private UnitToken allyToken;
        private OrderBoard orderBoard;
        private PlayerTurnController controller;

        [SetUp]
        public void SetUpEnvironment()
        {
            gridView = new GameObject("Test Grid").AddComponent<GridView>();
            gridView.Build(new GridMap(6, 6));

            highlighter = new GameObject("Highlighter").AddComponent<TileHighlighter>();
            highlighter.Initialize(gridView);

            playerToken = CreateToken(new GridPos(1, 1), "player", new Color(1f, 1f, 1f, 1f));
            allyToken = CreateToken(new GridPos(1, 2), "ally-x", new Color(1f, 1f, 1f, 1f));
            orderBoard = new OrderBoard();
        }

        [TearDown]
        public void TearDown()
        {
            if (highlighter != null)
            {
                Object.DestroyImmediate(highlighter.gameObject);
            }

            if (gridView != null)
            {
                Object.DestroyImmediate(gridView.gameObject);
            }

            if (playerToken != null)
            {
                Object.DestroyImmediate(playerToken.gameObject);
            }

            if (allyToken != null)
            {
                Object.DestroyImmediate(allyToken.gameObject);
            }

            foreach (var createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void NotPlayerTurn_ReportsIdleMode()
        {
            CreateEngine(
                CreateState("player", 10),
                CreateState("ally-x", 8),
                CreateState("enemy-x", 5));

            controller = BuildController("player");
            engine.Submit(new SkipTurnCommand("player"));
            engine.Submit(new SkipTurnCommand("ally-x"));

            Assert.That(controller.CurrentMode(), Is.EqualTo(PlayerTurnController.Mode.Idle));
        }

        [Test]
        public void MoveMode_ReturnsWalkableCells()
        {
            CreateEngine(
                CreateState("player", 10),
                CreateState("ally-x", 8),
                CreateState("enemy-x", 5));

            controller = BuildController("player");
            controller.EnterMoveMode();

            Assert.That(controller.CurrentMode(), Is.EqualTo(PlayerTurnController.Mode.Move));
            Assert.That(controller.GetWalkableCells(), Does.Contain(new GridPos(0, 0)));
            Assert.That(controller.GetWalkableCells().Count(), Is.EqualTo(36));
        }

        [Test]
        public void AbilityMode_RequestsTargetSelection()
        {
            var ability = CreateAbility("strike", AbilityTag.Apply, power: 5);
            CreateEngine(
                CreateState("player", 10, new[] { ability }),
                CreateState("ally-x", 8),
                CreateState("enemy-x", 5));

            controller = BuildController("player", new[] { ability });
            controller.EnterAbilityMode(0);

            Assert.That(controller.CurrentMode(), Is.EqualTo(PlayerTurnController.Mode.TargetSelect));
            Assert.That(controller.GetAbilityCandidateCells(0).Any(), Is.True);
        }

        [Test]
        public void OrderMode_RecordsFocusAndConsumesSlot()
        {
            CreateEngine(
                CreateState("player", 10),
                CreateState("ally-x", 8),
                CreateState("enemy-x", 5));

            controller = BuildController("player");
            controller.EnterOrderMode("enemy-x");

            Assert.That(orderBoard.HasFocus("enemy-x"), Is.True);
            Assert.That(orderBoard.RemainingOrders(), Is.EqualTo(1));
        }

        private void CreateEngine(params CharacterState[] states)
        {
            var combatants = new List<CombatantRef>();
            for (int index = 0; index < states.Length; index++)
            {
                string id = index == 0 ? "player" : index == 1 ? "ally-x" : "enemy-x";
                combatants.Add(CombatantRef.Create(id, index == 2 ? CombatTeam.Enemy : CombatTeam.Player, states[index]).Value);
            }

            engine = TurnEngine.Create(combatants.ToArray()).Value;
        }

        private PlayerTurnController BuildController(string playerId, IReadOnlyList<AbilityDef> abilities = null)
        {
            return new PlayerTurnController(
                engine,
                gridView,
                highlighter,
                playerToken,
                new[] { allyToken },
                orderBoard,
                playerId,
                abilities ?? new AbilityDef[0],
                new BattleHudPresenter());
        }

        private UnitToken CreateToken(GridPos pos, string id, Color color)
        {
            var token = new GameObject("Token" + id).AddComponent<UnitToken>();
            createdObjects.Add(token);
            token.Initialize(gridView, pos, id);
            return token;
        }

        private CharacterState CreateState(string id, int speed, AbilityDef[] abilities = null)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDef>();
            createdObjects.Add(definition);
            SetField(definition, "id", id);
            SetField(definition, "displayName", id);
            SetField(definition, "maxHp", 10);
            SetField(definition, "speed", speed);
            SetField(definition, "attack", 0);
            SetField(definition, "defense", 0);

            var list = new List<AbilityDef>(abilities ?? new AbilityDef[0]);
            int pad = 0;
            while (list.Count < 2)
            {
                list.Add(CreateAbility(id + "-fallback" + pad++, AbilityTag.None, power: 1));
            }
            var result = CharacterState.Create(definition, 10, assignedAbilities: list.ToArray());
            Assert.That(result.IsSuccess, Is.True, result.Error);
            return result.Value;
        }

        private AbilityDef CreateAbility(string id, AbilityTag tag, int power = 0)
        {
            var ability = ScriptableObject.CreateInstance<AbilityDef>();
            createdObjects.Add(ability);
            SetField(ability, "id", id);
            SetField(ability, "tag", tag);
            SetField(ability, "basePower", power);
            return ability;
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

    }
}
