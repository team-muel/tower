using System;
using System.Collections.Generic;
using System.Linq;

namespace Tower.Core
{
    // T8 macro-loop state: which stairway/floor the party is on, the live
    // roster, retreat count, shortcut flags, and the permanent records
    // (missing / fallen). Immutable; ExpeditionRules produces new states.
    //
    // v0 scope: one stairway of three floors, but the model supports any
    // counts so the vertical slice can grow without a rewrite.
    public sealed class ExpeditionState
    {
        public const int DefaultStairwayCount = 1;
        public const int DefaultFloorCount = 3;

        private readonly List<ExpeditionMember> roster;
        private readonly List<ExpeditionMember> initialRoster;
        private readonly List<string> missingIds;
        private readonly List<string> fallenIds;
        private readonly HashSet<int> shortcutStairways;

        internal ExpeditionState(
            int stairwayCount,
            int stairwayIndex,
            int floorCount,
            int floorIndex,
            int retreatCount,
            bool isComplete,
            List<ExpeditionMember> roster,
            List<ExpeditionMember> initialRoster,
            List<string> missingIds,
            List<string> fallenIds,
            HashSet<int> shortcutStairways)
        {
            StairwayCount = stairwayCount;
            StairwayIndex = stairwayIndex;
            FloorCount = floorCount;
            FloorIndex = floorIndex;
            RetreatCount = retreatCount;
            IsComplete = isComplete;
            this.roster = roster;
            this.initialRoster = initialRoster;
            this.missingIds = missingIds;
            this.fallenIds = fallenIds;
            this.shortcutStairways = shortcutStairways;
        }

        public int StairwayCount { get; }
        public int StairwayIndex { get; }
        public int FloorCount { get; }
        public int FloorIndex { get; }
        public int RetreatCount { get; }
        public bool IsComplete { get; }

        // Members currently on the expedition (may include dead members until
        // the next advance/retreat resolves them).
        public IReadOnlyList<ExpeditionMember> Roster => roster;

        // Snapshot taken at expedition creation; the great regression resets
        // the roster from this template (minus missing members).
        public IReadOnlyList<ExpeditionMember> InitialRoster => initialRoster;

        // Permanent record: members lost to the three-death rule. Survives
        // rollback and the great regression.
        public IReadOnlyList<string> MissingIds => missingIds;

        // Record: members whose deaths were locked in by an advance. Cleared
        // by the great regression (time rolls back past their deaths).
        public IReadOnlyList<string> FallenIds => fallenIds;

        // Conquered stairways. Survives rollback and the great regression.
        public IReadOnlyCollection<int> ShortcutStairways => shortcutStairways;

        public bool HasShortcut(int stairwayIndex)
        {
            return shortcutStairways.Contains(stairwayIndex);
        }

        public ExpeditionMember FindMember(string unitId)
        {
            return roster.FirstOrDefault(member => StringComparer.Ordinal.Equals(member.UnitId, unitId));
        }

        public static Result<ExpeditionState> CreateNew(
            IEnumerable<ExpeditionMember> roster,
            int stairwayCount = DefaultStairwayCount,
            int floorCount = DefaultFloorCount)
        {
            if (roster == null)
            {
                return Result<ExpeditionState>.Failure("Roster is required.");
            }

            var members = roster.ToList();
            if (members.Count == 0)
            {
                return Result<ExpeditionState>.Failure("Roster requires at least one member.");
            }

            if (members.Any(member => member == null))
            {
                return Result<ExpeditionState>.Failure("Roster cannot contain null members.");
            }

            if (members.Select(member => member.UnitId).Distinct(StringComparer.Ordinal).Count() != members.Count)
            {
                return Result<ExpeditionState>.Failure("Roster unit ids must be unique.");
            }

            return Restore(
                stairwayCount,
                1,
                floorCount,
                1,
                0,
                false,
                members,
                new List<ExpeditionMember>(members),
                new List<string>(),
                new List<string>(),
                new HashSet<int>());
        }

        internal static Result<ExpeditionState> Restore(
            int stairwayCount,
            int stairwayIndex,
            int floorCount,
            int floorIndex,
            int retreatCount,
            bool isComplete,
            List<ExpeditionMember> roster,
            List<ExpeditionMember> initialRoster,
            List<string> missingIds,
            List<string> fallenIds,
            HashSet<int> shortcutStairways)
        {
            if (stairwayCount < 1)
            {
                return Result<ExpeditionState>.Failure("Stairway count must be at least one.");
            }

            if (floorCount < 1)
            {
                return Result<ExpeditionState>.Failure("Floor count must be at least one.");
            }

            if (stairwayIndex < 1 || stairwayIndex > stairwayCount)
            {
                return Result<ExpeditionState>.Failure("Stairway index is out of range.");
            }

            if (floorIndex < 1 || floorIndex > floorCount)
            {
                return Result<ExpeditionState>.Failure("Floor index is out of range.");
            }

            if (retreatCount < 0)
            {
                return Result<ExpeditionState>.Failure("Retreat count cannot be negative.");
            }

            if (roster == null || initialRoster == null || missingIds == null || fallenIds == null || shortcutStairways == null)
            {
                return Result<ExpeditionState>.Failure("Expedition state collections are required.");
            }

            return Result<ExpeditionState>.Success(new ExpeditionState(
                stairwayCount,
                stairwayIndex,
                floorCount,
                floorIndex,
                retreatCount,
                isComplete,
                roster,
                initialRoster,
                missingIds,
                fallenIds,
                shortcutStairways));
        }
    }
}
