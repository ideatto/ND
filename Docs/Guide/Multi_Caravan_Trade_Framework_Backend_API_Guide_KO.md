# Multi-Caravan Trade Framework Backend API Guide

> 프로젝트: 누워서 돈벌기  
> 영역: Framework & Integration  
> 대상: UI & Data / Scene Owner  
> 기준 브랜치: `feature/framework/multi-caravan-arrival-sale-identity`  
> 기준 커밋: `dfbb554f06795d7df235920d1fa9f20dd9f2ab9f`  
> 문서 상태: 코드 조사 기반 초안  
> 조사 기준: Working tree clean

---

# 1. 문서 목적

이 문서는 멀티 Caravan 무역 흐름을 UI에서 연결할 때 사용할 **현재 구현 기준 Framework 백엔드 API**를 정리한다.

실제 구현 흐름은 다음과 같다.

```text
Caravan 선택
→ 준비
→ 출발
→ Traveling
→ 도착 처리
→ SettlementPending 생성
→ 도착 판매 UI 열기
→ 품목 판매 Commit(선택)
→ Settlement 화면 표시
→ Exact Claim
→ Town / Preparation 복귀
```

중요한 점은 현재 코드가 아래와 같이 동작한다는 것이다.

```text
계획 문서상 흐름
Traveling → Selling → SettlementPending

현재 실제 구현
Traveling → SettlementPending
                ↓
        Market SellOnly 판매 세션
                ↓
        Settlement 화면 표시
```

즉, 현재 Framework에는 `TradeProgressState.Selling`과 Selling 진입·완료 Command가 없다. 판매는 `SettlementPending` 상태를 유지한 채 `ND.UI.Market`의 판매 경로를 이용한다.

이 문서는 다음을 포함한다.

- UI에서 사용할 Query API
- UI에서 사용할 Command API
- UI에서 구독할 Event
- Request, Result, ViewData 타입
- `caravanId + tradeId` 식별 계약
- 저장 및 rollback 규칙
- 사용 금지 API
- 현재 UI-facing API 공백

Scene, Prefab, Button, TextMeshPro 연결은 별도 UI 통합 문서에서 다룬다.

---

# 2. 책임 경계

## 2.1 Framework 책임

- Caravan과 Trade의 권위 식별
- Runtime과 SaveData 연결
- Core 출발·진행·정산 호출
- 저장 성공 전후 순서 보장
- 저장 실패 시 rollback
- 재실행 후 Traveling·SettlementPending 복구
- Query, Command, Event 제공
- 다른 Caravan과의 처리 격리

## 2.2 Market/UI 판매 계층 책임

- 도착 판매 세션 열기
- 판매 수량 Draft 관리
- 판매 가격과 예상 결과 표시
- Cargo·시장 재고·공유 재화 변경
- 판매 Commit 저장 및 rollback
- 판매 후 Settlement 표시 요청

## 2.3 UI에서 직접 하면 안 되는 작업

- `FrameworkRoot.Instance.CurrentSaveData` 직접 수정
- `SaveData.caravan`, `SaveData.tradeProgress`, `SaveData.pendingSettlement` 단일 facade 직접 사용
- Runtime Caravan 직접 변경
- Cargo 수량 직접 감소
- 판매 수익 직접 증가
- Pending Settlement 직접 제거
- `selectedCaravanId`를 실제 처리 대상의 권위 ID로 사용
- `LastSettlementResult`를 durable 권위 데이터로 사용
- Command 성공 전에 화면 상태를 확정

---

# 3. 핵심 식별 계약

## 3.1 권위 식별자

| 식별자 | 역할 | 권위 수준 |
|---|---|---|
| `caravanId` | Caravan 소유권과 처리 대상 식별 | 권위 |
| `tradeId` / `activeTradeId` | 개별 무역 실행 식별 | 권위 |
| `tradeProgressEntries` | 복수 Caravan 진행 상태 저장소 | 권위 |
| `pendingSettlements` | 복수 정산 대기 저장소 | 권위 |
| `(caravanId, tradeId)` | 정산 표시·조회·Claim의 정확한 대상 | 권위 |
| `selectedCaravanId` | 현재 UI 선택 또는 호환 cursor | 비권위 |
| `ActiveCaravan` | selected Caravan 기반 호환 facade | 비권위 |
| `LastSettlementResult` | 마지막 런타임 캐시 | 비권위 |
| Bridge presented IDs | 현재 정산 표시 cursor | presentation 전용 |

