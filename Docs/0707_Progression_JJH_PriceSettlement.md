# Progression Price and Settlement Contract

작성일: 2026-07-07
담당: Progression
대상: Core Gameplay, UI & Data, Content & Tools, Framework & Integration

---

## 1. 목적

이 문서는 Progression이 M1에서 고정해야 하는 가격 계산과 정산 계약을 정의한다.

목표는 최종 밸런스 수치를 확정하는 것이 아니라, 팀 간 입력/출력 구조와 계산 순서를 고정하는 것이다.

- 같은 입력은 항상 같은 가격 결과를 낸다.
- 가격 보정 순서는 고정된다.
- 정산 화면에 표시할 항목은 분리된다.
- UI는 경제 계산을 중복 구현하지 않는다.
- Core는 Progression이 산출한 런타임 스탯만 적용한다.

---

## 2. 가격 계산 순서

모든 구매가와 판매가는 아래 순서로 계산한다.

```text
기본 가격
-> 도시 기본 보정
-> 계절 보정
-> 재난 보정
-> 도시/무역로 이벤트 보정
-> 과공급 보정
-> 플레이어 구매 할인 / 판매 할증
```

### M1 최소 적용

M1에서는 아래 항목만 실제 계산에 반영해도 된다.

1. 기본 가격
2. 도시 기본 보정
3. 플레이어 구매 할인 / 판매 할증

계절, 재난, 이벤트, 과공급은 M2 이후 실제 값이 들어가도 되지만 출력 구조에는 지금부터 포함할 수 있어야 한다.

---

## 3. PriceCalculationInput

가격 계산 요청 입력값이다.

| 필드 | 타입 | 설명 | M1 |
|---|---|---|---|
| `TradeItemId` | string | 상품 ID | 필수 |
| `FromTownId` | string | 구매 도시 ID | 필수 |
| `ToTownId` | string | 판매 도시 ID | 필수 |
| `RouteId` | string | 무역로 ID | 필수 |
| `Quantity` | int | 계산 수량 | 필수 |
| `BaseBuyPrice` | int | 기본 구매가 | 필수 |
| `BaseSellPrice` | int | 기본 판매가 | 필수 |
| `SeasonId` | string | 현재 계절 ID | 선택 |
| `DisasterId` | string | 현재 재난 ID | 선택 |
| `ActiveEventIds` | string[] | 적용 중인 이벤트 ID 목록 | 선택 |
| `PlayerGrowthLevel` | int | 플레이어 가격 성장 단계 | 선택 |
| `CaravanGrowthLevel` | int | 상단 성장 단계 | 선택 |
| `OversupplyLevel` | int | 과공급 단계 | 선택 |

### 입력 제한

- `Quantity`는 1 이상이어야 한다.
- `BaseBuyPrice`, `BaseSellPrice`는 1 이상이어야 한다.
- 필수 ID가 비어 있으면 계산하지 않고 오류 결과를 반환한다.
- M1에서는 선택 필드가 비어 있어도 계산 가능해야 한다.

---

## 4. PriceCalculationResult

가격 계산 결과다. UI는 이 결과를 표시하고, Core는 최종 금액만 사용한다.

| 필드 | 타입 | 설명 |
|---|---|---|
| `TradeItemId` | string | 상품 ID |
| `Quantity` | int | 계산 수량 |
| `UnitBuyPrice` | int | 최종 단위 구매가 |
| `UnitSellPrice` | int | 최종 단위 판매가 |
| `TotalBuyPrice` | int | 총 구매 비용 |
| `TotalSellPrice` | int | 총 판매 수익 |
| `ExpectedGrossProfit` | int | 판매 수익 - 구매 비용 |
| `Modifiers` | PriceModifierBreakdown[] | 보정 항목 목록 |
| `IsValid` | bool | 계산 가능 여부 |
| `ErrorCode` | string | 계산 실패 사유 |

### 반올림 규칙

- 내부 보정 계산은 소수 계산을 허용한다.
- 최종 단위 가격 산출 시 `round to nearest int`를 사용한다.
- 최종 단위 가격은 최소 1 이상이어야 한다.

---

## 5. PriceModifierBreakdown

정산/가격 화면에서 보정 요인을 표시하기 위한 항목이다.

