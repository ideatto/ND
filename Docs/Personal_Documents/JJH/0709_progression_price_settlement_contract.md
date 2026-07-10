# Progression Price and Settlement Contract

## 목적

Progression & System 담당자가 1순위로 고정해야 하는 가격/정산 계약이다.
이 문서는 Core Gameplay, UI & Data, Content & Tools, Framework & Integration이 같은 입력과 출력을 기준으로 작업하도록 만든다.

이 단계의 목표는 최종 밸런스가 아니라 다음을 보장하는 것이다.

- 같은 입력은 항상 같은 가격 결과를 낸다.
- 가격 보정 순서가 고정되어 있다.
- 정산 화면에 표시할 항목이 분리되어 있다.
- UI가 경제 계산을 중복 구현하지 않는다.
- Core가 성장/경제 보정값을 런타임에 적용할 수 있다.

---

## 1. 가격 계산 순서

모든 구매가/판매가는 아래 순서로 계산한다.

```text
기본 가격
-> 도시 기본 보정
-> 계절 보정
-> 재난 보정
-> 도시/무역로/이벤트 보정
-> 과공급 보정
-> 플레이어 구매 할인/판매 할증
```

### M1 최소 적용

M1에서는 아래 항목만 실제 계산에 반영해도 된다.

1. 기본 가격
2. 도시 기본 보정
3. 플레이어 구매 할인/판매 할증

계절, 재난, 이벤트, 과공급은 M2 이후 사용하되, 출력 구조에는 지금부터 포함한다.

---

## 2. PriceCalculationInput

가격 계산 요청 입력값이다.

| 필드 | 타입 | 설명 | M1 필수 |
|---|---|---|---|
| `tradeItemId` | string | 상품 ID | 필수 |
| `fromTownId` | string | 구매 도시 ID | 필수 |
| `toTownId` | string | 판매 도시 ID | 필수 |
| `routeId` | string | 무역로 ID | 필수 |
| `quantity` | int | 계산 수량 | 필수 |
| `baseBuyPrice` | int | 기본 구매가 | 필수 |
| `baseSellPrice` | int | 기본 판매가 | 필수 |
| `seasonId` | string | 현재 계절 ID | 선택 |
| `disasterId` | string | 현재 재난 ID | 선택 |
| `activeEventIds` | string[] | 적용 중인 이벤트 ID 목록 | 선택 |
| `playerGrowthLevel` | int | 플레이어 가격 성장 단계 | 선택 |
| `caravanGrowthLevel` | int | 상단 성장 단계 | 선택 |
| `oversupplyLevel` | int | 과공급 단계 | 선택 |

### 입력 제한

- `quantity`는 0보다 커야 한다.
- `baseBuyPrice`, `baseSellPrice`는 0보다 커야 한다.
- 알 수 없는 ID가 들어오면 계산하지 않고 오류 결과를 반환한다.
- M1에서는 선택 필드가 비어 있어도 계산 가능해야 한다.

---

## 3. PriceCalculationResult

가격 계산 결과다. UI는 이 결과를 받아 표시하고, Core는 최종 금액만 사용한다.

| 필드 | 타입 | 설명 |
|---|---|---|
| `tradeItemId` | string | 상품 ID |
| `quantity` | int | 계산 수량 |
| `unitBuyPrice` | int | 최종 단위 구매가 |
| `unitSellPrice` | int | 최종 단위 판매가 |
| `totalBuyPrice` | int | 총 구매 비용 |
| `totalSellPrice` | int | 총 판매 수익 |
| `expectedGrossProfit` | int | 판매 수익 - 구매 비용 |
| `modifiers` | PriceModifierBreakdown[] | 보정 항목 목록 |
| `isValid` | bool | 계산 가능 여부 |
| `errorCode` | string | 계산 불가 사유 코드 |

### 반올림 규칙

- 각 보정은 내부적으로 소수 계산을 허용한다.
- 최종 단위 가격 산출 시 `round to nearest int`를 사용한다.
- 최종 단위 가격은 최소 1 이상이어야 한다.

