namespace Tower.Core
{
    // Resolved, per-anchor snapshot the presenter/QA layer reads (33 Reference
    // §5: InteractionState). Produced by InteractionResolver from a def + the
    // anchor's runtime state + an InteractionContext. Immutable value.
    public readonly struct InteractionState
    {
        public InteractionState(
            string id,
            bool visible,
            bool enabled,
            string disabledReason,
            string preview,
            int usesRemaining,
            bool recordsOnUse)
        {
            Id = id;
            Visible = visible;
            Enabled = enabled;
            DisabledReason = disabledReason ?? string.Empty;
            Preview = preview ?? string.Empty;
            UsesRemaining = usesRemaining;
            RecordsOnUse = recordsOnUse;
        }

        public string Id { get; }

        public bool Visible { get; }

        // Hover is allowed when Visible; use is allowed only when Enabled.
        public bool Enabled { get; }

        // Non-empty only when the anchor is visible-but-disabled.
        public string DisabledReason { get; }

        // Combined risk/reward hover preview text.
        public string Preview { get; }

        // Negative = unlimited.
        public int UsesRemaining { get; }

        public bool RecordsOnUse { get; }

        public bool CanHover => Visible;

        public bool IsLockedButShown => Visible && !Enabled;
    }
}
