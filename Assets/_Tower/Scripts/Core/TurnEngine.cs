using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    public sealed class TurnEngine
    {
        public const int DefaultMovementPerTurn = 4;

        // T20: movement budgets are floats now; the epsilon absorbs tiny
        // euclidean rounding error from analog move costs.
        private const float MovementEpsilon = 0.001f;

        private readonly Dictionary<string, CombatantRef> combatants;
        private readonly HashSet<string> defeatedUnitIds = new HashSet<string>();
        private readonly IActionPresenter presenter;
        private readonly IAbilityExecutor abilityExecutor;
        private readonly ICombatObserver combatObserver;
        private readonly int seed;
        private List<string> roundOrder = new List<string>();
        private int activeOrderIndex;
        private bool combatEndObserved;

        private TurnEngine(
            Dictionary<string, CombatantRef> combatants,
            IActionPresenter presenter,
            IAbilityExecutor abilityExecutor,
            ICombatObserver combatObserver,
            int seed)
        {
            this.combatants = combatants;
            this.presenter = presenter ?? new NullPresenter();
            this.abilityExecutor = abilityExecutor;
            this.combatObserver = combatObserver;
            this.seed = seed;
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

        // T18: the ability pre-selected for the active unit's turn. Picked
        // seed-deterministically at turn start from the unit's equipped,
        // off-cooldown tagged abilities; null when every equipped ability is
        // cooling down (the unit can only move or skip).
        public string PendingAbilityId { get; private set; }

        public static Result<TurnEngine> Create(
            IEnumerable<CombatantRef> combatants,
            IActionPresenter presenter = null,
            IAbilityExecutor abilityExecutor = null,
            ICombatObserver combatObserver = null,
            int seed = 0)
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

            return Result<TurnEngine>.Success(new TurnEngine(byId, presenter, abilityExecutor, combatObserver, seed));
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

        // T18: regressor intervention seam (bullet-time direct select). The
        // pending ability may only be swapped for the active unit, before its
        // action is spent, to an equipped ability that is off cooldown.
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

            if (string.IsNullOrWhiteSpace(unitId))
            {
                return Result.Failure("Unit id is required.");
            }

            if (!combatants.TryGetValue(unitId, out var combatant))
            {
                return Result.Failure("Unknown combatant.");
            }

            if (!StringComparer.Ordinal.Equals(CurrentTurn.UnitId, unitId))
            {
                return Result.Failure("Unit is not the active turn unit.");
            }

            if (!CurrentTurn.HasAction)
            {
                return Result.Failure("Action has already been used this turn.");
            }

            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return Result.Failure("Ability id is required.");
            }

            var ability = FindEquippedAbility(combatant, abilityId);
            if (ability == null)
            {
                return Result.Failure($"Ability '{abilityId}' is not equipped.");
            }

            if (combatant.State.RemainingCooldown(abilityId) > 0)
            {
                return Result.Failure($"Ability '{abilityId}' is on cooldown.");
            }

            PendingAbilityId = abilityId;
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
            if (command.Distance < 0f)
            {
                return Result.Failure("Move distance cannot be negative.");
            }

            if (command.Distance > CurrentTurn.RemainingMovement + MovementEpsilon)
            {
                return Result.Failure("Move distance exceeds remaining movement.");
            }

            Present(new TurnPresentationEvent(TurnPresentationEventType.Move, command.UnitId, command.Distance));
            CurrentTurn = new TurnState(
                CurrentTurn.UnitId,
                Math.Max(0f, CurrentTurn.RemainingMovement - command.Distance),
                CurrentTurn.HasAction);
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

            // T18: an equipped ability that is still cooling down cannot be
            // used. Abilities outside the loadout stay the executor's problem
            // (the engine without an executor remains lenient, as before).
            var ability = FindEquippedAbility(combatants[command.UnitId], command.AbilityId);
            if (ability != null && combatants[command.UnitId].State.RemainingCooldown(ability.Id) > 0)
            {
                return Result.Failure($"Ability '{command.AbilityId}' is on cooldown.");
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

            // T18: a successful use records the ability's cooldown.
            RecordCooldown(command.UnitId, ability);

            Present(new TurnPresentationEvent(
                TurnPresentationEventType.Ability,
                command.UnitId,
                abilityId: command.AbilityId,
                targetUnitId: command.TargetUnitId));
            NotifyCommandCommitted(command);

            if (IsCombatEnded || CurrentTurn == null)
            {
                // Execution ended the combat (e.g. the last enemy was defeated).
                return Result.Success();
            }

            CurrentTurn = new TurnState(CurrentTurn.UnitId, CurrentTurn.RemainingMovement, false);
            AdvanceTurn();
            return Result.Success();
        }

        private void RecordCooldown(string unitId, AbilityDef ability)
        {
            if (ability == null || ability.CooldownRounds <= 0)
            {
                return;
            }

            if (!combatants.TryGetValue(unitId, out var combatant))
            {
                return;
            }

            var cooled = combatant.State.WithAbilityCooldown(ability.Id, ability.CooldownRounds);
            if (cooled.IsSuccess)
            {
                combatants[unitId] = combatant.WithState(cooled.Value);
            }
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
            PendingAbilityId = CurrentTurn != null ? PickPendingAbility(CurrentTurn.UnitId) : null;
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
                    CurrentTurn = new TurnState(roundOrder[index], DefaultMovementPerTurn, true);
                    PendingAbilityId = PickPendingAbility(roundOrder[index]);
                    return;
                }
            }

            RoundNumber++;
            // T18: cooldowns tick down at the round boundary, together with
            // the initiative recomputation.
            TickCooldowns();
            BeginRound();
        }

        private void TickCooldowns()
        {
            foreach (var unitId in combatants.Keys.ToList())
            {
                var combatant = combatants[unitId];
                var ticked = combatant.State.WithCooldownsTicked();
                if (!ReferenceEquals(ticked, combatant.State))
                {
                    combatants[unitId] = combatant.WithState(ticked);
                }
            }
        }

        // T18: seed-deterministic pending pick. Pool: equipped tagged
        // abilities off cooldown. Fallback: the first untagged
        // (AbilityTag.None) equipped ability off cooldown, i.e. the "basic
        // action". If everything is cooling down there is no pending ability.
        private string PickPendingAbility(string unitId)
        {
            if (!combatants.TryGetValue(unitId, out var combatant))
            {
                return null;
            }

            var state = combatant.State;
            List<AbilityDef> tagged = null;
            AbilityDef untaggedFallback = null;
            foreach (var ability in state.Loadout.Abilities)
            {
                if (ability == null || state.RemainingCooldown(ability.Id) > 0)
                {
                    continue;
                }

                if (ability.Tag == AbilityTag.None)
                {
                    untaggedFallback = untaggedFallback ?? ability;
                    continue;
                }

                (tagged ?? (tagged = new List<AbilityDef>())).Add(ability);
            }

            if (tagged == null)
            {
                return untaggedFallback != null ? untaggedFallback.Id : null;
            }

            var roll = ComputeDeterministicRoll(seed, RoundNumber, unitId);
            return tagged[roll % tagged.Count].Id;
        }

        // FNV-1a over (seed, round, unit id). Pure C# and stable across
        // processes, unlike string.GetHashCode, so the same seed always
        // produces the same pending pick.
        private static int ComputeDeterministicRoll(int seed, int roundNumber, string unitId)
        {
            unchecked
            {
                var hash = 2166136261u;
                hash = (hash ^ (uint)seed) * 16777619u;
                hash = (hash ^ (uint)roundNumber) * 16777619u;
                foreach (var character in unitId)
                {
                    hash = (hash ^ character) * 16777619u;
                }

                return (int)(hash & 0x7FFFFFFF);
            }
        }

        private static AbilityDef FindEquippedAbility(CombatantRef combatant, string abilityId)
        {
            foreach (var ability in combatant.State.Loadout.Abilities)
            {
                if (ability != null && StringComparer.Ordinal.Equals(ability.Id, abilityId))
                {
                    return ability;
                }
            }

            return null;
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
            PendingAbilityId = null;
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
    }
}
