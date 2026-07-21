# Framework Runtime · Save Contract 통합 조사

## 1. 조사 기준

- 브랜치: `docs/framework/runtime-save-contract-research`
- 최신 commit SHA: `045042f81c2e06f91f83670c0e6a03f38577d30f`
- 조사 일자: 2026-07-21
- 조사 방식: C# 선언·호출·이벤트 구독 검색, SaveData/정규화 구현 확인, 정책·마일스톤·handoff 교차 확인
- 조사 범위:
  - `Assets/_Project/11.CoreServices/Scripts/`
  - `Assets/_Project/01.Core/`, `03.Economy/`, `05.UI/`
  - `Assets/_Project/02.Data/`의 Shared Game Data 관련 정의
  - `Docs/Personal_Documents/CSU/SaveDataPolicy/`
  - `Docs/Planning_Milestone/`
  - `Docs/Personal_Documents/CSU/Handoff/`
- 상태 표현:
  - **현재 구현**: 현재 commit의 production 코드에서 확인됨
  - **문서상 목표**: 정책·기획 문서에 있으나 현재 코드 구현과 별도
  - **불일치**: 코드와 문서 또는 문서 상호 간 계약이 다름
  - **확인되지 않음**: 저장소 근거만으로 확정 불가
  - **후속 결정 필요**: 구현 전에 계약 선택이 필요
- 수정 범위: 이 조사 보고서만 생성했다. C#, Scene, Prefab, ScriptableObject, 기존 계약 문서는 수정하지 않았다.

> 주의: `Docs/Personal_Documents/CSU/SaveDataPolicy/Framework_API_Event_Inventory.md`는 같은 날짜의 이전 코드 상태를 기록한다. 현재 코드에는 그 문서에서 Contract Only로 적은 `ClaimSettlement(caravanId, tradeId)`와 Caravan-aware `TradeSettlementReady`가 이미 존재하므로, 아래 표는 현재 commit 코드를 우선 근거로 삼는다.

## 2. 핵심 요약

### 현재 구현된 것

1. `ISaveService.Save(SaveData)`는 `SaveResult`를 반환하고 `JsonSaveService`는 실패를 `InvalidData`, `SerializationFailed`, `WriteFailed`, `Unknown`으로 분류한다.
2. SaveData numeric schema는 version 6이며 `caravans[]`, `tradeProgressEntries[]`, `pendingSettlements[]`, `selectedCaravanId`가 직렬화 source of truth이다.
3. `ClaimSettlement(string caravanId, string tradeId)`와 `ClaimSettlementResult`가 구현되어 exact pending 항목을 대상으로 원자 claim/rollback을 수행한다.
4. `FrameworkEvents.TradeSettlementReady`는 현재 `(caravanId, tradeId, JourneyResultData)` 시그니처이다.
5. 구조 대출 발급·상환 command와 저장 성공 후 이벤트가 구현되어 있다.
6. 도시·경로 해금 저장 목록과 World Map의 해금 조회는 구현되어 있다.
7. 건물 건설 command는 `displayName`을 식별자로 사용하고 재료·레벨을 저장 실패 시 rollback한다.

### 부분 구현된 것

1. version 6 컬렉션은 복수 Caravan 상태를 저장할 수 있지만 coordinator, departure, progress recorder, 화면 router는 선택 Caravan/단일 runtime 전제를 유지한다.
2. 정산 claim은 명시 ID 계약으로 전환됐지만 settlement 생성·복구·ForceComplete·화면 상태는 여전히 selected compatibility getter와 단일 cache를 사용한다.
3. 정산 생성 시 `SaveResult`를 검사하지 않고 바로 `TradeSettlementReady`와 Settlement 화면 전환을 수행한다.
4. 저장 성공 여부를 검사하는 중요 command와, 반환값을 무시하는 lifecycle/debug 호출부가 혼재한다.
5. Building 비용 계산/command/scene registry는 있으나 canonical stable `buildingId`와 SharedGameData Building provider는 없다.

### 문서 계약만 존재하는 것

- Preparation command/event(`UpdatePreparation`, `UpdatePurchasePreview`, `CancelPreparation`, `PreparationChanged`)
- explicit-ID Depart command와 public `FinalizeSettlement(caravanId, tradeId)`
- Dirty tracking, retry/queue
- standalone growth purchase, wagon repair/destruction, cargo transfer command/event
- Investment Quest 모델·정의·command·UI·SaveData completion collection·event
- stable `buildingId + level` 저장 및 Building Shared Data 조회

### 주요 불일치

1. 과거 API inventory는 Claim/event를 Contract Only로 기록하지만 현재 코드는 구현 완료 상태이다.
2. 정책은 “저장 성공 후 committed event”를 요구하지만 현재 settlement finalize 내부 경로는 SaveResult를 무시한다.
3. 정책은 duplicate Caravan ID를 validation error로 남기라고 하지만 `NormalizeData`는 duplicate Caravan ID를 새 GUID로 교체한다.
4. 최신 목표 정책은 건물 재료를 home inventory에서만 쓰도록 하나, 현재 command와 별도 2026-07-20 M0 계약은 selected Caravan cargo를 직접 소비한다.
5. 최신 Investment Quest 정책은 일시불·즉시 보상·별도 Claim 없음이지만 `0720_Progression_Economy_M0_Contract.md`의 이전 기부/투자 절은 누적 progress와 reward claim 필드를 제안한다.

### 후속 결정이 필요한 것

- multi-active runtime cache/iteration/save batching/exception isolation 계약
- settlement finalization의 SaveResult 실패 처리와 이벤트 발행 시점
- Investment Quest completion DTO의 root와 collection key/중복 규칙
- building `displayName → buildingId` 매핑 원천, 구 JSON 변환 또는 reset 정책
- Investment와 Building 변경을 같은 numeric version 변경으로 묶을지 여부

## 3. Framework API/Event 조사표

### 3.1 Save