---

## 4. PriceModifierBreakdown

정산 화면에 보정 원인을 표시하기 위한 항목이다.

| 필드 | 타입 | 설명 |
|---|---|---|
| `modifierType` | enum | 보정 종류 |
| `sourceId` | string | 도시/계절/재난/이벤트/성장 ID |
| `displayNameKey` | string | UI 표시명 키 |
| `target` | enum | `BuyPrice`, `SellPrice`, `Both` |
| `operation` | enum | `Add`, `Multiply`, `Percent` |
| `value` | float | 적용값 |
| `amountDelta` | int | 실제 가격 변화량 |

### ModifierType

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

## 5. SettlementInput

무역 완료 후 정산 계산에 필요한 입력값이다.

| 필드 | 타입 | 설명 | M1 필수 |
|---|---|---|---|
| `tradeId` | string | 무역 1회 실행 ID | 필수 |
| `soldItems` | SoldItemInput[] | 판매한 상품 목록 | 필수 |
| `tradeMoneyBefore` | int | 정산 전 무역용 재화 | 필수 |
| `foodCost` | int | 식량 구매/소모 비용 | 필수 |
| `mercenaryCost` | int | 용병 고용비 | 필수 |
| `cartRepairCost` | int | 마차 내구도 손실/수리 비용 | 선택 |
| `lostItemValue` | int | 손실 상품 가치 | 선택 |
| `eventProfit` | int | 이벤트 수익 | 선택 |
| `eventLoss` | int | 이벤트 손실 | 선택 |
| `loanRepayment` | int | 대출 자동 상환액 | 선택 |
| `developmentCurrencyReward` | int | 발전용 재화 보상 | 필수 |

### SoldItemInput

| 필드 | 타입 | 설명 |
|---|---|---|
| `tradeItemId` | string | 상품 ID |
| `quantity` | int | 판매 수량 |
| `totalBuyPrice` | int | 해당 수량의 구매 비용 |
| `totalSellPrice` | int | 해당 수량의 판매 수익 |

---

## 6. SettlementBreakdown

정산 결과 출력이다.

| 필드 | 타입 | 설명 |
|---|---|---|
| `tradeId` | string | 무역 1회 실행 ID |
| `totalRevenue` | int | 총 수익 |
| `totalExpense` | int | 총 비용 |
| `grossTradeProfit` | int | 상품 판매 수익 - 상품 구매 비용 |
| `netProfit` | int | 최종 순이익 |
| `tradeMoneyAfter` | int | 정산 후 무역용 재화 |
| `developmentCurrencyReward` | int | 발전용 재화 획득량 |
| `entries` | SettlementEntry[] | 정산 표시 항목 |
| `isBankrupt` | bool | 파산 여부 |
| `minimumRecoveryMoney` | int | 파산 시 복구 필요 최소금 |

### SettlementEntry

| 필드 | 타입 | 설명 |
|---|---|---|
| `entryType` | enum | 정산 항목 종류 |
| `displayNameKey` | string | UI 표시명 키 |
| `amount` | int | 금액 또는 수량 |
| `isPositive` | bool | 수익 항목 여부 |
| `sourceId` | string | 상품/이벤트/시스템 ID |

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

### 정산 항목 확정표

`SettlementEntry.amount`는 항상 0 이상의 값으로 기록한다.
수익/비용 방향은 `isPositive`로 구분한다.

