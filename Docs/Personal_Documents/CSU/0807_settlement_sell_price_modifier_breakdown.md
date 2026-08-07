# 판매 배율 정산 영수증 표시 · 구현 로직 정리

**작성일:** 2026-08-07  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `feature/ui/settlement-sell-price-modifier-breakdown`  
**베이스:** `dev2`  
**문서화 시점 상태:** 워킹 트리 구현 기준 (base `ba98b729`)

관련 선행·인접 문서:

- [`0805_contextual_sell_price_modifier_logic.md`](./0805_contextual_sell_price_modifier_logic.md) — contextual SellPrice 계산·산지 예외·Lucky 소비
- [`0804_seasonal_sell_price_logic.md`](./0804_seasonal_sell_price_logic.md) — TradeItem Season SellPrice 선택
- [`0807_sell_price_modifier_buff_tooltips.md`](./0807_sell_price_modifier_buff_tooltips.md) — 시장 판매 화면 Buff Tooltip (별도 UI, 이번 작업과 무관)

---

## 1. 목적

도착 판매(Arrival Sale) Commit 시점에 이미 계산된 **판매가 modifier breakdown**을 손실 없이 정산 영수증(TMP)까지 전달·표시한다.

이번 작업이 하는 것:

1. Commit 시 `PriceCalculationResult`의 Sell modifier 스냅샷을 transaction item DTO에 보존
2. 같은 세션 runtime cache로 Settlement ViewData까지 전달
3. 기존 S8 `TradeSettlementPanelController.BuildReceipt()`에 품목별 상세 텍스트 추가
4. 긴 영수증에 대응해 TMP/Scroll content 높이를 런타임으로 맞춤

이번 작업이 하지 않는 것:

- 판매가 계산 공식 / `PriceCalculator` / `ContextualSellPriceModifierResolver` 변경
- 산지 판매 예외 로직 재구현
- Lucky 소비·Distance·Season 적용 규칙 변경
- SaveData / `PendingSettlement` / migration 확장
- `InGame.unity` / `MainUICanvas.prefab` 수정
- VFX·modifier item prefab·S9 재설계

핵심 한 줄:

```text
판매 가격을 다시 계산하지 않는다.
이미 계산된 breakdown을 정산 UI까지 전달해서 보여준다.
```

---

## 2. 문제와 해결 방향

### 2.1 Freshness Gate에서 확인된 손실 지점

조사 당시 계산 경로에는 breakdown이 있었다.

```text
ContextualSellPriceModifierResolver.Resolve
  → ContextualSellPriceCalculator.CalculateUnitPrices
  → PriceCalculator.CalculateUnitPrices
  → PriceCalculationResult.Modifiers
```

그러나 Commit 경계에서 `UnitSellPrice`만 추출되어 이후 경로에서 사라졌다.

```text
PriceCalculationResult
→ MarketInventoryMutationSession (UnitSellPrice만 사용)
→ [LOSS]
→ MarketTransactionItemSummary / SettlementItemSaveData
→ Settlement ViewData / BuildReceipt
```

`SettlementItemSaveData`는 계속 다음만 저장한다.

```text
itemId, quantity, unitPrice, totalAmount
```

### 2.2 Phase 1 선택

SaveData를 건드리지 않고, **같은 세션 runtime cache**로 breakdown을 전달한다.

```text
Commit Result snapshot
  → EconomyM1SettlementViewAdapter 세션 cache
  → SettlementUiDataAdapter → EconomyM1SettlementViewData.SaleLines
  → TradeSettlementPanelController.BuildReceipt
```

재시작 후 `SettlementPending` 복구 시에는 기존 persisted receipt만 표시하고, modifier 상세는 없을 수 있다.  
현재 Season / Distance / Lucky 상태로 breakdown을 재계산하지 않는다.

---

## 3. 변경 파일 요약

