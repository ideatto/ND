# Multi-Caravan Trade Start Command 구현 로직

- 작성일: 2026-07-22
- 담당: Framework & Integration (CSU)
- 브랜치: `feature/framework/multi-caravan-trade-start`
- 기준 브랜치: `dev2`
- 상태: 워킹 트리 구현 완료(커밋 전). Editor E2E(`RunMultiCaravanDepartureCommandChecks`) 포함
- 선행 작업: `Docs/Personal_Documents/CSU/0721_multi_caravan_save_cutover.md`
- 상위 계약: `Docs/Personal_Documents/CSU/SaveDataPolicy/Multi_Caravan_Save_Architecture.md`

## 1. 목적

선행 Save Cutover(version 6)로 **저장 모델**은 caravan ID 기반 컬렉션으로 전환되었지만, 무역 출발 API는 여전히 `TryStartTrade(CaravanData, ...)` 형태로 **선택 caravan 호환 경로**에 묶여 있었다.

이번 작업은 플레이어/UI가 호출할 **caravan ID 기반 출발 command**를 추가하여 다음을 만족한다.

- 특정 `caravanId` + `routeId`로 출발을 요청할 수 있다
- caravan A가 Traveling이어도 caravan B는 독립적으로 출발할 수 있다
- 같은 caravan의 중복 출발, SettlementPending 차단, Core 거부, 저장 실패를 **결과 타입으로 구분**한다
- 출발 성공 시 해당 caravan의 `tradeProgressEntries[]`만 갱신하고 다른 caravan 상태를 침범하지 않는다

이번 범위에 **포함하지 않는 것**:

- UI/TradePrepare 쪽에서 `Depart(...)`를 실제로 호출하는 연결 (후속 PR)
- `TradeProgressCoordinator`의 caravanId 인자 API 전면 전환
- 비선택 caravan 출발 시 runtime coordinator active caravan 등록 (현재 `setActiveCaravan`은 저장 성공 후 호출되나, 화면 전환은 선택 caravan에만 적용)
- `TryStartTrade(...)` 레거시 경로 제거 (debug harness / M1 loop smoke는 계속 사용)

## 2. 변경 파일 요약

| 파일 | 역할 |
|---|---|
| `TradeStartService.cs` | `Depart(...)` command, 요청/결과 DTO, caravan별 재진입 방지, snapshot rollback |
| `TradeProgressRecorder.cs` | `RecordStartedTrade(saveData, caravanId, ...)` 오버로드, `SaveDataLookup` 기반 per-caravan 기록 |
| `FrameworkRoot.cs` | `TradeStartService` 생성 시 `getSharedGameData` 주입 |
| `FrameworkM1LoopE2EEditorTests.cs` | `RunMultiCaravanDepartureCommandChecks` 추가 |

Scene / Prefab / Meta / Package 변경 없음.

## 3. 책임 분리

```text
TradeDepartureRequest
  └─ CaravanId, RouteId (최소 입력)

TradeStartService.Depart(...)
  └─ 프레임워크 검증 → Core 검증 → progress 기록 → Core 출발 → 저장 → (선택 caravan만) 화면 전환
  └─ caravanId 단위 재진입 잠금 (HashSet)

TradeProgressRecorder.RecordStartedTrade(saveData, caravanId, ...)
  └─ 지정 caravan의 tradeProgressEntries[] 항목 생성/갱신
  └─ 해당 caravan의 elapsedInGameSeconds = 0

SaveDataLookup
  └─ caravan / progress / pending ID 조회·쓰기 (선행 컷오버에서 도입, 이번 작업의 기반)

TryStartTrade(...)  [기존, 유지]
  └─ selectedCaravanId 호환 경로 (debug, rescue loan, M1 loop E2E)
  └─ 구조 대출 제한 해제, settlement cache clear 포함
```

## 4. 공개 API

### 4.1 요청

```csharp
public sealed class TradeDepartureRequest
{
    public string CaravanId { get; set; }  // caravans[].caravanId
    public string RouteId { get; set; }    // SharedGameData route catalog ID
}
```

### 4.2 결과