| entryType | isPositive | M1 사용 | 값 제공 | 설명 |
|---|---:|---:|---|---|
| `ItemPurchaseCost` | false | 필수 | Progression | 상품 구매 비용 합계 |
| `ItemSaleRevenue` | true | 필수 | Progression | 상품 판매 수익 합계 |
| `FoodCost` | false | 필수 | Progression | 식량 구매/소모 비용 |
| `MercenaryCost` | false | 필수 | Progression | 용병 고용비 |
| `CartRepairCost` | false | 선택 | Core + Progression | 마차 내구도 손실/수리 비용 |
| `LostItemValue` | false | 선택 | Core + Progression | 약탈/실패로 잃은 상품 가치 |
| `EventProfit` | true | 선택 | Core + Progression | 긍정 이벤트 수익 |
| `EventLoss` | false | 선택 | Core + Progression | 부정 이벤트 손실 |
| `LoanRepayment` | false | 선택 | Progression | 정산 시 자동 대출 상환액 |
| `DevelopmentCurrencyReward` | true | 필수 | Progression | 발전용 재화 획득량 |
| `GrowthPurchaseCost` | false | 선택 | Progression | 정산 이후 성장 구매 비용 |
| `Debug` | true/false | 선택 | 담당 파트 | 디버그/검증용 임시 항목 |

### 정산 항목 생성 원칙

- M1 필수 항목은 값이 0이어도 `entries`에 포함할 수 있다.
- 선택 항목은 값이 0이면 생략해도 된다.
- UI는 `entryType`, `displayNameKey`, `amount`, `isPositive`만으로 정산 목록을 그릴 수 있어야 한다.
- `sourceId`는 가능하면 원인 ID를 넣는다. 원인이 시스템이면 `system`을 사용한다.
- 같은 `entryType`이 여러 원인에서 발생하면 여러 줄로 나눠도 되고, 합산해 한 줄로 표시해도 된다.
- 최종 합계 계산은 Progression이 수행한다. UI는 `entries`를 다시 합산해 게임 상태를 바꾸지 않는다.

### sourceId 권장값

| entryType | sourceId 예시 |
|---|---|
| `ItemPurchaseCost` | `tradeItemId` |
| `ItemSaleRevenue` | `tradeItemId` |
| `FoodCost` | `food` |
| `MercenaryCost` | `mercenaryId` 또는 `mercenary` |
| `CartRepairCost` | `cartId` |
| `LostItemValue` | `eventId` 또는 `tradeItemId` |
| `EventProfit` | `eventId` |
| `EventLoss` | `eventId` |
| `LoanRepayment` | `loan` |
| `DevelopmentCurrencyReward` | `developmentCurrency` |
| `GrowthPurchaseCost` | `growthId` |

---

## 7. 재화 구분

1차 빌드에서는 최소 2종의 재화를 구분한다.

| 재화 | 권장 필드명 | 사용처 |
|---|---|---|
| 무역용 재화 | `tradeMoney` | 상품 구매, 식량, 용병, 마차 비용, 대출 상환 |
| 발전용 재화 | `developmentCurrency` | 성장 구매, 기부, 투자, 지역/상품 해금 |

### 원칙

- 무역용 재화와 발전용 재화의 사용처는 겹치지 않게 유지한다.
- 정산의 `netProfit`은 무역용 재화에 반영한다.
- `developmentCurrencyReward`는 별도 보상으로 반영한다.
- M1에서는 발전용 재화 사용처가 성장 1개뿐이어도 된다.
- UI는 두 재화의 수입/지출을 서로 다른 항목으로 표시한다.

---

## 8. 마차 비용 정책

`cartRepairCost`는 마차 내구도 손실을 비용으로 환산한 값이다.

### M1

- 마차 내구도 계산이 아직 없으면 `cartRepairCost = 0`으로 둔다.
- 정산 구조에는 항목을 유지한다.

### M2 이후

- 마차 내구도 손실 또는 수리 필요량을 비용으로 환산한다.
- UI는 `CartRepairCost` 항목으로 분리 표시한다.
- 실제 비용 차감 여부는 Progression 정책을 따른다.

---

## 9. 메타 경제 상태 최소 계약

M0에서 기부, 투자, 대출 상태를 저장/표시할 수 있도록 최소 상태를 정의한다.
M1에서는 값이 비어 있거나 기본값이어도 된다.