| 영역 | 파일 | 역할 |
|------|------|------|
| Commit DTO | `Assets/Scripts/UI/MarketInventoryIntegration.cs` | `MarketSaleModifierSnapshot` 추가, item summary에 Base/Final/Modifiers 보존 |
| Arrival Sale | `Assets/Scripts/UI/Market/CaravanArrivalSaleController.cs` | Commit 성공 후 SaleLines를 세션 cache에 저장 |
| ViewData | `Assets/_Project/03.Economy/06_Integration/EconomyM1SettlementViewAdapter.cs` | SaleLine ViewData·분류·세션 store/get/remove |
| Adapter | `Assets/_Project/11.CoreServices/Scripts/UI/Settlement/SettlementUiDataAdapter.cs` | ViewData에 SaleLines 부착, Claim 성공 시 cache 제거 |
| Receipt UI | `Assets/Scripts/UI/TradeSettlementPanelController.cs` | `AppendSaleDetails` + `PrepareReceiptLayout` |
| Tests | `Assets/_Project/11.CoreServices/Editor/CargoSellMarketTransactionTests.cs` | Base/Final 보존·Distance/Lucky SourceId 분류 |

보호·미변경:

- `InGame.unity`
- `MainUICanvas.prefab`
- `PendingSettlementSaveData` / Save version
- `PriceCalculator`, `ContextualSellPriceCalculator`, `ContextualSellPriceModifierResolver`

---

## 4. 데이터 흐름

```text
MarketInventoryMutationSession.ExecuteTransaction
  CaptureSellPriceContext()  (transaction당 1회)
  ResolveUnitPrices(...) → PriceCalculationResult 보관
  MarketTransactionCalculator (SellUnitPrice만 계산 입력으로 사용)
  CreateItemSummary(...)
    BaseSellPrice        ← TradeItemData.BaseSellPrice
    FinalUnitSellPrice   ← PriceCalculationResult.UnitSellPrice
    SaleModifiers        ← SellPrice/Both 대상 breakdown 복사
        ↓
CaravanArrivalSaleController.ConfirmSaleAndOpenSettlement
  EconomyM1SettlementViewAdapter.StoreArrivalSaleLines(caravanId, tradeId, items)
        ↓
SettlementUiBridge.PresentSettlement
        ↓
SettlementUiDataAdapter.CreateViewData
  GetArrivalSaleLines → EconomyM1SettlementViewData.SaleLines
  CreatePersistedReceiptView가 SaleLines를 유지
        ↓
TradeSettlementPanelController.ShowSettlement
  BuildReceipt → AppendSaleDetails (SaleLines 있을 때)
  PrepareReceiptLayout (preferredHeight로 Scroll content 확장)
        ↓
Claim 성공
  EconomyM1SettlementViewAdapter.RemoveArrivalSaleLines(caravanId, tradeId)
```

---

## 5. 주요 타입

### 5.1 Commit 스냅샷 (`MarketInventoryIntegration.cs`)

```text
MarketTransactionItemSummary
  + BaseSellPrice
  + FinalUnitSellPrice
  + SaleModifiers : List<MarketSaleModifierSnapshot>

MarketSaleModifierSnapshot
  ModifierType
  SourceId
  DisplayNameKey
  Operation
  Value
```

- calculator 컬렉션을 그대로 공유하지 않고, 표시용으로 복사한다.
- Buy 전용 modifier는 summary에 넣지 않는다 (`SellPrice` / `Both`만).

### 5.2 Settlement ViewData (`EconomyM1SettlementViewAdapter.cs`)

```text
EconomyM1SettlementViewData
  + SaleLines : List<SettlementSaleLineViewData>

SettlementSaleLineViewData
  ItemId, Quantity
  BaseUnitPrice, FinalUnitPrice, TotalAmount
  Modifiers : List<SettlementModifierLineViewData>

SettlementModifierPresentationKind
  Other | Season | Distance | Lucky
```

세션 cache API:

```text
StoreArrivalSaleLines(caravanId, tradeId, items)
GetArrivalSaleLines(caravanId, tradeId)
RemoveArrivalSaleLines(caravanId, tradeId)
```

키는 `caravanId + "\n" + tradeId`이다.

---

## 6. Modifier 식별·표시 계약

Distance와 Lucky는 기본 `ModifierType`이 둘 다 `RouteEvent`일 수 있으므로, UI 분류는 **`SourceId` prefix를 우선**한다.

