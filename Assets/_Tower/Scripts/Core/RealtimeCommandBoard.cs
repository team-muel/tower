using System;
using System.Collections.Generic;

namespace Tower.Core
{
    /// <summary>
    /// Real-time command state for package β.
    ///
    /// Stance is persistent until changed. A precise order is a one-shot
    /// ability/target instruction which can only be issued while the caller's
    /// bullet-time command window is open. The board itself stays Unity-free so
    /// the command contract can be tested independently of input and HUD.
    /// </summary>
    public sealed class RealtimeCommandBoard
    {
        public const float DefaultPreciseOrderLifetimeSeconds = 3f;

        private readonly CommandTelemetry telemetry;
        private readonly Dictionary<string, CommandStanceAssignment> assignments =
            new Dictionary<string, CommandStanceAssignment>(StringComparer.Ordinal);
        private readonly Dictionary<string, PreciseOrder> preciseOrders =
            new Dictionary<string, PreciseOrder>(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, CommandStanceAssignment> Assignments => assignments;
        public IReadOnlyDictionary<string, PreciseOrder> PreciseOrders => preciseOrders;
        public CommandTelemetrySnapshot Telemetry => telemetry.Snapshot;

        public RealtimeCommandBoard(CommandTelemetry commandTelemetry = null)
        {
            telemetry = commandTelemetry ?? new CommandTelemetry();
        }

        public CommandStanceAssignment GetAssignment(string unitId, DispositionType disposition)
        {
            if (!string.IsNullOrEmpty(unitId) && assignments.TryGetValue(unitId, out var assignment))
            {
                return assignment;
            }

            return new CommandStanceAssignment(CommandStanceRules.DefaultFor(disposition), null);
        }

        public Result SetStance(
            string unitId,
            CommandStance stance,
            string focusTargetId = null)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                return Result.Failure("Command stance unit id is required.");
            }

            if (!Enum.IsDefined(typeof(CommandStance), stance))
            {
                return Result.Failure("Command stance is invalid.");
            }

            if (stance == CommandStance.Focus && string.IsNullOrWhiteSpace(focusTargetId))
            {
                return Result.Failure("Focus stance requires a target.");
            }

            assignments[unitId] = new CommandStanceAssignment(
                stance,
                stance == CommandStance.Focus ? focusTargetId : null);
            telemetry.RecordStanceCommand();
            return Result.Success();
        }

        public Result IssuePreciseOrder(
            string unitId,
            string abilityId,
            string targetUnitId,
            BattlePos? targetPoint,
            bool commandWindowActive,
            float issuedAtSeconds,
            float lifetimeSeconds = DefaultPreciseOrderLifetimeSeconds)
        {
            if (!commandWindowActive)
            {
                return Result.Failure("Precise orders require the bullet-time command window.");
            }

            if (string.IsNullOrWhiteSpace(unitId))
            {
                return Result.Failure("Precise order unit id is required.");
            }

            if (string.IsNullOrWhiteSpace(abilityId))
            {
                return Result.Failure("Precise order ability id is required.");
            }

            if (string.IsNullOrWhiteSpace(targetUnitId) && !targetPoint.HasValue)
            {
                return Result.Failure("Precise order requires a unit or point target.");
            }

            if (issuedAtSeconds < 0f
                || float.IsNaN(issuedAtSeconds) || float.IsInfinity(issuedAtSeconds)
                || lifetimeSeconds <= 0f || float.IsNaN(lifetimeSeconds) || float.IsInfinity(lifetimeSeconds))
            {
                return Result.Failure("Precise order timing must be finite and positive.");
            }

            float expiresAtSeconds = issuedAtSeconds + lifetimeSeconds;
            if (float.IsInfinity(expiresAtSeconds))
            {
                return Result.Failure("Precise order expiration must be finite.");
            }

            bool replacedExisting = preciseOrders.ContainsKey(unitId);
            preciseOrders[unitId] = new PreciseOrder(
                unitId,
                abilityId,
                targetUnitId,
                targetPoint,
                issuedAtSeconds,
                expiresAtSeconds);
            telemetry.RecordPreciseOrderIssued(replacedExisting);
            return Result.Success();
        }

        public bool TryGetPreciseOrder(string unitId, float elapsedSeconds, out PreciseOrder order)
        {
            order = default;
            if (string.IsNullOrEmpty(unitId) || !preciseOrders.TryGetValue(unitId, out order))
            {
                return false;
            }

            if (elapsedSeconds >= order.ExpiresAtSeconds)
            {
                preciseOrders.Remove(unitId);
                telemetry.RecordPreciseOrderExpired();
                order = default;
                return false;
            }

            return true;
        }

        public bool ConsumePreciseOrder(string unitId)
        {
            bool consumed = !string.IsNullOrEmpty(unitId) && preciseOrders.Remove(unitId);
            if (consumed)
            {
                telemetry.RecordPreciseOrderConsumed();
            }

            return consumed;
        }

        public void RecordPreciseOrderFallback()
        {
            telemetry.RecordPreciseOrderFallback();
        }

        public void Advance(float elapsedSeconds)
        {
            if (float.IsNaN(elapsedSeconds) || float.IsInfinity(elapsedSeconds))
            {
                return;
            }

            foreach (var pair in new List<KeyValuePair<string, PreciseOrder>>(preciseOrders))
            {
                if (elapsedSeconds >= pair.Value.ExpiresAtSeconds)
                {
                    preciseOrders.Remove(pair.Key);
                    telemetry.RecordPreciseOrderExpired();
                }
            }
        }

        public void ClearPreciseOrders()
        {
            preciseOrders.Clear();
        }
    }

    public readonly struct PreciseOrder
    {
        public PreciseOrder(
            string unitId,
            string abilityId,
            string targetUnitId,
            BattlePos? targetPoint,
            float issuedAtSeconds,
            float expiresAtSeconds)
        {
            UnitId = unitId ?? string.Empty;
            AbilityId = abilityId ?? string.Empty;
            TargetUnitId = targetUnitId ?? string.Empty;
            TargetPoint = targetPoint;
            IssuedAtSeconds = issuedAtSeconds;
            ExpiresAtSeconds = expiresAtSeconds;
        }

        public string UnitId { get; }
        public string AbilityId { get; }
        public string TargetUnitId { get; }
        public BattlePos? TargetPoint { get; }
        public float IssuedAtSeconds { get; }
        public float ExpiresAtSeconds { get; }
    }
}
