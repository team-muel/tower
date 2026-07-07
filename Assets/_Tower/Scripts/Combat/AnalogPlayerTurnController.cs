using System;
using System.Collections.Generic;
using Tower.Core;

namespace Tower.Combat
{
    // T20: manual turn controller for the regressor on the analog
    // battlefield. Move mode shows the movement-radius ring; a click inside
    // the ring clamps the move along the straight line and submits it.
    // Ability mode targets units directly (click a token or a point near
    // one). The grid-mode PlayerTurnController remains untouched as the
    // rollback path.
    public sealed class AnalogPlayerTurnController
    {
        public enum Mode
        {
            Idle,
            Move,
            TargetSelect
        }

        private const float TargetPickRadius = 0.75f;

        private readonly TurnEngine engine;
        private readonly AnalogBattlefield battlefield;
        private readonly AnalogBattlefieldView view;
        private readonly OrderBoard orderBoard;
        private readonly string playerId;
        private readonly IReadOnlyList<AbilityDef> abilities;
        private readonly ICombatModePresenter presenter;

        private Mode currentMode = Mode.Idle;
        private int? selectedAbilitySlot;

        public AnalogPlayerTurnController(
            TurnEngine engine,
            AnalogBattlefield battlefield,
            AnalogBattlefieldView view,
            OrderBoard orderBoard,
            string playerId,
            IReadOnlyList<AbilityDef> abilities,
            ICombatModePresenter presenter = null)
        {
            this.engine = engine;
            this.battlefield = battlefield;
            this.view = view;
            this.orderBoard = orderBoard;
            this.playerId = playerId;
            this.abilities = abilities ?? Array.Empty<AbilityDef>();
            this.presenter = presenter;
        }

        public Mode CurrentMode()
        {
            return IsPlayerTurn() ? currentMode : Mode.Idle;
        }

        public bool IsPlayerTurn()
        {
            return engine.CurrentTurn != null
                && StringComparer.Ordinal.Equals(engine.CurrentTurn.UnitId, playerId);
        }

        public void EnterMoveMode()
        {
            if (!IsPlayerTurn() || engine.CurrentTurn.RemainingMovement <= 0f)
            {
                return;
            }

            var origin = battlefield.FindOccupant(playerId);
            if (!origin.HasValue)
            {
                return;
            }

            currentMode = Mode.Move;
            selectedAbilitySlot = null;
            view.ShowRing(origin.Value, engine.CurrentTurn.RemainingMovement);
            view.HideDestinationMarker();
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

            var origin = battlefield.FindOccupant(playerId);
            if (!origin.HasValue)
            {
                return;
            }

            selectedAbilitySlot = slotIndex;
            currentMode = Mode.TargetSelect;
            view.ShowRing(origin.Value, abilities[slotIndex].Range, targeting: true);
            presenter?.SetMode($"능력: {abilities[slotIndex].DisplayName}");
        }

        public void EnterOrderMode(string targetId)
        {
            if (!IsPlayerTurn() || !engine.CurrentTurn.HasAction)
            {
                return;
            }

            orderBoard.IssueFocus(targetId, engine.RoundNumber + 1);
            presenter?.SetMode($"오더: {targetId} ← {playerId}");
        }

        public void Skip()
        {
            if (!IsPlayerTurn())
            {
                return;
            }

            ResetMode();
            engine.Submit(new SkipTurnCommand(playerId));
        }

        // A click on the battlefield floor at a continuous position.
        public void OnPointClicked(BattlePos point)
        {
            if (!IsPlayerTurn())
            {
                return;
            }

            if (currentMode == Mode.TargetSelect && selectedAbilitySlot.HasValue)
            {
                SubmitAbilityAt(point, FindTargetUnit(point));
                return;
            }

            if (currentMode == Mode.Move)
            {
                SubmitMove(point);
            }
        }

        // A click that hit a unit token directly.
        public void OnUnitClicked(string unitId)
        {
            if (!IsPlayerTurn() || string.IsNullOrEmpty(unitId))
            {
                return;
            }

            if (currentMode == Mode.TargetSelect && selectedAbilitySlot.HasValue)
            {
                var position = battlefield.FindOccupant(unitId);
                if (position.HasValue)
                {
                    SubmitAbilityAt(position.Value, unitId);
                }

                return;
            }

            if (currentMode == Mode.Idle && engine.CurrentTurn.HasAction
                && !StringComparer.Ordinal.Equals(unitId, playerId))
            {
                EnterOrderMode(unitId);
            }
        }

        private void SubmitMove(BattlePos point)
        {
            if (engine.CurrentTurn.RemainingMovement <= 0f)
            {
                return;
            }

            var origin = battlefield.FindOccupant(playerId);
            if (!origin.HasValue)
            {
                return;
            }

            var destination = battlefield.ClampMove(playerId, origin.Value, point, engine.CurrentTurn.RemainingMovement);
            var cost = battlefield.Distance(origin.Value, destination);
            if (cost <= 0.01f)
            {
                return;
            }

            if (!battlefield.TryMoveOccupant(playerId, destination))
            {
                return;
            }

            var result = engine.Submit(new MoveCommand(playerId, cost, destination));
            if (result.IsFailure)
            {
                // Roll the battlefield back so state and engine stay in sync.
                battlefield.TryMoveOccupant(playerId, origin.Value);
                return;
            }

            view.ShowDestinationMarker(destination);
            ResetMode();
        }

        private void SubmitAbilityAt(BattlePos point, string targetUnitId)
        {
            if (!engine.CurrentTurn.HasAction || !selectedAbilitySlot.HasValue)
            {
                return;
            }

            var ability = abilities[selectedAbilitySlot.Value];
            var command = ability.TargetType == AbilityTargetType.Cell
                ? new UseAbilityCommand(playerId, ability.Id, targetPoint: point)
                : new UseAbilityCommand(playerId, ability.Id, targetUnitId);
            var result = engine.Submit(command);
            if (result.IsSuccess)
            {
                ResetMode();
            }
        }

        // Nearest living occupant within the pick radius of the click point.
        private string FindTargetUnit(BattlePos point)
        {
            string best = null;
            var bestDistance = float.MaxValue;
            foreach (var unitId in engine.CurrentRoundOrder)
            {
                if (!engine.IsAlive(unitId))
                {
                    continue;
                }

                var position = battlefield.FindOccupant(unitId);
                if (!position.HasValue)
                {
                    continue;
                }

                var distance = battlefield.Distance(position.Value, point);
                if (distance <= TargetPickRadius && distance < bestDistance)
                {
                    best = unitId;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private void ResetMode()
        {
            currentMode = Mode.Idle;
            selectedAbilitySlot = null;
            view.HideRing();
            presenter?.SetMode("");
        }
    }
}