## 3.2 UI 규칙

1. Caravan별 처리는 항상 `caravanId`를 전달한다.
2. Trade 또는 Pending 대상 처리는 `caravanId + tradeId`를 함께 전달한다.
3. `selectedCaravanId`는 화면 선택에만 사용한다.
4. Event payload를 장기 상태로 보관하지 않고, Event 수신 후 Query를 다시 수행한다.
5. `tradeId == null`을 exact pending 조회에 사용하지 않는다. 같은 Caravan에 여러 Pending이 존재하면 모호해질 수 있다.

---

# 4. 현재 상태 모델

## 4.1 `TradeProgressState`

```csharp
None
Preparing
Traveling
SettlementPending
Completed
Failed
```

`Selling`은 없다.

## 4.2 `JourneyState`

```csharp
Prepare
Traveling
Settling
Completed
Selling
```

Core enum에는 `Selling`이 있으나, 현재 조사된 Framework/Market 경로에서는 이를 도착 판매 lifecycle의 권위 상태로 사용하지 않는다.

## 4.3 `InGameScreenState`

```csharp
Preparation
Traveling
Settlement
Town
Market
```

Selling 전용 화면 상태는 없으며 판매 화면은 `Market`을 사용한다.

## 4.4 실제 상태 전이

| From | To | 실행 경로 | 저장 | 성공 Event |
|---|---|---|---:|---|
| `Preparing/None` | `Traveling` | `TradeStartService.Depart` | Yes | 화면/출발 관련 경로 |
| `Traveling` | `SettlementPending` | Coordinator settle | Yes | `TradeSettlementReady` |
| `SettlementPending` | 동일 | Market 판매 Commit | Yes | `CaravanCargoChanged`, `TradingCurrencyChanged` |
| `SettlementPending` | Settlement 화면 | `PresentSettlement` | No | Bridge `SettlementReady`, `InGameScreenChanged` |
| `SettlementPending` | `Completed/Failed` 후 Core Prepare | exact `ClaimSettlement` | Yes | 재화 변경 및 화면 전환 |
| Town | `Preparing` | 현재 selected 기반 preparation entry | Yes | Preparation 화면 |

---

# 5. FrameworkRoot 접근

```csharp
var root = FrameworkRoot.Instance;
```

## 5.1 UI에서 사용 가능한 주요 프로퍼티

| 프로퍼티 | 타입 | 사용 판단 | 목적 |
|---|---|---:|---|
| `TradeStart` | `TradeStartService` | 사용 | 명시적 Caravan 출발 |
| `TradeProgressCoordinator` | `TradeProgressCoordinator` | exact API만 사용 | 진행·Pending·Claim |
| `SettlementUiBridge` | `SettlementUiBridge` | 사용 | 정산 표시와 exact Claim |
| `CaravanManagement` | `CaravanManagementService` | 사용 | Caravan 생성 |
| `SharedGameData` | `ISharedGameDataProvider` | 사용 | ID 기반 정의 조회 |
| `InGameScreenRouter` | `InGameScreenStateRouter` | 주의 | selected 기반 매핑 존재 |
| `SceneFlow` | `SceneFlowService` | 사용 | 씬 전환 |
| `CurrentSaveData` | `SaveData` | 읽기도 주의, 수정 금지 | raw mutable 저장 데이터 |
| `TradeProgressRecorder` | `TradeProgressRecorder` | 사용 금지 | 내부 기록기 |
| `DebugCommands` | `FrameworkDebugCommands` | 제품 UI 사용 금지 | 디버그 전용 |

## 5.2 FrameworkRoot에 노출되지 않는 판매 계층

다음 타입은 존재하지만 FrameworkRoot public property로 직접 노출되지 않는다.

- `CaravanArrivalSaleController`
- `MarketTradePanelController`
- `MarketInventoryMutationSession`
- `JourneyRunner`
- `EconomyM1SettlementBridge`

판매 UI는 현재 Scene/Component 참조를 통해 `CaravanArrivalSaleController`와 `MarketTradePanelController`를 사용한다.

---

# 6. Query API

## 6.1 특정 Caravan 저장 데이터 조회

