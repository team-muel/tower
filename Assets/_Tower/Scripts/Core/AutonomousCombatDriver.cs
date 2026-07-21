using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // Fixed-tick execution seam between the data-driven scorer and the ability
    // resolver. Character speed controls each unit's real-time action cadence;
    // the caller still owns simulation tick and movement speed.
    public sealed class AutonomousCombatDriver
    {
        private const float Epsilon = 0.0001f;
        public const float BaselineSpeed = 10f;
        public const float TelegraphLeadSeconds = 0.75f;

        private readonly CombatState state;
        private readonly IBattlefield battlefield;
        private readonly ActionScorer scorer;
        private readonly IAbilityExecutor abilityExecutor;
        private readonly float tickSeconds;
        private readonly float movementUnitsPerSecond;
        private readonly RealtimeCommandBoard commandBoard;
        private readonly Dictionary<string, float> nextActionAtSeconds;
        private readonly Dictionary<string, AutonomousCombatIntent> activeIntents;
        private readonly HashSet<string> externallyPositionedUnitIds;

        private AutonomousCombatDriver(
            CombatState state,
            IBattlefield battlefield,
            ActionScorer scorer,
            IAbilityExecutor abilityExecutor,
            float tickSeconds,
            float movementUnitsPerSecond,
            IEnumerable<string> externallyPositionedUnitIds,
            RealtimeCommandBoard commandBoard)
        {
            this.state = state;
            this.battlefield = battlefield;
            this.scorer = scorer;
            this.abilityExecutor = abilityExecutor;
            this.tickSeconds = tickSeconds;
            this.movementUnitsPerSecond = movementUnitsPerSecond;
            this.commandBoard = commandBoard;
            this.externallyPositionedUnitIds = externallyPositionedUnitIds == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(externallyPositionedUnitIds, StringComparer.Ordinal);
            nextActionAtSeconds = new Dictionary<string, float>(StringComparer.Ordinal);
            activeIntents = new Dictionary<string, AutonomousCombatIntent>(StringComparer.Ordinal);
            foreach (string unitId in state.LivingUnitIds)
            {
                CharacterState unit = state.GetCombatant(unitId).State;
                nextActionAtSeconds[unitId] = state.ElapsedSeconds + ActionIntervalSeconds(unit.EffectiveSpeed);
            }
        }

        public CombatState State => state;
        public float TickSeconds => tickSeconds;
        public float MovementUnitsPerSecond => movementUnitsPerSecond;
        public RealtimeCommandBoard CommandBoard => commandBoard;

        public bool TryGetActiveIntent(string unitId, out AutonomousCombatIntent intent)
        {
            return activeIntents.TryGetValue(unitId, out intent);
        }

        // Speed 10 is the authored baseline: one action each second. Speed is
        // clamped to one so zero-speed data remains alive but acts very slowly.
        public static float ActionIntervalSeconds(int effectiveSpeed)
        {
            return BaselineSpeed / Math.Max(1, effectiveSpeed);
        }

        public float SecondsUntilNextAction(string unitId)
        {
            return unitId != null && nextActionAtSeconds.TryGetValue(unitId, out float readyAt)
                ? Math.Max(0f, readyAt - state.ElapsedSeconds)
                : float.PositiveInfinity;
        }

        public static Result<AutonomousCombatDriver> Create(
            CombatState state,
            IBattlefield battlefield,
            ActionScorer scorer,
            IAbilityExecutor abilityExecutor,
            float tickSeconds,
            float movementUnitsPerSecond,
            IEnumerable<string> externallyPositionedUnitIds = null,
            RealtimeCommandBoard commandBoard = null)
        {
            if (state == null)
            {
                return Result<AutonomousCombatDriver>.Failure("Combat state is required.");
            }

            if (battlefield == null)
            {
                return Result<AutonomousCombatDriver>.Failure("Battlefield is required.");
            }

            if (scorer == null)
            {
                return Result<AutonomousCombatDriver>.Failure("Action scorer is required.");
            }

            if (abilityExecutor == null)
            {
                return Result<AutonomousCombatDriver>.Failure("Ability executor is required.");
            }

            if (!IsPositiveFinite(tickSeconds))
            {
                return Result<AutonomousCombatDriver>.Failure("Tick seconds must be finite and greater than zero.");
            }

            if (!IsPositiveFinite(movementUnitsPerSecond))
            {
                return Result<AutonomousCombatDriver>.Failure("Movement units per second must be finite and greater than zero.");
            }

            if (externallyPositionedUnitIds != null)
            {
                foreach (string unitId in externallyPositionedUnitIds)
                {
                    if (string.IsNullOrWhiteSpace(unitId) || state.GetCombatant(unitId) == null)
                    {
                        return Result<AutonomousCombatDriver>.Failure(
                            "Externally positioned unit ids must identify combatants.");
                    }
                }
            }

            return Result<AutonomousCombatDriver>.Success(
                new AutonomousCombatDriver(
                    state,
                    battlefield,
                    scorer,
                    abilityExecutor,
                    tickSeconds,
                    movementUnitsPerSecond,
                    externallyPositionedUnitIds,
                    commandBoard));
        }

        // Performs exactly one externally configured simulation tick. Keeping
        // the tick fixed makes decision cadence explicit and keeps the Core
        // driver independent from Unity Update/FixedUpdate scheduling.
        public Result<AutonomousCombatTick> Step()
        {
            if (state.IsCombatEnded)
            {
                return Result<AutonomousCombatTick>.Failure("Combat has ended.");
            }

            var advanced = state.AdvanceElapsed(tickSeconds);
            if (advanced.IsFailure)
            {
                return Result<AutonomousCombatTick>.Failure(advanced.Error);
            }

            commandBoard?.Advance(state.ElapsedSeconds);
            RefreshTelegraphs();

            var events = new List<AutonomousCombatEvent>();
            foreach (var unitId in state.LivingUnitIds)
            {
                if (state.IsCombatEnded)
                {
                    break;
                }

                if (!state.IsAlive(unitId))
                {
                    continue;
                }

                if (!nextActionAtSeconds.TryGetValue(unitId, out float readyAt)
                    || state.ElapsedSeconds + Epsilon < readyAt)
                {
                    continue;
                }

                var executed = ExecutePlan(unitId);
                if (executed.IsFailure)
                {
                    return Result<AutonomousCombatTick>.Failure(executed.Error);
                }

                events.Add(executed.Value);
                activeIntents.Remove(unitId);
                CharacterState updated = state.GetCombatant(unitId).State;
                nextActionAtSeconds[unitId] = readyAt + ActionIntervalSeconds(updated.EffectiveSpeed);
            }

            return Result<AutonomousCombatTick>.Success(
                new AutonomousCombatTick(
                    state.ElapsedSeconds,
                    state.IsCombatEnded,
                    events,
                    ActiveIntentsInRegistrationOrder()));
        }

        private IReadOnlyList<AutonomousCombatIntent> ActiveIntentsInRegistrationOrder()
        {
            var intents = new List<AutonomousCombatIntent>();
            foreach (var unitId in state.LivingUnitIds)
            {
                if (activeIntents.TryGetValue(unitId, out AutonomousCombatIntent intent))
                {
                    intents.Add(intent);
                }
            }

            return intents;
        }

        private Result<AutonomousCombatEvent> ExecutePlan(string unitId)
        {
            var selection = PlanForUnit(unitId, consumePreciseOrder: true);
            if (selection.IsFailure)
            {
                return Result<AutonomousCombatEvent>.Failure(selection.Error);
            }

            var from = battlefield.FindOccupant(unitId);
            if (!from.HasValue)
            {
                return Result<AutonomousCombatEvent>.Failure("Unit is not on the battlefield.");
            }

            var plan = selection.Value.Plan;
            var to = from.Value;
            if (plan.MoveDistance > Epsilon && !externallyPositionedUnitIds.Contains(unitId))
            {
                var movementBudget = movementUnitsPerSecond * tickSeconds;
                to = battlefield.ClampMove(unitId, from.Value, plan.MovePosition, movementBudget);
                if (to != from.Value && !battlefield.TryMoveOccupant(unitId, to))
                {
                    return Result<AutonomousCombatEvent>.Failure("Planned movement could not be applied.");
                }
            }

            var abilityResolved = false;
            if (plan.Kind == AiPlanKind.Ability && HasReachedPlannedPosition(to, plan.MovePosition))
            {
                var resolved = abilityExecutor.Execute(
                    state,
                    new UseAbilityCommand(unitId, plan.AbilityId, plan.TargetUnitId, plan.TargetPoint));
                if (resolved.IsFailure)
                {
                    return Result<AutonomousCombatEvent>.Failure(resolved.Error);
                }

                abilityResolved = true;
                if (selection.Value.IsPreciseOrder)
                {
                    commandBoard?.ConsumePreciseOrder(unitId);
                }
            }

            return Result<AutonomousCombatEvent>.Success(
                new AutonomousCombatEvent(unitId, plan.Kind, from.Value, to, abilityResolved, selection.Value.IsPreciseOrder));
        }

        private void RefreshTelegraphs()
        {
            foreach (var unitId in new List<string>(activeIntents.Keys))
            {
                if (!state.IsAlive(unitId))
                {
                    activeIntents.Remove(unitId);
                }
            }

            foreach (var unitId in state.LivingUnitIds)
            {
                if (!nextActionAtSeconds.TryGetValue(unitId, out float readyAt)
                    || state.ElapsedSeconds + Epsilon >= readyAt
                    || state.ElapsedSeconds + Epsilon < readyAt - TelegraphLeadSeconds)
                {
                    continue;
                }

                var selection = PlanForUnit(unitId, consumePreciseOrder: false);
                if (selection.IsSuccess)
                {
                    var combatant = state.GetCombatant(unitId);
                    activeIntents[unitId] = new AutonomousCombatIntent(
                        unitId,
                        selection.Value.Plan,
                        combatant.State.Definition.Disposition,
                        ResolveStance(unitId, combatant),
                        readyAt,
                        selection.Value.IsPreciseOrder);
                }
            }
        }

        private Result<PlanSelection> PlanForUnit(string unitId, bool consumePreciseOrder)
        {
            CombatantRef combatant = state.GetCombatant(unitId);
            if (combatant == null)
            {
                return Result<PlanSelection>.Failure("Unknown unit.");
            }

            CommandStanceAssignment assignment = ResolveAssignment(unitId, combatant);
            if (commandBoard != null
                && commandBoard.TryGetPreciseOrder(unitId, state.ElapsedSeconds, out PreciseOrder order))
            {
                Result<AiPlan> precise = scorer.ChoosePreciseAction(
                    state,
                    unitId,
                    order.AbilityId,
                    string.IsNullOrEmpty(order.TargetUnitId) ? null : order.TargetUnitId,
                    order.TargetPoint,
                    assignment.Stance,
                    assignment.FocusTargetId,
                    !externallyPositionedUnitIds.Contains(unitId));
                if (precise.IsSuccess)
                {
                    return Result<PlanSelection>.Success(new PlanSelection(precise.Value, true));
                }

                // A stale target, cooldown, or range failure should not freeze
                // the companion. The companion falls back to its personality
                // and current stance for this action.
                if (consumePreciseOrder)
                {
                    commandBoard.ConsumePreciseOrder(unitId);
                }
            }

            Result<AiPlan> autonomous = externallyPositionedUnitIds.Contains(unitId)
                ? scorer.ChooseActionWithoutMovement(state, unitId, assignment.Stance, assignment.FocusTargetId)
                : scorer.ChooseAction(state, unitId, assignment.Stance, assignment.FocusTargetId);
            return autonomous.IsSuccess
                ? Result<PlanSelection>.Success(new PlanSelection(autonomous.Value, false))
                : Result<PlanSelection>.Failure(autonomous.Error);
        }

        private CommandStanceAssignment ResolveAssignment(string unitId, CombatantRef combatant)
        {
            return commandBoard == null
                ? new CommandStanceAssignment(
                    CommandStanceRules.DefaultFor(combatant.State.Definition.Disposition),
                    null)
                : commandBoard.GetAssignment(unitId, combatant.State.Definition.Disposition);
        }

        private CommandStance ResolveStance(string unitId, CombatantRef combatant)
        {
            return ResolveAssignment(unitId, combatant).Stance;
        }

        private static bool HasReachedPlannedPosition(BattlePos actual, BattlePos planned)
        {
            var deltaX = actual.X - planned.X;
            var deltaY = actual.Y - planned.Y;
            return (deltaX * deltaX) + (deltaY * deltaY) <= Epsilon * Epsilon;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private readonly struct PlanSelection
        {
            public PlanSelection(AiPlan plan, bool isPreciseOrder)
            {
                Plan = plan;
                IsPreciseOrder = isPreciseOrder;
            }

            public AiPlan Plan { get; }
            public bool IsPreciseOrder { get; }
        }
    }

    public sealed class AutonomousCombatTick
    {
        public AutonomousCombatTick(
            float elapsedSeconds,
            bool combatEnded,
            IReadOnlyList<AutonomousCombatEvent> events,
            IReadOnlyList<AutonomousCombatIntent> intents = null)
        {
            ElapsedSeconds = elapsedSeconds;
            CombatEnded = combatEnded;
            Events = events ?? throw new ArgumentNullException(nameof(events));
            Intents = intents ?? Array.Empty<AutonomousCombatIntent>();
        }

        public float ElapsedSeconds { get; }
        public bool CombatEnded { get; }
        public IReadOnlyList<AutonomousCombatEvent> Events { get; }
        public IReadOnlyList<AutonomousCombatIntent> Intents { get; }
    }

    public sealed class AutonomousCombatIntent
    {
        public AutonomousCombatIntent(
            string unitId,
            AiPlan plan,
            DispositionType disposition,
            CommandStance stance,
            float executeAtSeconds,
            bool isPreciseOrder)
        {
            UnitId = unitId ?? string.Empty;
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
            Disposition = disposition;
            Stance = stance;
            ExecuteAtSeconds = executeAtSeconds;
            IsPreciseOrder = isPreciseOrder;
        }

        public string UnitId { get; }
        public AiPlan Plan { get; }
        public DispositionType Disposition { get; }
        public CommandStance Stance { get; }
        public float ExecuteAtSeconds { get; }
        public bool IsPreciseOrder { get; }
    }

    public sealed class AutonomousCombatEvent
    {
        public AutonomousCombatEvent(
            string unitId,
            AiPlanKind plannedKind,
            BattlePos fromPosition,
            BattlePos toPosition,
            bool abilityResolved,
            bool isPreciseOrder = false)
        {
            UnitId = unitId;
            PlannedKind = plannedKind;
            FromPosition = fromPosition;
            ToPosition = toPosition;
            AbilityResolved = abilityResolved;
            IsPreciseOrder = isPreciseOrder;
        }

        public string UnitId { get; }
        public AiPlanKind PlannedKind { get; }
        public BattlePos FromPosition { get; }
        public BattlePos ToPosition { get; }
        public bool AbilityResolved { get; }
        public bool IsPreciseOrder { get; }
    }
}
