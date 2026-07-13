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
    // The scorer's chosen action in continuous battlefield coordinates.
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

        // Set when Kind == Ability.
        public string AbilityId { get; }
        public string TargetUnitId { get; }

        // Set for cell-targeted abilities only.
        public BattlePos? TargetPoint { get; }

        public float Score { get; }
    }
}
