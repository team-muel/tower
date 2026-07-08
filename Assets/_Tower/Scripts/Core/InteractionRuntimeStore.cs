using System;
using System.Collections.Generic;

namespace Tower.Core
{
    // Runtime-only persistence model for world interaction anchors. It keeps the
    // data shape serializable so SaveGame can adopt the same snapshots later.
    public sealed class InteractionRuntimeStore
    {
        private readonly Dictionary<string, AnchorRuntimeSnapshot> snapshots =
            new Dictionary<string, AnchorRuntimeSnapshot>(StringComparer.Ordinal);

        public int Count => snapshots.Count;

        public void Clear()
        {
            snapshots.Clear();
        }

        public Result Remember(AnchorRuntimeSnapshot snapshot)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.anchorId))
            {
                return Result.Failure("Anchor snapshot id is required.");
            }

            snapshots[snapshot.anchorId] = new AnchorRuntimeSnapshot(
                snapshot.anchorId,
                snapshot.kind,
                snapshot.state,
                snapshot.usesRemaining);
            return Result.Success();
        }

        public void Capture(InteractableRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            foreach (AnchorRuntimeSnapshot snapshot in registry.CaptureRuntimeState())
            {
                Remember(snapshot);
            }
        }

        public Result<AnchorRuntime> RuntimeFor(InteractableDef def)
        {
            if (def == null)
            {
                return Result<AnchorRuntime>.Failure("Interactable def is required.");
            }

            if (!snapshots.TryGetValue(def.Id, out AnchorRuntimeSnapshot snapshot))
            {
                return Result<AnchorRuntime>.Success(AnchorRuntime.CreateDefault(def.Kind, def.MaxUses));
            }

            if (snapshot.kind != def.Kind)
            {
                return Result<AnchorRuntime>.Failure(
                    $"Snapshot kind '{snapshot.kind}' does not match def kind '{def.Kind}'.");
            }

            return AnchorRuntime.Restore(def.Kind, snapshot.state, snapshot.usesRemaining);
        }

        public IReadOnlyList<AnchorRuntimeSnapshot> ToSnapshots()
        {
            var result = new List<AnchorRuntimeSnapshot>(snapshots.Count);
            foreach (AnchorRuntimeSnapshot snapshot in snapshots.Values)
            {
                result.Add(new AnchorRuntimeSnapshot(
                    snapshot.anchorId,
                    snapshot.kind,
                    snapshot.state,
                    snapshot.usesRemaining));
            }

            result.Sort((a, b) => StringComparer.Ordinal.Compare(a.anchorId, b.anchorId));
            return result;
        }
    }
}
