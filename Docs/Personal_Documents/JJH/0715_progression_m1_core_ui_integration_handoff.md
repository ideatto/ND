# Progression M1 Core/UI Integration Handoff

작성일: 2026-07-15  
담당: Progression  
대상: Core Gameplay, UI & Data, Content & Tools

## 현재 완료된 Progression 작업

- `EconomyM1LoopCalculator.Execute(EconomyM1LoopInput)` M1 계산 루프
- `LjhEconomyM1InputAdapter.ToEconomyM1LoopInput(...)` 입력 조립
- `LjhEconomyM1InputAdapter.ApplyFinalCurrencyState(...)` SaveData 화폐 반영
- `TradeItemData.Modifiers`의 가격 보정 변환 및 `PriceCalculator` 연결

## Core Gameplay 작업

무역 1회가 성공적으로 완료되는 시점에 아래 순서로 호출한다.

### 실제 거래 완료 및 정산 화면 연동 요청

현재 `JourneyRunTest`는 임시 검증 스크립트이므로 실제 게임 거래 완료를 소유하지 않는다.
Core는 실제 거래 완료 지점을 처리하는 전용 스크립트/클래스를 생성해야 한다.

- 실제 거래 완료 지점에서 `EconomyM1FlowService.ExecuteTradeAndApply(...)`를 무역 1회당 한 번 호출한다.
- `result.Success == false`이면 무역 완료를 성공 처리하지 않는다.
- 성공 시 `result.RuntimeStats`를 다음 무역 실행에 적용할 Core 런타임 상태로 보관한다.
- UI 정산 화면은 `result.Settlement.Entries`를 실제 목록 UI에 바인딩한다.
- UI는 `Settlement.Entries`를 다시 합산하거나 별도 정산 금액을 계산하지 않는다.

```csharp
EconomyM1LoopResult result = EconomyM1FlowService.ExecuteTradeAndApply(
    saveData,
    selectedTradeItem,
    selectedRoute,
    quantity,
    tradeId,
    developmentCurrencyReward,
    purchaseGrowth,
    growthId,
    playerGrowthLevel,
    caravanGrowthLevel);

if (!result.Success)
{
    // 무역 완료를 성공 처리하지 않고 result.ErrorCode를 사용한다.
    return;
}

// result.RuntimeStats를 다음 무역 실행의 Core 로직에 적용한다.
```

Core는 `Settlement.Entries`를 다시 합산하거나, 가격·성장·화폐를 별도로 계산하지 않는다.

## UI 작업

UI는 `EconomyM1LoopResult`를 받아 아래 항목을 표시한다.

- `PriceResult.UnitBuyPrice`, `UnitSellPrice`, `TotalBuyPrice`, `TotalSellPrice`
- `PriceResult.Modifiers`
- `Settlement.NetProfit`, `TradeMoneyAfter`, `DevelopmentCurrencyReward`
- `Settlement.Entries`
- `GrowthPurchase`, `RuntimeStats`, `Success`, `ErrorCode`

정산 목록은 반드시 `Settlement.Entries`로 구성한다. 기존 `_LJH.TradeResultData`의 합산값을
사용하거나, UI에서 정산 금액을 다시 계산하지 않는다.

## Data 및 Content 작업

- `TradeItemData`: `itemId`, `baseBuyPrice`, `baseSellPrice`, 수동 `modifiers`
- `RouteData`: `routeId`, 출발/도착 도시, `baseFoodCost`, `baseMercenaryCost`
- `SaveData`: 무역/발전 화폐, 계절, 재난 상태
- Content는 M1 검증용 실제 값을 입력한다. 예: apple 구매 100 / 판매 140, 식량 비용 50

`BaseRequiredFoodQuantity`, `BaseRequiredMercenaryPower`는 거래 준비 화면의 조건·경고 표시용이다.
M1 정산 비용에는 `BaseFoodCost`, `BaseMercenaryCost`만 전달한다.

## Cargo 무역 물품 적재 단계 구현 및 책임 정리

### 현재 연결된 계산과 표시

- 현재 적재 중량은 적재된 각 `TradeItemData.Weight * Quantity`의 합계를 사용한다.
- 적재 슬롯 수는 선택한 `WagonData.InventorySlotCount`를 사용한다.
- 최대 적재 중량 공식은 `WagonData.MaxLoad + sum(DraftAnimalData.IncreaseMaxLoad)`이며 기존 Core `CaravanCalculator.GetMaxLoad()` 결과를 사용한다.
- 견인동물 먹이 소비량은 선택한 모든 `DraftAnimalData.FeedConsumption`의 합계를 초당 값으로 표시한다.
- 적재 음식 수량은 `TradeItemCategory.Food`와 `TradeItemCategory.DraftAnimalsFood` 수량 합계를 사용한다.
- 마차 데이터가 없는 단독 UI 테스트에서는 Inspector의 `maximumLoad` 값을 fallback으로 사용한다.
- `RuntimeStats.MaxLoadBonus`, `MaxLoadMultiplier`와 `DraftAnimalData.IncreaseOverLoad` 기반 과적 감속은 M1 범위에 포함하지 않는다.

