# Core PR 요청 — 인게임 식량 소모 이중 시간 축 문서화

**요청 일자:** 2026-07-11  
**요청자:** Framework & Integration (천성욱)  
**대상 담당:** Core Gameplay (윤호영)  
**관련 Framework PR:** `fix/framework/caravan-ingame-food-sync`

---

## Purpose

Framework가 `elapsedInGameSeconds`를 active caravan에 동기화하는 연동이 완료됨에 따라, Core 쪽 **시간 축 계약**을 주석·참조문서에 명시해 팀 간 오해를 방지한다.

이 PR은 Framework 식량 연동의 **선행·차단 조건이 아니다.** 코드 동작 변경 없이 문서·주석 보강을 요청한다.

---

## 배경

### 현재 이중 시간 모델 (팀 합의)

| 시간 축 | Core 필드 / 계산 | Framework 역할 |
|---------|------------------|----------------|
| **현실 UTC** | `progress01` (0~1) | `CalculateProgress` — 도착 판정 |
| **인게임 경과** | `elapsedInGameSeconds` | `SyncElapsedInGameSeconds` — 식량 소모 |
| **현실 유예** | `starveGraceSeconds` + `progress01 × totalSeconds` | 값 설정·저장만 |

- 식량 **소모**는 인게임 초(`elapsedInGameSeconds`) 기준
- 식량 **고갈 후 도착 제한시간**(`starveGraceSeconds`)은 **현실 초** 기준 유지

### Framework 측 완료 항목 (참고)

- `TradeProgressCoordinator.SyncElapsedInGameSeconds` — `SetProgress` 전 active caravan + SaveData 동시 갱신
- `CaravanConsumptionRateNormalizer` — `ToConsumptionPerInGameSecond`로 raw rate 정규화
- Editor E2E `RunInGameFoodConsumptionE2E`

---

## Requested Changes

### 1. `CaravanData.cs` 주석 보강

| 필드 | 문서화 내용 |
|------|-------------|
| `elapsedInGameSeconds` | 식량 소모 전용. Framework가 매 progress 갱신 시 채움. Core는 직접 증가시키지 않음 |
| `starveGraceSeconds` | 식량 고갈 후 도착 제한시간. **현실 초** 단위 (`progress01 × totalSeconds` 축) |
| `foodPerKm` (동물) | 인게임 1초당 소모율로 해석. Framework `ToConsumptionPerInGameSecond` 정규화 후 값. 필드 rename은 별도 PR |

### 2. `JourneyRunner.cs` — `CheckFoodDepletion` 주석 보강

- 식량 잔량 판정: `CaravanCalculator.GetRemainingFood` → `elapsedInGameSeconds` 사용
- 유예 카운트다운: `(progress01 - runFoodDepletedProgress) × totalSeconds` → **현실 초** 의도적 사용
- 인게임 배율은 유예 시간에 영향을 주지 않음을 명시

### 3. `CaravanCalculator.cs` 식량 섹션 주석 보강

- `GetConsumptionPerSec` / `GetRemainingFood`가 인게임 초 축임을 재확인
- `GetEstimatedFood` / `GetRequiredFood`의 배율·예상 인게임 초 파라미터 의미 명시

### 4. [`_Core_참조문서.md`](../../Assets/_Project/01.Core/_Core_참조문서.md) 갱신

- 식량 소모 = 인게임 경과 (`elapsedInGameSeconds`)
- 고갈 유예 = 현실 경과 (`progress01` 기반)
- Framework 연동 계약 요약 (진행도는 바깥이 주입, elapsed는 Framework가 주입)
- 이중 시간 모델 간단 다이어그램 추가

---

## Optional Follow-up (별도 PR, 우선순위 낮음)

- `foodPerKm` → `foodPerInGameSecond` 필드 rename
  - 영향: `imsiAnimalData`, `AnimalSaveData`, `CaravanSaveDataMapper`, debug harness, SaveData
  - Framework rename PR과 동시 또는 직후 진행 권장

---

## Not in Scope

- `CheckFoodDepletion` 유예시간을 인게임 축으로 변경 (팀 결정: 현실 초 유지)
- `JourneyRunner` progress 계산 방식 변경
- Framework `elapsedInGameSeconds` 갱신 로직 변경

---

## 검증 (Core PR 완료 후)

- 주석이 실제 구현과 일치하는지 리뷰
- `JourneyRunTest` 주석이 이중 시간 모델 설명과 일치하는지 확인
- Framework E2E `RunInGameFoodConsumptionE2E` 회귀 Pass (Core 변경 없으면 자동 Pass)

---

## Related

- Framework guide: [`Docs/Guide/Framework_InGame_Time_Multiplier_API_Guide.md`](../../Guide/Framework_InGame_Time_Multiplier_API_Guide.md) §13
- Framework sync 문서: [`Docs/Personal_Documents/CSU/Core-services-M1-sync.md`](Core-services-M1-sync.md)
- 관련 Issue: 없음