```csharp
public static bool TryGetCaravan(
    SaveData data,
    string caravanId,
    out CaravanSaveData caravan);
```

- 명시적 `caravanId`
- raw SaveData DTO 반환
- UI가 반환 객체를 수정해서는 안 된다.

## 6.2 특정 Caravan 진행 상태 조회

```csharp
public static bool TryGetTradeProgress(
    SaveData data,
    string caravanId,
    out TradeProgressSaveData progress);
```

UI에서 확인 가능한 주요 값:

- `caravanId`
- `activeTradeId`
- `activeRouteId`
- `state`
- 시작 UTC tick
- 종료 예정 UTC tick

## 6.3 Runtime Caravan 조회

```csharp
public bool TryGetRuntimeCaravan(
    string caravanId,
    out CaravanData caravan);
```

Runtime 객체는 표시용으로만 읽고 직접 수정하지 않는다.

## 6.4 전체 Pending 목록 조회

```csharp
public IReadOnlyList<PendingSettlementSaveData> GetPendingSettlements();
```

특징:

- 모든 Caravan의 Pending 반환
- 복사본 반환
- 비선택 Caravan 정산 배지와 목록 구성에 사용 가능

## 6.5 Exact Pending 조회

```csharp
public bool TryGetPendingSettlement(
    string caravanId,
    string tradeId,
    out PendingSettlementSaveData pending);
```

사용 목적:

- 특정 Caravan·Trade의 정산 대기 여부 확인
- 정확한 Settlement 화면 표시 전 검증
- stale UI 요청 차단

## 6.6 Exact Pending Result 조회

```csharp
public bool TryGetPendingSettlementResult(
    string caravanId,
    string tradeId,
    out JourneyResultData result);
```

정산 결과 상세 표시용이다.

## 6.7 현재 표시 중인 Settlement 조회

```csharp
public bool TryGetPendingSettlement(
    out string caravanId,
    out string tradeId,
    out JourneyResultData result);
```

위치는 `SettlementUiBridge`이다.

주의:

- 전체 Pending 조회가 아니다.
- 현재 presentation cursor에 올라온 한 건만 반환한다.
- 복수 Pending 목록에는 Coordinator Query를 사용한다.

## 6.8 도착 판매 가능 여부

```csharp
public bool IsSalePending(string caravanId);
```

위치는 `CaravanArrivalSaleController`이다.

현재 판매 가능 판정은 별도 Selling state가 아니라 해당 Caravan의 `SettlementPending`과 정산 결과 조건을 사용한다.

## 6.9 Map Progress

현재 API:

```csharp
public bool TryGetMapProgress(
    out TradeMapProgressSnapshot snapshot);
```

### 제한

- selected Caravan facade를 사용한다.
- 복수 Caravan 진행 표시를 위한 안전한 Query가 아니다.
- UI가 여러 Caravan의 진행률을 표시하려면 explicit `caravanId` overload가 추가로 필요하다.

---

# 7. Command API

## 7.1 Caravan 선택

```csharp
public static bool TrySetSelectedCaravan(
    SaveData data,
    string caravanId);
```

### 주의

- Save를 수행하지 않는다.
- UI cursor 변경용이다.
- 실제 진행·판매·Claim 대상 선정에는 사용하지 않는다.

## 7.2 Caravan 생성

```csharp
public CaravanCreationResult CreateCaravan(int slotIndex);
```

주요 Result:

- `Succeeded`
- `CaravanId`
- `SlotIndex`
- `FailureReason`
- `SaveResult`

## 7.3 무역 출발

```csharp
public TradeDepartureResult Depart(
    TradeDepartureRequest request);
```

Request:

```csharp
public sealed class TradeDepartureRequest
{
    public string CaravanId;
    public string RouteId;
}
```

Result 주요 필드:

- `DepartureSucceeded`
- `TradeId`
- `FailureReason`
- `CoreResult`
- `SaveResult`
- `SaveSucceeded`

### 권장 UI 처리

```text
Depart 호출
→ DepartureSucceeded 확인
→ SaveSucceeded 확인
→ 성공 시 explicit caravanId 기준 진행 Query 재호출
→ 실패 시 FailureReason 표시
```

## 7.4 도착 판매 열기

```csharp
public bool OpenForCaravan(string caravanId);
```