| 필드 | 타입 | 설명 |
|---|---|---|
| `ModifierType` | enum | 보정 종류 |
| `SourceId` | string | 도시/계절/재난/이벤트/성장 ID |
| `DisplayNameKey` | string | UI 표시명 키 |
| `Target` | enum | `BuyPrice`, `SellPrice`, `Both` |
| `Operation` | enum | `Add`, `Multiply`, `Percent` |
| `Value` | float | 적용값 |
| `AmountDelta` | int | 실제 가격 변화량 |

### PriceModifierType

```text
Base
Town
Season
Disaster
RouteEvent
Oversupply
PlayerGrowth
CaravanGrowth
Debug
```

---

## 6. SettlementInput

무역 완료 후 정산 계산에 필요한 입력값이다.

| 필드 | 타입 | 설명 | M1 |
|---|---|---|---|
| `TradeId` | string | 무역 1회 실행 ID | 필수 |
| `SoldItems` | SoldItemInput[] | 판매한 상품 목록 | 필수 |
| `TradeMoneyBefore` | int | 정산 전 무역 재화 | 필수 |
| `FoodCost` | int | 식량 비용 | 필수 |
| `MercenaryCost` | int | 용병 고용비 | 필수 |
| `CartRepairCost` | int | 마차 내구도 손실/수리 비용 | 선택 |
| `LostItemValue` | int | 손실 상품 가치 | 선택 |
| `EventProfit` | int | 이벤트 수익 | 선택 |
| `EventLoss` | int | 이벤트 손실 | 선택 |
| `LoanRepayment` | int | 대출 자동 상환액 | 선택 |
| `DevelopmentCurrencyReward` | int | 발전 재화 보상 | 필수 |

### SoldItemInput

| 필드 | 타입 | 설명 |
|---|---|---|
| `TradeItemId` | string | 상품 ID |
| `Quantity` | int | 판매 수량 |
| `TotalBuyPrice` | int | 해당 수량의 구매 비용 |
| `TotalSellPrice` | int | 해당 수량의 판매 수익 |

---

## 7. SettlementBreakdown

정산 결과 출력이다.

| 필드 | 타입 | 설명 |
|---|---|---|
| `TradeId` | string | 무역 1회 실행 ID |
| `TotalRevenue` | int | 총 수익 |
| `TotalExpense` | int | 총 비용 |
| `GrossTradeProfit` | int | 상품 판매 수익 - 상품 구매 비용 |
| `NetProfit` | int | 최종 순이익 |
| `TradeMoneyAfter` | int | 정산 후 무역 재화 |
| `DevelopmentCurrencyReward` | int | 발전 재화 획득량 |
| `Entries` | SettlementEntry[] | 정산 표시 항목 |
| `IsBankrupt` | bool | 파산 여부 |
| `MinimumRecoveryMoney` | int | 파산 후 복구 최소 금액 |

### SettlementEntry

| 필드 | 타입 | 설명 |
|---|---|---|
| `EntryType` | enum | 정산 항목 종류 |
| `DisplayNameKey` | string | UI 표시명 키 |
| `Amount` | int | 금액 또는 수량 |
| `IsPositive` | bool | 수익 항목 여부 |
| `SourceId` | string | 상품/이벤트/시스템 ID |

### SettlementEntryType

```text
ItemPurchaseCost
ItemSaleRevenue
FoodCost
MercenaryCost
CartRepairCost
LostItemValue
EventProfit
EventLoss
LoanRepayment
DevelopmentCurrencyReward
GrowthPurchaseCost
Debug
```

---

## 8. 정산 항목 생성 원칙

- `SettlementEntry.Amount`는 항상 0 이상 값으로 기록한다.
- 수익/비용 방향은 `IsPositive`로 구분한다.
- M1 필수 항목은 값이 0이어도 `Entries`에 포함할 수 있다.
- 선택 항목은 값이 0이면 생략해도 된다.
- UI는 `EntryType`, `DisplayNameKey`, `Amount`, `IsPositive`만으로 정산 목록을 그릴 수 있어야 한다.
- UI는 `Entries`를 다시 합산해서 게임 상태를 바꾸지 않는다.

### M1 필수 정산 항목

| EntryType | IsPositive | 제공자 | 설명 |
|---|---:|---|---|
| `ItemPurchaseCost` | false | Progression | 상품 구매 비용 합계 |
| `ItemSaleRevenue` | true | Progression | 상품 판매 수익 합계 |
| `FoodCost` | false | Progression/Core | 식량 비용 |
| `MercenaryCost` | false | Progression/Core | 용병 비용 |
| `DevelopmentCurrencyReward` | true | Progression | 발전 재화 획득량 |

