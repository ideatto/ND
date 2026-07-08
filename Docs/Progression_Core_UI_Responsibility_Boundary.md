# Progression Core/UI Responsibility Boundary

작성일: 2026-07-08
담당: Progression
대상: Core Gameplay, UI & Data, Framework & Integration

---

## 1. 목적

이 문서는 M1 최소 경제 루프에서 Core, UI, Progression의 책임 경계를 고정한다.

핵심 원칙은 아래와 같다.

- Progression은 가격/정산/재화/성장/런타임 스탯 계산을 소유한다.
- Core는 Progression 결과를 게임 상태에 반영한다.
- UI는 Progression 결과를 표시만 한다.
- Core와 UI는 Progression 내부 계산식을 복제하지 않는다.

---

## 2. 단일 호출 지점

M1에서 Core/UI가 사용하는 Progression 진입점은 아래 하나로 고정한다.

```csharp
EconomyM1LoopCalculator.Execute(EconomyM1LoopInput input)
```

이 호출은 내부에서 아래 순서를 처리한다.

```text
가격 계산
-> 정산 항목 생성
-> 무역 재화 / 발전 재화 반영
-> 성장 구매 처리
-> Core 런타임 스탯 계산
-> 최종 재화 상태 반환
```

Core와 UI는 이 순서를 개별 계산기로 재조합하지 않는다.

---

## 3. Core 책임

Core는 무역 1회가 완료되는 시점에 Progression 루프를 호출한다.

Core가 해야 할 일:

- `SaveData`, `TradeItemData`, `RouteData` 또는 어댑터로 `EconomyM1LoopInput`을 준비한다.
- `EconomyM1LoopCalculator.Execute(input)`를 호출한다.
- `result.Success == false`이면 무역 완료 처리를 성공으로 확정하지 않는다.
- 저장/상태 갱신에는 `result.FinalCurrencyState`를 사용한다.
- 다음 무역 실행에는 `result.RuntimeStats`를 적용한다.

Core가 하지 않을 일:

- `SettlementBreakdown.Entries`를 다시 합산해서 재화를 계산하지 않는다.
- 성장 구매 비용을 별도로 차감하지 않는다.
- 성장 레벨이나 경제 상태를 직접 해석해 런타임 스탯을 재계산하지 않는다.

---

## 4. UI 책임

UI는 Progression 결과를 표시한다.

UI가 표시할 값:

- `result.PriceResult.UnitBuyPrice`
- `result.PriceResult.UnitSellPrice`
- `result.PriceResult.TotalBuyPrice`
- `result.PriceResult.TotalSellPrice`
- `result.PriceResult.ExpectedGrossProfit`
- `result.Settlement.NetProfit`
- `result.Settlement.TradeMoneyAfter`
- `result.Settlement.DevelopmentCurrencyReward`
- `result.Settlement.Entries`
- `result.GrowthPurchase`
- `result.RuntimeStats`
- `result.Success`
- `result.ErrorCode`

UI가 하지 않을 일:

- 최종 구매가/판매가를 다시 계산하지 않는다.
- 정산 항목을 합산해서 최종 재화를 다시 계산하지 않는다.
- 성장 구매 비용을 직접 차감하지 않는다.
- `RuntimeStats` 보정값을 직접 계산하지 않는다.

---

## 5. Progression 책임

Progression은 아래 계산과 결과 구조를 소유한다.

- 가격 계산
- 가격 보정 순서
- 정산 항목 생성
- 무역 재화 반영
- 발전 재화 보상 반영
- 성장 구매 비용 차감
- 성장 결과 산출
- Core용 `RuntimeStats` 산출
- 최종 저장용 `FinalCurrencyState` 반환

Progression 결과에서 저장에 우선 사용하는 값은 `FinalCurrencyState`다.
Progression 결과에서 Core 플레이 로직 적용에 사용하는 값은 `RuntimeStats`다.
Progression 결과에서 UI 정산 표시에 사용하는 값은 `Settlement.Entries`다.

---

## 6. Data/LJH 연결 기준

Data 쪽 원천은 아래 기준으로 Progression 입력에 연결한다.

```text
SaveData.player.tradingCurrency
-> CurrencyState.TradeMoney

SaveData.player.developmentCurrency
-> CurrencyState.DevelopmentCurrency

TradeItemData.ItemId
-> PriceCalculationInput.TradeItemId

TradeItemData.BaseBuyPrice
-> PriceCalculationInput.BaseBuyPrice

TradeItemData.BaseSellPrice
-> PriceCalculationInput.BaseSellPrice

RouteData.RouteId
-> PriceCalculationInput.RouteId

RouteData.FromTownId
-> PriceCalculationInput.FromTownId

RouteData.ToTownId
-> PriceCalculationInput.ToTownId

RouteData.BaseFoodCost
-> EconomyM1LoopInput.FoodCost

RouteData.BaseMercenaryCost
-> EconomyM1LoopInput.MercenaryCost
```

Progression 쪽 어댑터:

```csharp
LjhEconomyM1InputAdapter.ToEconomyM1LoopInput(...)
LjhEconomyM1InputAdapter.ApplyFinalCurrencyState(...)
```

---

## 7. 고정 문장

팀 간 합의 문장은 아래처럼 고정한다.

```text
UI/Core는 SettlementBreakdown.Entries를 다시 합산해서 재화를 계산하지 않는다.
저장에는 EconomyM1LoopResult.FinalCurrencyState만 사용한다.
Core 적용에는 EconomyM1LoopResult.RuntimeStats만 사용한다.
UI 표시는 EconomyM1LoopResult의 PriceResult, Settlement, GrowthPurchase, RuntimeStats를 사용한다.
```

---

## 8. 확인 방법

Unity 메뉴에서 아래 항목을 실행한다.

```text
ND/Economy/Run All M1 Economy Checks
```

성공 로그:

```text
[Economy M1 Checks] Success
```

M1 DebugRunner 기준 기대값:

```text
Buy: 500
Sell: 700
Net: 150
TradeMoney: 1150
DevCurrency: 0
MaxLoadBonus: 10
SpeedMultiplier: 1
```
