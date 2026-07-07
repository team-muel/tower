namespace Tower.Core
{
    // The semantic UX unit an anchor represents. Mirrors DE object taxonomy
    // (33 Reference §4): door/portal, container, orb-bearing shrine, grave,
    // trap, plain inspectable, resource node. Kept as a flat enum so the
    // interaction engine and QA harness stay data-driven and deterministic.
    public enum InteractableKind
    {
        // 문: expedition/camp portal anchor. Display source is T25 PortalDef
        // (referenced by PortalId), not re-implemented here.
        Portal = 0,

        // 컨테이너: lootable / inspectable environment object.
        Chest = 1,

        // 오브(orb) bearer: a cognition marker point (memory/companion cue).
        Shrine = 2,

        // 묘비: camp grave — confirmed death / missing / cognizer accrual.
        Grave = 3,

        // 함정: terrain hazard revealed before combat.
        Trap = 4,

        // Plain inspectable environment anchor (no loot, pure affordance).
        Inspect = 5,

        // Resource node (heal / consumable / trace pickup).
        Resource = 6,
    }
}
