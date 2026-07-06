namespace Tower.Core
{
    public sealed class TurnState
    {
        public TurnState(string unitId, int remainingMovement, bool hasAction, string pendingAbilityId = null)
        {
            UnitId = unitId;
            RemainingMovement = remainingMovement;
            HasAction = hasAction;
            PendingAbilityId = pendingAbilityId;
        }

        public string UnitId { get; }
        public int RemainingMovement { get; }
        public bool HasAction { get; }
        public string PendingAbilityId { get; }
    }
}
