# Progression M1 Core/UI Integration Handoff

작성일: 2026-07-10  
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

## 확인 방법

```text
ND/Economy/Run All M1 Economy Checks
```

성공 로그:

```text
[Economy M1 Checks] Success
```
