# Selling 상태 지도 표시 및 재실행 복구 보완 요청

## 1. 배경

현재 도착 성공 무역은 다음 상태 흐름을 사용한다.

`Traveling → Selling → Settling / SettlementPending → Completed`

- 성공 도착 직후에는 정산 UI를 자동으로 열지 않는다.
- Caravan Overview의 물건 적재 버튼을 아이템 판매 버튼으로 바꾼다.
- 판매를 확정한 뒤에만 Core는 `Settling`, Framework는 `SettlementPending`으로 전환한다.
- 실패 도착은 판매 단계 없이 곧바로 정산 단계로 진입한다.

`Selling`은 더 이상 미사용 확장 지점이 아니라, 성공 도착 후 판매 확정 전까지의 권위 상태다.

## 2. 현재 문제

### 2.1 지도 진행 조회 누락

`TradeProgressCoordinator.TryCreateMapProgressSnapshot(...)`은 현재 `Traveling`과 `SettlementPending`만 처리한다.

따라서 성공 도착 후 `Selling`으로 바뀐 Caravan은 지도 조회 대상에서 빠지거나 `MissingRoute`, `InvalidIdentity` 등의 경고와 폴백을 발생시킬 수 있다.

### 2.2 재실행 복구 누락

`TradeProgressCoordinator.RestorePendingSettlements(...)`와 legacy `RestorePendingSettlement(...)`은 `SettlementPending / Settling` 조합만 정산 복구 대상으로 인정한다.

따라서 판매 확정 전에 게임을 종료하면 저장된 `Selling` Caravan이 존재해도 복구 과정에서 잘못된 pending 검증 경고가 발생할 수 있다. 재실행 후에도 해당 Caravan은 판매 가능한 상태로 유지되어야 하며, 아직 판매 확정 전이므로 정산 결과를 표시하거나 Claim 가능 상태로 복구하면 안 된다.

## 3. 요구 동작

### 3.1 상태별 지도 동작

| Framework 상태 | 지도 의미 | 표시 위치 |
|---|---|---|
| `Traveling` | 이동 중 | 경로 진행률 위치 |
| `Selling` | 성공 도착, 판매 대기 | 목적지 마을 위치 |
| `SettlementPending` 성공 | 판매 완료, 정산 대기 | 목적지 마을 위치 |
| `SettlementPending` 실패 | 실패 정산 대기 | 출발지 마을 위치 |

`Selling`에서는 이동 경로가 끝난 상태이므로 진행률은 `1`로 취급한다. 목적지 식별은 해당 Caravan의 `activeTradeId`, progress의 도착지 정보, 저장된 pending result 가운데 현재 계약상 권위 데이터로 결정한다. 식별 데이터가 실제로 손상된 경우에만 경고하고, 정상적인 `Selling` 자체를 오류로 기록하지 않는다.

성공 도착 저장 시 `JourneyState.Selling`, `TradeProgressState.Selling`, `caravan.currentTownId = destinationTownId`를 같은 저장 단위로 반영한다. S9 Claim은 이 위치 변경의 발생 시점이 아니며, 기존 저장 호환을 위해 목적지를 재확인할 수만 있다. 실패 도착은 출발지 `currentTownId`를 유지한다.

Route의 `baseRequiredFoodQuantity`는 준비 화면에서 보여 주는 기준 예상량이다. 실제 정산의 `foodConsumed`는 Caravan 구성의 초당 식량 소비와 실제 경과 시간으로 계산하며, 기준 예상량을 최소 실제 소비량으로 강제하지 않는다. 따라서 식량 소비 동물이 없는 `Wagon_S` 구성은 Route SO를 수정하지 않아도 실제 식량을 소비하지 않는다.

### 3.2 상태별 재실행 복구 동작

저장 데이터가 다음 조건을 만족하면 판매 대기 상태로 복구한다.

- Caravan state: `JourneyState.Selling`
- Trade progress state: `TradeProgressState.Selling`
- Caravan ID와 active trade ID가 서로 일치
- 성공 도착 결과가 존재하고 실패 등급이 아님

복구 시에는 다음 원칙을 지킨다.

- runtime Caravan 상태를 `Selling`으로 재구성한다.
- Overview의 해당 Caravan 버튼이 아이템 판매 동작을 유지한다.
- 판매 UI는 자동으로 열지 않는다.
- `TradeSettlementReady` 또는 정산 표시 요청을 발행하지 않는다.
- Economy pending 및 Claim 가능 상태를 만들지 않는다.
- 사용자가 판매를 확정하면 기존의 원자적 `Selling → Settling / SettlementPending` 전환을 수행한다.
- 저장된 식별자가 불일치하거나 성공 결과가 없으면 판매를 임의로 완료하지 않고 진단 가능한 오류로 남긴다.