| 이름 | 종류 | 현재 시그니처·위치 | 실제 진입/호출 | 변경·저장·결과 | Multi/문서 차이 |
|---|---|---|---|---|---|
| Has save | Query | `bool ISaveService.HasSaveData()`; `ISaveService.cs`, `JsonSaveService.cs` | `FrameworkRoot.InitializeServices` | 파일 존재만 조회 | 동일 목표 |
| New data | Query/Factory | `SaveData CreateNewGameData()` | StartNewGame, load fallback | v6 DTO 생성·Normalize | 동일 목표 |
| Load | Query/Recovery | `SaveData Load()` | Continue, loading fallback, root init | v5만 v6으로 변환 후 즉시 저장; 그 외 unsupported/parse 실패는 새 게임 반환 | target policy의 “visible reject/preserve”보다 실제 복구가 더 파괴적 |
| Save | Command/Result | `SaveResult Save(SaveData data)` | FrameworkRoot, TradeStart, Coordinator, loan, building, debug | Normalize, `version=6`, UTC ticks 갱신, JSON write | API는 구현됨 |
| ResetSaveData | Command | `void ResetSaveData()` | `TitleSceneController.ResetSaveData()` 경유 | 파일 삭제; Result 없음 | 문서 목표에 별도 Result 정의 없음 |
| SaveResult | Result | `SaveResult { Succeeded, FailureReason, Message, FailedDataCategory }`; `ISaveService.cs` | 중요 command에서 검사 | write 완료만 성공 | 구현됨 |
| MarkDirty / dirty query | Missing candidate | production surface 검색 결과 없음 | 호출자 없음 | 없음 | `Framework_Command_Event_Contract.md`의 목표만 존재 |
| 즉시 저장 | Behavior | TradeStart, Claim, RescueLoan, Building command | 각 command 내부 | 대부분 SaveResult 검사 및 rollback | settlement finalize는 예외 |
| 저장 재시도/queue | Missing candidate | retry/queue production 타입·API 없음 | 없음 | 없음 | 정책/후속 branch 제안만 존재 |

SaveResult 확인 여부:

- **확인함**: `TradeStartService.TryStartTrade`, `RescueLoanCommandService.IssueRescueLoan/RepayRescueLoan`, `TradeProgressCoordinator.ClaimSettlement`, legacy claim 본문, `CaravanBuildingConstructionCommand.Execute`.
- **확인하지 않음**: `FrameworkRoot.StartNewGame`, `ExitGame`, `ReturnToTitle`, `TradeProgressCoordinator.CheckProgressAndCompletion`의 중간 save, `ApplyOfflineProgressOnLoad`의 중간 save, `SettleActiveTrade`, `FrameworkDebugCommands.ForceSeason/ForceDisaster`, debug harness save.
- **실제 위험**: `SettleActiveTrade`는 `saveService?.Save(saveData)` 반환값과 무관하게 `TradeSettlementReady`를 raise하고 Settlement 화면을 요청한다.

### 3.2 Preparation

| 이름 | 종류 | 현재 상태 근거 | Save/ID | 문서 목표 |
|---|---|---|---|---|
| UpdatePreparation | Command | 해당 production 메서드 없음 | 없음 | `UpdatePreparation(caravanId, patch)`, 성공 시 Dirty |
| UpdatePurchasePreview | Command | 해당 production 메서드 없음 | 없음 | `UpdatePurchasePreview(caravanId, preview)` |
| CancelPreparation | Command | 해당 production 메서드 없음 | 없음 | `CancelPreparation(caravanId)` |
| PreparationChanged | Event | `FrameworkEvents` 및 전체 C#에서 해당 framework event 없음 | 없음 | ID-bearing committed/dirty event |
| Trade prepare commit | Compatibility/Internal | `FrameworkTradePrepareCommitStore.TryStage/TryGet/TryComplete/Rollback`; key는 `tradeId` | SaveData의 단일 `tradePreparationCommit` | per-Caravan reusable preparation 목표와 다름 |

현재 UI에는 `TradePrepareFlowController`, `TradePrepareRuntimeContextProvider` 등의 준비 흐름이 있으나 위 Framework command/event 계약과 동일한 공개 API로 확인되지는 않는다.

추가 호환 제약: runtime `TradePrepareCommitData`에는 `caravanId`가 있지만 현재 `TradePreparationCommitSaveData`/`FrameworkTradePrepareCommitStore`의 저장 copy에는 caravanId가 없다. 따라서 commit persistence도 tradeId 중심 단일 구조이다.

### 3.3 Trade

| 이름 | 종류 | 현재 시그니처·위치 | 실제 호출자 | SaveData/SaveResult | Multi/호환 의존 |
|---|---|---|---|---|---|
| TryStartTrade | Command/Compatibility | `DepartureValidationResult TryStartTrade(CaravanData caravan, float distanceKm, string tradeId, string routeId, bool saveImmediately=true)`; `TradeStartService.cs` | `TradePreparePanel` direct path, `TradePrepareRuntimeContextProvider`→flow/start adapter path, debug harness, Editor E2E | selected `saveData.tradeProgress`, `saveData.caravan`; 즉시 save 시 결과 검사·snapshot rollback | explicit `caravanId` 없음, selected compatibility getter 의존 |
| Depart | Contract Only candidate | production `Depart(caravanId, request)` 없음 | 없음 | 없음 | 문서 목표 |
| Check progress | Command-like runtime | `bool CheckProgressAndCompletion(bool saveProgress=true)` | `FrameworkRoot.Update`가 0.2 unscaled seconds마다 `false`로 호출; debug/test | selected progress/caravan 갱신; 도착 시 settle | 단일 selected entry |
| Offline progress | Recovery command | `bool ApplyOfflineProgressOnLoad(SaveData saveData=null)` | `CompleteLoadingAndEnterGame`, tests | selected Traveling만 계산·save | 복수 entry 순회 없음 |
| ForceComplete | Compatibility/Debug command | `void ForceCompleteActiveTrade()` | `CompleteTradeRequested` 구독, debug/test | selected active trade settle | caravanId/tradeId 인자 없음 |
| Trade state query | Query | `TryGetMapProgress(out TradeMapProgressSnapshot)`; `SaveDataLookup.TryGetTradeProgress(data, caravanId, out ...)` | WorldMapPresenter/coordinator 내부 | query만 수행 | 공개 map query는 selected compatibility progress 사용 |
| TradeOfflineCompleted | Event | `Action<string>`; `FrameworkEvents.cs` | `ApplyOfflineProgressOnLoad`에서 settle 성공 후 raise | tradeId만 전달 | caravanId 없음 |
| CompleteTradeRequested | Event/Debug request | `Action`; FrameworkEvents | coordinator constructor가 `ForceCompleteActiveTrade` 구독 | ID 없음 | 단일 active 전용 |

