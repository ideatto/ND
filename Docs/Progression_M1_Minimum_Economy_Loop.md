# Progression M1 Minimum Economy Loop

작성일: 2026-07-07
담당: Progression

---

## 1. 목적

이 문서는 M1까지 Progression이 반드시 동작시켜야 하는 최소 경제 루프를 정의한다.

목표는 최종 밸런스 확정이 아니라, 아래 흐름을 3회 이상 반복 가능하게 만드는 것이다.

```text
상품 구매
-> 식량/용병 비용 반영
-> 무역 진행
-> 상품 판매
-> 정산
-> 성장 구매
-> 다음 무역에서 성장 효과 반영
```

참조 계약: `Docs/Progression_Price_Settlement_Contract.md`

---

## 2. M1 필수 계산

M1에서는 아래 계산만 반드시 동작하면 된다.

| 계산 | 필수 여부 | 설명 |
|---|---:|---|
| 기본 구매가 | 필수 | 상품 1개 이상의 구매 가격 |
| 기본 판매가 | 필수 | 목적지에서의 판매 가격 |
| 구매 비용 합계 | 필수 | `UnitBuyPrice * Quantity` |
| 판매 수익 합계 | 필수 | `UnitSellPrice * Quantity` |
| 식량 비용 | 필수 | 식량 수량 또는 고정 테스트값 |
| 용병 비용 | 필수 | 용병 고용비. 용병 미사용 시 0 가능 |
| 순이익 | 필수 | 수익 - 비용 |
| 무역 재화 반영 | 필수 | `TradeMoneyAfter = TradeMoneyBefore + NetProfit` |
| 발전 재화 보상 | 필수 | M1에서는 고정값 가능 |
| 성장 구매 비용 차감 | 필수 | 성장 구매 시 발전 재화 차감 |
| Core 런타임 스탯 반환 | 필수 | 성장 효과를 `CoreRuntimeStatModifier`로 반환 |

---

## 3. M1에서 비워둘 수 있는 계산

아래 항목은 구조만 유지하고 실제 값은 0 또는 기본값이어도 된다.

- 계절 보정
- 재난 보정
- 이벤트 보정
- 과공급 보정
- 마차 수리비
- 상품 손실 금액
- 대출 상환
- 기부 효과
- 투자 해금

정산 구조에는 해당 항목을 넣을 수 있는 자리가 있어야 한다.

---

## 4. 최소 데이터 예시

M1 테스트용 값은 아래처럼 단순해도 된다.

| 항목 | 예시 |
|---|---:|
| 시작 무역 재화 | 1000 |
| 시작 발전 재화 | 0 |
| 상품 ID | apple |
| 기본 구매가 | 100 |
| 기본 판매가 | 140 |
| 구매 수량 | 5 |
| 식량 비용 | 50 |
| 용병 비용 | 0 |
| 발전 재화 보상 | 1 |
| 성장 비용 | 1 발전 재화 |
| 성장 효과 | `MaxLoadBonus +10` |

예상 결과:

```text
상품 구매 비용 = 100 * 5 = 500
상품 판매 수익 = 140 * 5 = 700
총 수익 = 700
총 비용 = 500 + 50 + 0 = 550
순이익 = 150
정산 후 무역 재화 = 1000 + 150 = 1150
발전 재화 획득 = 1
성장 구매 후 발전 재화 = 0
성장 구매 후 MaxLoadBonus = 10
```

---

## 5. M1 정산 Entries 예시

```text
ItemPurchaseCost / false / 500 / apple
ItemSaleRevenue / true / 700 / apple
FoodCost / false / 50 / food
MercenaryCost / false / 0 / mercenary
DevelopmentCurrencyReward / true / 1 / developmentCurrency
```

M1 필수 항목은 값이 0이어도 포함할 수 있다.
UI는 `Entries`만으로 기본 정산 화면을 그릴 수 있어야 한다.

---

## 6. 성장 1종 최소 정의

M1에서는 성장 1종만 실제 적용해도 된다.

| 필드 | 값 |
|---|---:|
| `GrowthId` | `growth_load_01` |
| `DisplayNameKey` | `growth.load.01` |
| `CostDevelopmentCurrency` | 1 |
| `MaxLevel` | 1 |
| `MaxLoadBonus` | 10 |
| `SpeedMultiplier` | 1.0 |
| `LossLimitRate` | 0.5 |

성장 구매 후 Progression은 Core에 아래 값을 반환한다.

```text
CoreRuntimeStatModifier:
  MaxLoadBonus = 10
  MaxLoadMultiplier = 1.0
  SpeedMultiplier = 1.0
  FoodEfficiencyMultiplier = 1.0
  CombatPowerBonus = 0
  CombatPowerMultiplier = 1.0
  LossLimitRate = 0.5
  RiskMultiplier = 1.0
  MinRecoveryTradeMoney = 0
```

---

## 7. M1 루프 호출 계약

Core/UI가 사용할 최소 진입점은 아래 형태로 고정한다.

```csharp
EconomyM1LoopCalculator.Execute(EconomyM1LoopInput input)
```

이 호출은 아래 순서를 한 번에 처리한다.

```text
가격 계산
-> 정산 계산
-> 무역 재화 / 발전 재화 반영
-> 성장 구매 처리
-> 런타임 스탯 계산
-> 최종 재화 상태 반환
```

Core와 UI는 Progression 내부 계산 순서를 재구현하지 않는다.

---

## 8. 완료 기준

- [ ] 상품 1종으로 구매/판매 정산이 가능하다.
- [ ] 식량 비용이 정산 항목으로 분리된다.
- [ ] 용병 비용은 0이어도 정산 항목에 들어갈 수 있다.
- [ ] 정산 후 무역 재화가 갱신된다.
- [ ] 발전 재화가 최소 1 지급된다.
- [ ] 성장 1종을 구매할 수 있다.
- [ ] 성장 구매 후 Core 런타임 스탯이 달라진다.
- [ ] 같은 흐름을 3회 반복해도 재화 중복 보상이나 음수 오류가 생기지 않는다.

---

## 9. 팀 공유 문장

Progression은 M1에서 최종 밸런스가 아니라 최소 경제 루프를 보장한다.
계절, 재난, 이벤트, 과공급은 M2부터 실제 값을 넣고, M1에서는 구매/판매/비용/정산/성장 반영만 확실히 연결한다.