| 필드 | 타입 | 설명 |
|---|---|---|
| `donationLevel` | int | 현재 기부 단계 |
| `donationTotalAmount` | int | 누적 기부량 |
| `investmentProgress` | int | 투자 진행도 또는 납입량 |
| `unlockedRouteIds` | string[] | 투자 등으로 해금된 무역로 ID |
| `unlockedTradeItemIds` | string[] | 해금된 상품 ID |
| `loanPrincipal` | int | 대출 원금 |
| `loanRemaining` | int | 남은 대출 상환액 |
| `loanRepaymentRate` | float | 정산 수익에서 자동 상환할 비율 |
| `isBankruptcyProtected` | bool | 파산 구제 상태 여부 |

### M3 최소 동작

- 기부는 단계 1개와 효과 1개만 있어도 된다.
- 투자는 1회 결제 후 무역로 1개를 해금해도 된다.
- 대출은 파산 시 단일 구제금으로 시작해도 된다.
- 대출 반복으로 무한 자금이 생기지 않아야 한다.

---

## 10. M1 기본 정산 공식

M1에서는 아래 공식만 사용한다.

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

M1에서는 `cartRepairCost`, `lostItemValue`, `eventProfit`, `eventLoss`, `loanRepayment`가 0이어도 된다.

---

## 11. UI & Data에 전달할 항목

UI는 아래 값을 Progression 결과에서 받아 표시한다.

- 기본 구매가
- 기본 판매가
- 도시 보정
- 계절 보정
- 재난 보정
- 이벤트 보정
- 거리/무역로 보정
- 과공급 보정
- 할인/할증
- 식량 비용
- 용병 비용
- 손실 금액
- 총 판매 수익
- 총 구매 비용
- 최종 순이익
- 정산 전후 무역용 재화
- 발전용 재화

UI는 위 값을 다시 계산하지 않는다.

---

## 12. Core Gameplay에 전달할 항목

Core는 M1에서 아래 값만 받아도 된다.

| 항목 | 설명 |
|---|---|
| `maxLoadBonus` | 성장으로 증가한 최대 적재량 |
| `speedMultiplier` | 이동 속도 배율. M1에서는 기본값을 유지하고 실제 증가는 추후 기획 협의 대상 |
| `foodEfficiencyMultiplier` | 식량 효율 |
| `combatPowerMultiplier` | 용병/상단 전투 보정 |
| `lossLimitRate` | 실패 시 손실 상한 |

M1 최소값:

```text
maxLoadBonus = 0 또는 성장 1단계 효과
speedMultiplier = 1.0
foodEfficiencyMultiplier = 1.0
combatPowerMultiplier = 1.0
lossLimitRate = 0.5
```

### CoreRuntimeStatModifier

Progression은 성장, 기부, 대출 구제, 경제 상태에서 파생된 런타임 보정값을 아래 구조로 Core에 전달한다.
Core는 이 값을 다시 계산하지 않고 무역 실행 계산에 적용한다.

| 필드 | 타입 | M1 기본값 | 설명 |
|---|---|---:|---|
| `maxLoadBonus` | int | 0 | 최대 적재량에 더하는 고정 보너스 |
| `maxLoadMultiplier` | float | 1.0 | 최대 적재량 배율 |
| `speedMultiplier` | float | 1.0 | 이동 속도 배율. 값이 클수록 빠르다 |
| `foodEfficiencyMultiplier` | float | 1.0 | 식량 효율 배율. 값이 클수록 식량 소모가 줄어든다 |
| `combatPowerBonus` | int | 0 | 용병/상단 전투력에 더하는 고정 보너스 |
| `combatPowerMultiplier` | float | 1.0 | 전투력 배율 |
| `lossLimitRate` | float | 0.5 | 실패 시 최대 손실 비율 |
| `riskMultiplier` | float | 1.0 | 이벤트/약탈 위험도 배율. 값이 낮을수록 안전하다 |
| `minRecoveryTradeMoney` | int | 0 | 파산/대출 후 최소 재출발 보장 금액 |

### 적용 원칙

