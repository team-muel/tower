# Tower — Agent Working Contract

이 레포는 에이전트 교대 작업자 모델(Codex / Claude / Hermes)로 개발된다. 오케스트레이션은 Claude(Cowork), 구현 담당은 주로 Codex.

## 설계 정본 (코드 작성 전 필독)

로컬 vault: `C:\Users\fancy\Personal Agent Memory\40_Projects\Tower\`
읽기 순서: `00 Current Status.md` → `11 Design Pillars.md` → `13 Combat Model v0.1.md` → `20 Vertical Slice Plan.md`

- 게임: 싱글플레이 3D 그리드 턴제 크롤러. "회귀자가 파티를 꾸려 층계 단위로 탑을 정복한다."
- 전투: BG3식 그리드 + 개별 이니셔티브(속도 기반). 동료는 고유 성향 AI(직접 조작 불가), 회귀자만 수동 + 오더. 능력 태그: 부여/소모/증폭. 행동 연출 5초 상한.
- 루프: 전진(정복=세이브, 사망 확정) / 후퇴(롤백, 사망자 카운트+1 생환, 3후퇴=대회귀).

## 현재 작업: 수직 슬라이스 (vault `20 Vertical Slice Plan.md`)

태스크 브리프: `docs/tasks/` 폴더. 진행표는 vault 20 문서.

## 규약

1. 태스크 = 브랜치 = PR 1개. **main 직push 금지.** 브랜치명 `task/T<N>-<slug>`, PR 제목 `T<N>: <요약>`.
2. Unity **6000.3.19f1** 고정. 에디터 배치모드 실행 시 env 필수: `ALLUSERSPROFILE=C:\ProgramData`, `TMP=%TEMP%`.
3. asmdef 구조: `Tower.Core`(데이터·규칙, 엔진 비의존 로직 우선) / `Tower.Combat` / `Tower.Gen` / `Tower.UI`. 코어 로직은 MonoBehaviour 밖에서 — 유닛 테스트 가능하게.
4. T3(턴 엔진)·T4(능력 파이프라인)는 유닛 테스트(Unity Test Framework, EditMode) 없이 머지 금지.
5. 하드코딩 금지 원칙: 성향·패시브·능력·태그는 데이터(ScriptableObject) 추가로 확장되게. 스위치문 분기 증식 금지.
6. 커밋: `git -C C:\Users\fancy\Tower ...` 형태로 절대경로 사용 (cwd 신뢰 금지). gpgsign 이슈 시 `-c commit.gpgsign=false`.
7. 시크릿 절대 커밋 금지.
8. 작업 종료 시: PR 링크와 결정 사항을 vault `00 Current Status.md` 진행표에 갱신 (쓰기 불가 환경이면 PR 본문에 상세히).

## 검증

- 컴파일 확인: Unity 배치모드 `-quit -batchmode -projectPath C:\Users\fancy\Tower -logFile -` (라이선스 활성화 전이면 스킵하고 PR에 명시).
- EditMode 테스트: `-runTests -testPlatform EditMode`.
