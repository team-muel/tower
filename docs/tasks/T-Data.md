# T-Data — Unity 정적 데이터 로더 + 로드타임 검증 (CSV -> DataCatalog)

근거: vault `50 Data Layer, Type-Index, and Dispatch Assembly` (§1 토폴로지·§3 조립). 스키마 원천 = `tools/DataSchema/Records/GameRecords.cs`. 파이프라인 = `tools/data/README.md`. (Sdp CLI는 빌드타임 추출용; 이 태스크는 Unity 런타임 쪽.)

## 목표
CSV(추출 결과) → Unity 런타임 `DataCatalog`(id로 조회) + **로드타임 재검증(2차 게이트)**. 나쁜 데이터는 명확한 예외/에러로 즉시 실패(런타임 조용한 폭발 금지).

## v0 규칙 (바꾸지 말 것)
1. **Tower.Core 순수** 유지. Physics/Time.timeScale/외부패키지/ manifest 변경 금지. Sdp.dll 참조 금지.
2. CSV는 `Assets/_Tower/Data/Generated/<sheet>.csv` (Unity가 TextAsset로 임포트 → `.text` 파싱). 파일 IO 대신 TextAsset 로드.
3. 컬럼 헤더 = 아래 필드명. 리스트(defaultAbilities)는 한 컬럼에 ";" 구분.

## 산출물
1. **생성 CSV 6종** (`Assets/_Tower/Data/Generated/`), 아래 데이터로. 헤더=필드명.
2. **Tower.Data asmdef** (또는 Core 내 순수 클래스): 레코드 미러 타입(POCO) + `DataCatalog`(Immutable/ReadOnly dict by id; Abilities/Characters/Marks/Passives/Items/DropTables).
3. **CsvTable 파서**(따옴표·쉼표 안전) + **DataCatalog.Load(TextAsset[])**.
4. **로드타임 검증**: 필수 빈값, int/float/bool 파싱, enum 멤버십(AbilityTag/DispositionType/AbilityTargetType/ResourceScope/RewardType), id 유일성, **FK**: Abilities.targetMark∈Marks.id(공란 허용), Characters.passive∈Passives.id(공란 허용), Characters.defaultAbilities 각 항목∈Abilities.id. 위반 시 수집 후 한 번에 명확한 예외/에러(시트·행·컬럼 명시).
5. **EditMode 테스트**: 정상 로드 성공(개수 검증), 그리고 bad enum/빈 필수/깨진 FK 각각이 검증에서 걸리는지. 기존 360 유지.

## 데이터 (정본 — 이대로 CSV 생성)
Marks(id,displayName,durationTurns,stackable):
`M_Frost,Frost,2,true` / `M_Burn,Burn,3,true`

Passives(id,displayName,effectHookKey):
`P_Reckless,Reckless,passive.reckless` / `P_Guardian,Guardian,passive.guardian` / `P_Tempo,Tempo,passive.tempo`

Abilities(id,displayName,tag,targetMark,range,cost,basePower,amplificationMultiplier,targetType,cooldownRounds):
```
A_FrostBolt,Frost Bolt,Apply,M_Frost,5,1,6,1,Enemy,0
A_BurningBrand,Burning Brand,Apply,M_Burn,4,1,5,1,Enemy,0
A_ChillTrap,Chill Trap,Apply,M_Frost,3,2,4,1,Cell,0
A_ShatterFrost,Shatter Frost,Consume,M_Frost,4,2,12,1,Enemy,2
A_IgniteAsh,Ignite Ash,Consume,M_Burn,4,2,11,1,Enemy,0
A_ThermalBreak,Thermal Break,Consume,M_Burn,1,2,13,1,Enemy,2
A_FocusStrike,Focus Strike,Amplify,,1,1,8,1.5,Enemy,1
A_GuardedSurge,Guarded Surge,Amplify,,2,1,3,1.35,Ally,0
A_QuickSlash,Quick Slash,None,,1,0,7,1,Enemy,0
A_HoldLine,Hold Line,None,,1,0,2,1,Ally,0
```
Characters(id,displayName,maxHp,attack,defense,speed,disposition,passive,defaultAbilities,isReturner,chainLocked,isPreset,factionId):
```
C_Returner,Returner,34,8,5,11,Protective,P_Tempo,A_QuickSlash;A_FrostBolt,true,false,false,0
C_EmberVanguard,Ember Vanguard,38,10,6,8,Aggressive,P_Reckless,A_BurningBrand;A_ThermalBreak,false,false,false,0
C_GlassBreaker,Glass Breaker,28,12,3,12,Aggressive,P_Tempo,A_FocusStrike;A_ShatterFrost,false,false,false,0
C_WardBearer,Ward Bearer,44,6,9,7,Protective,P_Guardian,A_HoldLine;A_GuardedSurge,false,false,false,0
```
Items(id,displayName,resourceScope,power,stackMax,description):
`I_Poultice,Poultice,Temporary,10,3,원정 중 HP 소폭 회복(예시)` / `I_ShortcutKey,Shortcut Key,Permanent,0,1,숏컷 확보 토큰(예시)`

DropTables(tableId,entryId,weight,rewardType,refId,minDepth,maxDepth):
```
DT_FloorReward,heal_small,40,Heal,,0,
DT_FloorReward,resource,35,Resource,,0,
DT_FloorReward,ability,15,Ability,,2,
DT_FloorReward,shortcut,10,Shortcut,,3,
```

## 완료 정의
- 배치모드 EditMode `result="Passed"`(≥360 + 신규), 0 error. Windows64 빌드 성공.
- 결정성/QA 불변. 브랜치 `task/T-Data`, main 직push 금지.

## 함정 (Windows 호스트)
- `git -C C:\Users\fancy\Tower`. gpgsign 실패 시 `-c commit.gpgsign=false`. 산출 .cs/.csv만 커밋(.meta는 오케스트레이터 생성). Unity 실행 금지(게이트는 오케스트레이터). 최신 main에서 분기.
