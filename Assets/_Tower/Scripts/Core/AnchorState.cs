namespace Tower.Core
{
    // Runtime lifecycle state of an interactable anchor. A single flat enum
    // covers every kind's states so persistence (T20 candidate) and the QA
    // harness can serialize one field. Transitions are guarded per-kind by
    // AnchorRuntime; not every state is legal for every kind.
    public enum AnchorState
    {
        // Neutral start / no special state (e.g. a plain Inspect anchor).
        Idle = 0,

        // 문 locked → unlocked (Portal, Chest).
        Locked = 1,
        Unlocked = 2,

        // Shrine / grave sealed → open/revealed (오브 dormant → revealed).
        Sealed = 3,
        Open = 4,

        // 함정: armed → disarmed | triggered.
        Armed = 5,
        Disarmed = 6,
        Triggered = 7,

        // Container: unlooted → looted.
        Unlooted = 8,
        Looted = 9,

        // Orb / revealer: dormant → revealed.
        Dormant = 10,
        Revealed = 11,
    }
}