`JourneyRunner` 자체는 static stateless API이며 전달받은 각 `CaravanData` 인스턴스만 변경한다. 따라서 서로 다른 인스턴스를 순차 처리할 수 있는 형태지만, collection orchestration·동시성·save batching은 제공하지 않는다.

### 3.4 Settlement

| 이름 | 종류 | 현재 시그니처·위치 | 실제 호출/구독 | Save·결과 | Multi/문서 차이 |
|---|---|---|---|---|---|
| FinalizeSettlement | Contract Only candidate | public `FinalizeSettlement(caravanId, tradeId)` 없음; private `SettleActiveTrade(SaveData, CaravanData)` 존재 | Check/Offline/ForceComplete 내부 | pending 저장 후 SaveResult 무시 | selected 단일 경로 |
| ClaimSettlement | Command/Result | `ClaimSettlementResult ClaimSettlement(string caravanId, string tradeId)` | SettlementUiBridge, debug harness, E2E | exact caravan/progress/pending 검증, snapshot, economy/core/route 적용, save 확인, 실패 rollback | 명시 ID 구현; 내부 staging 중 selectedCaravanId를 임시 전환하는 호환 의존 존재 |
| ClaimSettlementAndReset | Deprecated compatibility | `[Obsolete] bool TradeProgressCoordinator.ClaimSettlementAndReset()` | legacy/debug 가능 | selected pending을 찾아 새 Result API에 위임 | selected 의존 |
| Bridge claim | Compatibility/UI | `bool SettlementUiBridge.ClaimSettlementAndReset()` | `SettlementUiDataAdapter.OnClickClaimSettlement()` | bridge cache `(caravanId, tradeId)`로 Result API 호출; bool로 축약 | bridge public 이름은 legacy 유지 |
| Pending query | Query | `SettlementUiBridge.TryGetPendingSettlement(out caravanId, out tradeId, out result)` | SettlementUiDataAdapter | runtime shared reference 반환 | cache는 1개 |
| TradeSettlementReady | Event | `Action<string,string,JourneyResultData>` | raise: settle 및 restore; subscriber: SettlementUiBridge | event 자체는 SaveResult 없음 | canonical ID payload 구현됨 |
| Bridge SettlementReady | Compatibility event | `Action<string,JourneyResultData>` | SettlementUiBridge가 raise, SettlementUiDataAdapter가 구독 | caravanId를 다시 제거 | UI 내부 compatibility surface |

`ClaimSettlementResult`:

- `Succeeded`
- `ClaimSettlementFailureReason`: invalid IDs, not found, ambiguous pending, trade mismatch, invalid state, already claimed, invalid result, Town/Economy/Core 실패, Save 실패, rollback 실패
- `SaveResult`: save를 시도한 실패 또는 성공 상세

공개 facade 불일치: `FrameworkRoot`는 `TradeProgressCoordinator`와 `SettlementUiBridge` property를 노출하지만 `FrameworkRoot.ClaimSettlementAndReset()` 메서드는 없다. `Handoff/07-21_atomic_claim_town_routing_economy_handoff.md` §3.1의 해당 facade 표기는 현재 코드와 다르다.

### 3.5 Progression

| 기능 | 현재 구현 근거 | 공개 command/event | 저장·결과 | 문서 목표와 차이 |
|---|---|---|---|---|
| 성장 구매 | Economy calculator/result와 settlement M1 경로에 `GrowthPurchaseResult` 존재; standalone Framework command 없음 | 없음 | claim mapper가 성공 결과의 growth level을 적용 가능 | 별도 즉시 저장 command/event 목표 미구현 |
| 마차 수리 | calculator/command production surface 미확인 | 없음 | 없음 | 목표만 존재 |
| 건물 건설·업그레이드 | `CaravanBuildingConstructionCommand.Execute(...)` + `BuildingCostCatalog/Calculator` + `VillageBuildingRegistry` | `SaveResult` command는 있으나 FrameworkRoot facade/event 없음; command의 실제 호출은 Editor tests에서만 확인 | command는 selected Caravan cargo 차감 + `displayName` level upsert + save 실패 rollback | 실제 UI `BuildingAddPopup`은 command가 아니라 `VillageBuildingRegistry.AddOrUpgrade`를 호출하여 비용 검증·즉시 Save를 우회; stable ID/home inventory 목표와도 다름 |
| 투자 퀘스트 | runtime 모델/definition/command/UI/Save DTO 없음 | 없음 | unlock list만 별도 존재 | 계약·마일스톤만 존재 |
| 기부 | production command/Save DTO 없음; `TownData.CanContribute/MaximumContributionLimit`가 SharedTownDefinition으로 매핑되고 `TownInfoPopup`에 contribution 표시가 남아 있음 | 없음 | Save/Investment 연동 없음 | 공식 범위에서 제거됐으나 legacy data/UI 잔존 |
| 마차 파괴 | JourneyRunner가 durability 0을 `WagonBroken` 실패로 표시하지만 owned wagon 제거/적재 손실 command 미확인 | 없음 | 일반 진행 save에 상태 일부 반영 가능 | 원자 destruction snapshot 목표 미구현 |
| 화물 이전 | home inventory DTO는 있으나 Caravan→home command 미확인 | 없음 | 없음 | 최신 정책 목표 미구현 |
| 도시·경로 해금 | `WorldSaveData.unlockedTownIds/unlockedRouteIds`, WorldMapPresenter query 구현 | mutation command/event 없음 | 목록 Normalize만 구현 | Investment completion transaction과 미연결 |