```csharp
public sealed class TradeDepartureResult
{
    public bool DepartureSucceeded { get; }   // Core 출발까지 성공 (저장 실패 rollback 후에도 true)
    public string TradeId { get; }            // 성공 시 생성된 GUID(D 형식), 거부 시 ""
    public TradeDepartureFailureReason FailureReason { get; }
    public DepartureValidationResult CoreResult { get; }  // CoreRejected일 때만
    public SaveResult SaveResult { get; }                 // Core 통과 후 저장 시도 시
    public bool SaveSucceeded => SaveResult != null && SaveResult.Succeeded;
}
```

### 4.3 실패 사유 (`TradeDepartureFailureReason`)

| 값 | 발생 조건 | SaveData 변경 |
|---|---|---|
| `InvalidRequest` | request null, CaravanId/RouteId 공백 | 없음 |
| `RequestInProgress` | 동일 caravanId 출발이 이미 처리 중 | 없음 |
| `CaravanNotFound` | `SaveDataLookup.TryGetCaravan` 실패 | 없음 |
| `AlreadyTraveling` | 해당 caravan progress.state == Traveling | 없음 |
| `SettlementPending` | progress.state == SettlementPending **또는** pending settlement 보유 | 없음 |
| `ActiveTradeExists` | progress entry는 있으나 lookup 실패(중복/손상) **또는** activeTradeId 잔존 | 없음 |
| `RouteNotFound` | SharedGameData route 조회 실패 | 없음 |
| `CoreRejected` | `CaravanValidator.Validate` 또는 `JourneyRunner.TryDepart` 실패 | snapshot rollback |
| `RecordFailed` | `TradeProgressRecorder` 기록 실패 | 없음 (기록 전 단계) |
| `SaveFailed` | Core 출발 성공 후 `ISaveService.Save` 실패 | snapshot rollback |
| `None` | 출발·저장 모두 성공 | 확정 반영 |

**호출자 판정 가이드**

- `DepartureSucceeded == false` → Core 출발 자체가 이루어지지 않았거나, 기록 전 단계에서 거부됨
- `DepartureSucceeded == true && SaveSucceeded == false` → Core 출발은 수행됐으나 저장 실패로 **메모리 상태는 rollback**됨. `FailureReason == SaveFailed`
- `DepartureSucceeded == true && SaveSucceeded == true` → 영속 저장까지 완료

## 5. `Depart(...)` 처리 흐름

### 5.1 개요

```text
Depart(request)
  ├─ [1] 입력 검증
  ├─ [2] caravanId 재진입 잠금 (departureRequestsInProgress)
  ├─ [3] DepartInternal(caravanId, routeId)
  │     ├─ caravan 존재 확인
  │     ├─ 해당 caravan progress/pending 상태 검증
  │     ├─ SharedGameData route 확인
  │     ├─ CaravanValidator.Validate (사전 Core 검증)
  │     ├─ snapshot 캡처 (runtime caravan, caravan save, progress|null)
  │     ├─ tradeId = Guid.NewGuid().ToString("D")
  │     ├─ RecordStartedTrade(saveData, caravanId, tradeId, routeId, expectedDuration)
  │     ├─ JourneyRunner.TryDepart(runtimeCaravan, route.Distance)
  │     ├─ CaravanSaveDataMapper.CopyToSave(runtime, caravanSave)
  │     ├─ saveService.Save(saveData)
  │     ├─ (실패 시) RestoreCommandSnapshot
  │     └─ (성공 시) setActiveCaravan + (선택 caravan이면) Traveling 화면
  └─ [4] 재진입 잠금 해제 (finally)
```

### 5.2 Multi-caravan 핵심 불변식

1. **검증은 caravanId 스코프** — 다른 caravan의 Traveling 여부는 출발 차단 조건이 아니다.
2. **progress 기록은 caravanId 스코프** — `SaveDataLookup.SetTradeProgress(saveData, caravanId, progress)`로 해당 entry만 갱신한다.
3. **tradeId는 요청마다 새로 생성** — caravan A/B가 동시에 출발해도 서로 다른 tradeId를 가진다.
4. **화면 전환은 선택 caravan만** — `saveData.selectedCaravanId == caravanId`일 때만 `InGameScreenState.Traveling` 요청.

