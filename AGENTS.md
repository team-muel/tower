# Tower — Agent Working Contract

이 레포는 에이전트 교대 작업자 모델(Codex / Claude / Hermes)로 개발된다. 구현과 문서화는 태스크 단위 브랜치·PR로 수행한다.

## 설계·결정 조회 계약

로컬 통합 기록: `C:\Users\fancy\Personal Agent Memory\40_Projects\Tower\`

현재 읽기 순서:

1. `00 Current Status.md`
2. `11 Design Pillars.md`
3. `13 Combat Model v0.1.md`의 재정본 주의와 §8
4. 현재 태스크와 관련된 최신 설계 문서·`docs/tasks/T<N>.md`
5. `20 Vertical Slice Plan.md`은 초기 슬라이스 이력으로만 사용

`AGENTS.md`의 게임 요약은 탐색을 돕는 미러이며 독립 정본이 아니다. 날짜가 명확한 최신 소유자 결정이나 위 문서와 충돌하면, 오래된 요약을 근거로 새 입력을 자동으로 “비정본” 처리하지 않는다.

2026-08-18 대조 기준:

- 현행 구현 계열은 싱글플레이 3D Tower 크롤러다.
- 로컬 전투 정본은 2026-07-13 `13 Combat Model v0.1.md` §8의 **연속공간·실시간 능동 오토배틀러**다. 플레이어는 직접 조작하고, 동료는 자율 전투하며, 좌Shift 불릿타임은 파티 명령 창이다. `3D 그리드 턴제`는 폐기된 옛 요약이다.
- 전진/후퇴/대회귀의 런 루프와 데이터 구동 능력·성향 원칙은 유지된다.
- T77 AA 액션 탐색은 **소유자 직접 입력 / 탐색 / vault 대조 완료 / 미착수**다. 현행 Tower의 장기 피벗인지 별도 신작인지는 아직 결정하지 않았다.

## 모바일 Work 착수

Notion `Creations / Tower`는 모바일 인입·결정 캡처·작업 지시를 위한 제어면이다. 상세 규칙과 작업 패킷은 `docs/process/mobile-work-protocol.md`를 따른다.

- 로컬 vault에 접근할 수 없다는 사실만으로 모바일 소유자 입력을 무효화하지 않는다.
- 접근하지 못한 vault를 읽었다고 주장하지 않고 상태를 **동기화 대기**로 남긴다.
- 모바일에서도 문서, 태스크 브리프, 격리 스파이크와 Draft PR은 착수할 수 있다.
- 제품 정체성 충돌, 파괴적 마이그레이션, `main` 머지는 로컬 정합 전 보류한다.
- “비정본” 단일 라벨 대신 출처 권한 / 설계 성숙도 / 동기화 / 구현의 네 축을 기록한다.

## 현재 작업 조회

현재 작업은 특정 고정 번호가 아니라 다음 순서로 확인한다.

1. GitHub의 관련 브랜치·PR
2. 해당 `docs/tasks/T<N>.md`
3. vault `00 Current Status.md`
4. 모바일 착수라면 연결된 Notion Tower 작업 패킷

## 규약

1. 태스크 = 브랜치 = PR 1개. **main 직push 금지.** 브랜치명 `task/T<N>-<slug>`, PR 제목 `T<N>: <요약>`.
2. 태스크 번호는 만들기 전에 GitHub 브랜치와 PR을 조회해 충돌을 피한다.
3. Unity **6000.3.19f1** 고정. 에디터 배치모드 실행 시 env 필수: `ALLUSERSPROFILE=C:\ProgramData`, `TMP=%TEMP%`.
4. asmdef 구조: `Tower.Core`(데이터·규칙, 엔진 비의존 로직 우선) / `Tower.Combat` / `Tower.Gen` / `Tower.UI`. 코어 로직은 MonoBehaviour 밖에서 유닛 테스트 가능하게 둔다.
5. T3(턴 엔진)·T4(능력 파이프라인)의 기존 테스트 계약은 보존한다. 새 런타임 변경은 영향 범위에 맞는 EditMode·빌드·플레이 증거 없이 머지하지 않는다.
6. 하드코딩 금지: 성향·패시브·능력·태그는 데이터(ScriptableObject) 추가로 확장되게 한다. 스위치문 분기 증식을 피한다.
7. 커밋: `git -C C:\Users\fancy\Tower ...` 형태로 절대경로 사용(cwd 신뢰 금지). gpgsign 이슈 시 `-c commit.gpgsign=false`.
8. 시크릿 절대 커밋 금지.
9. 작업 종료 시 PR 링크·결정·검증 결과를 vault `00 Current Status.md`에 갱신한다. 로컬 쓰기가 불가능하면 PR과 Notion 작업 패킷에 **동기화 대기**로 기록하고 다음 데스크톱 세션에서 수렴한다.

## 검증

- 컴파일 확인: Unity 배치모드 `-quit -batchmode -projectPath C:\Users\fancy\Tower -logFile -` (라이선스 활성화 전이면 스킵하고 PR에 명시).
- EditMode 테스트: `-runTests -testPlatform EditMode`.
- 사용자 판정이 필요한 시각·조작 작업은 실제 Windows 빌드와 캡처 또는 플레이 로그를 우선한다.
- 문서 전용 변경은 변경 문서를 GitHub·Notion·vault에서 다시 읽고 링크와 상태 분류를 확인한다.