### 3.6 Rescue Loan

| 이름 | 종류 | 시그니처·위치 | 호출/이벤트 | 저장·결과 |
|---|---|---|---|---|
| EvaluateStatus | Query | `RescueStatusResult EvaluateStatus()`; `RescueLoanCommandService` | 외부 UI 호출자는 현재 조사에서 확정되지 않음 | 변경 없음 |
| IssueRescueLoan | Command/Result | `SaveResult IssueRescueLoan()` | save 성공 후 `RescueLoanIssued`, 필요 시 `RescueRestrictedModeEntered` | currency + full loan DTO snapshot, save 실패 rollback |
| RepayRescueLoan | Command/Result | `SaveResult RepayRescueLoan(long amount)` | 성공 후 `RescueLoanRepaid`, 전액 상환 시 `RescueLoanClosed` | currency + loan snapshot, save 실패 rollback |
| Restriction exit | Event | `RescueRestrictedModeExited` | rescue 상태에서 TradeStart save 성공 후 raise | departure와 함께 저장 |

이벤트의 production subscriber는 Editor E2E 외에는 검색에서 확인되지 않았다. Player UI subscriber는 **확인되지 않음**이다.

### 3.7 Scene/UI/Shared Data

| 이벤트/쿼리 | 선언·Raise | 실제 구독자 | 데이터 소유/주의 |
|---|---|---|---|
| SharedGameDataLoaded | `Action<ISharedGameDataProvider>`; FrameworkRoot가 initial data 검증 후 raise | `SharedDataTestListener` 확인 | provider는 read interface |
| LoadCompleted | `Action<SaveData>`; InGame 진입 전 FrameworkRoot, debug 강제 경로 | WorldMapPresenter, TradePrepareRuntimeContextProvider, SharedDataTestListener | mutable SaveData 참조를 그대로 전달 |
| SceneChanged | `Action<string>`; SceneFlowService async load 완료 콜백 | PlayerMainManager | scene name |
| InGameScreenChanged | `Action<InGameScreenState>`; router가 state 설정 후 raise | SettlementUiDataAdapter, WorldMapPresenter, TradePrepareRuntimeContextProvider, tests | forceNotify 시 반복 가능 |
| Shared data query | `TryGetTown/Market/TradeItem/Wagon/DraftAnimal/Route` | Framework/UI | Building/InvestmentQuest 조회 API 없음 |

## 4. Multi-active Coordinator 현재 구조

### 4.1 단일 활성 전제

| 항목 | 현재 구현 | 근거 |
|---|---|---|
| runtime Caravan cache | `private CaravanData activeCaravan`; public `ActiveCaravan`, `SetActiveCaravan` | `TradeProgressCoordinator` |
| settlement cache | `LastSettlementTradeId` + `LastSettlementResult` 단일 쌍 | `TradeProgressCoordinator` |
| selected compatibility | `saveData.caravan`, `tradeProgress`, `pendingSettlement`가 `selectedCaravanId` 대상 하나만 해석 | `SaveData`, `SaveDataLookup` |
| 진행 범위 | `CheckProgressAndCompletion`은 `saveData.tradeProgress` 하나만 검사 | `TradeProgressCoordinator.CheckProgressAndCompletion` |
| 호출 주기 | `FrameworkRoot.Update`, 0.2초 unscaled interval, `saveProgress:false` | `FrameworkRoot.Update` |
| ForceComplete | 인자 없는 active-only method | `ForceCompleteActiveTrade()` |
| 로드 복구 | selected Traveling에 Offline 적용 후 selected Pending 복구 | `CompleteLoadingAndEnterGame` |
| runtime 생성 | `EnsureActiveCaravan`이 selected `saveData.caravan`을 `CaravanSaveDataMapper.ToRuntime`으로 변환 | coordinator |
| departure cache 설정 | 성공한 `TryStartTrade`의 runtime 인스턴스를 `SetActiveCaravan` callback으로 등록 | TradeStartService/FrameworkRoot |

### 4.2 실제 진행 흐름과 식별자

```text
TryStartTrade(runtime CaravanData, distance, tradeId, routeId)
  → selected SaveData.tradeProgress 기록
    식별: selectedCaravanId(암시), tradeId, routeId
  → JourneyRunner.TryDepart(runtime reference)
  → selected SaveData.caravan snapshot
  → SaveResult 확인
  → coordinator.activeCaravan = runtime reference

FrameworkRoot.Update (0.2초)
  → CheckProgressAndCompletion
  → selected tradeProgress UTC ticks로 progress 계산
  → JourneyRunner.SetProgress(activeCaravan)
  → selected caravan 저장 snapshot 갱신
  → 도착/실패 시 private SettleActiveTrade
  → LastSettlement* 단일 cache
  → selected pendingSettlement 생성
  → Save (결과 미확인)
  → TradeSettlementReady(caravanId, tradeId, result)

Claim
  → ClaimSettlement(explicit caravanId, tradeId)
  → collections에서 exact caravan/progress/pending 조회
  → stage 동안 selectedCaravanId 임시 전환
  → JourneyRunner.ClaimSettlement / Economy / Town / progress / pending remove
  → SaveResult 확인
  → 성공 시 Town 화면
```

### 4.3 다중화 제약 10개

1. **JourneyRunner 독립 처리 가능성 — 현재 구현 근거상 가능**  
   static 전역 상태 없이 인자로 받은 `CaravanData`만 변경한다. 다만 여러 인스턴스를 순회·저장하는 coordinator는 없다.
2. **TradeProgressRecorder의 caravanId entry 수정 — 불가**  
   모든 public mutation이 `saveData.tradeProgress` compatibility property를 사용하며 caravanId 인자가 없다.