위치는 `CaravanArrivalSaleController`이다.

내부 흐름:

```text
SettlementPending 검증
→ 목적지 Route/Town/Market 조회
→ Market SellOnly 세션 열기
```

실패 상세는 Controller의 오류 상태/Event를 통해 확인한다.

## 7.5 판매 Draft 설정

```csharp
public bool SetSellDraft(
    string itemId,
    int quantity);
```

Draft는 런타임 전용이며 저장되지 않는다.

## 7.6 판매 Commit

```csharp
public MarketTransactionResult Commit();
```

성공 시 변경:

- 대상 Caravan Cargo 감소
- 시장 재고 증가
- `player.tradingCurrency` 즉시 증가
- SaveData 저장
- 저장 성공 후 `CaravanCargoChanged`
- 저장 성공 후 `TradingCurrencyChanged`

저장 실패 시 rollback:

- 공유 거래 재화
- 대상 Caravan Cargo
- 시장 재고

## 7.7 판매 확인 후 Settlement 표시

```csharp
public bool ConfirmSaleAndOpenSettlement();
```

동작:

```text
판매 Draft가 있으면 Commit
→ exact Pending identity 조회
→ SettlementUiBridge.PresentSettlement
```

주의:

- 구조화된 Result가 아닌 `bool`을 반환한다.
- 실패 상세는 `LastErrorCode` 또는 Error Event를 확인해야 한다.
- Framework lifecycle의 `FinalizeSelling` Command는 아니다.
- 이미 `SettlementPending`인 상태에서 Settlement 화면을 표시할 뿐이다.

## 7.8 Settlement 표시

```csharp
public bool PresentSettlement(
    string caravanId,
    string tradeId);
```

위치는 `SettlementUiBridge`이다.

성공 시:

- exact Pending 검증
- presentation cursor 설정
- `SettlementReady` Event 발행
- Settlement 화면 요청

Save는 수행하지 않는다.

## 7.9 Exact Claim

권장 API:

```csharp
public ClaimSettlementResult ClaimSettlement(
    string caravanId,
    string tradeId);
```

사용 가능 위치:

- `SettlementUiBridge`
- `TradeProgressCoordinator`

Result 주요 필드:

- `Succeeded`
- `FailureReason`
- `SaveResult`

검증:

- exact Caravan 존재
- exact Trade ID 일치
- exact Pending 존재
- `SettlementPending`
- 중복 Claim 아님

저장 실패 시 복원:

- 전체 SaveData snapshot
- Runtime Caravan
- Pending
- 진행 상태
- 경제 반영

성공 Event와 재화 Event는 Save 성공 이후에만 발행한다.

---

# 8. 판매 데이터 계약

## 8.1 판매 대상 데이터

판매 세션이 열린 뒤 다음 View를 사용할 수 있다.

- `CargoInventoryView`
- `MarketStockView`

주요 필드:

- Item
- 수량
- UnitPrice

### 가격 주의

실제 판매 Commit은 `item.BaseSellPrice`를 사용한다. 현재 읽기 View의 `UnitPrice`가 동일한 판매 가격을 표시하는지는 런타임 검증이 필요하다.

## 8.2 현재 판매 결과 계약

`MarketTransactionResult` 주요 정보:

- 성공 여부
- `ErrorCode`
- `SaleRevenue`
- 거래 line 결과

## 8.3 현재 판매 정책

| 항목 | 현재 구현 |
|---|---|
| 판매 수익 지급 시점 | Commit 즉시 공유 거래 재화에 반영 |
| Claim 시 판매 수익 지급 | 아님 |
| 일부 판매 | 가능 |
| 미판매 Cargo | Caravan에 유지 |
| 판매 Draft 저장 | 저장 안 함 |
| 판매된 품목 ledger | 없음 |
| 무역별 누적 판매 수익 | 전용 durable ledger 없음 |
| 재실행 후 미판매 Cargo | SaveData Cargo로 복구 |
| 재실행 후 판매 세션 | 다시 열어야 함 |

---

# 9. Event 계약

## 9.1 Framework Events

### 정산 준비

```csharp
FrameworkEvents.TradeSettlementReady +=
    (caravanId, tradeId, result) => { };
```

- settle Save 성공 후 발행
- online batch settle은 batch Save 성공 후 발행
- `caravanId`, `tradeId` 포함

