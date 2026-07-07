using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tower.UI
{
    // Pure, UI-free logic for the Loadout action-order chain. The player now
    // reorders the roster by drag-and-drop (the ①~④ badge + ▲▼ buttons of T21
    // were retired), but the underlying model is unchanged: chain position 0..3
    // derives initiative 100/90/80/70, and chainLocked members are excluded
    // from the chain (they never receive an order slot / initiative).
    //
    // Everything here is deterministic and side-effect free so it can be
    // unit-tested without instantiating any uGUI objects. The controller owns
    // the runtime widgets; this owns the rules.
    public static class LoadoutChainModel
    {
        // Chain-derived initiative for allies by chain position (0-based).
        public static readonly IReadOnlyList<int> InitiativeByPosition =
            new[] { 100, 90, 80, 70 };

        // Positions past the authored table share the tail initiative.
        public const int TailInitiative = 70;

        public static int DeriveInitiative(int chainPosition)
        {
            if (chainPosition < 0)
            {
                return 0;
            }

            return chainPosition < InitiativeByPosition.Count
                ? InitiativeByPosition[chainPosition]
                : TailInitiative;
        }

        // Drag-and-drop reorder: pull the member at fromIndex out and reinsert
        // it at toIndex, returning a NEW list (inputs are not mutated). Out of
        // range indices are clamped so a drag past the ends is a no-op-ish move
        // to the nearest slot rather than an exception.
        public static List<string> Reorder(IReadOnlyList<string> order, int fromIndex, int toIndex)
        {
            var list = order == null ? new List<string>() : new List<string>(order);
            if (list.Count == 0)
            {
                return list;
            }

            if (fromIndex < 0 || fromIndex >= list.Count)
            {
                return list;
            }

            var item = list[fromIndex];
            list.RemoveAt(fromIndex);

            int clamped = Mathf.Clamp(toIndex, 0, list.Count);
            list.Insert(clamped, item);
            return list;
        }

        // The chain-eligible members in order (chainLocked members removed).
        // Preserves the relative order of the survivors.
        public static List<string> ChainOrder(IEnumerable<string> order, Func<string, bool> isChainLocked)
        {
            var result = new List<string>();
            if (order == null)
            {
                return result;
            }

            foreach (var id in order)
            {
                if (isChainLocked != null && isChainLocked(id))
                {
                    continue;
                }

                result.Add(id);
            }

            return result;
        }

        // Resolved per-member outcome for a given roster order: chain position,
        // derived initiative and locked flag. Locked members keep ChainPosition
        // -1 and Initiative 0 (excluded). Non-locked members are numbered by
        // their position AMONG the non-locked members, so a locked member in the
        // middle does not consume an order slot.
        public readonly struct ChainAssignment
        {
            public ChainAssignment(string id, bool chainLocked, int chainPosition, int initiative)
            {
                Id = id;
                ChainLocked = chainLocked;
                ChainPosition = chainPosition;
                Initiative = initiative;
            }

            public string Id { get; }
            public bool ChainLocked { get; }
            public int ChainPosition { get; }
            public int Initiative { get; }
        }

        public static List<ChainAssignment> BuildAssignments(
            IReadOnlyList<string> order,
            Func<string, bool> isChainLocked)
        {
            var result = new List<ChainAssignment>();
            if (order == null)
            {
                return result;
            }

            int slot = 0;
            foreach (var id in order)
            {
                bool locked = isChainLocked != null && isChainLocked(id);
                if (locked)
                {
                    result.Add(new ChainAssignment(id, true, -1, 0));
                    continue;
                }

                result.Add(new ChainAssignment(id, false, slot, DeriveInitiative(slot)));
                slot++;
            }

            return result;
        }
    }
}
