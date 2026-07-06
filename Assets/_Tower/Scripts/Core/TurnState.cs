namespace Tower.Core
{
    public sealed class TurnState
    {
        // T20: movement is tracked as a float budget so analog moves can
        // consume fractional distances; grid moves keep spending integers.
        public TurnState(string unitId, float remainingMovement, bool hasAction)
        {
            UnitId = unitId;
            RemainingMovement = remainingMovement;
            HasAction = hasAction;
        }

        public string UnitId { get; }
        public float RemainingMovement { get; }
        public bool HasAction { get; }
    }
}
