# Save 호출부 및 상태 변경 순서 인벤토리

## 1. 문서 목적

* 현재 저장소의 `ISaveService.Save(SaveData)` 호출 구조를 코드 근거로 조사한다.
* 각 호출부에서 유효성 검사 → 런타임 상태 → SaveData → Save → Event → UI/Scene 전환 순서를 기록한다.
* Save 실패가 확인되지 않는 구조, 중복 처리 가능성, 디스크/메모리 불일치 위험을 분류한다.
* 팀 회의에서 SaveResult / Snapshot·Rollback / Event timing 정책을 결정할 때 사용할 근거 자료를 제공한다.

이 문서는 **실태 조사**이다. API·이벤트·런타임 동작을 변경하지 않는다.

관련 문서와의 역할 구분:

| 문서 | 역할 |
| --- | --- |
| `Framework_API_Event_Inventory.md` | 메서드·이벤트·소유권·마이그레이션 대상 목록(템플릿 포함) |
| `Immediate_Save_and_Dirty_Policy.md` | 장기 목표(TrySave, Dirty, 즉시 저장 대상) |
| `Framework_Command_Event_Contract.md` | Command/Event 계약과 단계적 마이그레이션 |
| **본 문서** | 실제 상태 변경·Save·Event·UI/Scene 순서와 실패 위험 분석 |

---

## 2. 조사 기준

