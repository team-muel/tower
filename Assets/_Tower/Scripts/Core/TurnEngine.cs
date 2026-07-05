using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    public sealed class TurnEngine
    {
        public const int DefaultMovementPerTurn = 4;

        private readonly Dictionary<string, CombatantRef> combatants;
        private readonly HashSet<string> defeatedUnitIds = new HashSet<string>();
        private readonly IActionPresenter presenter;
        private List<string> roundOrder = new List<string>();
        private int activeOrderIndex;

        private TurnEngine(Dictionary<string, CombatantRef> combatants, IActionPresenter presenter)
        {
            this.combatants = combatants;
            this.presenter = presenter ?? new NullPresenter();
            RoundNumber = 1;
            BeginRound();
            UpdateCombatEnd();
        }

        public int RoundNumber { get; private set; }
        public TurnState CurrentTurn { get; private set; }
        public bool IsCombatEnded { get; private set; }
        public CombatTeam? WinningTeam { get; private set; }
        public IReadOnlyList<string> CurrentRoundOrder => roundOrder.AsReadOnly();

        public static Result<TurnEngine> Create(IEnumerable<CombatantRef> combatants, IActionPresenter presenter = null)
        {
            if (combatants == null)
            {
                return Result<TurnEngine>.Failure("Combatants are required.");
            }

            var byId = new Dictionary<string, CombatantRef>(StringComparer.Ordinal);
            foreach (var combatant in combatants)
            {
                if (combatant == null)
                {
                    return Result<TurnEngine>.Failure("Combatants cannot contain null entries.");
                }

                if (byId.ContainsKey(combatant.UnitId))
                {
                    return Result<TurnEngine>.Failure("Combatant unit ids must be unique.");
                }

                byId.Add(combatant.UnitId, combatant);
            }

            if (byId.Count == 0)
            {
                return Result<TurnEngine>.Failure("At least one combatant is required.");
            }

            if (byId.Values.Where(combatant => combatant.IsAlive).Select(combatant => combatant.Team).Distinct().Count() < 2)
            {
                return Result<TurnEngine>.Failure("Combat requires at least two living teams.");
            }

            return Result<TurnEngine>.Success(new TurnEngine(byId, presenter));
        }

        public CombatantRef GetCombatant(string unitId)
        {
            return combatants.TryGetValue(unitId, out var combatant) ? combatant : null;
        }

        public bool IsAlive(string unitId)
        {
            return combatants.ContainsKey(unitId) && IsAlive(combatants[unitId]);
        }

        public Result UpdateCombatantState(string unitId, CharacterState state)
        {
            if (!combatants.ContainsKey(unitId))
            {
                return Result.Failure("Unknown combatant.");
            }

            if (state == null)
            {
                return Result.Failure("Character state is required.");
            }

            combatants[unitId] = combatants[unitId].WithState(state);
            if (state.CurrentHp <= 0)
            {
                defeatedUnitIds.Add(unitId);
                RemoveDefeatedFromCurrentTurn(unitId);
            }

            UpdateCombatEnd();
            return Result.Success();
        }

        public Result DefeatCombatant(string unitId)
        {
            if (!combatants.ContainsKey(unitId))
            {
                return Result.Failure("Unknown combatant.");
            }

            defeatedUnitIds.Add(unitId);
            RemoveDefeatedFromCurrentTurn(unitId);
            UpdateCombatEnd();
            return Result.Success();
        }

        public Result Submit(TurnCommand command)
        {
            if (command == null)
            {
                return Result.Failure("Command is required.");
            }

            if (IsCombatEnded)
            {
                return Result.Failure("Combat has ended.");
            }

            if (CurrentTurn == null)
            {
                return Result.Failure("No active turn.");
            }

            if (!StringComparer.Ordinal.Equals(command.UnitId, CurrentTurn.UnitId))
            {
                return Result.Failure("Command unit is not active.");
            }

            if (!IsAlive(command.UnitId))
            {
                return Result.Failure("Command unit is defeated.");
            }

            if (command is MoveCommand move)
            {
                return SubmitMove(move);
            }

            if (command is UseAbilityCommand ability)
            {
                return SubmitAbility(ability);
            }

            if (command is SkipTurnCommand)
            {
                Present(new TurnPresentationEvent(TurnPresentationEventType.Skip, command.UnitId));
                AdvanceTurn();
                return Result.Success();
            }

            return Result.Failure("Unsupported command type.");
        }

        private Result SubmitMove(MoveCommand command)
        {
            if (command.Distance < 0)
            {
                return Result.Failure("Move distance cannot be negative.");
            }

            if (command.Distance > CurrentTurn.RemainingMovement)
            {
                return Result.Failure("Move distance exceeds remaining movement.");
            }

            Present(new TurnPresentationEvent(TurnPresentationEventType.Move, command.UnitId, command.Distance));
            CurrentTurn = new TurnState(
                CurrentTurn.UnitId,
                CurrentTurn.RemainingMovement - command.Distance,
                CurrentTurn.HasAction);
            return Result.Success();
        }

        private Result SubmitAbility(UseAbilityCommand command)
        {
            if (!CurrentTurn.HasAction)
            {
                return Result.Failure("Action has already been used.");
            }

            if (string.IsNullOrWhiteSpace(command.AbilityId))
            {
                return Result.Failure("Ability id is required.");
            }

            Present(new TurnPresentationEvent(
                TurnPresentationEventType.Ability,
                command.UnitId,
                abilityId: command.AbilityId,
                targetUnitId: command.TargetUnitId));
            CurrentTurn = new TurnState(CurrentTurn.UnitId, CurrentTurn.RemainingMovement, false);
            AdvanceTurn();
            return Result.Success();
        }

        private void BeginRound()
        {
            roundOrder = combatants.Values
                .Where(IsAlive)
                .OrderByDescending(combatant => combatant.State.EffectiveSpeed)
                .ThenBy(combatant => combatant.UnitId, StringComparer.Ordinal)
                .Select(combatant => combatant.UnitId)
                .ToList();

            activeOrderIndex = 0;
            CurrentTurn = roundOrder.Count > 0
                ? new TurnState(roundOrder[activeOrderIndex], DefaultMovementPerTurn, true)
                : null;
        }

        private void AdvanceTurn()
        {
            if (UpdateCombatEnd())
            {
                return;
            }

            for (var index = activeOrderIndex + 1; index < roundOrder.Count; index++)
            {
                if (IsAlive(roundOrder[index]))
                {
                    activeOrderIndex = index;
                    CurrentTurn = new TurnState(roundOrder[index], DefaultMovementPerTurn, true);
                    return;
                }
            }

            RoundNumber++;
            BeginRound();
        }

        private void RemoveDefeatedFromCurrentTurn(string unitId)
        {
            var isActiveUnit = CurrentTurn != null && StringComparer.Ordinal.Equals(CurrentTurn.UnitId, unitId);
            RemoveFromRoundOrder(unitId, isActiveUnit);

            if (isActiveUnit)
            {
                AdvanceTurn();
            }
        }

        private void RemoveFromRoundOrder(string unitId, bool isActiveUnit)
        {
            var index = roundOrder.FindIndex(candidate => StringComparer.Ordinal.Equals(candidate, unitId));
            if (index < 0)
            {
                return;
            }

            roundOrder.RemoveAt(index);
            if (isActiveUnit)
            {
                activeOrderIndex = index - 1;
            }
            else if (index < activeOrderIndex)
            {
                activeOrderIndex--;
            }
        }

        private bool UpdateCombatEnd()
        {
            var livingTeams = combatants.Values
                .Where(IsAlive)
                .Select(combatant => combatant.Team)
                .Distinct()
                .ToArray();

            if (livingTeams.Length > 1)
            {
                return false;
            }

            IsCombatEnded = true;
            WinningTeam = livingTeams.Length == 1 ? livingTeams[0] : (CombatTeam?)null;
            CurrentTurn = null;
            return true;
        }

        private bool IsAlive(CombatantRef combatant)
        {
            return combatant.IsAlive && !defeatedUnitIds.Contains(combatant.UnitId);
        }

        private void Present(TurnPresentationEvent presentationEvent)
        {
            presenter.Present(presentationEvent, null);
        }
    }
}
