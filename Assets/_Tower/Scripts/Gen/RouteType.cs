namespace Tower.Gen
{
    // DD2 갈림길 route 종류. 정적 데이터 어휘 — 구체 내용(조우 테이블·로드이벤트)은 나중에.
    public enum RouteType
    {
        Safe,     // 안전
        Combat,   // 전투
        Hazard,   // 위험
        Special   // 특수
    }
}
