using System.Collections.Generic;

namespace Tower.Core
{
    // One candidate reposition from GetMoveCandidates. Cost is the movement
    // points consumed by the clamped move.
    public readonly struct BattleMoveCandidate
    {
        public BattleMoveCandidate(BattlePos position, float cost)
        {
            Position = position;
            Cost = cost;
        }

        public BattlePos Position { get; }
        public float Cost { get; }
    }

    // T20: battlefield abstraction — the single seam through which the combat
    // engine consumes positions, distance, line of sight, occupancy and
    // movement. Implementations must be deterministic: the same call sequence
    // on the same platform always produces the same results.
    public interface IBattlefield
    {
        CombatSpaceMode Mode { get; }

        // Area extents in analog units.
        float Width { get; }
        float Height { get; }

        bool Contains(BattlePos pos);

        float Distance(BattlePos a, BattlePos b);

        // Melee-contact test.
        bool AreAdjacent(BattlePos a, BattlePos b);

        bool HasLineOfSight(BattlePos from, BattlePos to);

        // Deterministically clamps a move from 'from' toward 'to' so that the
        // result is valid (in bounds, not colliding) and costs at most
        // moveBudget. Returns 'from' when no forward progress is possible.
        BattlePos ClampMove(string unitId, BattlePos from, BattlePos to, float moveBudget);

        bool IsOccupied(BattlePos pos);
        string GetOccupantAt(BattlePos pos);
        BattlePos? FindOccupant(string unitId);
        bool TryPlaceOccupant(string unitId, BattlePos pos);
        bool TryMoveOccupant(string unitId, BattlePos to);
        bool RemoveOccupant(string unitId);

        // Deterministic, finite candidate set for AI move planning. Always
        // contains the stay-put candidate (cost 0) first.
        IReadOnlyList<BattleMoveCandidate> GetMoveCandidates(string unitId, BattlePos from, float moveBudget);
    }
}