| PresentationKind | 판정 |
|------------------|------|
| Lucky | `SourceId`가 `lucky-money:` |
| Distance | `SourceId`가 `distance:` |
| Season | `SourceId`가 `category-season:` 이거나 `ModifierType == Season` |
| Other | 그 외 |

영수증 라벨:

| Kind | 표시 |
|------|------|
| Season | 계절 효과 |
| Distance | 거리 효과 |
| Lucky | 행운 효과 |
| Other | `DisplayNameKey` 또는 `기타 효과` |

값 표기:

- `Percent` → `+20%` 형태 (`Value * 100`)
- 그 외 → `Operation +Value` 형태

**UI에서 Percent를 합산해 최종 배율을 만들지 않는다.**  
최종 단가·판매 금액은 Commit 시점 값(`FinalUnitPrice`, `TotalAmount`)을 그대로 쓴다.

---

## 7. 영수증 표시 예시

`SaleLines`가 있으면 기존 한 줄 판매 entry 대신 상세 블록을 쓴다.

```text
판매 상품
  apple x10
    기본 판매가: 100 G
    계절 효과: +20%
    거리 효과: +15%
    행운 효과: +50%
    최종 단가: 207 G
    판매 금액: 2,070 G
```

`SaleLines`가 비어 있으면 기존 `AppendTradeItems(..., ItemSaleRevenue)` fallback을 유지한다.  
재시작 복구·구형 경로 호환용이다.

레이아웃:

- Prefab의 고정 `ReceiptText` 높이를 전제로 하지 않는다.
- `PrepareReceiptLayout`이 TMP `preferredHeight`로 text/content 세로 크기를 키워 ScrollRect가 감당하게 한다.
- Prefab / Scene 수정 없음.

---

## 8. SaveData·세션 한계

| 상황 | 동작 |
|------|------|
| 같은 세션에서 판매 직후 정산 | SaleLines cache로 modifier 상세 표시 |
| Claim 성공 | 해당 caravan/trade cache 제거 |
| 게임 재실행 후 SettlementPending 복구 | 기존 soldItems 금액 receipt만 표시, modifier 상세 없음 가능 |
| 재계산 | 금지 (시즌 변화·Lucky 소비·commit context 불일치) |

SaveData 확장·migration은 후속 작업이다.

---

## 9. 보호 자산·외부 영향

수정하지 않음:

```text
Assets/_Project/07.Scenes/04_InGame/InGame.unity
Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab
```

정산 패널은 `TradePrepareUI` nested `S8_TradeSettlementPanel`의 기존 TMP 경로를 사용한다.  
실패 Claim loss popup 게이트(`SettlementUiDataAdapter`의 `isWaitingForFailureConfirmation`)는 유지하고, Claim 성공 직후에만 SaleLines cache를 제거한다.

---

## 10. 테스트

`CargoSellMarketTransactionTests`

- 기존 exact price-group 판매 테스트에서 `BaseSellPrice` / `FinalUnitSellPrice` 보존 확인
- `ArrivalSaleLines_ClassifyModifiersAndKeepItemsIsolated`
  - 동일 `RouteEvent`라도 `distance:` / `lucky-money:` SourceId로 Distance·Lucky 분류
  - 품목별 line 격리
  - 테스트 종료 시 `RemoveArrivalSaleLines`로 cache 정리

권장 회귀(수동/Edit Mode):

- `ContextualSellPriceTests`, `MarketPriceModifierTests`
- `FrameworkM1LoopE2EEditorTests`
- `TradeFailureLossPopupWiringTests` (Adapter claim 경로)

---

## 11. 후속 작업 (이번 브랜치 제외)

```text
SaveData에 modifier breakdown 영구 저장
SettlementPending migration
재시작 후 modifier 상세 완전 복구
VFX / SellPriceModifierItem prefab화
S9 PaymentPanel 대규모 재설계
InGame / MainUICanvas 변경
판매가 계산 공식 변경
```

---

## 12. 한 줄 요약

도착 판매 Commit 결과의 Base/Final/Modifier 스냅샷을 세션 cache로 Settlement ViewData까지 전달하고, 기존 S8 TMP 영수증에 품목별 판매 배율 상세를 Prefab·SaveData 변경 없이 표시한다.