## 4. 구현 대상

### TradeProgressCoordinator.cs

1. `TryCreateMapProgressSnapshot(...)`
   - `TradeProgressState.Selling`을 유효한 지도 상태에 포함한다.
   - `Selling`의 `Progress01`을 `1`로 고정한다.
   - 성공 도착 목적지 마을을 표시할 수 있도록 스냅샷 식별 정보를 구성한다.

2. `RestorePendingSettlements(...)`
   - `Selling` entry를 정산 pending 복구 실패로 처리하지 않는다.
   - `Selling`은 별도 판매 대기 복구 분기로 검증한다.
   - 정산 cache, Economy pending, 정산 준비 이벤트를 만들지 않는다.

3. `RestorePendingSettlement(...)`
   - legacy 경로가 계속 사용된다면 동일한 `Selling` 분기 규칙을 적용한다.
   - 더 이상 호출되지 않는 경로라면 호출 위치와 호환성을 확인한 뒤 별도 정리 작업으로 분리한다.

### 지도 Presenter 및 Overview Provider

- `Selling` 스냅샷을 이동 중 Route marker로 다시 계산하지 않는다.
- 목적지 Town marker로 표시한다.
- 재실행 후 Overview가 저장된 Caravan ID와 trade ID로 동일한 판매 대상을 연다.

## 5. 비범위

- 판매 UI 자동 열기
- `Selling` 상태에서 Claim 허용
- 판매 확정 전 Economy pending 생성
- 실패 도착을 `Selling`으로 전환
- 저장 데이터가 손상된 경우 임의 복구 또는 자동 완료

## 6. 검증 항목

1. 성공 도착 후 `Selling` Caravan이 목적지 마을에 표시된다.
2. `Selling` 상태에서 정상 데이터임에도 `MissingRoute`, `InvalidIdentity`, `PendingValidation failed` 경고가 발생하지 않는다.
3. `Selling` 상태에서 종료 후 재실행해도 동일 Caravan의 Overview 버튼이 아이템 판매로 유지된다.
4. 재실행만으로 판매 UI 또는 정산 UI가 자동으로 열리지 않는다.
5. 재실행한 `Selling` Caravan의 판매 확정 후 `Settling / SettlementPending`으로 정확히 한 번 전환된다.
6. 판매 확정 전에는 Claim이 거부된다.
7. 실패 도착은 기존처럼 판매 단계를 건너뛰고 정산 대기로 복구된다.
8. 두 개 이상의 Caravan이 각각 `Traveling`, `Selling`, `SettlementPending`일 때 지도와 Overview의 Caravan ID 및 trade ID가 섞이지 않는다.

## 7. 완료 기준

- 위 검증 항목을 Editor test 또는 Play Mode 로그로 확인한다.
- 저장 후 재실행 시 상태와 식별자가 유지된다.
- 정상 `Selling` 상태에 대한 지도·복구 경고가 사라진다.
- 기존 `Traveling`, 성공/실패 `SettlementPending`, Claim 흐름에 회귀가 없다.
# 2026-08-10 무소모 운송 수단 식량 판정

`Wagon_S`처럼 견인 동물이 없어 `CaravanCalculator.GetConsumptionPerSec(caravan) == 0`인 구성은 식량을 Cargo에 싣지 않아도 `FoodDepleted`가 아니다. `JourneyRunner.CheckFoodDepletion`은 남은 식량이 0인지 보기 전에 실제 소비율을 확인하며, 소비율이 0이면 고갈 플래그와 고갈 진행도를 유지하지 않는다.

이 규칙은 예상 식량 UI 보정이 아니라 실제 Journey 실패 판정 계약이다. `baseRequiredFoodQuantity`나 Wagon SO를 변경해 우회하지 않는다. 회귀 검증은 `JourneyFoodDepletionTests.SetProgress_ZeroConsumptionAndZeroFood_DoesNotFail`로 수행한다.

## 2026-08-11 화물 손실 후 cargo 정규화

산적 등 Journey 이벤트가 화물의 마지막 수량을 차감해 `quantity == 0`이 되면 해당 행을 `caravan.cargo`에서 즉시 제거한다. UI에서만 숨기고 데이터 행을 남기면 Caravan 구성 변경 시 보이지 않는 화물이 검증을 방해할 수 있기 때문이다.

과거 저장 데이터에 이미 남은 0 이하 cargo 행은 `CaravanSaveDataMapper`의 SaveData↔런타임 변환 양쪽에서 제외한다. 음수 행은 손실 대상으로 계산하지 않는다. 회귀 검증은 `JourneyCargoLossCleanupTests`로 수행한다.
