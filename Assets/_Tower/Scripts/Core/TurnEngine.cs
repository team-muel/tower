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
        private readonly IAbilityExecutor abilityExecutor;
        private readonly ICombatObserver combatObserver;
        private List<string> roundOrder = new List<string>();
        private int activeOrderIndex;
        private bool combatEndObserved;
        private readonly Random random;
        private readonly Dictionary<string, string> targetOverrides = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly bool disablePendingRules;

        private TurnEngine(
            Dictionary<string, CombatantRef> combatants,
            IActionPresenter presenter,
            IAbilityExecutor abilityExecutor,
            ICombatObserver combatObserver,
            Random random = null,
            bool disablePendingRules = false)
        {
            this.combatants = combatants;
            this.presenter = presenter ?? new NullPresenter();
            this.abilityExecutor = abilityExecutor;
            this.combatObserver = combatObserver;
            this.random = random ?? new Random();
            this.disablePendingRules = disablePendingRules;
            RoundNumber = 1;
            this.combatObserver?.OnCombatStarted(this);
            BeginRound();
            UpdateCombatEnd();
        }

        public int RoundNumber { get; private set; }
        public TurnState CurrentTurn { get; private set; }
        public bool IsCombatEnded { get; private set; }
        public CombatTeam? WinningTeam { get; private set; }
        public IReadOnlyList<string> CurrentRoundOrder => roundOrder.AsReadOnly();

        public static Result<TurnEngine> Create(
            IEnumerable<CombatantRef> combatants,
            IActionPresenter presenter = null,
            IAbilityExecutor abilityExecutor = null,
            ICombatObserver combatObserver = null,
            Random random = null,
            bool disablePendingRules = false)
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

            return Result<TurnEngine>.Success(new TurnEngine(byId, presenter, abilityExecutor, combatObserver, random, disablePendingRules));
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
                NotifyCommandCommitted(command);
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
                CurrentTurn.HasAction,
                CurrentTurn.PendingAbilityId);
            NotifyCommandCommitted(command);
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

            if (abilityExecutor != null)
            {
                // T4 seam: resolve the ability before the action is consumed so
                // failed uses (range, target, line of sight) do not cost the turn.
                var execution = abilityExecutor.Execute(this, command);
                if (execution.IsFailure)
                {
                    return execution;
                }
            }

            Present(new TurnPresentationEvent(
                TurnPresentationEventType.Ability,
                command.UnitId,
                abilityId: command.AbilityId,
                targetUnitId: command.TargetUnitId));
            NotifyCommandCommitted(command);

            // Cooldown registration
            var caster = GetCombatant(command.UnitId);
            var ability = FindAbility(caster, command.AbilityId);
            if (ability != null && ability.CooldownRounds > 0)
            {
                UpdateCombatantState(command.UnitId, caster.State.WithCooldown(ability.Id, ability.CooldownRounds));
            }

            // Clear target override on use
            targetOverrides.Remove(command.UnitId);
            if (CurrentTurn != null)
            {
                CurrentTurn = new TurnState(CurrentTurn.UnitId, CurrentTurn.RemainingMovement, false, CurrentTurn.PendingAbilityId);
                AdvanceTurn();
            }
            return Result.Success();
        }

        private void BeginRound()
        {
            // Advance cooldowns for all living units at the start of the round
            foreach (var kvp in combatants.ToList())
            {
                if (kvp.Value.IsAlive)
                {
                    UpdateCombatantState(kvp.Key, kvp.Value.State.AdvanceCooldowns());
                }
            }

            roundOrder = combatants.Values
                .Where(IsAlive)
                .OrderByDescending(combatant => combatant.State.EffectiveSpeed)
                .ThenBy(combatant => combatant.UnitId, StringComparer.Ordinal)
                .Select(combatant => combatant.UnitId)
                .ToList();

            activeOrderIndex = 0;
            CurrentTurn = roundOrder.Count > 0
                ? new TurnState(roundOrder[activeOrderIndex], DefaultMovementPerTurn, true, GetPendingAbilityId(GetCombatant(roundOrder[activeOrderIndex])))
                : null;
            combatObserver?.OnRoundStarted(this, RoundNumber, roundOrder.AsReadOnly());
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
                    CurrentTurn = new TurnState(roundOrder[index], DefaultMovementPerTurn, true, GetPendingAbilityId(GetCombatant(roundOrder[index])));
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
            if (!combatEndObserved)
            {
                combatEndObserved = true;
                combatObserver?.OnCombatEnded(this);
            }

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

        private void NotifyCommandCommitted(TurnCommand command)
        {
            combatObserver?.OnCommandCommitted(this, command);
        }

        private string GetPendingAbilityId(CombatantRef combatant)
        {
            if (disablePendingRules)
            {
                return null;
            }

            if (combatant == null || combatant.State == null || combatant.State.Definition == null)
            {
                return null;
            }

            if (combatant.State.Definition.IsReturner)
            {
                return null;
            }

            if (combatant.Team == CombatTeam.Player)
            {
                return PickRandomPendingAbility(combatant);
            }
            return null;
        }

        private string PickRandomPendingAbility(CombatantRef combatant)
        {
            if (combatant == null || combatant.State == null || combatant.State.Loadout == null)
            {
                return null;
            }

            var abilities = combatant.State.Loadout.Abilities
                .Where(ab => ab != null && ab.Tag != AbilityTag.None && (!combatant.State.Cooldowns.TryGetValue(ab.Id, out var cd) || cd <= 0))
                .ToList();

            if (abilities.Count == 0)
            {
                return null;
            }

            var index = random.Next(abilities.Count);
            return abilities[index].Id;
        }

        private AbilityDef FindAbility(CombatantRef caster, string abilityId)
        {
            if (caster == null || caster.State == null || caster.State.Loadout == null)
            {
                return null;
            }
            return caster.State.Loadout.Abilities.FirstOrDefault(ab => ab != null && StringComparer.Ordinal.Equals(ab.Id, abilityId));
        }

        public Result SetPendingAbility(string unitId, string abilityId)
        {
            if (IsCombatEnded)
            {
                return Result.Failure("Combat has ended.");
            }

            if (CurrentTurn == null)
            {
                return Result.Failure("No active turn.");
            }

            if (!StringComparer.Ordinal.Equals(unitId, CurrentTurn.UnitId))
            {
                return Result.Failure("Unit is not active.");
            }

            var combatant = GetCombatant(unitId);
            if (combatant == null)
            {
                return Result.Failure("Unknown unit.");
            }

            var ability = FindAbility(combatant, abilityId);
            if (ability == null)
            {
                return Result.Failure($"Unit does not have ability '{abilityId}'.");
            }

            if (combatant.State.Cooldowns.TryGetValue(abilityId, out var cd) && cd > 0)
            {
                return Result.Failure($"Ability '{abilityId}' is on cooldown.");
            }

            CurrentTurn = new TurnState(CurrentTurn.UnitId, CurrentTurn.RemainingMovement, CurrentTurn.HasAction, abilityId);
            return Result.Success();
        }

        public void SetTargetOverride(string unitId, string targetUnitId)
        {
            if (string.IsNullOrEmpty(targetUnitId))
            {
                targetOverrides.Remove(unitId);
            }
            else
            {
                targetOverrides[unitId] = targetUnitId;
            }
        }

        public string GetTargetOverride(string unitId)
        {
            return targetOverrides.TryGetValue(unitId, out var target) ? target : null;
        }
    }
}
