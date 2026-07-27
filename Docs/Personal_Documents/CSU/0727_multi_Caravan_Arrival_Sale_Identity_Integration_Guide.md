# 다중 카라반 도착 판매 정확 식별 연동

## 브랜치 정보

- 브랜치: `feature/ui/multi-caravan-arrival-sale-identity`
- 커밋: `feat(ui): integrate exact multi-caravan arrival sale identity`
- 베이스: `dev2`

## 목적

여러 카라반이 동시에 `SettlementPending` 상태일 때, Overview 슬롯의 Cargo 버튼이 **특정 카라반·특정 trade Pending**을 정확히 가리키도록 식별 계약을 강화한다.

기존에는 Caravan ID만으로 도착 판매를 열 수 있어, 다중 Pending 환경에서 잘못된 정산 세션이 열리거나 전역 단일 Pending 추론에 의존할 위험이 있었다. 이번 변경은 **caravanId + tradeId 쌍**을 UI부터 Market 패널 Commit 검증까지 일관되게 유지한다.

## 변경 파일 요약

| 영역 | 파일 | 역할 |
|------|------|------|
| ViewData | `CaravanBlockViewData.cs` | `arrivalSaleTradeId`, `canOpenArrivalSale` 필드 추가 |
| Provider | `SaveDataCaravanOverviewProviderBehaviour.cs` | SaveData에서 정확한 Pending trade ID 해석 |
| View | `CaravanSlotView.cs` | Cargo 버튼의 적재/도착판매 이중 동작 |
| Presenter | `CaravanOverviewPresenter.cs` | 슬롯 의도를 공유 `CaravanArrivalSaleButton`에 위임 |
| Market UI | `CaravanArrivalSaleButton.cs` | 명시적 `(caravanId, tradeId)` 바인딩 |
| Market UI | `CaravanArrivalSaleController.cs` | trade ID 검증 후 sell-only 패널 오픈 |
| Market UI | `MarketTradePanelController.cs` | 세션·Commit 시 trade ID 권위 유지 |
| Prefab | `MainUICanvas.prefab` | Presenter ↔ `CaravanArrivalSaleButton` 참조 연결 |

---

## 전체 흐름

```text
SaveData
  └─ SaveDataCaravanOverviewProviderBehaviour.TryResolveArrivalSaleTradeId
       └─ CaravanBlockViewData { caravanId, arrivalSaleTradeId, canOpenArrivalSale }
            └─ CaravanSlotView.Bind / ShowOccupied
                 └─ Cargo 버튼 클릭
                      ├─ canOpenArrivalSale → ArrivalSaleRequested(caravanId, tradeId)
                      └─ canOpenCargo       → CargoRequested(caravanId)

CaravanOverviewPresenter.HandleArrivalSaleRequested
  └─ arrivalSaleButton.Bind(caravanId, tradeId)
  └─ arrivalSaleButton.OpenSale()

CaravanArrivalSaleButton.OpenSale
  └─ CaravanArrivalSaleController.OpenForCaravan(caravanId, tradeId)
       └─ MarketTradePanelController.OpenForArrivalSale(caravanId, tradeId, destinationMarket)
            └─ Commit 시 ValidateArrivalSaleAccess(..., tradeId, ...)
```

---

## 1. Provider — Pending trade ID 해석

`SaveDataCaravanOverviewProviderBehaviour.CreateOccupiedBlock`에서 각 Occupied 슬롯마다 `TryResolveArrivalSaleTradeId`를 호출한다.

### 성공 조건 (모두 충족)

1. `caravanId`가 비어 있지 않음
2. `SaveDataLookup.TryGetTradeProgress` 성공
3. `progress.state == SettlementPending`
4. `progress.activeTradeId`가 비어 있지 않음
5. `SaveDataLookup.TryGetPendingSettlement(caravanId, activeTradeId)` 성공
6. `pending.hasResult == true`
7. `pending.grade != Failed`

### 실패 시 동작

- `canOpenArrivalSale = false`
- `arrivalSaleTradeId = string.Empty`
- Pending 데이터가 손상된 경우 `Debug.LogWarning`으로 `ExactPendingInvalid` 사유 기록

즉, **유일하고 유효한 Pending만** 슬롯에 노출되며, 중복·불완전 Pending은 fail-closed로 버튼을 비활성화한다.

---

## 2. ViewData 계약

`CaravanBlockViewData`에 추가된 필드:

| 필드 | 설명 |
|------|------|
| `arrivalSaleTradeId` | 해당 슬롯 카라반이 열 수 있는 정확한 trade Pending ID. 해석 실패 시 빈 문자열 |
| `canOpenArrivalSale` | `caravanId + arrivalSaleTradeId` 쌍으로 도착 판매를 열 수 있는지 여부 |

`Occupied` 상태가 아니면 두 필드 모두 무시된다.

---