### 1. 실제 현재 도시 상점 연결

현재 도시 상점, 결정적 재고 생성 및 저장 연결 요구사항은 [`0715_Progression MarketInventory_Change_Request.md`](<./0715_Progression MarketInventory_Change_Request.md>)에 정의되어 있다.

### 2. 음식 상품 최소 1종 보장

모든 마을 상점에서 음식 카테고리 상품을 최소 1종 제공하는 요구사항과 검증 책임은 `0715_Progression MarketInventory_Change_Request.md`를 따른다.

### 3. 선택한 마차·견인동물의 실제 전달

3-1·3-2 선택 패널을 제작한 Core 담당자가 선택 확정 결과를 Cargo 패널에 전달한다.

```csharp
cargoPanel.SetSelectedWagon(selectedWagon);
cargoPanel.SetSelectedDraftAnimals(selectedAnimals);
```

- `DraftAnimalSelectionSummaryBridge`는 기존 `AnimalInventoryPanel.OnSelectionChanged`를 구독해 최종 선택 동물 수량과 수량만큼 펼친 `DraftAnimalType[]`을 표시한다.
- 선택 마차는 기존 `AnimalInventoryPanel.OnWagonSelected` 결과를 사용한다.
- `Horse x2`, `Donkey x1`은 `[Horse, Horse, Donkey]`와 세 개의 `DraftAnimalData` 항목으로 전달한다.
- Cargo 패널은 전달받은 마차·견인동물을 Core `CaravanCalculator.GetMaxLoad()` 입력으로 변환한다.

### 4. 뒤로 가기 분기

통합 패널 관리자인 Core 담당자가 선택한 이동수단 타입에 따라 이전 화면을 결정한다.

- `WagonWithAnimals`: 동물 선택 패널로 복귀
- `None`, `Mount`: 이동수단 선택 패널로 복귀

현재 `CargoLoadingPanelController.BackToPreviousStep()`은 `previousStepPanel` 하나만 활성화하므로 타입별 분기 연결이 필요하다.

### 5. 무역 취소 시 전체 준비 데이터 초기화

주 담당은 전체 준비 흐름과 통합 패널을 관리하는 Core다.

- Core: 선택 도시·경로·마차·견인동물·적재 상품·용병과 현재 패널 단계를 초기화하고 일반 상태로 전환
- QA / 통합: 저장된 `marketPurchasePreparation`, pending 상태와 임시 화물 데이터를 초기화
- Economy / Progression: 구매가 이미 확정된 경우 `CurrencyWallet` 환불 결과 제공
- Core: QA / 통합과 Economy / Progression의 초기화·환불 API가 성공한 뒤 화면 상태 초기화 완료 처리

`CargoLoadingPanelController.ResetCargo()`는 현재 적재 상품과 구매 예정 금액만 초기화하므로 전체 거래 준비 컨텍스트 초기화는 상위 통합 흐름에서 수행한다.

### 6. 실제 Gold 및 결제

실제 Save 화폐, `CurrencyWallet` 결제·환불 및 시장 세션 연결은 `0715_Progression MarketInventory_Change_Request.md`를 따른다. `ND_MARKET_SAVE_SCHEMA_VNEXT`가 활성화되기 전의 `currentGold`는 표시·테스트용 값이며 최종 재화 원장으로 취급하지 않는다.

### 7. 시장 단가

- M1에서는 `TradeItemData.BaseBuyPrice`를 기준 가격으로 사용한다.
- `ND_MARKET_SAVE_SCHEMA_VNEXT` 활성화 후 Economy / Progression이 도시·재고 기반 단가를 계산해 `MarketStockSaveData.unitPrice`에 저장한다.
- 계절·이벤트·성장 보정을 포함한 `PriceCalculationResult.UnitBuyPrice`는 아직 확정되지 않았으므로 현재 범위에 포함하지 않는다.
- 상세 책임과 가격 고정 시점은 `0715_Progression MarketInventory_Change_Request.md`를 따른다.

## 확인 방법

```text
ND/Economy/Run All M1 Economy Checks
```

성공 로그:

```text
[Economy M1 Checks] Success
```
