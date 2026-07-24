# Framework API and Event Inventory

## 기준

- 현재 구현 기준: `../Research/0721_Framework_Runtime_Save_Contract_Research.md`의 commit `045042f81c2e06f91f83670c0e6a03f38577d30f` 조사 결과
- 현재 numeric version: `6`; 승인된 다음 numeric version: `7`
- 상태: `Implemented`, `Partial`, `Contract Only`, `Missing`, `Deprecated`
- `Implemented`는 선언뿐 아니라 조사된 production 동작과 사용이 목표 계약의 핵심 의미를 충족할 때만 사용한다.

## API/Event inventory

| 영역 | 이름 | 종류 | 현재 시그니처 | 목표 시그니처 | 상태 | 구현 위치 | 호출자/구독자 | Multi-Caravan | 후속 작업 |
|---|---|---|---|---|---|---|---|---|---|
| Save | Save | Command/Result | `SaveResult Save(SaveData data)` | 동일 | Implemented; caller migration Partial | `ISaveService.cs`, `JsonSaveService.cs` | FrameworkRoot, TradeStart, Coordinator, Loan, Building, debug | Aggregate 저장 가능 | 반환값을 무시하는 lifecycle/debug 호출부와 rollback 정리 |
| Save | SaveResult | Result | `SaveResult { Succeeded: bool, FailureReason: SaveFailureReason, Message: string, FailedDataCategory: string }` | 동일 | Implemented; caller migration Partial | `ISaveService.cs` | 중요 Command가 검사; 일부 호출부는 무시 | 해당 없음 | 모든 committed mutation에서 검사; retry/queue는 별도 Missing |
| Save | MarkDirty | Command | 없음 | `void MarkDirty(SaveDirtyReason reason, string entityId = null)` | Contract Only | 문서 계약만 존재 | production 호출자 없음 | ID 수용 목표 | 구현 및 dirty Query 추가 |
| Save | dirty state | Query | 없음 | read-only dirty Query | Contract Only | 문서 계약만 존재 | production 호출자 없음 | Aggregate 상태 | 구체 타입 확정 |
| Save | retry/queue | Service | 없음 | 순차 retry/queue | Missing | 없음 | 없음 | batch 결과 보존 필요 | 구체 계약 결정 |
| Trade | TryStartTrade | Command/Compatibility | `DepartureValidationResult TryStartTrade(CaravanData caravan, float distanceKm, string tradeId, string routeId, bool saveImmediately = true)` | `TradeDepartureResult Depart(string caravanId, TradeDepartureRequest request)` | Partial | `TradeStartService.cs` | TradePrepare UI/flow, debug, Editor E2E | explicit `caravanId` 없음; selected compatibility 의존 | Command 내부 full GUID 생성, canonical API로 이관 |
| Trade | ForceCompleteActiveTrade | Debug/Compatibility | `void ForceCompleteActiveTrade()` | `TradeForceCompleteResult ForceCompleteTrade(string caravanId, string tradeId)` | Deprecated | `TradeProgressCoordinator.cs` | `CompleteTradeRequested`, debug/test | selected active 전용 | canonical finalization 경로에 1회 위임 |
| Settlement | FinalizeSettlement | Command | public API 없음; private selected 경로 존재 | structured result `FinalizeSettlement(string caravanId, string tradeId)` | Contract Only | private `SettleActiveTrade` | Check/Offline/ForceComplete 내부 | 현재 단일 selected 경로 | Save 성공 전 runtime commit/Event/화면 전환 금지 |
| Settlement | ClaimSettlement | Command/Result | `ClaimSettlementResult ClaimSettlement(string caravanId, string tradeId)` | 동일 | Implemented | `TradeProgressCoordinator.cs` | SettlementUiBridge, debug, E2E | exact composite key 지원; 내부 selected 호환 전환 존재 | 내부 selected 의존 제거 |
| Settlement | ClaimSettlementAndReset | Compatibility | `[Obsolete] bool ClaimSettlementAndReset()` | `ClaimSettlement(caravanId, tradeId)` | Deprecated | `TradeProgressCoordinator.cs` | legacy/debug 가능 | selected pending 추론 | canonical API에 한 번만 위임 후 제거 준비 |
| Settlement | Bridge ClaimSettlementAndReset | UI Compatibility | `bool ClaimSettlementAndReset()` | ID 기반 Claim 호출 | Deprecated | `SettlementUiBridge` | `SettlementUiDataAdapter.OnClickClaimSettlement()` | bridge cache로 ID 보완, bool로 결과 축약 | serialized callback 확인 후 adapter 교체 |
| Settlement | TradeSettlementReady | Event | `Action<string caravanId, string tradeId, JourneyResultData>` | 동일 ID payload, read-only snapshot | Partial | `FrameworkEvents.cs`; raise: coordinator/restore | `SettlementUiBridge`; bridge event는 UI adapter가 구독 | ID payload는 대응 | Save 성공→runtime commit 뒤 발행; mutable payload 교체 시점 결정 |
| Settlement | SettlementClaimed | Event | 없음 | `Action<string caravanId, string tradeId>` | Contract Only | 문서 계약만 존재 | production 구독자 없음 | exact composite key | Claim mutation과 Save 및 runtime commit 성공 뒤 entry당 1회 발행 |
| Trade | TradeOfflineCompleted | Event | `Action<string tradeId>` | `Action<string caravanId, string tradeId>` recovery notification | Partial | `FrameworkEvents.cs` | offline recovery raise; 구독자 확인 필요 | 현재 `caravanId` 없음 | canonical recovery 계약에 통합 |
| Trade | CompleteTradeRequested | Debug Event | `Action` | 호환 adapter 내부에서 explicit-ID `ForceCompleteTrade(caravanId, tradeId)`에 1회 위임 | Deprecated | `FrameworkEvents.cs` | coordinator 구독 | 현재 대상 ID 없음 | 자체 mutation/save/event 경로 금지; ID를 제공할 수 없으면 visible rejection |
| Preparation | UpdatePreparation | Command | 없음 | explicit `caravanId` Command | Contract Only | 문서 계약만 존재 | production 호출자 없음 | 목표 대응 | 구현 |
| Preparation | UpdatePurchasePreview | Command | 없음 | explicit `caravanId` Command | Contract Only | 문서 계약만 존재 | production 호출자 없음 | 목표 대응 | 구현 |
| Preparation | CancelPreparation | Command | 없음 | explicit `caravanId` Command | Contract Only | 문서 계약만 존재 | production 호출자 없음 | 목표 대응 | 구현 |
| Preparation | PreparationChanged | Event | 없음 | `Action<string caravanId, long dirtyRevision>` non-committed Event | Contract Only | 문서 계약만 존재 | production 구독자 없음 | explicit `caravanId` | 메모리 변경/UI 갱신만 통지; 저장 완료는 `SaveSucceeded`가 담당 |
| InvestmentQuest | Complete with currency | Command | 없음 | `InvestmentQuestCommandResult CompleteInvestmentQuestWithCurrency(string investmentQuestId)` | Contract Only | 문서 계약만 존재 | production 호출자 없음 | Quest ID 명시 | Stage 2 구현 |
| InvestmentQuest | Complete with goods | Command | 없음 | `InvestmentQuestCommandResult CompleteInvestmentQuestWithGoods(string investmentQuestId, IReadOnlyList<InvestmentGoodsContribution> contributions)` | Contract Only | 문서 계약만 존재 | production 호출자 없음 | 각 contribution에 `caravanId` | Stage 2 구현 |
| InvestmentQuest | Completed | Event | 없음 | ID-bearing committed Event | Contract Only | 문서 계약만 존재 | production 구독자 없음 | 대응 목표 | Save 성공 후 발행 |
| InvestmentQuest | completion collection | SaveData | 없음 | `world.investmentQuestCompletions` | Contract Only | 문서 계약만 존재 | 없음 | ID collection | v7 DTO 구현 |
| InvestmentQuest | definition query | Query | 없음 | SharedGameData InvestmentQuest Query | Contract Only | provider에 현재 없음 | 없음 | stable ID | 실제 definition 형태/소유 위치 결정 |
| Building | current construction command | Command | displayName 기반 `Execute(...)` | `BuildingCommandResult UpgradeBuilding(string buildingId)` | Partial | `CaravanBuildingConstructionCommand.cs` | Editor tests만 확인; UI는 registry 직접 경로 | selected Caravan cargo와 displayName 의존 | home inventory + buildingId 경로로 전환 |
| Building | UpgradeBuilding | Command | 없음 | `BuildingCommandResult UpgradeBuilding(string buildingId)` | Contract Only | 문서 계약만 존재 | 없음 | stable ID | Stage 2 구현 |
| Building | BuildingUpgraded | Event | 없음 | ID-bearing committed Event | Contract Only | 문서 계약만 존재 | production 구독자 없음 | 대응 목표 | Save 성공 후 발행 |
| Building | definition query | Query | 없음 | `bool TryGetBuilding(string buildingId, out SharedBuildingDefinition building)` | Contract Only | Shared provider에 현재 없음 | 없음 | stable ID | definition asset/소유 위치 결정 |
| Rescue Loan | EvaluateStatus | Query | `RescueStatusResult EvaluateStatus()` | 동일 | Implemented | `RescueLoanCommandService` | production Player UI 소비는 확인되지 않음 | 해당 없음 | UI 사용 여부 확인 |
| Rescue Loan | IssueRescueLoan | Command/Result | `SaveResult IssueRescueLoan()` | 동일 | Implemented | `RescueLoanCommandService` | Editor E2E 확인 | 해당 없음 | 회귀 유지 |
| Rescue Loan | RepayRescueLoan | Command/Result | `SaveResult RepayRescueLoan(long amount)` | 동일 | Implemented | `RescueLoanCommandService` | Editor E2E 확인 | 해당 없음 | 회귀 유지 |
| Rescue Loan | committed events | Event | issued/repaid/closed/restriction events | 동일 | Implemented | Framework events/service | production Player UI 구독은 확인되지 않음 | 해당 없음 | 구독자 확인 |

