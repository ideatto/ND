# LJH Data -> Progression M1 Handoff

## Progression 쪽에서 처리한 것

- `SaveData.player.tradingCurrency`를 `ND.Economy.CurrencyState.TradeMoney`로 매핑한다.
- `SaveData.player.developmentCurrency`를 `ND.Economy.CurrencyState.DevelopmentCurrency`로 매핑한다.
- `EconomyM1LoopResult.FinalCurrencyState`를 다시 `SaveData.player`의 두 재화 필드에 반영할 수 있게 했다.
- `TradeItemData + RouteData + SaveData`에서 `EconomyM1LoopInput`을 조립하는 어댑터를 추가했다.

파일:

```text
unity_changes/Assets/_Project/03.Economy/06_Integration/LjhEconomyM1InputAdapter.cs
```

## LJH 쪽에서 진행해야 할 것

1. `_LJH.PriceCalculationInput`은 더 이상 확장하지 않고 `ND.Economy.PriceCalculationInput`으로 교체한다.
2. 가격 계산 입력은 직접 계산하지 말고 아래 데이터에서 조립한다.

```text
TradeItemData.ItemId        -> PriceCalculationInput.TradeItemId
TradeItemData.BaseBuyPrice  -> PriceCalculationInput.BaseBuyPrice
TradeItemData.BaseSellPrice -> PriceCalculationInput.BaseSellPrice
RouteData.RouteId           -> PriceCalculationInput.RouteId
RouteData.FromTownId        -> PriceCalculationInput.FromTownId
RouteData.ToTownId          -> PriceCalculationInput.ToTownId
SaveData.world.currentSeason   -> PriceCalculationInput.SeasonId
SaveData.world.currentDisaster -> PriceCalculationInput.DisasterId
```

3. M1 비용은 `RouteData`에서 가져온다.

```text
RouteData.BaseFoodCost      -> EconomyM1LoopInput.FoodCost
RouteData.BaseMercenaryCost -> EconomyM1LoopInput.MercenaryCost
```

`RouteData.baseRequiredFoodQuantity`, `RouteData.baseRequiredMercenaryPower`는 상단 출발 시 UI에 보여줄 요구량/요구 전력이며,
Progression M1 정산 비용 입력으로 사용하지 않는다.

4. `TradeItemData.BaseBuyPrice`, `BaseSellPrice`, 선택 수량은 1 이상이어야 한다. 0이면 Progression 가격 계산 실패가 정상이다.
5. UI 정산 표시는 `_LJH.TradeResultData` 합산값이 아니라 `ND.Economy.SettlementBreakdown.Entries` 기준으로 구성한다.
6. `_LJH.SettlementInput`, `_LJH.SoldItemInput`도 가능하면 `ND.Economy` 타입으로 교체하거나 미사용 처리한다.

## 확인용 기본 흐름

```text
SaveData + TradeItemData + RouteData
-> LjhEconomyM1InputAdapter.ToEconomyM1LoopInput(...)
-> EconomyM1LoopCalculator.Execute(...)
-> result.FinalCurrencyState 저장
-> result.Settlement.Entries UI 표시
-> result.RuntimeStats Core 적용
```

현재 `_LJH` 더미 데이터는 `baseBuyPrice = 10`, `baseSellPrice = 8`, route 비용 0이라 문서 예시의 순이익 150 케이스와 다르다. 성공 루프 예시는 별도 apple 100/140, food 50 테스트 데이터가 필요하다.