3. **Coordinator의 전체 entries 순회 — 현재 없음**  
   `tradeProgressEntries` iteration이 없고 selected getter 하나만 처리한다.
4. **runtime cache 제한 — 하나로 제한**  
   `activeCaravan` 단일 필드이다.
5. **동일 frame 두 완료 충돌 — 현재 두 항목을 처리하지 않으므로 두 번째는 방치됨**  
   향후 단순 loop만 추가하면 단일 LastSettlement/cache/UI 화면 overwrite 위험이 있다.
6. **Settlement cache — 단일 필드**  
   Coordinator와 SettlementUiBridge 모두 pending result 하나만 보관한다.
7. **여러 완료 save 반복 — 현재 경로에서는 한 check에 하나만 가능**  
   향후 per-entry `SettleActiveTrade` 재사용 시 entry마다 Save가 호출될 수 있어 batch 정책이 필요하다.
8. **한 Caravan 예외의 전체 갱신 영향 — 현재 복수 loop가 없어 직접 판단 불가**  
   `CheckProgressAndCompletion`에 entry별 try/catch가 없으므로 future loop에서 예외 격리 여부를 결정해야 한다.
9. **화면 router의 전체/선택 구분 — 구분하지 않음**  
   `MapFromSaveData`가 selected `tradeProgress/pendingSettlement`만 읽고 `CurrentScreenState`도 전역 하나다.
10. **ForceComplete explicit ID — 불가**  
    `ForceCompleteActiveTrade()`와 `CompleteTradeRequested` 모두 ID가 없다.

### 4.4 로드·복구 조합

| 데이터 조합 | 현재 처리 |
|---|---|
| 복수 Traveling | 목록은 유지되나 selected entry 하나만 Offline/online 진행 처리; 나머지는 실행 복구되지 않음 |
| 복수 SettlementPending | 목록은 유지되나 selected pending 하나만 runtime/UI cache로 복구 |
| Traveling + Pending 혼합 | selected state만 화면·복구에 반영; 전역 화면은 한 상태만 표현 |
| TravelProgress orphan | Normalize가 Error 로그 후 보존; 자동 차단/수리 없음 |
| Pending orphan | Error 로그 후 보존 |
| 동일 Caravan 복수 TradeProgress | Error 로그 후 보존; `TryGetTradeProgress`는 duplicate를 감지해 lookup 실패 |
| 동일 Caravan 복수 Pending | exact/무역 ID 미지정 lookup에서 duplicate 감지 후 실패; Normalize는 exact composite duplicate만 명시 로그하고 서로 다른 tradeId의 same-caravan ambiguity는 별도 분류하지 않음 |
| duplicate Caravan ID | 현재 Normalize가 뒤 항목에 새 ID를 발급하고 child relink는 하지 않음 |

정책 문서(`Multi_Caravan_Save_Architecture.md`, `Settlement_Recovery_and_Trade_ID_Contract.md`)는 duplicate/orphan을 조용히 선택·삭제·재키잉하지 말고 visible validation failure 및 해당 Caravan command 차단으로 처리할 것을 목표로 한다. 현재 `NormalizeData`의 duplicate Caravan ID 교체는 이 목표와 불일치한다.

## 5. Investment Quest 현재 구조 및 정책

### 5.1 현재 구현

| 조사 항목 | 결과 |
|---|---|
| runtime Investment Quest 모델 | 없음 |
| `InvestmentQuestDefinition` 또는 ScriptableObject | 없음; `Assets/_Project/03.Economy/07_Investment`은 `.meta`만 확인 |
| completion command/result | 없음 |
| UI/ViewData | 없음 |
| SaveData completion field/collection | 없음 |
| 완료/보상 중복 방지 key | 없음 |
| 도시 해금 저장 | `WorldSaveData.unlockedTownIds` 존재 |
| 경로 해금 저장 | `WorldSaveData.unlockedRouteIds` 존재 |
| 해금 mutation command/event | 없음 |
| 해금 UI 조회 | WorldMapPresenter가 Save list와 Shared `UnlockedByDefault`를 함께 조회 |

### 5.2 최신 공식/승인 정책

주요 근거:

- `Docs/Planning_Milestone/Earn_Money_While_Lying_Down_Game_Design.md` §J, lines 616-663
- `Docs/Personal_Documents/CSU/SaveDataPolicy/Donation_Investment_Loan_Save_Contract.md` lines 13-17
- `Docs/Personal_Documents/CSU/SaveDataPolicy/SaveData_V2_Field_Contract.md`
- `Docs/Planning_Milestone/00_Team_Rules_and_Milestone_Second_Build.md` §5.6

확정 문구 요약:

- 제출 가능: 거래 재화, Caravan이 현재 직접 보유/적재한 적격 무역품.
- 제출 불가: 거점 도시 인벤토리, 건축 재료, 비적격/미승인 아이템, Traveling/SettlementPending에 잠긴 goods.
- 여러 Caravan 제출은 허용하되 각 stack에 `caravanId`, `itemId`, `amount`가 필요하다.
- 누적 progress와 분할 납부는 없다. 전체 비용을 한 번에 지불한다.
- 동일 quest 중복 완료·중복 보상을 금지한다.
- 완료, unlock, save가 한 transaction이며 실패 시 currency/goods/completion/unlock을 함께 rollback한다.
- 보상은 완료와 동시에 적용하고 별도 Claim을 기본적으로 사용하지 않는다.
- 정의 비용·unlock IDs는 Shared Data/SO가 소유하며 SaveData는 결과 상태만 저장한다.
- 목표 DTO 의미: `investmentQuestId`, `townId`, `isCompleted`, `completedUtcTicks`.

### 5.3 문서 충돌

`Docs/Personal_Documents/JJH/0720_Progression_Economy_M0_Contract.md` §8~9에는 다음 과거안이 남아 있다.

- donation quest: `contributedAmount`, `isRewardClaimed`
- investment: `progressAmount`, 도시 기부금 전환 가능성, 해금 ID 목록

