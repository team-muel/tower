namespace Tower.Core
{
    // T19: one popup entry for the command-mode pending-ability picker.
    public readonly struct PendingAbilityOption
    {
        public PendingAbilityOption(int slotNumber, string abilityId, string displayName, int remainingCooldown, bool isPending)
        {
            SlotNumber = slotNumber;
            AbilityId = abilityId;
            DisplayName = displayName;
            RemainingCooldown = remainingCooldown;
            IsPending = isPending;
        }

        public int SlotNumber { get; }
        public string AbilityId { get; }
        public string DisplayName { get; }
        public int RemainingCooldown { get; }
        public bool IsPending { get; }

        // UX rule 2: cooling abilities stay visible but cannot be selected.
        public bool IsSelectable => RemainingCooldown <= 0;
    }
}
