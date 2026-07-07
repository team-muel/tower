using System;
using System.Collections.Generic;
using System.Linq;
using Tower.Core;
using UnityEngine;

namespace Tower.Combat
{
    public sealed class PlayerTurnController
    {
        public enum Mode
        {
            Idle,
            Move,
            Ability,
            TargetSelect
        }

        private readonly TurnEngine engine;
        private readonly GridView gridView;
        private readonly TileHighlighter highlighter;
        private readonly UnitToken playerToken;
        private readonly UnitToken[] orderedTokens;
        private readonly IReadOnlyList<AbilityDef> abilities;
        private readonly ICombatModePresenter presenter;
        private readonly OrderBoard orderBoard;
        private readonly string playerId;

        private Mode currentMode = Mode.Idle;
        private int? selectedAbilitySlot;
        private bool awaitsTarget;

        public PlayerTurnController(
            TurnEngine engine,
            GridView gridView,
            TileHighlighter highlighter,
            UnitToken playerToken,
            UnitToken[] orderedTokens,
            OrderBoard orderBoard,
            string playerId,
            IReadOnlyList<AbilityDef> abilities,
            ICombatModePresenter presenter = null)
        {
            this.engine = engine;
            this.gridView = gridView;
            this.highlighter = highlighter;
            this.playerToken = playerToken;
            this.orderedTokens = orderedTokens;
            this.orderBoard = orderBoard;
            this.playerId = playerId;
            this.abilities = abilities ?? Array.Empty<AbilityDef>();
            this.presenter = presenter;
        }

        public Mode CurrentMode()
        {
            if (!IsPlayerTurn())
            {
                return Mode.Idle;
            }

            return currentMode;
        }

        public bool IsPlayerTurn()
        {
            return engine.CurrentTurn != null && string.Equals(engine.CurrentTurn.UnitId, playerId, StringComparison.Ordinal);
        }

        public void EnterMoveMode()
        {
            if (!IsPlayerTurn() || !engine.CurrentTurn.HasAction)
            {
                return;
            }

            currentMode = Mode.Move;
            selectedAbilitySlot = null;
            awaitsTarget = false;
            highlighter.SetMoveHints(GetWalkableCells());
            presenter?.SetMode("이동");
        }

        public void EnterAbilityMode(int slotIndex)
        {
            if (!IsPlayerTurn() || !engine.CurrentTurn.HasAction)
            {
                return;
            }

            if (slotIndex < 0 || slotIndex >= abilities.Count)
            {
                return;
            }

            selectedAbilitySlot = slotIndex;
            currentMode = Mode.TargetSelect;
            awaitsTarget = true;
            highlighter.SetTargetHints(GetAbilityCandidateCells(slotIndex));
            presenter?.SetMode($"능력: {abilities[slotIndex].DisplayName}");
        }

        public void EnterOrderMode(string targetId)
        {
            if (!IsPlayerTurn() || !engine.CurrentTurn.HasAction)
            {
                return;
            }

            orderBoard.IssueFocus(targetId, engine.RoundNumber + 1);
            presenter?.SetMode($"오더: {targetId} ← {playerToken.OccupantId}");
        }

        public void Skip()
        {
            if (!IsPlayerTurn())
            {
                return;
            }

            ClearHighlights();
            currentMode = Mode.Idle;
            selectedAbilitySlot = null;
            awaitsTarget = false;
            presenter?.SetMode("");
            engine.Submit(new SkipTurnCommand(playerId));
        }

        public void OnCellClicked(GridPos cell)
        {
            if (!IsPlayerTurn())
            {
                return;
            }

            if (awaitsTarget && selectedAbilitySlot.HasValue)
            {
                SubmitAbility(cell);
                return;
            }

            if (currentMode == Mode.Move)
            {
                SubmitMove(cell);
                return;
            }

            // Default idle behavior: interpret as order target when clicked after HUD setup.
            if (currentMode == Mode.Idle && engine.CurrentTurn.HasAction && orderedTokens != null)
            {
                foreach (var token in orderedTokens)
                {
                    if (token != null && token.Position.Equals(cell))
                    {
                        EnterOrderMode(token.OccupantId);
                        return;
                    }
                }
            }
        }

        public IEnumerable<GridPos> GetWalkableCells()
        {
            if (gridView == null || gridView.Map == null || !IsPlayerTurn() || engine.CurrentTurn.RemainingMovement <= 0)
            {
                yield break;
            }

            for (int y = 0; y < gridView.Map.Height; y++)
            {
                for (int x = 0; x < gridView.Map.Width; x++)
                {
                    yield return new GridPos(x, y);
                }
            }
        }

        public IEnumerable<GridPos> GetAbilityCandidateCells(int slotIndex)
        {
            if (gridView == null || gridView.Map == null || !IsPlayerTurn() || !engine.CurrentTurn.HasAction)
            {
                yield break;
            }

            if (slotIndex < 0 || slotIndex >= abilities.Count)
            {
                yield break;
            }

            for (int y = 0; y < gridView.Map.Height; y++)
            {
                for (int x = 0; x < gridView.Map.Width; x++)
                {
                    yield return new GridPos(x, y);
                }
            }
        }

        private void SubmitMove(GridPos cell)
        {
            if (!IsPlayerTurn() || engine.CurrentTurn.RemainingMovement <= 0)
            {
                return;
            }

            var path = Pathfinder.FindPath(gridView.Map, playerToken.Position, cell, playerToken.OccupantId);
            if (path.Count == 0)
            {
                highlighter.SetMoveHints(GetWalkableCells());
                return;
            }

            int distance = path.Count - 1;
            var result = engine.Submit(new MoveCommand(playerId, distance));
            if (result.IsSuccess)
            {
                playerToken.MoveAlong(path);
                highlighter.SetSelected(cell);
                highlighter.SetMoveHints(Array.Empty<GridPos>());
                currentMode = Mode.Idle;
                selectedAbilitySlot = null;
                awaitsTarget = false;
                presenter?.SetMode("");
            }
            else
            {
                highlighter.SetMoveHints(GetWalkableCells());
            }
        }

        private void SubmitAbility(GridPos cell)
        {
            if (!IsPlayerTurn() || !engine.CurrentTurn.HasAction)
            {
                return;
            }

            if (!selectedAbilitySlot.HasValue || selectedAbilitySlot.Value >= abilities.Count)
            {
                return;
            }

            var ability = abilities[selectedAbilitySlot.Value];
            var targetUnitId = ability.TargetType == AbilityTargetType.Cell
                ? null
                : gridView.Map.GetOccupant(cell);
            var targetCell = ability.TargetType == AbilityTargetType.Cell
                ? (GridPos?)cell
                : null;
            var command = new UseAbilityCommand(playerId, ability.Id, targetUnitId, targetCell);
            var result = engine.Submit(command);
            if (result.IsSuccess)
            {
                highlighter.SetTargetHints(Array.Empty<GridPos>());
                currentMode = Mode.Idle;
                selectedAbilitySlot = null;
                awaitsTarget = false;
                presenter?.SetMode("");
            }
        }

        private void ClearHighlights()
        {
            highlighter.SetMoveHints(Array.Empty<GridPos>());
            highlighter.SetTargetHints(Array.Empty<GridPos>());
            highlighter.SetSelected(null);
            highlighter.ClearAll();
        }
    }
}