### Cargo 변경

```csharp
FrameworkEvents.CaravanCargoChanged +=
    caravanId => { };
```

- Market Commit Save 성공 후 발행
- Event 수신 후 해당 Caravan Cargo Query 재호출

### 공유 재화 변경

```csharp
FrameworkEvents.TradingCurrencyChanged +=
    tradingCurrency => { };
```

- 판매 Commit 또는 Claim Save 성공 후 발행

### 화면 상태 변경

```csharp
FrameworkEvents.InGameScreenChanged +=
    screenState => { };
```

주의:

- 대상 `caravanId`가 없다.
- 현재 Router 매핑은 selected Caravan에 의존하는 경로가 있다.
- 복수 Caravan 카드 갱신 Event로 사용하지 않는다.

## 9.2 SettlementUiBridge Events

```csharp
root.SettlementUiBridge.SettlementReady +=
    (tradeId, result) => { };
```

주의:

- `caravanId`가 Event 인자에 없다.
- Bridge의 현재 presented identity를 함께 조회해야 한다.

## 9.3 판매 Controller Events

```csharp
SaleOpened(caravanId, tradeId)
SettlementRequested(caravanId, tradeId)
ErrorChanged(errorCode)
```

이 Event는 Framework 전역 Event가 아니라 판매 UI Controller Event이다.

## 9.4 현재 없는 Event

- 명시적 Caravan 선택 Event
- Framework 수준 SellingStarted
- 품목별 ItemSold
- Framework 수준 SellingCompleted
- 전역 SaveFailed Event

현재는 Command Result와 기존 Cargo/Currency Event를 이용해야 한다.

---

# 10. UI 권장 호출 흐름

## 10.1 Caravan 목록 및 상태

```text
SaveData의 caravan 목록 읽기
→ 각 caravanId로 TryGetTradeProgress
→ 상태 배지 표시
→ Pending 목록과 identity 매칭
```

현재 전용 read-only CaravanOverview ViewData API는 없다.

## 10.2 출발

```csharp
var result = root.TradeStart.Depart(
    new TradeDepartureRequest
    {
        CaravanId = caravanId,
        RouteId = routeId
    });

if (!result.DepartureSucceeded || !result.SaveSucceeded)
{
    // FailureReason 표시
    return;
}

// 명시적 caravanId 기준 데이터 재조회
```

## 10.3 도착 판매 열기

```text
TradeSettlementReady(caravanId, tradeId, result)
→ 성공 도착인지 확인
→ CaravanArrivalSaleController.OpenForCaravan(caravanId)
→ 판매 Cargo 표시
```

## 10.4 판매

```text
SetSellDraft(itemId, quantity)
→ Commit()
→ MarketTransactionResult 확인
→ 성공 시 Cargo와 공유 재화 재조회
```

UI가 로컬 Cargo 수량을 직접 감소시키지 않는다.

## 10.5 판매 생략 또는 종료

```text
ConfirmSaleAndOpenSettlement()
→ 성공 시 SettlementUiBridge presentation 확인
→ Settlement ViewData 표시
```

빈 Draft 상태에서도 미판매 Cargo를 유지한 채 Settlement 화면으로 이동할 수 있다.

## 10.6 Exact Claim

```csharp
var claim = root.SettlementUiBridge.ClaimSettlement(
    caravanId,
    tradeId);

if (!claim.Succeeded)
{
    // FailureReason 및 SaveResult 확인
    // 권위 Pending과 Progress 재조회
    return;
}

// Caravan 목록, progress, pending, wallet 재조회
```

---

# 11. 재실행 및 복구

## 11.1 Traveling

- `tradeProgressEntries` 전체 순회
- selected Caravan과 무관하게 복구
- 복수 Traveling 지원

## 11.2 SettlementPending

- `pendingSettlements` 전체 순회
- malformed entry 격리
- 복수 Pending 복구
- exact `(caravanId, tradeId)` 유지

## 11.3 판매

별도 Selling state나 세션 snapshot은 없다.

재실행 후 유지되는 것:

- 남은 Cargo
- 이미 반영된 공유 거래 재화
- 시장 재고
- Pending Settlement
- Progress `SettlementPending`

재실행 후 유지되지 않는 것:

- 판매 Draft
- 열려 있던 판매 패널 상태
- Bridge presentation cursor
- 판매 세션 자체
- 판매 품목별 누적 ledger