## 3. CaravanSlotView — Cargo 버튼 이중 역할

Cargo 버튼 하나가 **적재 편집**과 **도착 판매** 두 가지 의도를 표현한다. 우선순위는 도착 판매가 높다.

### Occupied 슬롯 버튼 활성 조건

| 동작 | 조건 |
|------|------|
| Setting (설정) | `caravanId` 유효 && `JourneyState == Prepare` |
| Cargo (적재) | 위와 동일 |
| Cargo (도착 판매) | `caravanId` 유효 && `canOpenArrivalSale` && `arrivalSaleTradeId` 비어 있지 않음 |

### 아이콘 전환

- `canOpenArrivalSale == true` → `cargoSellButtonIcon`
- 그 외 → `cargoLoadButtonIcon`

### 클릭 분기 (`HandleCargoClicked`)

```text
if canRequestArrivalSale
   → ArrivalSaleRequested(caravanId, arrivalSaleTradeId)
else if canRequestCargo
   → CargoRequested(caravanId)
```

`Prepare` 상태가 아니어도 `SettlementPending` Pending이 유효하면 Cargo 버튼은 **판매 아이콘 + interactable** 상태가 될 수 있다. Setting 버튼은 여전히 `Prepare`에서만 활성이다.

---

## 4. CaravanOverviewPresenter — 공유 버튼 위임

각 슬롯마다 별도의 Market 컨트롤러를 두지 않고, **하나의 `CaravanArrivalSaleButton`**을 공유한다.

`HandleArrivalSaleRequested` 순서:

1. `arrivalSaleButton.Bind(caravanId, tradeId)` — 명시적 2-ID 바인딩
2. `arrivalSaleButton.OpenSale()` — 즉시 판매 패널 오픈 시도

`arrivalSaleButton` 미할당 시 Error 로그 후 중단한다.

`MainUICanvas.prefab`에서 Presenter의 `arrivalSaleButton` 필드가 Market 쪽 `CaravanArrivalSaleButton` 컴포넌트를 참조하도록 연결되어 있다.

---

## 5. CaravanArrivalSaleButton — 바인딩 모드

### 명시적 바인딩 (`Bind(caravanId, tradeId)`)

- 두 ID 모두 non-whitespace이면 `explicitlyBound = true`
- 이후 `RefreshInteractable`, `OpenSale`은 **전역 단일 Pending 추론으로 ID를 덮어쓰지 않음**
- Overview 슬롯 클릭 경로가 이 모드를 사용

### 레거시 바인딩 (`Bind(caravanId)`)

- `tradeId`를 비우고 `explicitlyBound = false`
- `OpenSale` / `RefreshInteractable` 시 `TryResolveSinglePendingIdentity`로 전역 유일 Pending 추론
- Pending이 0개 또는 2개 이상이면 실패

### OpenSale

```text
if !explicitlyBound → TryResolveSinglePendingIdentity
saleController.OpenForCaravan(caravanId, tradeId)
성공 → button.interactable = false
실패 → Error 로그 (CaravanId, TradeId, ErrorCode 포함)
```

---

## 6. CaravanArrivalSaleController — 오픈·정산

### `IsSalePending(caravanId, tradeId)`

Provider와 동일한 SaveData 조건을 재검증한다. 추가로 `progress.activeTradeId == tradeId` ordinal 비교를 수행한다.

### `OpenForCaravan(caravanId, tradeId)`

1. Framework / SaveData / SharedGameData 유효성
2. tradeId non-whitespace
3. TradeProgress + PendingSettlement 유효성 (Provider와 동일)
4. **`progress.activeTradeId == tradeId`** — 불일치 시 `ErrorIdentityMismatch`
5. `progress.activeRouteId` → 목적지 Town → `marketCatalog`에서 `MarketData` 조회
6. `marketPanel.OpenForArrivalSale(caravanId, tradeId, destinationMarket)`

성공 시 `activeCaravanId`, `activeTradeId`를 컨트롤러 내부에 보관하고 `SaleOpened` 이벤트 발생.

### `ConfirmSaleAndOpenSettlement`

Commit 전후로 `marketPanel.ActiveCaravanId/ActiveTradeId`와 컨트롤러의 active ID가 일치하는지 재확인한다. 불일치 시 `ErrorIdentityMismatch`.

---

## 7. MarketTradePanelController — 세션 trade ID 권위

### 추가 상태

- `activeTradeId` — 현재 패널 세션이 대상으로 하는 trade Pending ID
- `ActiveCaravanId`, `ActiveTradeId` public getter

### `OpenForArrivalSale(caravanId, tradeId, destinationMarket)`

시그니처가 `(caravanId, tradeId, destinationMarket)` 3인자로 확장되었다. `tradeId`는 세션 전체와 Commit 검증의 기준값이다.

### 세션 재사용 조건 (`OpenResolved`)