| 항목 | 값 |
| --- | --- |
| 기준 브랜치 | `dev2` |
| 기준 SHA | `c11046495ba8a8768ac6881b36b882d2a027fb50` (`c110464` — Merge PR #142 multi-caravan-save-contract) |
| 작업 브랜치 | `document/framework/save-callsite-inventory` |
| 조사 날짜 | 2026-07-19 |
| SaveResult PR | `feature/framework/save-result-api`는 존재하나, 기준 SHA ancestry에 **포함되지 않음** (`bf90e4d` is-ancestor = false) |
| 조사 범위 | `Assets/` 내 `ISaveService.Save` / `SaveService.Save` / `saveService.Save` 직접 호출부, 및 P0·P1 관련 상태 변경 경로 |
| 제외 범위 | SaveResult API 구현 가정, 미병합 브랜치 코드, Scene/Prefab YAML 직접 해석, Unity 런타임 재현 |

### 조사 전 확인 결과

1. 작업 브랜치: `document/framework/save-callsite-inventory` (최신 `origin/dev2`에서 생성)
2. 기준 `dev2` SHA: `c11046495ba8a8768ac6881b36b882d2a027fb50`
3. `ISaveService.Save` 시그니처: `void Save(SaveData data);`
4. `JsonSaveService.Save`: null skip, try/catch로 예외 흡수, 성공/실패를 호출자에게 반환하지 않음
5. SaveResult PR 변경: 현재 브랜치에 **미포함**
6. `SaveData.CurrentVersion`: **5**
7. 기존 SaveDataPolicy 문서 목록: 아래 §2.1
8. 기존 Save 호출부 인벤토리 문서: **없음** (본 문서가 신규)
9. Framework API/Event 인벤토리: `Framework_API_Event_Inventory.md` 존재(템플릿 단계, 대부분 `확인 필요`)
10. 코드 소유권 근거: 스크립트 헤더 `Technical Ownership` / 일부 Core 파일 주석의 담당 표기. 폴더명만으로 확정하지 않음. `Save_API_Owner_Migration_Guide.md` / `Save_Result_API_Implementation_Decision.md`는 **해당 경로에 없음**

### 2.1 기존 SaveDataPolicy 문서 목록 (실파일)

```text
Docs/Personal_Documents/CSU/SaveDataPolicy/
  Donation_Investment_Loan_Save_Contract.md
  Framework_API_Event_Inventory.md
  Framework_Command_Event_Contract.md
  Immediate_Save_and_Dirty_Policy.md
  Multi_Caravan_Save_Architecture.md
  Pull_Request_Description.md
  Save_Callsite_and_Mutation_Order_Inventory.md   ← 본 문서
  Save_Recovery_Test_Matrix.md
  Save_Version_and_Normalization_Policy.md
  SaveData_V2_Field_Contract.md
  Settlement_Recovery_and_Trade_ID_Contract.md
```

핸드오프에 언급되었으나 현재 경로에 없는 파일:

* `Save_API_Owner_Migration_Guide.md` → 없음
* `Save_Result_API_Implementation_Decision.md` → 없음

---

## 3. 현재 Save API 요약

### 인터페이스

* 파일: `Assets/_Project/11.CoreServices/Scripts/Save/ISaveService.cs`
* `void Save(SaveData data);`
* 결과 타입(`SaveResult`) 없음
* 기술 책임(헤더): Framework & Integration

### 구현체

* 파일: `Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs`
* 경로: `Application.persistentDataPath/save_data.json`
* 직렬화: `JsonUtility.ToJson` / `FromJson`
* 저장 전: `NormalizeData`, `version = CurrentVersion`, `lastSavedUtcTicks = UtcNow.Ticks`
* null 입력: 경고 후 return (디스크 미변경)
* IO/기타 예외: `FrameworkLog.Error` 후 **외부로 던지지 않음**
* 호출자는 저장 성공 여부를 구분할 수 없음

### 버전

* `SaveData.CurrentVersion = 5`
* Load 시 version 불일치면 migration 없이 새 게임 데이터 반환

### 정책 문서와의 차이

* `Immediate_Save_and_Dirty_Policy.md`는 장기적으로 `TrySave` / Dirty를 제안한다.
* 현재 production 코드는 `void Save`만 사용한다. 본 문서는 **현재 코드**를 기준으로 한다.

---

## 4. 전체 Save 호출부 요약 표

검색 키워드: `.Save(`, `SaveService.Save`, `saveService.Save`, `ISaveService`, `JsonSaveService`  
Unity/외부 일반 `Save()`와 구분하여 **프로젝트 `ISaveService.Save(SaveData)`만** 집계했다.

| ID | 기능 | 파일/메서드 | Production 여부 | Save 목적 | 위험도 | 담당 |
| --- | --- | --- | --- | --- | --- | --- |
| S01 | 새 게임 초기 저장 | `FrameworkRoot.StartNewGame` | Production | 새 SaveData 영속화 | P1 | Framework |
| S02 | 종료 전 저장 | `FrameworkRoot.ExitGame` | Production | 유실 방지 | P1 | Framework |
| S03 | Title 복귀 전 저장 | `FrameworkRoot.ReturnToTitle` | Production | 유실 방지 | P1 | Framework |
| S04 | 무역 출발 즉시 저장 | `TradeStartService.TryStartTrade` | Production | 출발 상태 영속화 | P0 | Framework |
| S05 | 진행률 중간 저장 | `TradeProgressCoordinator.CheckProgressAndCompletion` | Production API / 기본 루프는 `saveProgress:false` | 중간 progress 영속화(옵션) | P2 | Framework |
| S06 | 오프라인 진행 저장 | `TradeProgressCoordinator.ApplyOfflineProgressOnLoad` | Production | 오프라인 elapsed 영속화 | P1 | Framework |
| S07 | 정산 생성 저장 | `TradeProgressCoordinator.SettleActiveTrade` | Production | SettlementPending + pendingSettlement | P0 | Framework |
| S08 | 정산 Claim 저장 | `TradeProgressCoordinator.ClaimSettlementAndReset` | Production | 보상 반영 + Completed/Failed + pending clear | P0 | Framework |
| S09 | ForceSeason 저장 | `FrameworkDebugCommands.ForceSeason` | Debug | World 계절 강제 | P2 | Framework |
| S10 | ForceDisaster 저장 | `FrameworkDebugCommands.ForceDisaster` | Debug | World 재난 강제 | P2 | Framework |
| S11 | Pending restore smoke | `TradeStartDebugHarness` (smoke) | Debug | smoke 검증용 명시 Save | P2 | Framework |
| S12 | 시장 draft 저장 | `MarketInventorySession.PersistDraft` | 조건부 컴파일 (`ND_MARKET_SAVE_SCHEMA_VNEXT`) | 구매 준비 draft | P0* | 소유자 확인 필요 |
| S13 | 시장 commit 저장 | `MarketInventorySession.Commit` | 조건부 컴파일 | 재화·재고·cargo 확정 | P0* | 소유자 확인 필요 |
| S14 | 시장 reopen draft | `MarketInventorySession.ReopenCommittedAsDraft` | 조건부 컴파일 | 환불 후 draft | P0* | 소유자 확인 필요 |
| S15 | 시장 cancel | `MarketInventorySession.CancelPreparation` | 조건부 컴파일 | 환불·cargo clear | P0* | 소유자 확인 필요 |
| S16 | 시장 재고 refresh | `MarketInventorySession.ResolveOrRefreshInventory` | 조건부 컴파일 | 재고 생성 영속화 | P1* | 소유자 확인 필요 |
| S17 | Editor Probe MemorySave | `MarketInventoryIntegrationProbe.MemorySaveService` | Test/Editor | 인메모리 stub | P2 | 소유자 확인 필요 |

\* `ND_MARKET_SAVE_SCHEMA_VNEXT`는 `ProjectSettings.asset`의 `scriptingDefineSymbols`가 비어 있어 **현재 기본 빌드에서는 컴파일되지 않음**. 심볼 활성화 시 P0 후보.

### 호출부 수 요약

| 구분 | 개수 | 비고 |
| --- | --- | --- |
| Production (항상 컴파일) | 8 | S01–S08 |
| Debug | 3 | S09–S11 |
| 조건부(비활성) | 5 | S12–S16 |
| Test/Editor stub | 1 | S17 |
| **직접 Save 호출 합계** | **17** | 키워드 검색 후 수동 분류 |

---

## 5. 상태 변경 순서 상세

### S01 — 새 게임 (`FrameworkRoot.StartNewGame`)

```text
ID: S01
기능: 새 게임 시작
파일: Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs
클래스: FrameworkRoot
메서드: StartNewGame
호출 경로: TitleSceneController.StartNewGame → FrameworkRoot.StartNewGame
호출 주체: Title UI / Scene controller
Production / Debug / Test: Production
```

#### 실행 순서

1. 입력 또는 요청: Title 새 게임 버튼
2. 검증: 없음
3. 런타임 상태 변경: `CurrentSaveData = CreateNewGameData()`
4. SaveData 변경: 새 인스턴스 생성(기본값, `currentTownId = BaseCamp`)
5. Save 호출: `SaveService.Save(CurrentSaveData)`
6. Save 결과 확인: **없음** (`void`)
7. Event 발행: 없음(이 메서드 내)
8. UI 갱신: 없음
9. Scene/화면 전환: `SceneFlow.GoToLoading()` — Save 성공과 무관하게 실행
10. 반환: `void`

#### 실패 상태

| 항목 | 관찰 |
| --- | --- |
| Save 실패 시 런타임 상태 | 새 `CurrentSaveData` 참조 유지 |
| Save 실패 시 SaveData 메모리 | 새 데이터 |
| Save 실패 시 디스크 | 이전 파일 유지 또는 부분 실패 가능(구현상 예외 흡수) |
| Save 실패 시 Event | 없음 |
| Save 실패 시 UI/Scene | Loading으로 이동함 |
| 재시도 시 중복 위험 | 새 게임 재호출 시 또 다른 새 데이터로 덮어씀 |
| 복구 수단 | Continue/Load 정책에 의존 — 확인 필요 |

#### 소유권 / 위험도

* 기술 책임: Framework & Integration (스크립트 헤더)
* 기능 책임: Framework scene flow
* 확정 근거: `FrameworkRoot` 헤더
* 확정 여부: 기술 영역 Framework로 기록
* 위험도: **P1** — Save 실패해도 Loading 진입

---

### S02 — 종료 전 저장 (`FrameworkRoot.ExitGame`)

```text
ID: S02
기능: 게임 종료 직전 저장
파일: FrameworkRoot.cs
메서드: ExitGame
호출 경로: TitleSceneController.ExitGame → FrameworkRoot.ExitGame
Production / Debug / Test: Production
```

#### 실행 순서

1. 요청: Exit
2. 검증: `CurrentSaveData != null`일 때만 Save
3. 런타임 변경: 없음(이 메서드 내)
4. SaveData 변경: 없음(이미 변경된 메모리 저장)
5. Save 호출: 있음
6. Save 결과 확인: **없음**
7. Event: 없음
8. UI: 없음
9. Scene: Editor면 Play Mode 종료, 빌드면 `Application.Quit`
10. 반환: `void`

#### 실패 상태

* Save 실패해도 종료 진행
* 위험도: **P1** — 최근 변경 유실 가능, 사용자에게 실패 미표시

---

### S03 — Return to Title (`FrameworkRoot.ReturnToTitle`)

```text
ID: S03
기능: Title 복귀 전 저장
파일: FrameworkRoot.cs
메서드: ReturnToTitle
호출 경로: InGameSceneController.ReturnToTitle → FrameworkRoot.ReturnToTitle
Production / Debug / Test: Production
```

#### 실행 순서

1. 요청: Title 복귀
2. 검증: `CurrentSaveData != null`일 때만 Save
3–4. 런타임/SaveData: 이 메서드 내 추가 변경 없음
5. Save 호출: 있음
6. Save 결과 확인: **없음**
7. Event: 없음
8. UI: 없음
9. Scene: `SceneFlow.GoToTitle()` — Save 성공과 무관
10. 반환: `void`

#### 실패 상태

* Save 실패해도 Title로 이동
* 위험도: **P1**

---

### S04 — 무역 출발 (`TradeStartService.TryStartTrade`)

```text
ID: S04
기능: 무역 출발
파일: Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeStartService.cs
클래스: TradeStartService
메서드: TryStartTrade
호출 경로 예:
  TradePreparePanel / TradePrepareRuntimeContextProvider
  → TradeStart.TryStartTrade
  Debug: TradeStartDebugHarness
Production / Debug / Test: Production
```

#### 실행 순서 (코드 근거)

1. 입력: `caravan`, `distanceKm`, `tradeId`, `routeId`, `saveImmediately`
2. 검증: `CaravanValidator.Validate` → recorder null 검사
3. **SaveData 변경(선기록)**: `TradeProgressRecorder.RecordStartedTrade`  
   (`activeTradeId`, `state=Traveling`, start/end UTC tick 등)
4. Record 실패 시: `canDepart=false` 반환 (Core 출발 안 함)
5. **런타임 상태 변경**: `JourneyRunner.TryDepart`  
   - Core 실패 시: **이미 기록된 tradeProgress를 되돌리지 않음** (파일 헤더·구현 모두 명시)
6. Settlement cache clear + `CaravanSaveDataMapper.CopyToSave` + `SetActiveCaravan`
7. **화면 전환**: `InGameScreenRouter.RequestScreen(Traveling)` — **Save 이전**
8. Save 호출: `saveImmediately`이고 saveService 있으면 `saveService.Save(saveData)`
9. Save 결과 확인: **없음**
10. Event: 이 메서드 내 Framework trade-start completion event **없음**
11. 반환: `DepartureValidationResult` (`canDepart` 등). Save 실패와 무관하게 Core 성공이면 성공 결과 반환 가능

#### 실패 상태

| 항목 | 관찰 |
| --- | --- |
| Save 실패 시 런타임 | Traveling caravan + Traveling 화면 요청 유지 |
| Save 실패 시 메모리 SaveData | Traveling / caravan 반영 상태 유지 |
| Save 실패 시 디스크 | 이전 상태 가능 |
| Event | 출발 전용 completion event 없음 |
| UI/Scene | Traveling 화면은 Save 전에 요청됨 |
| 재시도 시 중복 위험 | 동일 tradeId Traveling이면 `RecordStartedTrade`가 false. Core 실패 후 SaveData만 Traveling인 잔존 상태 가능(헤더 경고) |
| 복구 수단 | 자동 rollback 코드 **확인되지 않음** |

#### 소유권 / 위험도

* 기술 책임: Framework & Integration
* 위험도: **P0**  
  - 상태/화면이 Save보다 먼저 확정됨  
  - Save 실패를 성공과 구분하지 못함  
  - Core 실패 시 SaveData Traveling 잔존

---

### S05 — 진행률 중간 저장 (`CheckProgressAndCompletion`)

```text
ID: S05
기능: Traveling 중 progress 갱신 후 선택적 Save
파일: TradeProgressCoordinator.cs
메서드: CheckProgressAndCompletion(bool saveProgress = true)
Production / Debug / Test: Production API
```

#### 실행 순서

1. Traveling·pause·caravan 검증
2. `SyncElapsedInGameSeconds` → `JourneyRunner.SetProgress` → `CopyToSave`
3. 미도착이면 `saveProgress`일 때만 Save
4. 도착/실패면 `SettleActiveTrade`로 위임(S07)

#### 현재 production 루프

* `FrameworkRoot.Update`는 `CheckProgressAndCompletion(saveProgress: false)` 호출  
  → **프레임마다 중간 Save는 하지 않음**
* 중간 Save는 명시적으로 `saveProgress: true`를 넘기는 호출자(Debug/테스트 등)에 한정

#### 위험도

* **P2** (기본 루프는 디스크 미기록). API 자체는 Save 실패 무시.

---

### S06 — 오프라인 진행 (`ApplyOfflineProgressOnLoad`)

```text
ID: S06
기능: Continue/Load 시 Traveling 오프라인 복구
파일: TradeProgressCoordinator.ApplyOfflineProgressOnLoad
호출 경로: FrameworkRoot.CompleteLoadingAndEnterGame
Production / Debug / Test: Production
```

#### 실행 순서

1. Traveling 검증
2. evaluationUtc 계산(역행 시 `TimeRollbackDetected` 후 return, 상태 미변경)
3. elapsed/progress 동기화 → `CopyToSave`
4. 미도착: **Save 호출** → return false
5. 도착: `SettleActiveTrade`(S07) → 성공 시 `RaiseTradeOfflineCompleted`

#### 실패 상태

* 미도착 경로: Save 실패해도 메모리 elapsed는 갱신된 채 Loading→InGame 계속
* 위험도: **P1**

---

### S07 — 정산 생성 (`SettleActiveTrade`)

```text
ID: S07
기능: Traveling → SettlementPending + pendingSettlement 영속화
파일: TradeProgressCoordinator.cs (private SettleActiveTrade)
호출 경로:
  CheckProgressAndCompletion / ApplyOfflineProgressOnLoad / ForceCompleteActiveTrade
Production / Debug / Test: Production (ForceComplete는 Debug 경로에서도 진입)
```

#### 실행 순서

1. `CanCreateSettlement` (Traveling + activeTradeId)
2. `JourneyRunner.Settle` → runtime Settling + result
3. `MarkSettlementPending` (SaveData.state)
4. `LastSettlementTradeId` / `LastSettlementResult` 설정
5. Economy preview `TryCalculateAndFill` (화폐는 아직 미반영 — bridge 주석)
6. `pendingSettlement = ToSave(...)`
7. `CopyToSave(caravan)`
8. **Save 호출**
9. Save 결과 확인: **없음**
10. **Event**: `FrameworkEvents.RaiseTradeSettlementReady` — Save **호출 이후**, 성공 여부 미확인
11. **화면**: `RequestScreen(Settlement)`
12. 반환: true

#### 실패 상태

| 항목 | 관찰 |
| --- | --- |
| Save 실패 시 런타임 | Settling + LastSettlementResult 유지 |
| Save 실패 시 메모리 SaveData | SettlementPending + pendingSettlement 유지 |
| Save 실패 시 디스크 | Traveling(이전) 가능 |
| Event | **발행됨** |
| UI | Settlement 화면 요청됨 |
| 재시도/재로드 | 디스크가 Traveling이면 재진입 시 다시 settle 가능 → 팀 런타임 검증 필요 |
| 복구 | pendingSettlement가 디스크에 없으면 Restore 불가 |

#### 위험도

* **P0** — 성공 Event/UI가 durable success와 분리됨; 디스크와 메모리 불일치 가능

---

### S08 — 정산 Claim (`ClaimSettlementAndReset`)

```text
ID: S08
기능: 정산 수령 및 Preparation 복귀
파일: TradeProgressCoordinator.ClaimSettlementAndReset
UI 경로: SettlementUiDataAdapter.OnClickClaimSettlement
  → SettlementUiBridge.ClaimSettlementAndReset
  → TradeProgressCoordinator.ClaimSettlementAndReset
Production / Debug / Test: Production
```

#### Bridge 측 추가 순서

* `isClaimProcessing`로 중복 클릭 차단
* `IsPendingSettlementValid` 후 coordinator 호출
* coordinator 성공 시 bridge `ClearPendingSettlement`

#### Coordinator 실행 순서

1. SaveData/caravan/`CanClaimCachedSettlement` 검증
2. `JourneyRunner.ClaimSettlement` (runtime)
3. **`TryApplyPendingEconomy`** — `tradingCurrency` / `developmentCurrency` / growth level 등 **Save보다 먼저** 메모리 반영  
   (Economy 실패 시 warning만 하고 claim 계속 가능)
4. `MarkCompleted` 또는 `MarkFailed`
5. `JourneyRunner.ResetToPrepare`
6. **`PendingSettlementSaveDataMapper.Clear`** — Save **이전**에 pending clear
7. prepare commit `TryComplete`
8. `CopyToSave` → **Save**
9. Save 결과 확인: **없음**
10. Event: claim 전용 Framework completion event **없음** (이 메서드 내)
11. 화면: `RequestScreen(Preparation)`
12. `ClearSettlementCache` (LastSettlementResult / Economy pending 삭제)
13. 반환: **true** (Save 성공과 무관)

#### 실패 상태

| 항목 | 관찰 |
| --- | --- |
| Save 실패 시 런타임 | Claimed + Prepare, 보상 메모리 반영, cache cleared |
| Save 실패 시 메모리 SaveData | Completed/Failed, pending cleared, 화폐 반영 |
| Save 실패 시 디스크 | SettlementPending + pendingSettlement(hasResult) **잔존 가능** |
| Event | claim completion event 없음 |
| UI/Scene | Preparation으로 전환 |
| 재시도 시 중복 위험 | 프로세스 종료 후 재로드 시 `RestorePendingSettlement`가 디스크 pending을 복구하면 **동일 정산 재claim 가능성** — 자동 rollback 코드 확인되지 않음. 팀·런타임 검증 필요 |
| 동일 세션 중복 | bridge `isClaimProcessing` + cache/state 검증으로 완화. Save 실패 후 동일 세션 재claim은 cache clear로 차단될 수 있음 |

#### 위험도

* **P0** — 보상 적용·pending clear·성공 반환이 Save durable success보다 앞섬

---

### S09 / S10 — Debug ForceSeason / ForceDisaster

```text
파일: FrameworkDebugCommands.cs
메서드: ForceSeason / ForceDisaster
Production / Debug / Test: Debug
```

#### 실행 순서

1. 검증 → `WorldSaveData` 필드 변경 → Save → 로그 → `true` 반환  
2. Save 결과 확인 없음에도 주석/반환은 “Save가 완료되면 true”로 기술되어 있음  
   → **void Save로는 durable success를 보장하지 못함** (문서-코드 불일치 후보)

#### 위험도: **P2**

---

### S11 — Debug Harness 명시 Save

```text
파일: TradeStartDebugHarness.cs
용도: Pending settlement restore smoke에서 cache clear 전 디스크 보장
Production / Debug / Test: Debug
위험도: P2
```

---

### S12–S16 — MarketInventorySession (조건부)

```text
파일: Assets/Scripts/UI/MarketInventoryIntegration.cs
심볼: #if ND_MARKET_SAVE_SCHEMA_VNEXT
현재 기본 빌드: 비활성 (scriptingDefineSymbols 비어 있음)
```

공통 패턴:

* 검증 → 재화/재고/cargo/preparation 변경 → `saveService.Save` → `CargoIntegrationResult.Ok`  
* Save 결과 미확인
* Commit은 `isCommitted`+hash로 일부 idempotent
* Event: Framework trade/settlement event 없음

심볼 활성화 시 **P0** 후보(재화 차감 후 Save 실패).

---

### S17 — Editor MemorySaveService

```text
파일: Assets/Scripts/UI/Editor/MarketInventoryIntegrationProbe.cs
용도: 인메모리 ISaveService stub
위험도: P2
```

---

## 6. Save 호출 없이 SaveData를 변경하는 관련 경로 (참고)

직접 `Save()`는 아니지만, 이후 S02/S03/무역 Save에 실려 나가거나 유실될 수 있는 경로.

| ID | 기능 | 파일/메서드 | Save 호출 | 비고 |
| --- | --- | --- | --- | --- |
| M01 | 재화 증감 | `PlayerMainManager.AddGold` / `SpendGold` | 없음 | UI 이벤트만. 디스크는 다음 Save에 의존 |
| M02 | 거점 인벤토리 | `PlayerMainManager.AddItem` / `RemoveItem` | 없음 | 동일 |
| M03 | 건물 업그레이드 | `VillageBuildingRegistry.WriteBuildingToSave` via `AddOrUpgrade` | 없음 | SaveData만 upsert |
| M04 | Awake 시 CurrentSaveData | `FrameworkRoot.InitializeServices` | 없음 | HasSaveData면 Load, 없으면 Create(미저장일 수 있음) |

기부·투자·대출:

* SaveDataPolicy 계약 문서만 존재
* production `ISaveService.Save` 호출부 **미발견** → **미구현(또는 미연결)** 으로 기록

성장 구매(플레이어/카라반 상점형):

* Economy `GrowthPurchaseCalculator` 등은 존재
* Framework 무역 claim 경로에서 `PurchaseGrowth = false`로 입력 조립
* **즉시 Save를 동반하는 standalone 성장 구매 production 호출부 미발견**

마차/동물/수리/용병/식량 구매:

* Market VNEXT 세션이 비활성인 현재 빌드에서는 Framework Save 연동 구매 경로 **미활성**
* Core/UI 프로토타입 재화 변경은 M01 등 메모리 변경에 해당할 수 있음 — 전체 구매 UX는 **확인 필요**

---

## 7. 위험도별 분류

### P0

| ID | 요약 |
| --- | --- |
| S04 | 출발: SaveData/화면이 Save보다 앞서 확정, Save 실패 무시, Core 실패 시 Traveling 잔존 |
| S07 | 정산 생성: Save 후 Event/화면이나 durable success 미확인; 디스크 Traveling 잔존 시 재settle 가능성 |
| S08 | Claim: 보상·pending clear·true 반환이 Save보다 앞섬; Save 실패 후 재로드 시 중복 수령 가능성(검증 필요) |
| S12–S15 | (심볼 ON 시) 구매/환불 후 Save 실패 무시 |

### P1

| ID | 요약 |
| --- | --- |
| S01 | 새 게임 Save 실패해도 Loading |
| S02 | 종료 Save 실패해도 Quit |
| S03 | Title Save 실패해도 Title 이동 |
| S06 | 오프라인 elapsed Save 실패 가능 |
| S16 | (심볼 ON 시) 재고 refresh Save 실패 |
| M01–M03 | Save 없는 경제/건물 변경 → 유실 또는 의도치 않은 묶음 저장 |

### P2

| ID | 요약 |
| --- | --- |
| S05 | 기본 루프는 중간 Save 안 함 |
| S09–S11 | Debug |
| S17 | Test stub |

### 확인 필요

* Save 실패를 실제로 재현했을 때의 디스크 부분 기록 여부(`File.WriteAllText` 원자성)
* Scene/Prefab에 연결된 추가 Save 호출(직렬화 콜백)
* 다른 팀원 미병합 브랜치의 Save 호출
* Claim 중복 수령의 런타임 E2E 재현
* PlayerMainManager / Village 변경이 어떤 Save에 묶이는지 제품 의도

---

## 8. 담당자별 분류

스크립트 헤더·주석 근거. 폴더명만으로 개인을 확정하지 않음.

| 영역 | 관련 ID | 근거 |
| --- | --- | --- |
| Framework | S01–S11, S05–S08 | CoreServices 헤더 `Framework & Integration` |
| Core Gameplay | M01–M03, JourneyRunner 상태 | `PlayerMainManager` 주석에 Core Gameplay(윤호영) 표기; JourneyRunner는 Core |
| UI & Data | SettlementUiDataAdapter, TradePrepare UI, MarketInventory* | UI 경로 호출. Market 파일 개인 소유 **확인 필요** |
| Progression & System | 기부/투자/대출 계약만 | production Save 호출 미발견 |
| Content & Tools | S09–S11, S17, Economy debug runners | Debug/Test |
| 소유자 확인 필요 | S12–S17, M01–M03의 기능 책임 경계 | 팀 확인 필요 |

---

## 9. 저장 실패 시 현재 동작

`JsonSaveService.Save`는 실패해도 예외를 던지지 않으므로, 아래는 **호출부 관측 동작**이다.

| 기능 | 런타임 변경 유지 | Event 발행 | UI/Scene 전환 | 중복 위험 | 복구 가능 |
| --- | --- | --- | --- | --- | --- |
| 새 게임 S01 | 예(새 CurrentSaveData) | 없음 | Loading | 낮음 | 확인 필요 |
| Exit S02 | 해당 없음 | 없음 | Quit | 유실 | 없음 |
| Return Title S03 | 해당 없음 | 없음 | Title | 유실 | 없음 |
| 출발 S04 | 예 | 출발 completion 없음 | Traveling(Save 전) | Core실패 잔존 / 재시도 복잡 | rollback 없음 |
| 오프라인 S06 | 예 | rollback event만 특수 | InGame 계속 | 확인 필요 | 부분 |
| 정산 생성 S07 | 예 | **TradeSettlementReady** | Settlement | 재settle 가능(검증 필요) | pending 디스크 없으면 약함 |
| Claim S08 | 예(보상 포함) | claim completion 없음 | Preparation | **재로드 중복 수령 가능(검증 필요)** | 자동 복구 없음 |
| ForceSeason/Disaster | 예 | 없음 | 없음 | 낮음 | Debug 재실행 |
| Market Commit(비활성) | 예 | 없음 | UI 결과 Ok | 재화/재고 불일치 | 확인 필요 |

---

## 10. SaveResult 적용 우선순위 제안

최종 정책이 아니다. 조사 결과 기반 **논의용** 순서.

1. **Settlement Claim (S08)** — 보상·pending clear·true 반환이 Save와 분리
2. **Settlement 생성 (S07)** — Event/UI가 Save durable success와 분리
3. **Trade Departure (S04)** — 상태/화면이 Save 이전, Core 실패 시 SaveData 잔존
4. **중요 구매/성장** — Market VNEXT 활성화 시 S13 등; 현재는 M01–M03 Dirty 유실도 포함 논의
5. **Return to Title / Exit / New Game (S03/S02/S01)** — 실패 무시 scene 전환
6. **오프라인 진행 (S06)**
7. **Debug/Test (S09–S11, S17)**

API 형태(`Save`→`SaveResult` vs `void Save`+`TrySave`)는 팀 회의 전 미확정.

---

## 11. Snapshot/Rollback 필요 후보

### 반드시 필요한 호출부 (논의 권장)

* S08 Claim — 다단계 mutation(보상, state, pending clear, caravan reset)
* S04 Departure — RecordStartedTrade와 TryDepart 사이 불일치, Save 전 UI

### 필요 가능성이 높은 호출부

* S07 Settlement 생성 — Settle + pending DTO + Event
* S13 Market Commit (심볼 ON 시) — 재화·재고·cargo 동시 변경

### 결과 확인만으로 충분할 수 있는 호출부

* S01/S02/S03 — 단일 Save 후 scene/quit (단, 실패 시 전환 정책은 별도 결정 필요)
* S09/S10 Debug — 단순 필드 1개
* S05 중간 progress — 현재 기본 루프 미사용

---

## 12. Event timing 전환 후보

| 흐름 | 현재 | 비고 |
| --- | --- | --- |
| S07 TradeSettlementReady | Save **호출 후** 발행 | durable success 미검증 → “Save 후”이지만 “committed 후”는 아님 |
| S08 Claim | Event 없음 | UI는 반환값·화면 전환에 의존 |
| S04 출발 | Framework completion event 없음 | 화면 Request가 Save 전 |
| RestorePendingSettlement | Save 없이 TradeSettlementReady 재발행 | 재구독/중복 UI 처리 필요(기존 정책과 정합) |
| S01/S03 | Event 없음 | Scene 전환만 |

전환 후보:

* Save 전 Event 발행: **현재 S04 화면 전환이 이에 가까움**
* Save 후 Event 발행: S07 (단, 결과 확인 필요)
* Event 없음: S01–S03, S08
* 확인 필요: UI 구독자가 Save 실패를 어떻게 표시하는지

---

## 13. 팀 회의 필요 항목

1. 최종 Save API: `SaveResult Save` vs `void Save` + `TrySave`
2. 중요 행동의 durable success 정의(출발/정산/claim/구매)
3. 상태 변경과 Save 순서(선 Save vs 선 mutation + rollback)
4. Rollback 책임(Framework vs 각 feature)
5. Event 발행 시점(committed-only로 옮길지)
6. 담당자별 수정 범위(Framework / Core / UI / Progression)
7. 우선 적용 순서(본 문서 §10 초안)
8. Market `ND_MARKET_SAVE_SCHEMA_VNEXT` 활성화 일정과 Save 계약
9. PlayerMainManager/Village의 Dirty vs 즉시 Save 정책
10. 기부·투자·대출 구현 착수 시점과 Save 계약 적용

---

## 14. 후속 브랜치 제안

조사 결과 기반 제안(확정 아님).

```text
feature/framework/save-result-api              (이미 존재 — 회의 후 병합 여부 결정)
feature/framework/settlement-claim-transaction
feature/framework/trade-departure-save-result
feature/framework/settlement-create-committed-events
feature/framework/title-exit-save-gate
docs/framework/save-api-owner-migration        (가이드 문서가 경로에 없을 때)
chore/integration/market-vnext-save-hardening  (심볼 활성화와 함께)
```

---

## 15. 미확인 항목

* Unity serialized Button → Save 직접 연결 여부(Editor에서 Scene/Prefab 확인 필요)
* `File.WriteAllText` 실패 시 부분 파일 상태
* 다른 팀원 미병합 브랜치의 추가 Save 호출
* Claim 중복 지급의 Play Mode 재현
* `Framework_API_Event_Inventory.md` 템플릿을 본 조사로 채울지 여부
* Sandbox(`99.Sandbox`)의 별도 SaveData 타입 — Framework `ISaveService` 호출은 검색상 없음
* `ContinueGame`은 Save를 호출하지 않음(의도된 Load-only)

---

## 16. 발견한 문제 기록 형식 (수정하지 않음)

### 문제 A — Claim이 Save durable success 없이 성공 처리

* 문제: 보상 적용·pending clear·Preparation 전환·`true` 반환이 `void Save` 이후에도 실패를 반영하지 않음
* 근거: `TradeProgressCoordinator.ClaimSettlementAndReset` 순서; `JsonSaveService.Save` 예외 흡수
* 현재 영향: Save 실패 직후 메모리와 디스크 불일치; 재로드 시 pending 복구로 재claim 가능 여부 검증 필요
* 위험도: P0
* 책임 영역: Framework (coordinator) + UI claim 진입점
* 권장 후속 작업: SaveResult + staging/rollback 또는 claim transaction 브랜치
* 팀 논의 필요: 예

### 문제 B — 출발이 Save 전 Traveling UI 확정

* 문제: `RequestScreen(Traveling)`이 Save보다 앞섬; Save 실패 무시
* 근거: `TradeStartService.TryStartTrade`
* 현재 영향: UI/런타임 Traveling vs 디스크 불일치 가능
* 위험도: P0
* 책임 영역: Framework
* 권장 후속 작업: departure save-result + 화면 전환 시점 이동
* 팀 논의 필요: 예

### 문제 C — 정산 생성 Event가 durable success와 분리

* 문제: `RaiseTradeSettlementReady`가 Save 호출 직후, 성공 확인 없이 발행
* 근거: `SettleActiveTrade`
* 현재 영향: UI Settlement 진입과 디스크 Traveling 잔존 가능
* 위험도: P0
* 책임 영역: Framework
* 권장 후속 작업: committed-save events
* 팀 논의 필요: 예

### 문제 D — Core 출발 실패 시 SaveData Traveling 잔존

* 문제: `RecordStartedTrade` 후 `TryDepart` 실패 시 tradeProgress 미복원
* 근거: `TradeStartService` 헤더 및 구현
* 현재 영향: SaveData만 Traveling인 불완전 상태
* 위험도: P0
* 책임 영역: Framework (+ Core 검증 경계)
* 권장 후속 작업: record/depart 원자화 또는 실패 시 rollback
* 팀 논의 필요: 예
