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

        private readonly CombatState state;
        private readonly IBattlefield battlefield;
        private readonly ActionScorer scorer;
        private readonly IAbilityExecutor abilityExecutor;
        private readonly float tickSeconds;
        private readonly float movementUnitsPerSecond;
        private readonly Dictionary<string, float> nextActionAtSeconds;
        private readonly HashSet<string> externallyPositionedUnitIds;

        private AutonomousCombatDriver(
            CombatState state,
            IBattlefield battlefield,
            ActionScorer scorer,
            IAbilityExecutor abilityExecutor,
            float tickSeconds,
            float movementUnitsPerSecond,
            IEnumerable<string> externallyPositionedUnitIds)
        {
            this.state = state;
            this.battlefield = battlefield;
            this.scorer = scorer;
            this.abilityExecutor = abilityExecutor;
            this.tickSeconds = tickSeconds;
            this.movementUnitsPerSecond = movementUnitsPerSecond;
            this.externallyPositionedUnitIds = externallyPositionedUnitIds == null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(externallyPositionedUnitIds, StringComparer.Ordinal);
            nextActionAtSeconds = new Dictionary<string, float>(StringComparer.Ordinal);
            foreach (string unitId in state.LivingUnitIds)
            {
                CharacterState unit = state.GetCombatant(unitId).State;
                nextActionAtSeconds[unitId] = state.ElapsedSeconds + ActionIntervalSeconds(unit.EffectiveSpeed);
            }
        }

        public CombatState State => state;
        public float TickSeconds => tickSeconds;
        public float MovementUnitsPerSecond => movementUnitsPerSecond;

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
            IEnumerable<string> externallyPositionedUnitIds = null)
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
                    externallyPositionedUnitIds));
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
                CharacterState updated = state.GetCombatant(unitId).State;
                nextActionAtSeconds[unitId] = readyAt + ActionIntervalSeconds(updated.EffectiveSpeed);
            }

            return Result<AutonomousCombatTick>.Success(
                new AutonomousCombatTick(state.ElapsedSeconds, state.IsCombatEnded, events));
        }

        private Result<AutonomousCombatEvent> ExecutePlan(string unitId)
        {
            var planResult = externallyPositionedUnitIds.Contains(unitId)
                ? scorer.ChooseActionWithoutMovement(state, unitId)
                : scorer.ChooseAction(state, unitId);
            if (planResult.IsFailure)
            {
                return Result<AutonomousCombatEvent>.Failure(planResult.Error);
            }

            var from = battlefield.FindOccupant(unitId);
            if (!from.HasValue)
            {
                return Result<AutonomousCombatEvent>.Failure("Unit is not on the battlefield.");
            }

            var plan = planResult.Value;
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
            }

            return Result<AutonomousCombatEvent>.Success(
                new AutonomousCombatEvent(unitId, plan.Kind, from.Value, to, abilityResolved));
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
    }

    public sealed class AutonomousCombatTick
    {
        public AutonomousCombatTick(float elapsedSeconds, bool combatEnded, IReadOnlyList<AutonomousCombatEvent> events)
        {
            ElapsedSeconds = elapsedSeconds;
            CombatEnded = combatEnded;
            Events = events ?? throw new ArgumentNullException(nameof(events));
        }

        public float ElapsedSeconds { get; }
        public bool CombatEnded { get; }
        public IReadOnlyList<AutonomousCombatEvent> Events { get; }
    }

    public sealed class AutonomousCombatEvent
    {
        public AutonomousCombatEvent(
            string unitId,
            AiPlanKind plannedKind,
            BattlePos fromPosition,
            BattlePos toPosition,
            bool abilityResolved)
        {
            UnitId = unitId;
            PlannedKind = plannedKind;
            FromPosition = fromPosition;
            ToPosition = toPosition;
            AbilityResolved = abilityResolved;
        }

        public string UnitId { get; }
        public AiPlanKind PlannedKind { get; }
        public BattlePos FromPosition { get; }
        public BattlePos ToPosition { get; }
        public bool AbilityResolved { get; }
    }
}
