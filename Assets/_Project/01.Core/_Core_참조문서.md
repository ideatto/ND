# Core Gameplay — 무역 여정 시스템 참조 문서

> 상단(캐러밴)이 도시로 무역을 떠났다 돌아오는 **여정**의 이동·식량·성공/실패를 다루는 부분.
> 도시에서의 **거래(매매)·수익 계산**은 여기 없음 → UI & Data / Progression 영역.

---

## 0. 전체 그림 — 뭐가 뭘 하나

| 파일 | 역할 | 한 줄 요약 |
|---|---|---|
| `CaravanData` | 데이터 | 상단 하나의 전체 구성 + 현재 여정 상태 |
| `TradeDataDraft` | 데이터(초안) | 무역품·마차·동물·용병 부품 데이터 |
| `CaravanValidator` | 검증 | 출발 가능한지 판정 → 불가 사유 목록 |
| `CaravanCalculator` | 계산 | 적재·이동시간·식량 계산 (통합) |
| `CaravanConfig` | 설정값 | 밸런싱 튜닝 상수 모음 |
| `JourneyState` | 상태 | 여정 단계 enum (준비→이동중→정산대기→완료) |
| `JourneyRunner` | 흐름 | 상태 전환 담당 (출발/진행/정산) |
| `JourneyResultData` | 데이터 | 여정 결과 (등급·손실) |
| `JourneyRunTest` | 테스트 | UI 없이 흐름 확인용 임시 스크립트 |

**호출 흐름 한눈에:**
```
[UI 준비화면]
  CaravanCalculator.GetCurrentLoad / GetTravelSeconds / GetRequiredFood  → 예상치 표시
  CaravanValidator.Validate                                             → 출발 버튼 상태
      ↓ 유저가 출발
  JourneyRunner.TryDepart(caravan, 거리)                                → 준비→이동중
      ↓ 매 프레임(또는 Framework)
  JourneyRunner.SetProgress(caravan, 진행도)                            → 이동 + 식량소진 자동체크
  (이벤트 발생 시) JourneyRunner.ApplyCargoLoss / ApplyFoodLoss / MarkFatal
      ↓ 도착 또는 실패
  JourneyRunner.Settle(caravan)                                        → 이동중→정산대기 + 결과생성
  JourneyRunner.ClaimSettlement(caravan)                               → 정산대기→완료 (중복방지)
  JourneyRunner.ResetToPrepare(caravan)                                → 완료→준비 (다음 여정)
```

---

## 1. 데이터

### CaravanData — 상단 하나의 전체 상태
**구성 (준비 단계에서 채움)**
| 필드 | 타입 | 역할 |
|---|---|---|
| `wagon` | WagonData | 마차 (1대) |
| `animals` | List\<AnimalData> | 견인 동물 목록 |
| `mercenaries` | List\<MercenaryData> | 용병 목록 |
| `cargo` | List\<CargoEntry> | 적재한 무역품 목록 |
| `foodAmount` | int | 실은 식량 수량 |
| `foodUnitWeight` | float | 식량 1개당 무게(적재량 계산용) |

**런타임/저장 상태 (여정 중 변함)**
| 필드 | 타입 | 역할 |
|---|---|---|
| `state` | JourneyState | 현재 여정 단계 |
| `currentDistanceKm` | float | 이번 여정 거리 — 출발 시 복사됨 |
| `totalSeconds` | float | 총 소요 시간 — 출발 시 계산돼 저장 |
| `progress01` | float | 진행도 0~1 (바깥이 갱신). 1.0이면 도착 |
| `settlementClaimed` | bool | 정산 수령 여부 (중복 보상 방지) |
| `runCargoLost` | int | 이번 여정 무역품 손실 누적 |
| `runFoodLost` | float | 이번 여정 식량 이벤트 차감 누적(도난 등) |

> `CargoEntry` = `item`(TradeItemData) + `quantity`(int). 무역품 한 종류.

### TradeDataDraft — 부품 데이터 (초안, 정의 소유는 UI & Data)
- **TradeItemData**: `id` / `itemName` / `weight`(적재 계산) / `basePrice`(정산용, 예정)
- **WagonData**: `wagonName` / `overLoad`(속도 100% 한계) / `maxLoad`(출발 불가 상한) / `minAnimals` / `maxAnimals` / `speedModifier`(예정)
- **AnimalData**: `animalName` / `speed`(속도 배수) / `foodPerKm`(⚠️ 팀결정으로 이제 '1초당' 식량 소모. 필드명은 SO 교체 때 `foodPerSec`로 rename 예정)
- **MercenaryData**: `mercName` / `combatPower`(예정) / `contractCount`(예정)

> **적재 두 기준**: `overLoad` 넘으면 속도만 감소(출발 됨), `maxLoad` 넘으면 출발 불가.

---

## 2. 검증 — CaravanValidator

```
DepartureValidationResult result = CaravanValidator.Validate(caravan);
```
**반환** `DepartureValidationResult`:
- `canDepart` (bool) → 출발 버튼 활성/비활성
- `reasons` (List\<DepartureBlockReason>) → UI에 표시할 불가 사유

**검사 항목 (DepartureBlockReason):**
| 값 | 조건 |
|---|---|
| `NoWagon` | 마차 없음 |
| `NotEnoughAnimals` | 동물 < minAnimals |
| `TooManyAnimals` | 동물 > maxAnimals |
| `Overloaded` | 적재 > maxLoad (물리 상한) |
| `NoCargo` | 무역품 없음 |

> UI가 max까지만 붙이게 막아도, Core는 안전장치로 여기서도 검사.

---

## 3. 계산기 — CaravanCalculator