이는 같은 날짜에 업데이트된 공식 game design과 승인 SaveData policy의 “독립 기부 제거, 누적 progress 제거, 분할 없음, 즉시 보상/기본 no-Claim”과 충돌한다. 후속 계약에서는 문서 효력이 명시된 공식 game design 및 승인 SaveData policy를 우선할지 확인해야 한다.

### 5.4 재사용 가능한 현재 패턴

- **Result/rollback 패턴**: `ClaimSettlementResult`, `SaveResult`, Rescue Loan snapshot rollback, Building command snapshot rollback.
- **collection/key 패턴**: `caravans[]` + stable ID, pending `(caravanId, tradeId)`, `SaveDataLookup`의 duplicate-reject lookup.
- **resource mutation 대상**: `PlayerSaveData.tradingCurrency`, 각 `CaravanSaveData.cargo`.
- **unlock 저장 대상**: `WorldSaveData.unlockedTownIds/unlockedRouteIds`.
- **definition query 기반**: SharedGameData provider의 town/route/item 조회. InvestmentQuest 자체 provider는 없음.

재사용 “가능성”은 설계 확정이 아니며, 특히 completion collection root와 result/event 타입은 후속 결정 사항이다.

## 6. Building 저장 및 식별 구조

### 6.1 현재 저장 구조

- Root: `SaveData.player.villageBuildings`
- 타입: `List<VillageBuildingSaveData>`
- DTO 필드:
  - `string displayName`
  - `int level`
- `displayName`은 표시와 식별을 겸하며 exact ordinal string 비교에 사용된다.
- level은 DTO 문서상 `0 이하 = 미건축`, `1 이상 = 보유`이다.
- Normalize:
  - list null → empty list
  - entry null은 보존/skip
  - `displayName == null` → empty string
  - negative level → 0
  - duplicate displayName validation 없음
- version 6에 별도 Building migration은 없다. 이 필드는 version 5에서 additive field로 도입되어 null normalization으로 호환했다.

### 6.2 displayName 사용 분류

| 분류 | 사용 위치 | 식별/표시 |
|---|---|---|
| 저장 키 | `VillageBuildingSaveData.displayName` | 식별 |
| Command 정의 조회 | `BuildingCostCatalog.TryGetDefinition(displayName)` | 식별 |
| 현재 level 조회/upsert | `CaravanBuildingConstructionCommand.TryGetCurrentLevel/TryUpsertBuilding` | 식별 |
| 비용 계산 input/result | `BuildingCostDefinition/Input/Result.DisplayName` | 식별 + 표시 |
| Scene registry lookup | `VillageBuildingRegistry.FindByName/FindCatalogByName/ApplySavedBuildingLevel/WriteBuildingToSave` | 식별 |
| UI label | `BuildingListPanel`, `BuildingAddPopup` | 표시 |
| GameObject 이름 | `Building_` + displayName | 표시/디버그 |
| validation/log | `BuildingCostCatalogFinding.BuildingDisplayName` | 진단 |
| tests | `CaravanBuildingConstructionCommandTests`, `BuildingCostCatalogTests` | fixture 식별 |
| SharedGameData lookup | 없음 | Building provider 자체 없음 |

### 6.3 canonical Building 정의

현재 두 정의 계층이 있다.

1. `VillageBuildingRegistry.CatalogEntry`
   - Scene serialized `displayName + prefab`
   - `Village_Home.unity`에 저장
2. `BuildingCostCatalog` / `BuildingCostDefinition`
   - ScriptableObject 타입은 있으나 repository에서 `BuildingCostCatalog.asset` 인스턴스가 검색되지 않음
   - 정의 키는 `DisplayName`

현재 `buildingId` 필드, ID 생성 규칙, ID uniqueness validator, `ISharedGameDataProvider.TryGetBuilding`은 없다. 건물 효과 계산/적용의 production 연결도 **확인되지 않음**이다.

문서 root 표기에도 차이가 있다. `SaveData_V2_Field_Contract.md`의 목표 표는 Building collection을 aggregate 수준의 `buildings[]`로 표현하지만 현재 코드는 `SaveData.player.villageBuildings`에 저장한다.

또한 현재 제품 UI 후보 경로는 `BuildingAddPopup.BuildCatalog` → `VillageBuildingRegistry.AddOrUpgrade`이다. 이 legacy 메서드는 `WriteBuildingToSave`로 메모리 SaveData만 upsert하고 `ISaveService.Save`를 호출하지 않는다. 반면 원자 save/rollback을 제공하는 `CaravanBuildingConstructionCommand.Execute`는 Editor tests 외 production 호출자가 검색되지 않았다.

### 6.4 현재 Building 데이터 목록

Scene serialized catalog 근거: `Assets/_Project/07.Scenes/04_InGame/Village_Home.unity`, `VillageBuildingRegistry`.

| 현재 표시 이름 | 기존 ID | 저장 키 | Scene 초기 레벨 | 정의 파일 |
|---|---|---|---:|---|
| 상점 | 없음 | `상점` | 1 | `Village_Home.unity` |
| 창고 | 없음 | `창고` | 1 | `Village_Home.unity` |
| 목장 | 없음 | `목장` | 1 | `Village_Home.unity` |
| 대장간 | 없음 | `대장간` | 0(카탈로그만) | `Village_Home.unity` |
| 시장 | 없음 | `시장` | 0(카탈로그만) | `Village_Home.unity` |
| 여관 | 없음 | `여관` | 0(카탈로그만) | `Village_Home.unity` |

위 값은 Scene의 현재 registry 초기값이며 사용자 SaveData의 실제 레벨 목록은 런타임 저장 파일에 따라 달라진다.

### 6.5 마이그레이션 영향