---

## 9. 재화 구분

M1에서는 최소 2종의 재화를 구분한다.

| 재화 | 권장 필드명 | 사용처 |
|---|---|---|
| 무역 재화 | `TradeMoney` | 상품 구매, 식량, 용병, 마차 비용, 대출 상환 |
| 발전 재화 | `DevelopmentCurrency` | 성장 구매, 기부, 투자, 지역/상품 해금 |

### 원칙

- 무역 재화와 발전 재화의 사용처는 섞지 않는다.
- 정산의 `NetProfit`은 무역 재화에 반영한다.
- `DevelopmentCurrencyReward`는 별도 보상으로 반영한다.
- M1에서 발전 재화 사용처는 성장 1종만 있어도 된다.

---

## 10. M1 기본 정산 공식

```text
totalRevenue = sum(item.totalSellPrice) + eventProfit
totalExpense = sum(item.totalBuyPrice)
             + foodCost
             + mercenaryCost
             + cartRepairCost
             + lostItemValue
             + eventLoss
             + loanRepayment

grossTradeProfit = sum(item.totalSellPrice) - sum(item.totalBuyPrice)
netProfit = totalRevenue - totalExpense
tradeMoneyAfter = tradeMoneyBefore + netProfit
```

M1에서는 `CartRepairCost`, `LostItemValue`, `EventProfit`, `EventLoss`, `LoanRepayment`가 0이어도 된다.

---

## 11. Core에 전달할 런타임 스탯

Progression은 성장/경제 상태에서 파생된 보정값을 `CoreRuntimeStatModifier`로 Core에 전달한다.
Core는 성장 레벨, 기부 단계, 대출 상태를 직접 해석하지 않고 결과값만 적용한다.

| 필드 | 타입 | M1 기본값 | 설명 |
|---|---|---:|---|
| `MaxLoadBonus` | int | 0 | 최대 적재량 고정 보너스 |
| `MaxLoadMultiplier` | float | 1.0 | 최대 적재량 배율 |
| `SpeedMultiplier` | float | 1.0 | 이동 속도 배율 |
| `FoodEfficiencyMultiplier` | float | 1.0 | 식량 효율 배율 |
| `CombatPowerBonus` | int | 0 | 전투력 고정 보너스 |
| `CombatPowerMultiplier` | float | 1.0 | 전투력 배율 |
| `LossLimitRate` | float | 0.5 | 실패 시 최대 손실 비율 |
| `RiskMultiplier` | float | 1.0 | 위험도 배율 |
| `MinRecoveryTradeMoney` | int | 0 | 파산/대출 후 최소 복구 금액 |

### M1 성장 예시

아래 능력치 증가 수치는 확정 기획값이 아니라 M1 연결 검증용 임시값이다.
최종 성장/레벨업 능력치 증가는 추후 기획 단계 협의에 따라 변경한다.

```text
PlayerGrowthLevel 1:
  MaxLoadBonus = 10
  SpeedMultiplier = 1.0
  LossLimitRate = 0.5

CaravanGrowthLevel 1:
  MaxLoadBonus = 20
  SpeedMultiplier = 1.0
  LossLimitRate = 0.5
```

---

## 12. UI & Data 전달 항목

UI는 아래 값을 Progression 결과에서 받아 표시한다.

- 기본 구매가
- 기본 판매가
- 도시/계절/재난/이벤트/무역로/과공급/성장 보정
- 총 구매 비용
- 총 판매 수익
- 예상 상품 차익
- 식량 비용
- 용병 비용
- 손실 금액
- 최종 순이익
- 정산 후 무역 재화
- 획득 발전 재화
- 정산 상세 항목

UI는 위 값을 다시 계산하지 않는다.

---

## 13. M1 완료 기준

- 상품 1개 이상에 대해 구매가/판매가/차익을 계산할 수 있다.
- 식량 비용과 용병 비용을 정산 항목으로 분리 표시한다.
- 성장 구매 비용을 차감할 수 있다.
- 성장 1종이 Core 런타임 스탯에 반영된다.
- `SettlementBreakdown.Entries`만으로 정산 화면을 구성할 수 있다.



