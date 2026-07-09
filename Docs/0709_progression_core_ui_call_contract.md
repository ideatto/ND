# Progression Core/UI Call Contract

작성일: 2026-07-07
담당: Progression
대상: Core Gameplay, UI & Data, Framework & Integration

---

## 1. 목적

이 문서는 M1 최소 경제 루프를 Core와 UI가 어떤 순서와 책임으로 호출해야 하는지 고정한다.

Progression은 가격 계산, 정산, 재화 반영, 성장 구매, 런타임 스탯 계산을 하나의 흐름으로 제공한다.
Core와 UI는 내부 계산식을 다시 구현하지 않고 Progression 결과값을 읽어서 사용한다.

---

## 2. M1 호출 단위

M1에서 Core/UI가 호출해야 하는 Progression 진입점은 아래 1개로 고정한다.

```csharp
EconomyM1LoopCalculator.Execute(EconomyM1LoopInput input)
```

이 호출은 아래 순서를 내부에서 처리한다.

```text
1. 가격 계산
2. 정산 항목 생성
3. 무역 재화 / 발전 재화 반영
4. 성장 구매 처리
5. Core 런타임 스탯 계산
6. 최종 재화 상태 반환
```

Core와 UI는 위 순서를 개별로 재조합하지 않는다.

---

## 3. 호출 책임

### Core Gameplay

Core는 무역 1회가 완료되는 시점에 Progression 루프를 호출한다.

Core가 넘겨야 하는 값:

| 값 | 설명 |
|---|---|
| 현재 무역 재화 | 정산 전 `tradeMoney` |
| 현재 발전 재화 | 정산 전 `developmentCurrency` |
| 상품 ID | 거래한 상품 |
| 출발 도시 ID | 구매 도시 |
| 도착 도시 ID | 판매 도시 |
| 무역로 ID | 실행한 route |
| 수량 | 거래 수량 |
| 기본 구매가 | Content/Data에서 받은 기본값 |
| 기본 판매가 | Content/Data에서 받은 기본값 |
| 식량 비용 | 이번 무역의 식량 비용 |
| 용병 비용 | 이번 무역의 용병 비용 |
| 성장 구매 요청 | 성장 구매를 시도할 경우 true |

Core가 받아서 적용해야 하는 값:

| 값 | 사용처 |
|---|---|
| `FinalCurrencyState.TradeMoney` | 플레이어 무역 재화 상태 갱신 |
| `FinalCurrencyState.DevelopmentCurrency` | 플레이어 발전 재화 상태 갱신 |
| `RuntimeStats` | 다음 무역 실행 시 적재량, 속도, 손실 제한 등에 적용 |
| `Success` / `ErrorCode` | 실패 시 무역 완료 처리 중단 또는 에러 표시 |

Core는 `SettlementBreakdown.entries`를 보고 재화를 다시 계산하지 않는다.

---

## 4. UI & Data 책임

UI는 Progression 결과를 표시만 한다.

UI가 표시할 값:

| 값 | 표시 예시 |
|---|---|
| `PriceResult.UnitBuyPrice` | 단위 구매가 |
| `PriceResult.UnitSellPrice` | 단위 판매가 |
| `PriceResult.TotalBuyPrice` | 총 구매 비용 |
| `PriceResult.TotalSellPrice` | 총 판매 수익 |
| `PriceResult.ExpectedGrossProfit` | 예상 상품 차익 |
| `Settlement.NetProfit` | 최종 순이익 |
| `Settlement.TradeMoneyAfter` | 정산 후 무역 재화 |
| `Settlement.DevelopmentCurrencyReward` | 획득 발전 재화 |
| `Settlement.Entries` | 정산 상세 항목 |
| `GrowthPurchase` | 성장 구매 성공/실패 및 비용 |
| `RuntimeStats` | 성장 효과 요약 |

UI는 아래 계산을 직접 하지 않는다.

- 최종 구매가 / 판매가 재계산
- 정산 항목 합산으로 최종 재화 재계산
- 성장 구매 비용 차감
- 런타임 스탯 보정값 계산

---

## 5. EconomyM1LoopInput 계약

| 필드 | 제공자 | M1 필수 | 설명 |
|---|---|---:|---|
| `PriceInput` | Core + Data | 필수 | 가격 계산 입력 |
| `CurrencyState` | Core / Save | 필수 | 정산 전 재화 상태 |
| `TradeId` | Core | 필수 | 무역 1회 실행 ID |
| `FoodCost` | Core / Progression | 필수 | 식량 비용 |
| `MercenaryCost` | Core / Progression | 필수 | 용병 비용 |
| `CartRepairCost` | Core + Progression | 선택 | 마차 수리 비용. M1 기본값 0 |
| `LostItemValue` | Core + Progression | 선택 | 사고/실패로 잃은 상품 가치. M1 기본값 0 |
| `EventProfit` | Core + Progression | 선택 | 이벤트 수익. M1 기본값 0 |
| `EventLoss` | Core + Progression | 선택 | 이벤트 손실. M1 기본값 0 |
| `LoanRepayment` | Progression | 선택 | 대출 상환액. M1 기본값 0 |
| `DevelopmentCurrencyReward` | Progression | 필수 | 정산 보상 발전 재화 |
| `PurchaseGrowth` | UI/Core | 선택 | 성장 구매 시도 여부 |
| `GrowthPurchaseInput` | UI/Core | 선택 | 성장 구매 입력 |
| `PlayerGrowthLevel` | Save / Progression | 필수 | 현재 플레이어 성장 레벨 |
| `CaravanGrowthLevel` | Save / Progression | 필수 | 현재 상단 성장 레벨 |

