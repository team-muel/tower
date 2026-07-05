namespace Tower.Core
{
    public sealed class TurnState
    {
        public TurnState(string unitId, int remainingMovement, bool hasAction)
        {
            UnitId = unitId;
            RemainingMovement = remainingMovement;
            HasAction = hasAction;
        }

        public string UnitId { get; }
        public int RemainingMovement { get; }
        public bool HasAction { get; }
    }
}