- Core는 `CoreRuntimeStatModifier`를 입력값으로 받아 무역 실행에만 사용한다.
- Progression은 성장/기부/대출 등에서 파생된 보정값을 계산해 넘긴다.
- Core는 성장 레벨, 기부 단계, 대출 상태를 직접 해석하지 않는다.
- UI는 이 구조를 직접 수정하지 않는다.
- 저장은 원본 상태와 최종 보정값 중 무엇을 저장할지 Framework와 협의한다. M1에서는 원본 성장 상태만 저장해도 된다.

### M1 필수 적용값

M1에서 반드시 연결해야 하는 값은 아래 3개다.

| 필드 | 이유 |
|---|---|
| `maxLoadBonus` 또는 `maxLoadMultiplier` | 성장 후 적재량 차이를 체감하기 위해 필요 |
| `speedMultiplier` | 이동 시간 보정을 위한 필드. 실제 성장/상단 증가값은 추후 기획 협의 대상 |
| `lossLimitRate` | 실패 후 회복 불가능 상태를 막기 위한 최소 안전장치 |

`foodEfficiencyMultiplier`, `combatPowerBonus`, `combatPowerMultiplier`, `riskMultiplier`는 M2부터 실제 의미를 가져도 된다.

### 값 범위

| 필드 | 허용 범위 |
|---|---|
| `maxLoadBonus` | 0 이상 |
| `maxLoadMultiplier` | 1.0 이상 |
| `speedMultiplier` | 0.1 이상 |
| `foodEfficiencyMultiplier` | 0.1 이상 |
| `combatPowerBonus` | 0 이상 |
| `combatPowerMultiplier` | 0.1 이상 |
| `lossLimitRate` | 0.0 이상 1.0 이하 |
| `riskMultiplier` | 0.0 이상 |
| `minRecoveryTradeMoney` | 0 이상 |

### 성장 효과 M1 예시

M1에서는 성장 1종만 실제 적용해도 된다.
아래 능력치 증가 수치는 확정 기획값이 아니라 M1 연결 검증용 임시값이다.
최종 성장/레벨업 능력치 증가는 추후 기획 단계 협의에 따라 변경한다.

```text
PlayerGrowthLevel 1:
  maxLoadBonus = 10
  speedMultiplier = 1.0
  lossLimitRate = 0.5

CaravanGrowthLevel 1:
  maxLoadBonus = 20
  speedMultiplier = 1.0
  lossLimitRate = 0.5
```

위 수치는 테스트용 예시이며, 최종 밸런스 값은 Content/Progression 조정 대상이다.

---

## 13. 오늘 고정할 결정

Progression 담당자는 오늘 아래 내용을 팀에 공유하고 확정한다.

- 가격 보정 순서는 이 문서의 순서를 사용한다.
- 정산 항목은 `SettlementEntry`로 분리한다.
- UI는 가격/정산 계산을 하지 않는다.
- Core는 Progression의 런타임 스탯 결과만 적용한다.
- M1에서는 계절/재난/이벤트/과공급 계산을 비워둘 수 있지만 출력 구조는 유지한다.
- 무역용 재화와 발전용 재화는 사용처를 분리한다.
- M1에서는 마차 수리비가 0이어도 정산 항목은 유지한다.
- 기부/투자/대출 상태는 최소 저장 필드를 먼저 합의한다.
- Core에 전달하는 런타임 스탯은 `CoreRuntimeStatModifier` 구조를 사용한다.
- Core는 성장/기부/대출 상태를 직접 해석하지 않고 Progression이 계산한 보정값을 적용한다.
- 가격과 비용은 음수가 될 수 없다.
- 최종 단위 가격은 최소 1이다.

---

## 14. M1 완료 기준

- 상품 1개 이상에 대해 구매가/판매가/순이익을 계산할 수 있다.
- 식량 비용과 용병 비용이 정산에 분리 표시된다.
- 성장 구매 비용을 차감할 수 있다.
- 성장 1종이 Core 런타임 스탯에 반영된다.
- `SettlementBreakdown.entries`만으로 정산 화면을 구성할 수 있다.