| 영역 | 현재 결합 | buildingId 전환 시 검토 대상 |
|---|---|---|
| SaveData DTO | displayName + level | additive ID/대체 필드, source of truth |
| Normalize/load | null/negative만 보정 | legacy displayName→ID mapping, unknown/duplicate 처리 |
| numeric version | v6 | semantic key 변경이 additive safe-default인지 incompatible migration인지 결정 |
| 기존 JSON | displayName만 보유 | converter, compatibility adapter 또는 reset |
| construction command | definition.DisplayName key | command/definition/result key |
| upgrade/scene registry | catalog displayName exact match | scene catalog identity와 display 분리 |
| SharedGameData | Building 없음 | provider/definition ownership 여부 |
| UI ViewData | registry name 직접 표시 | ID resolve 후 displayName 제공 여부 |
| tests | displayName fixture | legacy/load/unknown/duplicate ID tests |

정책 충돌:

- 최신 `Multi_Caravan_Save_Architecture.md`, `SaveData_V2_Field_Contract.md`, 공식 game design은 stable `buildingId + level`, home inventory 소비를 목표로 한다.
- 현재 코드와 `JJH/0720_Construction_Material_Building_Cost_Contract.md`는 `displayName + level`, selected Caravan cargo 직접 소비를 명시한다.
- 따라서 buildingId migration과 재료 source 변경은 서로 독립적인 결정으로 분리해야 한다.

## 7. 공통 SaveData version·migration 현황

### 7.1 현재 구현

- `SaveData.CurrentVersion = 6`.
- `JsonSaveService.Load`:
  1. 파일 없음 → new game.
  2. version 5 → `MigrateVersion5`로 단일 caravan/progress/pending을 v6 collections로 옮기고 즉시 Save.
  3. 그 외 version mismatch/null → 경고 후 new game.
  4. parse/IO 예외 → Error 후 new game.
- 일반-purpose migration registry는 없고 v5→v6 전용 converter만 있다.
- `Save`는 항상 Normalize 후 version을 CurrentVersion으로 덮어쓴다.

### 7.2 누락 필드와 version 증가 정책

문서 정책 `Save_Version_and_Normalization_Policy.md`:

- incompatible semantic/type/collection-model change는 future numeric version 대상.
- safe default가 있는 additive optional field는 version을 올리지 않아도 된다.
- Normalize는 missing child/list 생성, 명시적으로 invalid인 scalar default 보정까지만 수행한다.
- gameplay 계산·resource 지출·reward claim·repair write를 Normalize에서 하면 안 된다.

실제 선례:

- v5에 market, home inventory, village building fields를 additive로 추가하고 version 유지 + null normalization.
- v6은 single→collection model 변경이라 explicit migration을 추가했다.

### 7.3 Investment + buildingId 판단에 필요한 사실

- Investment completion collection은 현재 완전히 없으므로 optional empty collection로 추가할 수 있는 형태이지만, duplicate key와 unlock rollback 계약도 함께 필요하다.
- buildingId는 단순 누락 optional 값이 아니라 기존 `displayName` 식별 의미를 대체한다. 기존 JSON에서 canonical ID를 얻을 mapping source가 현재 없다.
- 두 변경을 같은 version에 넣으면 한 converter/normalization boundary에서 처리할 수 있지만, Investment는 additive이고 Building은 legacy conversion 성격이라 실패·rollback·unknown mapping 정책이 다르다.
- 현재 loader는 v5 외 unsupported version을 new game으로 바꾸므로 version bump만 하고 v6 converter를 만들지 않으면 기존 v6 save는 reset된다.
- version 7 적용 여부는 이 조사에서 결정하지 않는다.

### 7.4 정책과 실제 Normalize 차이

| 항목 | 정책 목표 | 현재 구현 |
|---|---|---|
| duplicate Caravan ID | validation error, no last-write-wins | 새 GUID로 교체, child relink 안 함 |
| duplicate progress/pending | visible error, preserve | 로그 후 preserve; lookup은 duplicate면 실패 |
| unknown shared ID | report/preserve | 일반적인 load-level shared ID validation 없음 |
| unsupported version | visible reject | warning 후 new game |
| repair write | 별도 visible save | v5 migration은 load 중 즉시 Save |

## 8. 공통 의존성 지도

```text
Framework API/Event Inventory
  ├─ explicit-ID Depart/Finalize 존재 여부
  ├─ settlement event/save timing
  └─ Multi-active Coordinator 공개 API 결정

Multi-active Coordinator
  ├─ SaveData v6 collections / SaveDataLookup
  ├─ runtime Caravan cache 구조
  ├─ per-entry settlement cache/query/event
  ├─ screen: selected Caravan vs aggregate 상태
  └─ save batching, retry, exception isolation

Investment Quest
  ├─ tradingCurrency + per-Caravan cargo
  ├─ Caravan state/asset lock query
  ├─ completion SaveData collection
  ├─ unlockedTownIds / unlockedRouteIds
  ├─ SharedGameData InvestmentQuest definition
  └─ SaveResult + committed event

Building migration
  ├─ VillageBuildingSaveData
  ├─ VillageBuildingRegistry Scene catalog
  ├─ BuildingCostCatalog/Command
  ├─ SharedGameData Building definition/lookup
  └─ legacy displayName mapping

Investment + Building
  └─ numeric version/legacy conversion release boundary를 함께 쓸지 결정
```

## 9. 미확인 및 모호한 사항

1. production Player UI가 Rescue Loan events/query를 실제로 소비하는지는 확인되지 않았다.
2. BuildingCostCatalog 타입은 있으나 repository asset 인스턴스가 없어 실제 비용 정의 목록은 확인되지 않았다.
3. stable building ID의 승인된 이름/생성 규칙/mapping table은 문서와 코드 어디에서도 확인되지 않았다.
4. InvestmentQuest definition의 실제 콘텐츠 ID, 비용, unlock 대상은 아직 없다.
5. `FrameworkRoot`가 progression command의 최종 facade가 되어야 하는지 별도 command service가 되어야 하는지는 문서 목표만 있고 확정 구현이 없다.
6. multi-active 화면이 selected Caravan 상태만 보여줄지, aggregate 경고/배지를 별도로 가질지는 미확정이다.
7. future loop에서 한 Caravan 예외를 격리할지 fail-fast할지, 여러 completion을 한 Save로 묶을지 per-entry Save할지는 미확정이다.
8. Scene catalog의 6개 표시명을 canonical production Building 목록으로 동결할지는 확인되지 않았다.
9. 기존 실제 사용자 JSON에 어떤 displayName 변형이 존재하는지는 repository만으로 확인할 수 없다.
10. 공식 game design의 구조 대출 일부 문구와 현재 승인 loan policy 사이에도 과거안 흔적이 있으나 본 조사 주제에서는 Investment/Building과 직접 연결되는 부분만 기록했다.

