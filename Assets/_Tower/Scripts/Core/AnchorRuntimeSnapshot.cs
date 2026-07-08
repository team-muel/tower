using System;

namespace Tower.Core
{
    [Serializable]
    public sealed class AnchorRuntimeSnapshot
    {
        public string anchorId;
        public InteractableKind kind;
        public AnchorState state;
        public int usesRemaining;

        public AnchorRuntimeSnapshot()
        {
        }

        public AnchorRuntimeSnapshot(string anchorId, InteractableKind kind, AnchorState state, int usesRemaining)
        {
            this.anchorId = anchorId ?? string.Empty;
            this.kind = kind;
            this.state = state;
            this.usesRemaining = usesRemaining;
        }

        public static AnchorRuntimeSnapshot From(string anchorId, AnchorRuntime runtime)
        {
            return runtime == null
                ? null
                : new AnchorRuntimeSnapshot(anchorId, runtime.Kind, runtime.State, runtime.UsesRemaining);
        }
    }
}
