using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // T23: pure, engine-free mapping from a turn-order snapshot to the ordered
    // ribbon items the initiative HUD renders. Kept in Tower.Core (no
    // UnityEngine dependency) so it is trivially unit testable and never
    // touches game logic — the ribbon is display-only.
    //
    // Inputs mirror what the TurnEngine already exposes:
    //   roundOrder    = TurnEngine.CurrentRoundOrder (ally chain order from T21)
    //   currentUnitId = TurnEngine.CurrentTurn?.UnitId
    //   isAlive       = TurnEngine.IsAlive(unitId)
    //   teamOf        = CombatantRef.Team lookup
    public sealed class InitiativeRibbonItem
    {
        public InitiativeRibbonItem(string unitId, CombatTeam team, int orderIndex, bool isCurrent, bool isNext, bool isDead)
        {
            UnitId = unitId;
            Team = team;
            OrderIndex = orderIndex;
            IsCurrent = isCurrent;
            IsNext = isNext;
            IsDead = isDead;
        }

        public string UnitId { get; }
        public CombatTeam Team { get; }

        // Position within the source round order (stable across a round).
        public int OrderIndex { get; }

        // The active actor: strong highlight (pulse/scale/outline).
        public bool IsCurrent { get; }

        // The next living actor after the current one: light highlight.
        public bool IsNext { get; }

        // Defeated this round: dimmed (or filtered out entirely).
        public bool IsDead { get; }
    }

    public static class InitiativeRibbonModel
    {
        /// <summary>
        /// Builds the ordered ribbon items for the given turn-order snapshot.
        /// The "next" actor is the first living unit that follows the current
        /// one in round order, wrapping to the front of the round if the
        /// current unit is last (so handoff still reads correctly at a round
        /// boundary). Dead units are flagged but retain their slot so the
        /// caller can either dim them or drop them.
        /// </summary>
        /// <param name="includeDead">
        /// When false, defeated units are omitted from the result entirely.
        /// When true they are returned with <see cref="InitiativeRibbonItem.IsDead"/>
        /// set so the UI can dim them in place.
        /// </param>
        public static IReadOnlyList<InitiativeRibbonItem> Build(
            IReadOnlyList<string> roundOrder,
            string currentUnitId,
            Func<string, bool> isAlive,
            Func<string, CombatTeam> teamOf,
            bool includeDead = true)
        {
            var items = new List<InitiativeRibbonItem>();
            if (roundOrder == null || roundOrder.Count == 0)
            {
                return items;
            }

            var alive = isAlive ?? (_ => true);
            var team = teamOf ?? (_ => CombatTeam.Player);

            var nextUnitId = ResolveNextUnitId(roundOrder, currentUnitId, alive);

            for (int index = 0; index < roundOrder.Count; index++)
            {
                var unitId = roundOrder[index];
                if (string.IsNullOrEmpty(unitId))
                {
                    continue;
                }

                var isDead = !alive(unitId);
                if (isDead && !includeDead)
                {
                    continue;
                }

                var isCurrent = !isDead && StringComparer.Ordinal.Equals(unitId, currentUnitId);
                var isNext = !isDead && !isCurrent && StringComparer.Ordinal.Equals(unitId, nextUnitId);

                items.Add(new InitiativeRibbonItem(unitId, team(unitId), index, isCurrent, isNext, isDead));
            }

            return items;
        }

        // First living unit strictly after the current one in round order; if
        // the current unit is last (or unknown), wrap to the first living unit
        // that is not the current one. Returns null when nobody qualifies.
        private static string ResolveNextUnitId(
            IReadOnlyList<string> roundOrder,
            string currentUnitId,
            Func<string, bool> alive)
        {
            var currentIndex = -1;
            for (int index = 0; index < roundOrder.Count; index++)
            {
                if (StringComparer.Ordinal.Equals(roundOrder[index], currentUnitId))
                {
                    currentIndex = index;
                    break;
                }
            }

            var count = roundOrder.Count;
            var start = currentIndex >= 0 ? currentIndex + 1 : 0;
            for (int offset = 0; offset < count; offset++)
            {
                var index = (start + offset) % count;
                var candidate = roundOrder[index];
                if (string.IsNullOrEmpty(candidate))
                {
                    continue;
                }

                if (StringComparer.Ordinal.Equals(candidate, currentUnitId))
                {
                    continue;
                }

                if (alive(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
