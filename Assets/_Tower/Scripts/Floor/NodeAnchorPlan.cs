using System.Collections.Generic;
using Tower.Core;
using UnityEngine;

namespace Tower.Floor
{
    // T41 (M4): one deterministic interaction-anchor placement. Pairs a Core
    // InteractableDef (pure, engine-free) with a world XZ position; the renderer
    // grounds Y onto the terrain height field at spawn time (same convention as
    // ForestProp). Kept a readonly struct so the plan is immutable and cheap.
    public readonly struct PlacedAnchor
    {
        public PlacedAnchor(InteractableDef def, Vector3 position)
        {
            Def = def;
            Position = position;
        }

        public InteractableDef Def { get; }

        // World XZ; Y = field baseline (renderer resamples terrain height).
        public Vector3 Position { get; }

        public InteractableKind Kind => Def != null ? Def.Kind : InteractableKind.Inspect;
    }

    // The full deterministic anchor set for one node. Mirrors ForestContentPlan:
    // identical (seed, nodeId, field, role) always yields the identical list.
    public sealed class NodeAnchorPlan
    {
        public NodeAnchorPlan(IReadOnlyList<PlacedAnchor> anchors)
        {
            Anchors = anchors;
        }

        public IReadOnlyList<PlacedAnchor> Anchors { get; }
    }
}
