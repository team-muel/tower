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
    // T20: positions are continuous BattlePos values; the legacy GridPos
    // views (MoveDestination/TargetCell) are derived for grid-mode callers.
    public sealed class AiPlan
    {
        public AiPlan(
            AiPlanKind kind,
            BattlePos movePosition,
            float moveDistance,
            string abilityId,
            string targetUnitId,
            BattlePos? targetPoint,
            float score)
        {
            Kind = kind;
            MovePosition = movePosition;
            MoveDistance = moveDistance;
            AbilityId = abilityId;
            TargetUnitId = targetUnitId;
            TargetPoint = targetPoint;
            Score = score;
        }

        public AiPlanKind Kind { get; }

        // Always set; equals the current position when MoveDistance is zero.
        public BattlePos MovePosition { get; }
        public float MoveDistance { get; }

        // Legacy grid view: the cell containing MovePosition.
        public GridPos MoveDestination
        {
            get { return BattleScale.ToGridPos(MovePosition); }
        }

        // Set when Kind == Ability.
        public string AbilityId { get; }
        public string TargetUnitId { get; }

        // Set for cell-targeted abilities only.
        public BattlePos? TargetPoint { get; }

        public GridPos? TargetCell
        {
            get { return TargetPoint.HasValue ? BattleScale.ToGridPos(TargetPoint.Value) : (GridPos?)null; }
        }

        public float Score { get; }
    }
}