> 전부 `CaravanCalculator.함수명(caravan)` 형태. **값만 계산, 판단은 안 함**(막을지 경고할지는 UI).
> 튜닝 값은 `CaravanConfig`에 분리.

### 적재
| 함수 | 반환 | 역할 |
|---|---|---|
| `GetCurrentLoad(caravan)` | float | 현재 적재 무게 = 무역품 + 식량 |
| `GetMaxLoad(caravan)` | float | 물리 상한 = 마차 maxLoad |

### 이동  `속도 = 기준속도 × 동물수효율 × 적재효율 × 동물종류속도 × 마차보정`
| 함수 | 반환 | 역할 |
|---|---|---|
| `GetSpeedEfficiency(동물수)` | float | 동물 수 효율 (1→1.0 / 2→1.5 / 3→2.0) |
| `GetLoadEfficiency(적재, overLoad)` | float | 적재 초과 시 감속 배수 |
| `GetAnimalTypeSpeed(animals)` | float | **[미정, 지금 1.0]** 동물 종류별 속도 |
| `GetWagonSpeedModifier(wagon)` | float | **[미정, 지금 1.0]** 마차 보정 |
| `GetBaseSpeedKmPerSec()` | float | 1마리 기준 속도(Km/초) |
| `GetTravelSeconds(caravan, 거리)` | float | **소요 시간(초)** ← UI 예상시간, 출발 시 저장 |

### 식량 — 시간 기반 (2026-07-09 팀결정, 초당 소모)  `남은 = 실은 - (초당소모 × 흐른시간) - 이벤트차감`
| 함수 | 반환 | 역할 |
|---|---|---|
| `GetConsumptionPerSec(caravan)` | float | 초당 소모 = 동물들 소모율 합 |
| `GetRequiredFood(caravan)` | float | 필요 총 식량 ← UI 표시(보유량과 비교해 경고) |
| `GetRemainingFood(caravan)` | float | 지금 진행도 남은 식량 (0 이하 = 고갈) |

---

## 4. 여정 흐름 — JourneyState + JourneyRunner

### JourneyState (단계)
```
Prepare(준비) → Traveling(이동중) → Settling(정산대기) → Completed(완료) → (다시 Prepare)
```
> 실패는 별도 상태 아님. 실패해도 Settling→Completed로 가고, 결과 데이터가 "실패"를 말함.

### JourneyRunner (상태 전환 — 상태는 여기서만 바꿈)
| 함수 | 전환 | 역할 |
|---|---|---|
| `TryDepart(caravan, 거리)` | 준비→이동중 | 검증 통과 시 출발. 소요시간 계산·저장. **반환=검증결과** |
| `SetProgress(caravan, 진행도)` | (이동중) | 진행도 갱신. **식량 소진 자동 체크** |
| `IsArrived(caravan)` | — | 도착 여부 (진행도 ≥ 1.0) |
| `ApplyCargoLoss(caravan, 양)` | (이동중) | 무역품 손실 이벤트 → 부분성공 요인 |
| `ApplyFoodLoss(caravan, 양)` | (이동중) | 식량 차감 이벤트(도난 등) |
| `MarkFatal(caravan, 사유)` | (이동중) | 치명 상태 → 실패 확정 |
| `Settle(caravan)` | 이동중→정산대기 | 도착/실패 시 결과 등급 판정. **반환=JourneyResultData** |
| `ClaimSettlement(caravan)` | 정산대기→완료 | 정산 수령. 이미 받았으면 false(중복방지) |
| `ResetToPrepare(caravan)` | 완료→준비 | 다음 여정 준비 |

> 전환은 허용된 상태에서만 일어남. 아니면 아무 일 없이 false/무시.

### JourneyResultData (결과)
| 필드 | 채우는 쪽 | 역할 |
|---|---|---|
| `grade` (JourneyResultGrade) | Core | Success / PartialSuccess / Failed |
| `failureReason` (JourneyFailureReason) | Core | 실패 시 사유(FoodDepleted 등) |
| `cargoLost` (int) | Core | 잃은 무역품 수량 |
| `durabilityLost` (float) | Core | 마차 내구도 손실 [M2] |
| `revenue` / `cost` / `netProfit` (int) | **Progression** | 판매 수익·비용·순이익 (지금 비어있음) |

> 등급 판정: 치명상태 → Failed / 손실만 있음 → PartialSuccess / 무손실 → Success

---

## 5. 설정값 — CaravanConfig (밸런싱은 여기만 수정)

| 상수 | 기본값 | 역할 |
|---|---|---|
| `BaseDistanceKm` / `BaseSeconds` | 100 / 10 | 1마리로 100Km를 10초에 (=10Km/초) |
| `PerExtraAnimal` | 0.5 | 동물 한 마리당 +0.5배 속도 |
| `MaxEfficiency` | 0 | 효율 상한 (0=무제한, 마차 maxAnimals가 자연 상한) |
| `LoadPenalty` | 0.5 | overLoad 100% 초과당 -0.5배 |
| `LoadFactorMin` | 0.2 | 적재 감속 하한 |

---

## 6. 아직 안 된 것 / 다른 담당 영역

- **수익 계산** → Progression. `JourneyResultData`의 revenue/cost/netProfit 자리만 있음.
- **오프라인 정산·이벤트 재생** → Framework 시간 확정 후. (진행도만 있으면 재현되는 구조는 갖춤)
- **거래(매매) 화면** → UI & Data + Progression.
- **미정 계산 항** (CaravanCalculator): 동물 종류별 속도(`foodPerKm`처럼 speed 필드 필요), 마차 speedModifier.
- **식량 부족 속도저하 유예구간** (기획서 9번) — 지금은 0이면 즉시 실패.
- **시간 출처** — 지금 진행도를 바깥이 주입. Framework의 게임시간/배속과 합의 필요.