동일 Market + Caravan + TradeMode + **tradeId**가 모두 일치하면 Draft를 유지하고 재오픈하지 않는다.

### `ValidateArrivalSaleAccess` 강화

| 검증 | 실패 코드 |
|------|-----------|
| `tradeId` 비어 있음 | `ARRIVAL_SALE_IDENTITY_MISMATCH` |
| `progress.activeTradeId != tradeId` | `ARRIVAL_SALE_IDENTITY_MISMATCH` |
| PendingSettlement 조회 실패 / Failed grade | `MARKET_NOT_IN_TOWN` 등 기존 코드 |

Commit 시에도 동일한 `activeTradeId`로 재검증한다.

---

## 8. 다중 카라반 시나리오

```text
Caravan A → SettlementPending, activeTradeId = "trade-001"
Caravan B → SettlementPending, activeTradeId = "trade-002"
```

| 동작 | 결과 |
|------|------|
| A 슬롯 Cargo(판매) 클릭 | `Bind(A, trade-001)` → A의 Pending만 오픈 |
| B 슬롯 Cargo(판매) 클릭 | `Bind(B, trade-002)` → B의 Pending만 오픈 |
| A 세션 Commit | `ValidateArrivalSaleAccess(A, trade-001)` — B 데이터와 무관 |
| 전역 단일 Pending 추론 | Pending 2개 → `TryResolveSinglePendingIdentity` false |

각 슬롯은 Provider가 **자기 카라van의 activeTradeId**만 해석하므로, 슬롯 간 ID 혼선이 발생하지 않는다.

---

## 9. 에러 코드 정리

| 코드 | 발생 위치 | 의미 |
|------|-----------|------|
| `ARRIVAL_SALE_IDENTITY_MISMATCH` | Controller, MarketPanel | 요청 tradeId와 progress.activeTradeId 불일치 또는 tradeId 누락 |
| `ARRIVAL_SALE_PENDING_MISSING` | Controller | Pending 조건 미충족 |
| `ARRIVAL_SALE_PANEL_MISSING` | Controller | Market 패널 참조 없음 |
| `ARRIVAL_SALE_DESTINATION_MISSING` | Controller | Route/Town 해석 실패 |
| `ARRIVAL_SALE_MARKET_MISSING` | Controller | marketCatalog에 목적지 Market 없음 |

Open 실패 로그에는 `CaravanId`, `TradeId`, `Error`가 함께 출력되어 다중 카라반 환경에서 원인 추적이 가능하다.

---

## 10. 기존 Cargo(적재) 경로와의 관계

도착 판매가 **활성화되지 않은** Occupied 슬롯(`Prepare` 상태)에서는 기존과 동일하게:

```text
CargoRequested(caravanId) → CaravanOverviewPresenter → (Binding) → S4 Cargo 편집
```

`canOpenArrivalSale == true`인 슬롯에서는 Cargo 클릭이 **항상 도착 판매**로 라우팅되며, 동시에 `Prepare`이면 Setting 버튼으로 설정 편집은 여전히 가능하다.

---

## 11. Prefab / Inspector 연결

`MainUICanvas.prefab`:

- `CaravanOverviewPresenter.arrivalSaleButton` → 씬 내 `CaravanArrivalSaleButton` 컴포넌트

`CaravanSlotView` Inspector:

- `cargoSellButtonIcon` — 도착 판매 모드 Cargo 아이콘 (기존 reserved 필드 활성화)

---

## 12. 설계 원칙

1. **Fail-closed** — trade ID를 확정할 수 없으면 버튼 비활성, 오픈 거부
2. **ID 쌍의 권위** — UI Bind → Controller active → Panel session → Commit 검증까지 동일 tradeId 유지
3. **슬롯별 독립 해석** — Provider는 카라van별 SaveData lookup만 사용, 전역 선택 상태에 의존하지 않음
4. **레거시 호환** — 단일 Pending 환경에서는 `Bind(caravanId)` + 전역 추론 경로 유지
5. **UI 의도 분리** — View는 이벤트만 발생, SaveData 변경은 Market/ Framework 계층에서 수행

---

## 13. 확인 항목 (수동 테스트)

- [ ] 카라van 1개 SettlementPending → 해당 슬롯 Cargo가 판매 아이콘, 클릭 시 목적지 sell-only 패널 오픈
- [ ] 카라van 2개 동시 SettlementPending → 각 슬롯이 서로 다른 tradeId로 패널 오픈
- [ ] Prepare 상태 카라van → Cargo는 적재, Setting 활성
- [ ] SettlementPending + Failed grade Pending → `canOpenArrivalSale == false`, Cargo 비활성 또는 적재 전용
- [ ] Commit 시 tradeId 불일치 SaveData → `ARRIVAL_SALE_IDENTITY_MISMATCH`
- [ ] `CaravanOverviewPresenter.arrivalSaleButton` 미연결 → Error 로그, 오픈 중단
