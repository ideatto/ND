# Progression ModifierInput Price Adapter

작성일: 2026-07-10  
담당: Progression

## 목적

Sandbox의 수동 가격 보정 데이터인 `ModifierInput`을 `ND.Economy.PriceModifierInput` 목록으로 변환해,
기존 `PriceCalculator` 계산에 반영한다.

흐름:

```text
TradeItemData.Modifiers
-> LjhEconomyM1InputAdapter.ToPriceModifierInputs(...)
-> PriceCalculationInput.Modifiers
-> PriceCalculator.Calculate(...)
```

## 변환 규칙

`ModifierInput`의 각 `modifierBundles`를 순회한다. 타입, 대상, 연산이 모두 대응될 때만
`PriceModifierInput`을 생성한다.

| Sandbox `ModifierType` | Economy `PriceModifierType` |
|---|---|
| `Season` | `Season` |
| `Disaster` | `Disaster` |
| `ActiveEvent` | `RouteEvent` |
| `PlayerGrowth` | `PlayerGrowth` |
| `OverSupply` | `Oversupply` |
| `AffectToTown` | `Town` |

| Sandbox | Economy |
|---|---|
| `Target.BuyPrice` | `PriceModifierTarget.BuyPrice` |
| `Target.SellPrice` | `PriceModifierTarget.SellPrice` |
| `Operation.Add` | `PriceModifierOperation.Add` |
| `Operation.Percent` | `PriceModifierOperation.Percent` |

`sourceId`, `displayName`, `value`는 각각 `SourceId`, `DisplayNameKey`, `Value`로 전달한다.

## 제외 규칙

아래 값은 현행 `PriceModels`에 가격 보정 대응값이 없으므로 변환하지 않는다.

- `ModifierType.None`, `ModifierType.LowerSupply`
- `Target.None`, `Target.BaseMoveSpeed`
- `Operation.None`, `Operation.Subtract`

`BaseMoveSpeed`는 가격이 아니라 Core 이동속도 보정용 데이터로 별도 처리한다.

## 반영 파일

- `unity_changes/Assets/_Project/03.Economy/06_Integration/LjhEconomyM1InputAdapter.cs`
  - `ToPriceCalculationInput`에서 상품 modifier 목록을 전달한다.
  - `ToPriceModifierInputs`와 타입/대상/연산 변환 함수를 추가했다.
- `unity_changes/Assets/_Project/03.Economy/03_Settlement/Editor/EconomyM1SmokeScenarioTests.cs`
  - 변환 가능한 modifier만 생성되는지 검증한다.
  - 구매가 100에 `Percent +0.1`을 적용해 110, 판매가 100에 `Add +20`을 적용해 120이 되는지 검증한다.

## 확인 방법

Unity 메뉴에서 아래 검사를 실행한다.

```text
ND/Economy/Run All M1 Economy Checks
```

기대 결과:

```text
[Economy M1 Checks] Success
```