### 5.3 Snapshot / Rollback (`RestoreCommandSnapshot`)

Core 최종 출발 또는 저장 실패 시, 출발 시점 이전 상태로 되돌린다.

| 대상 | 복원 방법 |
|---|---|
| `CaravanSaveData` | `caravanSnapshot` JSON overwrite |
| `CaravanData` (runtime) | `runtimeSnapshot` JSON overwrite |
| `TradeProgressSaveData` | snapshot null → `SetTradeProgress(..., null)` (entry 제거) |
| | snapshot 존재 → JSON deserialize 후 `SetTradeProgress` |

선행 컷오버 문서(§9)와 동일한 원칙: **존재하지 않던 progress entry를 rollback 중 새로 만들지 않는다.**

### 5.4 재진입 방지

`departureRequestsInProgress` (`HashSet<string>`)는 **caravanId 단위** 잠금이다.

- 첫 `Depart` 호출이 `Save()` 내부에서 아직 완료되지 않은 동안, 같은 caravanId로 두 번째 `Depart`가 들어오면 `RequestInProgress`를 반환한다.
- 다른 caravanId는 독립적으로 동시 호출 가능하다 (잠금 키가 caravanId이므로).

## 6. `TradeProgressRecorder` 변경

### 6.1 오버로드 분리

| 메서드 | 대상 caravan | 용도 |
|---|---|---|
| `RecordStartedTrade(saveData, tradeId, routeId, duration)` | `saveData.selectedCaravanId` | `TryStartTrade` 호환 경로 |
| `RecordStartedTrade(saveData, caravanId, tradeId, routeId, duration)` | 명시적 `caravanId` | `Depart` command |

### 6.2 per-caravan 기록 로직

1. `SaveDataLookup.TryGetCaravan` — caravan 없으면 false
2. `SaveDataLookup.TryGetTradeProgress` — 없으면 `{ caravanId }` 새 DTO 생성
3. 중복 Traveling tradeId / 다른 active trade 덮어쓰기 차단 (기존과 동일)
4. UTC 시작·예상 종료 tick, routeId, Traveling state 기록
5. **해당 caravan**의 `elapsedInGameSeconds = 0`
6. `SaveDataLookup.SetTradeProgress(saveData, caravanId, progress)` — 컬렉션에 반영

기존 `saveData.tradeProgress = new ...` / `saveData.caravan.elapsedInGameSeconds` 직접 접근은 제거되었다.

## 7. `FrameworkRoot` 배선

```csharp
TradeStart = new TradeStartService(
    () => CurrentSaveData,
    SaveService,
    TradeProgressRecorder,
    InGameScreenRouter,
    ClearSettlementRuntimeCache,
    TradeProgressCoordinator.SetActiveCaravan,
    () => SharedGameData);   // ← 이번 작업에서 추가
```

`getSharedGameData`는 `Depart` 내부에서 route catalog 조회(`TryGetRoute`)에 사용된다.  
`TryStartTrade`는 호출자가 distance/route를 직접 넘기므로 이 주입과 무관하게 동작한다.

## 8. `TryStartTrade` vs `Depart` 비교

| 항목 | `TryStartTrade` | `Depart` |
|---|---|---|
| 입력 | runtime `CaravanData` + distance + tradeId + routeId | `caravanId` + `routeId` |
| caravan 소스 | 호출자가 runtime 객체 제공 | SaveData에서 `CaravanSaveDataMapper.ToRuntime` |
| tradeId | 호출자 지정 | 서비스 내부 GUID 생성 |
| route 거리 | 호출자가 distanceKm 전달 | SharedGameData route.Distance |
| 대상 caravan | selectedCaravanId (progress 기록) | 요청의 caravanId |
| 구조 대출 제한 해제 | 있음 (`rescueLoan.isRestrictedPreparation`) | **없음** (플레이어 command는 별도 정책) |
| settlement cache clear | `clearSettlementCache` callback | **없음** |
| 화면 전환 | 항상 Traveling | 선택 caravan일 때만 |
| 반환 타입 | `DepartureValidationResult` | `TradeDepartureResult` (저장 결과 분리) |
| 주 사용처 | debug harness, M1 loop E2E, rescue loan | UI/플레이어 출발 (연결 예정) |