재실행 후 UI는 해당 Caravan이 여전히 sale pending인지 확인하고 판매 화면을 다시 열어야 한다.

---

# 12. Result와 실패 처리

## 12.1 구조화된 Result가 있는 Command

| Command | Result |
|---|---|
| Caravan 생성 | `CaravanCreationResult` |
| 출발 | `TradeDepartureResult` |
| exact Claim | `ClaimSettlementResult` |
| Market Commit | `MarketTransactionResult` |
| Save | `SaveResult` |

## 12.2 `bool` 중심 API

- `OpenForCaravan`
- `ConfirmSaleAndOpenSettlement`
- `PresentSettlement`
- selected 기반 preparation entry

이 API는 UI가 도메인 실패와 Save 실패를 일관된 방식으로 구분하기 어렵다. Controller 오류 코드 또는 관련 상태를 추가로 확인해야 한다.

## 12.3 UI 공통 처리 원칙

```text
Command 호출
→ Result/오류 코드 확인
→ 성공 시 권위 Query 재호출
→ Save 실패 시 성공 연출 금지
→ 실패 후에도 Query 재호출
→ 다른 Caravan 데이터가 변하지 않았는지 identity 기준 확인
```

---

# 13. 사용 금지·주의 API

| API | 상태 | 문제 | 사용해야 할 대체 경로 |
|---|---|---|---|
| `Coordinator.ClaimSettlementAndReset()` | Obsolete | selected 기반, bool | `ClaimSettlement(caravanId, tradeId)` |
| `Bridge.ClaimSettlementAndReset()` | 호환 | 실패 상세 손실 | exact Claim Result |
| `TryStartTrade(...)` | Legacy 성격 | 구조화된 결과 부족 | `Depart(TradeDepartureRequest)` |
| `ActiveCaravan` | Compatibility facade | selected 전용 | `TryGetRuntimeCaravan(caravanId)` |
| `TryGetMapProgress(out ...)` | selected 전용 | 멀티 카드 부적합 | explicit-ID API 필요 |
| `LastSettlementResult` | 런타임 캐시 | stale/last-writer 위험 | exact durable Pending Query |
| `CurrentSaveData` 직접 수정 | raw mutable | Save/rollback 우회 | Command Service |
| `TradeProgressRecorder` 직접 호출 | Internal | Runtime/Save 불일치 | TradeStart/Coordinator |
| `SaveData.caravan` 등 단일 facade | Legacy | selected-only | list + `SaveDataLookup` |
| Market open overload without `caravanId` | selected-dependent | 잘못된 Caravan 가능 | explicit `caravanId` overload |
| Debug Complete/Force APIs | Debug-only | 제품 흐름 우회 | production lifecycle |

---

# 14. 현재 UI-facing API 공백

## 14.1 Blocker

### A. Per-Caravan Travel Progress Query

현재:

```csharp
TryGetMapProgress(out snapshot)
```

문제:

- selected Caravan만 반환
- Caravan 카드 여러 개의 진행률 표시 불가

필요 최소 계약:

```csharp
bool TryGetMapProgress(
    string caravanId,
    out TradeMapProgressSnapshot snapshot);
```

### B. Selling lifecycle 명시 계약

현재:

- Framework progress는 도착 즉시 `SettlementPending`
- Market SellOnly가 판매를 처리

필요한 결정:

1. 현재 구조를 공식 계약으로 문서화한다.
2. 또는 Framework에 Selling state와 Begin/Finalize Command를 도입한다.

현재 일정상 최소안은 다음 Query를 공개하는 것이다.

```csharp
bool IsArrivalSaleAvailable(string caravanId);
```

## 14.2 High

### A. Explicit Caravan Town → Preparation

현재 Root API는 selected Caravan에 의존한다.

필요 계약:

```csharp
Result TryBeginTradePreparation(string caravanId);
```

### B. Multi-Caravan Screen Mapping

현재 Screen Router는 selected 상태 중심이다.

권장 정책:

- 전체 앱 화면은 사용자가 선택한 Caravan 기준
- 비선택 Caravan의 상태는 카드/배지 Event와 Query로 표현
- 자동 화면 강제 전환은 selected Caravan에만 허용

### C. Sale Result ViewData

