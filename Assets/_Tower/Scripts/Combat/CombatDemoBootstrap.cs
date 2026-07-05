using System;
using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    public sealed class CombatDemoBootstrap : MonoBehaviour
    {
        [SerializeField] private int _width = 12;
        [SerializeField] private int _height = 12;

        private GridView _gridView;
        private TileHighlighter _highlighter;
        private UnitToken _playerToken;
        private Camera _camera;
        private TurnEngine _engine;
        private StatusBoard _statusBoard;
        private PlayerTurnController _playerController;
        private IActionPresenter _presenter;
        private OrderBoard _orderBoard;
        private bool _combatEnded;

        private void Start()
        {
            _statusBoard = new StatusBoard();
            GridMap map = new GridMap(_width, _height);
            AddDemoObstacles(map);

            var engineResult = TurnEngine.Create(CreateCombatants());
            UnityEngine.Debug.Assert(engineResult.IsSuccess, engineResult.Error);
            _engine = engineResult.Value;

            GameObject gridObject = new GameObject("Runtime Grid");
            _gridView = gridObject.AddComponent<GridView>();
            _gridView.Build(map);

            _highlighter = gridObject.AddComponent<TileHighlighter>();
            _highlighter.Initialize(_gridView);

            _playerToken = UnitToken.Spawn(_gridView, new GridPos(1, 1), "regressor", new Color(0.2f, 0.55f, 1f));
            UnitToken.Spawn(_gridView, new GridPos(2, 1), "ally-a", new Color(0.25f, 0.9f, 0.55f, 1f));
            UnitToken.Spawn(_gridView, new GridPos(1, 2), "ally-b", new Color(0.9f, 0.55f, 0.25f, 1f));

            UnitToken.Spawn(_gridView, new GridPos(5, 5), "enemy-a", new Color(0.6f, 0.25f, 0.25f, 1f));
            UnitToken.Spawn(_gridView, new GridPos(6, 6), "enemy-b", new Color(0.55f, 0.2f, 0.45f, 1f));

            _orderBoard = new OrderBoard();
            _presenter = new BattleHudPresenter(message => UnityEngine.Debug.Log("[HUD] " + message));
            _playerController = new PlayerTurnController(
                _engine,
                _gridView,
                _highlighter,
                _playerToken,
                new UnitToken[0],
                _orderBoard,
                _playerToken.OccupantId,
                Array.Empty<AbilityDef>(),
                _presenter);

            GameObject cameraRigObject = new GameObject("Iso Camera Rig");
            IsoCameraRig cameraRig = cameraRigObject.AddComponent<IsoCameraRig>();
            cameraRig.Focus(_gridView, new GridPos(_width / 2, _height / 2));
            _camera = cameraRig.Camera;
        }

        private void Update()
        {
            if (_gridView == null || _camera == null || _engine == null)
            {
                return;
            }

            if (_combatEnded)
            {
                return;
            }

            if (!string.Equals(_engine.CurrentTurn.UnitId, _playerToken.OccupantId, StringComparison.Ordinal))
            {
                RunNonPlayerTurn();
                return;
            }

            GridPos hover;
            if (TryGetMouseCell(out hover) && _gridView.Map.InBounds(hover))
            {
                _highlighter.SetHover(hover);

                if (Input.GetMouseButtonDown(0))
                {
                    _playerController.OnCellClicked(hover);
                }
            }
            else
            {
                _highlighter.SetHover(null);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                _playerController.Skip();
            }
        }

        private void RunNonPlayerTurn()
        {
            var active = _engine.CurrentTurn;
            if (active == null)
            {
                return;
            }

            var command = AiTurnDriver.ChooseCommand(_engine, active.UnitId);
            var result = _engine.Submit(command);
            if (result.IsFailure)
            {
                UnityEngine.Debug.LogError("AI turn failed: " + result.Error);
            }

            UpdateCombatEnd();
        }

        private void UpdateCombatEnd()
        {
            if (_engine == null || !_engine.IsCombatEnded)
            {
                return;
            }

            _combatEnded = true;
            _highlighter.ClearAll();
            UnityEngine.Debug.Log("Combat ended. Winner: " + (_engine.WinningTeam.HasValue ? _engine.WinningTeam.Value.ToString() : "draw"));
        }

        public void IssueOrderToCompanions(string targetUnitId)
        {
            if (_orderBoard == null || _playerController == null || !_playerController.IsPlayerTurn())
            {
                return;
            }

            _orderBoard.IssueFocus(targetUnitId, _engine.RoundNumber + 1);
            _playerController.EnterOrderMode(targetUnitId);
            UnityEngine.Debug.Log("Order issued: focus " + targetUnitId);
        }

        public void SkipHighlighting()
        {
            if (_playerController != null && _playerController.IsPlayerTurn())
            {
                _playerController.Skip();
            }
        }

        private IEnumerable<CombatantRef> CreateCombatants()
        {
            yield return CreatePlayerState("player", "regressor", CombatTeam.Player, 5, 10);
            yield return CreatePlayerState("player", "ally-a", CombatTeam.Player, 4, 8);
            yield return CreatePlayerState("player", "ally-b", CombatTeam.Player, 3, 8);
            yield return CreateEnemyState("enemy-a", CombatTeam.Enemy, 4, 12);
            yield return CreateEnemyState("enemy-b", CombatTeam.Enemy, 2, 12);
        }

        private CombatantRef CreatePlayerState(string id, string suffix, CombatTeam team, int speed, int hp)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDef>();
            SetId(definition, id + "-" + suffix);
            SetField(definition, "maxHp", hp);
            SetField(definition, "speed", speed);
            SetField(definition, "attack", 3);
            SetField(definition, "defense", 1);

            var state = CharacterState.Create(definition, hp, slotCount: 2);
            UnityEngine.Debug.Assert(state.IsSuccess, state.Error);
            var refResult = CombatantRef.Create(id + "-" + suffix, team, state.Value);
            UnityEngine.Debug.Assert(refResult.IsSuccess, refResult.Error);
            return refResult.Value;
        }

        private CombatantRef CreateEnemyState(string id, CombatTeam team, int speed, int hp)
        {
            var definition = ScriptableObject.CreateInstance<CharacterDef>();
            SetId(definition, id);
            SetField(definition, "maxHp", hp);
            SetField(definition, "speed", speed);
            SetField(definition, "attack", 2);
            SetField(definition, "defense", 0);

            var state = CharacterState.Create(definition, hp, slotCount: 2);
            UnityEngine.Debug.Assert(state.IsSuccess, state.Error);
            var refResult = CombatantRef.Create(id, team, state.Value);
            UnityEngine.Debug.Assert(refResult.IsSuccess, refResult.Error);
            return refResult.Value;
        }

        private bool TryGetMouseCell(out GridPos pos)
        {
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                pos = _gridView.WorldToCell(hit.point);
                return true;
            }

            pos = new GridPos();
            return false;
        }

        private static void AddDemoObstacles(GridMap map)
        {
            for (int y = 3; y < 9; y++)
            {
                if (y == 6)
                {
                    continue;
                }

                map.SetBlocked(new GridPos(5, y), true);
            }

            map.SetBlocked(new GridPos(8, 4), true);
            map.SetBlocked(new GridPos(8, 5), true);
            map.SetBlocked(new GridPos(8, 6), true);
        }

        private static void SetId(CharacterDef def, string value)
        {
            SetField(def, "id", value);
            SetField(def, "displayName", value);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            UnityEngine.Debug.Assert(field != null, name);
            field.SetValue(target, value);
        }
    }
}