## 공통 계약

- `selectedCaravanId`는 UI 선택과 마지막 선택 복원에만 사용한다. 진행, 완료, Force Complete, settlement 생성, Claim, runtime context의 대상을 추론하지 않는다.
- canonical 진행·완료·정산 identity는 `caravanId + tradeId`이다.
- committed Event는 staging → Save → `SaveResult` 성공 → runtime commit 뒤 발행한다.
- compatibility API는 canonical API에 한 번만 위임하며 자체 mutation/save/event 경로를 유지하지 않는다.
- duplicate/orphan은 보존하고 visible validation failure로 처리한다. 자동 삭제, 선택, ID 교체를 하지 않는다.

## 구현 순서

1. SaveData v7 DTO, validation, v6 명시적 reset 경계
2. ID 기반 Depart/Building/InvestmentQuest Commands
3. Multi-active runtime context와 전체 Traveling 순회
4. settlement batch save와 committed Event timing
5. load/UI 복구 및 deprecated API 제거 준비

## 검증 계약

- settlement batch Save 성공 후 완료 entry마다 `TradeSettlementReady` 1회, 실패 시 전체 0회
- Claim Save 성공 후 해당 entry에 `SettlementClaimed` 1회, 실패 시 0회
- legacy API는 canonical API에 1회만 위임
- Building/InvestmentQuest Save 실패 시 committed Event 없음
- Rescue Loan rollback/Event 회귀
- Scene/Prefab callback 검증 전 legacy UI method 제거 금지
