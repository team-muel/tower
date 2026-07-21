namespace Tower.Core
{
    /// <summary>
    /// Small deterministic counters for the package-β command loop. These are
    /// intentionally separate from CombatMetrics: they describe commander
    /// decisions, including orders which were later invalidated, rather than
    /// damage or ability resolution.
    /// </summary>
    public sealed class CommandTelemetry
    {
        public CommandTelemetrySnapshot Snapshot => new CommandTelemetrySnapshot(
            StanceCommands,
            PreciseOrdersIssued,
            PreciseOrdersReplaced,
            PreciseOrdersConsumed,
            PreciseOrdersExpired,
            PreciseOrderFallbacks);

        internal int StanceCommands { get; private set; }
        internal int PreciseOrdersIssued { get; private set; }
        internal int PreciseOrdersReplaced { get; private set; }
        internal int PreciseOrdersConsumed { get; private set; }
        internal int PreciseOrdersExpired { get; private set; }
        internal int PreciseOrderFallbacks { get; private set; }

        internal void RecordStanceCommand()
        {
            StanceCommands++;
        }

        internal void RecordPreciseOrderIssued(bool replacedExisting)
        {
            PreciseOrdersIssued++;
            if (replacedExisting)
            {
                PreciseOrdersReplaced++;
            }
        }

        internal void RecordPreciseOrderConsumed()
        {
            PreciseOrdersConsumed++;
        }

        internal void RecordPreciseOrderExpired()
        {
            PreciseOrdersExpired++;
        }

        internal void RecordPreciseOrderFallback()
        {
            PreciseOrderFallbacks++;
        }
    }

    public readonly struct CommandTelemetrySnapshot
    {
        public CommandTelemetrySnapshot(
            int stanceCommands,
            int preciseOrdersIssued,
            int preciseOrdersReplaced,
            int preciseOrdersConsumed,
            int preciseOrdersExpired,
            int preciseOrderFallbacks)
        {
            StanceCommands = stanceCommands;
            PreciseOrdersIssued = preciseOrdersIssued;
            PreciseOrdersReplaced = preciseOrdersReplaced;
            PreciseOrdersConsumed = preciseOrdersConsumed;
            PreciseOrdersExpired = preciseOrdersExpired;
            PreciseOrderFallbacks = preciseOrderFallbacks;
        }

        public int StanceCommands { get; }
        public int PreciseOrdersIssued { get; }
        public int PreciseOrdersReplaced { get; }
        public int PreciseOrdersConsumed { get; }
        public int PreciseOrdersExpired { get; }
        public int PreciseOrderFallbacks { get; }
    }
}
