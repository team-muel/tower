namespace Tower.Core
{
    // An orb is a cognition marker, not dialogue (33 Reference §0/§4). Each cue
    // is one of four kinds that drive the QA orb filter (기본/인지/위험/전리품).
    public enum OrbCueKind
    {
        // 회귀자 기억: memory only the regressor perceives.
        Memory = 0,

        // 동료 경고/제안: companion disposition AI marker.
        Companion = 1,

        // 지형 위험: pre-combat hazard tell.
        Hazard = 2,

        // 전리품 흔적: loot / trace marker.
        Loot = 3,
    }

    // QA-facing orb visibility filter. Cognition groups Memory + Companion.
    public enum OrbFilter
    {
        // 기본: show every cue kind.
        All = 0,

        // 인지: Memory + Companion cues.
        Cognition = 1,

        // 위험: Hazard cues.
        Hazard = 2,

        // 전리품: Loot cues.
        Loot = 3,
    }
}