현재 판매 후 UI는 Commit Result와 Cargo 재조회를 조합해야 한다.

필요 ViewData 예:

- CaravanId
- TradeId
- Sold lines
- ThisCommitRevenue
- Remaining cargo
- CanContinueSelling
- CanOpenSettlement

## 14.3 Medium

- `SettlementViewData`에 `CaravanId` 추가
- `ConfirmSaleAndOpenSettlement` 구조화된 Result
- Item sale / selling complete Event
- 전용 read-only CaravanOverview ViewData

## 14.4 Low

- 무역별 누적 판매 수익 ledger
- Core `JourneyState.Selling` 공식 사용

둘 다 현재 정책과 저장 계약을 다시 결정한 뒤 도입해야 한다.

---

# 15. UI 통합 체크리스트

- [ ] 모든 Caravan별 처리에 explicit `caravanId`를 사용한다.
- [ ] Pending·Settlement·Claim에는 `tradeId`도 함께 사용한다.
- [ ] `selectedCaravanId`는 화면 선택에만 사용한다.
- [ ] `CurrentSaveData`를 직접 수정하지 않는다.
- [ ] Command 성공 전 패널 전환이나 성공 연출을 확정하지 않는다.
- [ ] Command 성공 후 Query를 다시 호출한다.
- [ ] Market Commit 성공 후 Cargo와 공유 재화를 재조회한다.
- [ ] Save 실패 시 판매·Claim 성공 UI를 표시하지 않는다.
- [ ] 복수 Pending은 Coordinator 전체 목록으로 표시한다.
- [ ] Bridge Query는 현재 표시 중인 한 건이라는 점을 유지한다.
- [ ] `LastSettlementResult`를 권위 데이터로 사용하지 않는다.
- [ ] static Event는 `OnEnable`에서 구독하고 `OnDisable`에서 해제한다.
- [ ] 비선택 Caravan 도착은 화면 강제 전환 대신 배지로 표시한다.
- [ ] 재실행 후 판매 Draft가 복구되지 않는다는 점을 처리한다.

---

# 16. 런타임 검증 필요 항목

| 시나리오 | 확인 목적 |
|---|---|
| Caravan A 정산 표시 중 Caravan B 도착 | Bridge cursor와 복수 Pending 독립성 |
| 두 Traveling 동시 완료 중 Save 실패 | Event 억제와 rollback |
| 판매 UI 표시 UnitPrice와 실제 Commit 수익 비교 | 가격 표시 계약 검증 |
| 복수 Pending 재실행 후 비선택 Caravan Claim | durable exact claim 검증 |
| 빈 판매 Draft로 Settlement 이동 | 미판매 Cargo 유지 확인 |
| Caravan A와 B 연속 판매 | 공유 재화 합산과 Cargo 격리 |

---

# 17. 최종 UI 사용 가능 범위

## 현재 바로 사용 가능

- 명시적 Caravan 출발
- Caravan별 Progress SaveData 조회
- 전체 Pending 목록
- exact Pending 조회
- exact Settlement Result 조회
- exact Settlement 표시
- exact Claim
- 도착 판매 세션 열기
- 판매 Draft와 Commit
- 판매 Save 실패 rollback
- Cargo/Currency 변경 Event

## 현재 제한적으로 사용 가능

- Caravan 목록: raw SaveData 조합 필요
- Traveling 진행률: selected Caravan만 편리하게 조회 가능
- 화면 상태: selected Caravan 중심
- 판매 결과 요약: Commit Result와 Cargo Query 조합 필요
- 정산 ViewData: `CaravanId` 누락

## 현재 사용 불가 또는 미구현

- Framework `Selling` 상태
- Framework `BeginSelling` / `FinalizeSelling`
- 품목별 durable 판매 ledger
- 무역별 누적 판매 수익 Query
- explicit Caravan 재준비 Command
- per-Caravan map progress Query

---

# 18. 한 줄 계약 요약

```text
UI는 Depart와 exact (caravanId, tradeId) Pending/Present/Claim으로 멀티 Caravan 무역 정산을 처리한다.
도착 판매는 Framework Selling 상태가 아니라 SettlementPending 상태 위에서 Market SellOnly 세션으로 수행하며,
판매 Commit 성공 시 Cargo와 공유 거래 재화가 즉시 저장된다.
```