### 입력 원칙

- `PriceInput.Quantity`는 1 이상이어야 한다.
- `PriceInput.BaseBuyPrice`, `PriceInput.BaseSellPrice`는 1 이상이어야 한다.
- `CurrencyState`는 호출 전 상태를 넣는다.
- `PurchaseGrowth = true`일 때만 성장 구매를 처리한다.
- 성장 구매 비용은 `DevelopmentCurrency`에서만 차감한다.
- 무역 재화와 발전 재화의 사용처는 서로 섞지 않는다.

---

## 6. EconomyM1LoopResult 계약

| 필드 | 사용자 | 설명 |
|---|---|---|
| `Success` | Core/UI | 전체 루프 성공 여부 |
| `ErrorCode` | Core/UI | 실패 사유 |
| `PriceResult` | UI/Core | 가격 계산 결과 |
| `Settlement` | UI/Core | 정산 결과 |
| `SettlementCurrencyApply` | Core | 정산 후 재화 반영 결과 |
| `GrowthPurchase` | UI/Core | 성장 구매 결과 |
| `GrowthCurrencyApply` | Core | 성장 구매 후 재화 반영 결과 |
| `RuntimeStats` | Core/UI | Core에 넘길 런타임 스탯 |
| `FinalCurrencyState` | Core/Save | 최종 저장해야 할 재화 상태 |

### 결과 사용 원칙

- 저장에는 `FinalCurrencyState`를 우선 사용한다.
- UI 표시는 `PriceResult`, `Settlement`, `GrowthPurchase`, `RuntimeStats`를 사용한다.
- Core 런타임 적용은 `RuntimeStats`만 사용한다.
- 실패 시 `FinalCurrencyState`가 있더라도 저장 반영은 하지 않는다.

---

## 7. 실패 처리

`Success = false`이면 Core는 해당 무역 완료 흐름을 성공 처리하지 않는다.

M1에서 예상하는 주요 실패:

| ErrorCode 접두어 | 의미 |
|---|---|
| `InputNull` | 입력이 없음 |
| `PriceCalculationFailed` | 가격 계산 입력이 잘못됨 |
| `SettlementCurrencyApplyFailed` | 정산 재화 반영 실패 |
| `GrowthPurchaseFailed` | 성장 구매 실패 |
| `GrowthCurrencyApplyFailed` | 성장 구매 재화 반영 실패 |

UI는 실패 시 `ErrorCode`를 개발용 로그 또는 임시 팝업에 표시한다.
최종 현지화 문구는 UI & Data에서 별도 매핑한다.

---

## 8. M1 기준 예시

입력:

| 값 | 예시 |
|---|---:|
| 무역 재화 | 1000 |
| 발전 재화 | 0 |
| 상품 | apple |
| 수량 | 5 |
| 단위 구매가 | 100 |
| 단위 판매가 | 140 |
| 식량 비용 | 50 |
| 발전 재화 보상 | 1 |
| 성장 구매 비용 | 1 |

예상 결과:

| 값 | 결과 |
|---|---:|
| 총 구매 비용 | 500 |
| 총 판매 수익 | 700 |
| 상품 차익 | 200 |
| 최종 순이익 | 150 |
| 정산 후 무역 재화 | 1150 |
| 정산 후 발전 재화 | 1 |
| 성장 구매 후 발전 재화 | 0 |
| 성장 레벨 | 1 |
| `RuntimeStats.MaxLoadBonus` | 10 |

---

## 9. 팀 간 경계

### Progression 소유

- 가격 계산 공식
- 가격 보정 순서
- 정산 항목 구조
- 무역 재화 / 발전 재화 반영 규칙
- 성장 구매 비용 차감
- Core 런타임 스탯 산출

### Core Gameplay 소유

- 무역 시작/완료 타이밍
- 실제 이동, 위험, 실패 판정
- Progression 결과를 게임 상태에 반영하는 시점
- `RuntimeStats`를 실제 플레이 로직에 적용하는 방식

### UI & Data 소유

- 가격/정산/성장 결과 표시
- 버튼 활성화/비활성화
- 에러 문구 현지화
- 데이터 테이블에서 기본 가격과 표시명을 제공

### Framework & Integration 소유

- 저장/로드 위치
- 데이터 로딩 경로
- 이벤트/로그 연결
- 최종 서비스 레이어 배치

---

## 10. M1 완료 기준

아래 조건을 만족하면 Progression의 M1 최소 경제 루프 계약은 완료로 본다.

- Core가 `EconomyM1LoopCalculator.Execute()`를 1회 호출해 무역 완료 처리를 할 수 있다.
- UI가 `EconomyM1LoopResult`만으로 가격/정산/성장 결과를 표시할 수 있다.
- 저장 시스템이 `FinalCurrencyState`를 저장할 수 있다.
- Core가 `RuntimeStats`를 다음 무역 실행에 적용할 수 있다.
- UI/Core가 Progression 내부 계산식을 복제하지 않는다.