두 API는 공존한다. M1 loop / rescue loan 회귀는 기존 `TryStartTrade` 경로로 유지된다.

## 9. 시나리오별 동작 요약

| 시나리오 | 결과 | 근거 |
|---|---|---|
| 유효 caravan Preparation → 출발 | 성공, Traveling, tradeId 생성, Save 1회 | happy path |
| 같은 caravan Traveling 중 재요청 | `AlreadyTraveling`, Save 호출 없음 | progress.state 검사 |
| caravan A Traveling, caravan B 출발 | B만 성공, tradeId 독립 | caravanId 스코프 검증·기록 |
| caravan SettlementPending / pending 보유 | `SettlementPending` | progress + pending 검사 |
| 존재하지 않는 caravanId | `CaravanNotFound` | 예외 없이 거부 |
| Core 검증 실패 (빈 caravan 등) | `CoreRejected`, Save 호출 없음 | Validate 거부 |
| routeId 없음 | `RouteNotFound` | SharedGameData |
| Core 출발 성공 + Save 실패 | `DepartureSucceeded=true`, `SaveFailed`, rollback | RestoreCommandSnapshot |
| 같은 caravan 처리 중 재호출 | `RequestInProgress` | HashSet 잠금 |

## 10. Editor E2E 검증

### 10.1 실행 방법

```text
Unity Editor: ND/Framework/Run M1 Loop + Economy E2E Checks
batchmode:    -executeMethod ND.Framework.Editor.FrameworkM1LoopE2EEditorTests.RunAllFromBatchMode
```

NUnit `[Test]` 기반이 아니므로 `-runTests`가 아닌 `-executeMethod`로 실행한다.

### 10.2 `RunMultiCaravanDepartureCommandChecks`

`RunMultiCaravanSaveDataChecks` 직후, rescue loan 검증 전에 실행된다.

검증 항목:

1. caravan A 정상 출발 (TradeId, Traveling, SaveCalls==1)
2. caravan A 중복 출발 차단 (`AlreadyTraveling`, SaveCalls 불변)
3. caravan B 독립 출발 (서로 다른 tradeId, 각 progress entry 독립)
4. pending settlement 보유 caravan → `SettlementPending`
5. missing caravanId → `CaravanNotFound`
6. Core 거부 / invalid route → `CoreRejected` / `RouteNotFound`, SaveCalls 불변
7. Save 실패 → `SaveFailed`, rollback (Traveling 상태·progress entry 없음)
8. Save 콜백 중 재진입 → `RequestInProgress`, Save 1회

2026-07-22 batchmode 실행 결과: 전체 스위트 exit 0, `"[Framework M1 E2E] All checks passed."`

## 11. 후속 작업 (이번 PR 이후)

- TradePrepare UI / LJH adapter에서 `FrameworkRoot.Instance.TradeStart.Depart(...)` 연결
- 비선택 caravan 출발 시 coordinator·world map UI 반영 정책 확정
- 필요 시 `Depart`에 settlement cache clear / rescue loan 제한 해제 정책 추가 여부 결정
- Coordinator claim/settle API의 caravanId 인자 전환 (선행 문서 §「포함하지 않는 것」 참고)

## 12. 관련 문서

| 문서 | 관계 |
|---|---|
| `0721_multi_caravan_save_cutover.md` | SaveData v6, SaveDataLookup, 호환 accessor |
| `0721_multi_caravan_atomic_claim.md` | 정산 claim caravanId 경로 (별도 PR) |
| `0710_M1_Trade_Loop_Integrity.md` | TryStartTrade 기반 loop smoke |
| `LJH/0721_TradePrepare_Multi_Caravan_Provider_Integration_Request.md` | UI 쪽 multi-caravan 연동 요청 |
