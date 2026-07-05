namespace Tower.Core
{
    // Tie-break order between equally scored plans: lower value wins.
    public enum AiPlanKind
    {
        Ability = 0,
        Move = 1,
        Skip = 2
    }

    // The scorer's chosen turn: an optional reposition followed by an ability
    // use (Kind == Ability), a pure reposition (Move), or a stay-put (Skip).
    public sealed class AiPlan
    {
        public AiPlan(
            AiPlanKind kind,
            GridPos moveDestination,
            int moveDistance,
            string abilityId,
            string targetUnitId,
            GridPos? targetCell,
            float score)
        {
            Kind = kind;
            MoveDestination = moveDestination;
            MoveDistance = moveDistance;
            AbilityId = abilityId;
            TargetUnitId = targetUnitId;
            TargetCell = targetCell;
            Score = score;
        }

        public AiPlanKind Kind { get; }

        // Always set; equals the current position when MoveDistance is zero.
        public GridPos MoveDestination { get; }
        public int MoveDistance { get; }

        // Set when Kind == Ability.
        public string AbilityId { get; }
        public string TargetUnitId { get; }

        // Set for cell-targeted abilities only.
        public GridPos? TargetCell { get; }

        public float Score { get; }
    }
}