## 10. 후속 ChatGPT 결정 입력

### Framework API/Event

1. `SettleActiveTrade`를 public `FinalizeSettlement(caravanId, tradeId)` command로 승격할 것인가?
2. finalize save 실패 시 runtime/pending을 rollback하고 event/screen 전환을 금지할 것인가?
3. legacy `ClaimSettlementAndReset`과 Bridge bool API의 제거/유지 기간은 언제까지인가?
4. `TradeOfflineCompleted`와 debug `CompleteTradeRequested`도 caravanId/tradeId를 포함할 것인가?
5. Dirty tracking과 retry queue를 multi-active 이전 필수 선행으로 둘 것인가?

### Multi-active Coordinator

6. runtime Caravan을 `caravanId` keyed dictionary로 보관할지 매 tick SaveData에서 재구성할지?
7. `tradeProgressEntries` 전체를 어떤 순서로 순회하고 한 entry 실패를 어떻게 격리할지?
8. 한 tick의 여러 progress/settlement 변경을 한 Save로 묶을지, settlement별 SaveResult를 보존할지?
9. pending runtime cache를 없애고 SaveData snapshot query로 대체할지 keyed cache로 확장할지?
10. global screen router가 selected Caravan만 반영하도록 명시할지 aggregate 상태를 추가할지?
11. ForceComplete API를 `(caravanId, tradeId)`로 바꾸고 debug compatibility wrapper를 남길지?

### Investment Quest

12. completion DTO root를 `SaveData`, `PlayerSaveData`, `WorldSaveData` 중 어디에 둘지?
13. key를 `investmentQuestId` 단독으로 할지 `(townId, investmentQuestId)`로 할지?
14. `isCompleted=true`만 저장할지 completion entry의 존재 자체를 완료로 볼지?
15. unlock ID를 quest completion DTO에도 snapshot할지, world unlock lists만 source of truth로 둘지?
16. 명시적 Claim 없는 즉시 보상 정책을 최종 확정하고 이전 donation/progress/rewardClaim 문서를 폐기 표기할지?
17. 여러 Caravan item submission DTO와 asset-lock validation contract를 어떻게 표현할지?

### Building migration/version

18. 6개 현재 displayName 각각의 canonical `buildingId`는 무엇인가?
19. mapping source를 SharedGameData Building definition, 별도 migration table, Scene catalog 중 어디에 둘지?
20. legacy displayName이 unknown/duplicate/localized일 때 load 차단, 보존, fallback, reset 중 무엇을 할지?
21. BuildingCostCatalog/command/registry/UI를 한 번에 ID 전환할지 compatibility adapter를 단계적으로 둘지?
22. 건축 재료 source를 현재 Caravan cargo로 유지할지 최신 home inventory 정책으로 전환할지?
23. Investment additive schema와 Building key migration을 같은 numeric version에 포함할지?
24. v6→future version converter를 제공할지 의도적 reset/release boundary를 택할지?

## 부록 A. 주요 근거 파일

- `Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/ISaveService.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/SaveDataLookup.cs`
- `Assets/_Project/11.CoreServices/Scripts/Events/FrameworkEvents.cs`
- `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs`
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeStartService.cs`
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressRecorder.cs`
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`
- `Assets/_Project/11.CoreServices/Scripts/SceneFlow/InGameScreenStateRouter.cs`
- `Assets/_Project/11.CoreServices/Scripts/Building/CaravanBuildingConstructionCommand.cs`
- `Assets/_Project/11.CoreServices/Scripts/Building/CaravanBuildingCostInputFactory.cs`
- `Assets/_Project/03.Economy/09_Building/BuildingCostCatalog.cs`
- `Assets/_Project/03.Economy/09_Building/BuildingConstructionModels.cs`
- `Assets/_Project/01.Core/04_TradeLoop/YHY/JourneyRunner.cs`
- `Assets/_Project/01.Core/07_Village/YHY/VillageBuildingRegistry.cs`
- `Assets/_Project/05.UI/04_WorldMap/Scripts/WorldMapPresenter.cs`
- `Assets/_Project/07.Scenes/04_InGame/Village_Home.unity`
- `Docs/Personal_Documents/CSU/SaveDataPolicy/Framework_API_Event_Inventory.md`
- `Docs/Personal_Documents/CSU/SaveDataPolicy/Framework_Command_Event_Contract.md`
- `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`
- `Docs/Personal_Documents/CSU/SaveDataPolicy/Settlement_Recovery_and_Trade_ID_Contract.md`
- `Docs/Personal_Documents/CSU/SaveDataPolicy/Donation_Investment_Loan_Save_Contract.md`
- `Docs/Personal_Documents/CSU/SaveDataPolicy/SaveData_V2_Field_Contract.md`
- `Docs/Personal_Documents/CSU/SaveDataPolicy/Save_Version_and_Normalization_Policy.md`
- `Docs/Planning_Milestone/Earn_Money_While_Lying_Down_Game_Design.md`
- `Docs/Planning_Milestone/00_Team_Rules_and_Milestone_Second_Build.md`
- `Docs/Personal_Documents/JJH/0720_Progression_Economy_M0_Contract.md`
- `Docs/Personal_Documents/JJH/0720_Construction_Material_Building_Cost_Contract.md`
- `Docs/Personal_Documents/CSU/0721_multi_caravan_atomic_claim.md`
- `Docs/Personal_Documents/CSU/Handoff/07-21_atomic_claim_town_routing_economy_handoff.md`
