# Framework 인게임 시간 배율(In-Game Time Multiplier) API 가이드

## 목적

이 문서는 Framework가 제공하는 **인게임 시간 배율(`inGameTimeMultiplier`)** 기능의 정책, Public API, 팀원 사용 방법을 설명한다.

인게임 시간 배율과 pause, UTC 변환 API는 **Framework & Integration**(`11.CoreServices`)이 소유한다.  
Core, UI, Progression 등 다른 feature는 `UnityEngine.Time.timeScale`이나 자체 시간 축을 직접 만들기보다 **`IInGameTimeProvider` / `IGameTimeProvider` 계약**을 통해 조회·변환해야 한다.

---

## 1. 이 기능이 무엇인가

**인게임 시간 배율**은 현실 wall-clock 시간을 게임 내부 시간으로 변환하는 gameplay용 배율이다.

| 항목 | 정책 |
|------|------|
| 배율 의미 | 현실 **1초** = 인게임 **N초** |
| 현실 시간 기준 | UTC wall-clock (`DateTime.UtcNow`) |
| 내부 계산 단위 | **인게임 초** (in-game seconds) |
| Inspector 표시 단위 | 분 / 시간 / 하루 (config에서 선택) |
| 무역 중 배율 | 출발 시 `inGameTimeMultiplierAtStart`로 **고정** |
| Pause | `progress01`과 `elapsedInGameSeconds` **모두** 정지 |

### Unity `Time.timeScale`과의 차이

| 구분 | `inGameTimeMultiplier` | `Time.timeScale` |
|------|------------------------|------------------|
| 용도 | gameplay 시간 축 (식량, 월드 시뮬 등) | 연출 / debug 재생 속도 |
| 영향 범위 | Framework가 계산하는 인게임 경과 | Unity `Update`, 애니메이션, `WaitForSeconds` 등 |
| Release 변경 | config 고정 (runtime 변경 불가) | debug API로 변경 가능 |
| 무역 progress01 | **영향 없음** | **영향 없음** |

**progress01(도착 진행률)은 항상 현실 UTC 경과 기준**이다.  
배율을 올려도 도착 시각(`expectedTradeEndUtcTick`)은 변하지 않는다.

---

## 2. 이중 시간 모델

무역 진행 중 두 시간 축이 **의도적으로 분리**되어 있다.

```text
[현실 시간 축]  progress01, totalSeconds, tradeStartUtc, expectedTradeEndUtc
                → 이동·거리·도착 판정

[인게임 시간 축] elapsedInGameSeconds, inGameTimeMultiplierAtStart
                → 식량 소모, 계절·재난, 마을 갱신 (M2/M3)
```

### progress01 (도착 진행률)

```text
progress01 = (CurrentUtc - tradeStartUtc) / (expectedTradeEndUtc - tradeStartUtc)
```

- `totalSeconds` = `(expectedTradeEndUtc - tradeStartUtc)`의 **현실 초**
- Core `JourneyRunner.SetProgress(caravan, progress01)`에 전달되는 값
- 배율과 무관하게 UTC만 사용

### elapsedInGameSeconds (인게임 경과)

```text
elapsedInGameSeconds = (CurrentUtc - tradeStartUtc).TotalSeconds × inGameTimeMultiplierAtStart
```

- 무역 **출발 시점** runtime 배율을 `inGameTimeMultiplierAtStart`에 스냅샷
- 출발 후 runtime 배율을 바꿔도 **진행 중 무역에는 적용되지 않음**
- Pause 중에는 Framework coordinator가 갱신하지 않음

### 오프라인 복구 (구현됨)

```text
evaluationUtc = min(loadUtc, lastSavedUtc + maxOfflineRealSeconds)
offlineElapsedInGameSeconds = (evaluationUtc - tradeStartUtc) × inGameTimeMultiplierAtStart
```

- call site: `TradeProgressCoordinator.ApplyOfflineProgressOnLoad` → `GetOfflineElapsedInGameSeconds` / `GetElapsedInGameSecondsForActiveTrade`
- `loadUtc < lastSavedUtc`이면 `TimeRollbackDetected`를 발행하고 오프라인 적용을 건너뛴다
- 공식 변경 시 `InGameTimeConversionPolicy.GetOfflineElapsedInGameSeconds(...)` 한 곳만 수정한다
- 상세: [`Docs/Personal_Documents/CSU/0712_m3-offline-progress-pipeline.md`](../Personal_Documents/CSU/0712_m3-offline-progress-pipeline.md)

---

## 3. Policy Config

기본 배율과 단위 해석은 ScriptableObject config에서 설정한다.

| 항목 | 값 |
|------|-----|
| 타입 | `ND.Framework.InGameTimePolicyConfig` |
| 생성 메뉴 | `Assets > Create > ND > Framework > In-Game Time Policy Config` |
| Runtime asset 경로 | `Assets/_Project/11.CoreServices/Resources/InGameTimePolicyConfig.asset` |
| Resources 로드 이름 | `InGameTimePolicyConfig` (확장자 제외) |

### Inspector 필드

| 필드 | 설명 |
|------|------|
| `defaultInGameTimeMultiplier` | Release·Editor 공통 **초기** runtime 배율 |
| `elapsedTimeDisplayUnit` | 경과 인게임 시간 UI 표시 단위 |
| `foodConsumptionUnit` | 견인 동물 raw rate(`foodPerKm`) 해석 단위 |
| `maxOfflineRealSeconds` | Continue/Load 시 인정하는 최대 오프라인 현실 초 (`lastSaved` 기준 evaluationUtc 상한, 기본 259200 = 72h) |

### `InGameTimeUnit`

| enum | 1단위 = 인게임 초 | UI 라벨 |
|------|-------------------|---------|
| `Second` | 1 | sec |
| `Minute` | 60 | min |
| `Hour` | 3600 | hr |
| `Day` | 86400 | day |

config asset이 없으면 `FrameworkRoot`가 코드 기본값(배율 1, 단위 Hour)으로 fallback한다.

---

## 4. 진입점과 서비스 구조

```text
FrameworkRoot.Instance
  ├── GameTime              → GameTimeService (IGameTimeProvider + IInGameTimeProvider)
  ├── DebugCommands         → FrameworkDebugCommands (debug 래퍼)
  ├── CurrentSaveData       → SaveData (배율 스냅샷·elapsed 저장)
  └── TradeProgressCoordinator → progress01 / elapsed 갱신
```

- **Namespace:** `ND.Framework`
- **권장 진입점:** `FrameworkRoot.Instance.GameTime`
- **interface 주입이 필요한 경우:** `IInGameTimeProvider`, `IGameTimeProvider`

`GameTimeService`는 `FrameworkRoot` Awake에서 policy config를 로드해 생성된다.

---

## 5. 팀원 사용 방법

### 5.1 현재 UTC 조회 (진행률·tick 비교)

도착 progress, 저장 tick 비교 등 **현실 시간**이 필요할 때 사용한다.

```csharp
using ND.Framework;
using System;

// FrameworkRoot가 준비된 이후
var gameTime = FrameworkRoot.Instance.GameTime;
DateTime nowUtc = gameTime.CurrentUtc;
```

`IGameTimeProvider`는 배율·pause를 다루지 않는다. UTC wall-clock만 제공한다.

### 5.2 active trade의 인게임 경과 조회 (UI·표시)

```csharp
using ND.Framework;

var gameTime = FrameworkRoot.Instance.GameTime;
var saveData = FrameworkRoot.Instance.CurrentSaveData;

if (saveData?.tradeProgress != null
    && saveData.tradeProgress.state == TradeProgressState.Traveling
    && saveData.tradeProgress.tradeStartUtcTick > 0)
{
    double elapsedInGameSeconds = gameTime.GetElapsedInGameSecondsForActiveTrade(
        saveData.tradeProgress,
        gameTime.CurrentUtc);

    string formatted = gameTime.FormatInGameDuration(
        elapsedInGameSeconds,
        gameTime.ElapsedTimeDisplayUnit);
}
```

- `GetElapsedInGameSecondsForActiveTrade`는 저장된 `inGameTimeMultiplierAtStart`를 사용한다.
- traveling 상태가 아니거나 start tick이 없으면 0을 반환한다.

### 5.3 임의 UTC 구간의 인게임 경과 계산

```csharp
var gameTime = FrameworkRoot.Instance.GameTime;

double elapsed = gameTime.GetElapsedInGameSeconds(
    startUtc,
    endUtc,
    multiplier: 60f);   // 현실 1초 = 인게임 60초
```

음수 구간(`endUtc < startUtc`)은 0으로 처리된다.

### 5.4 식량 raw rate → 인게임 초당 소모율 변환

Core `foodPerKm` 필드는 팀 결정에 따라 **시간 기준 raw rate**로 해석한다.  
config의 `FoodConsumptionUnit`에 맞춰 인게임 초당 소모율로 정규화한다.

```csharp
var gameTime = FrameworkRoot.Instance.GameTime;

// 예: raw rate = "인게임 1시간당 2.5" 이고 FoodConsumptionUnit = Hour
float rawRate = 2.5f;
float perInGameSecond = gameTime.ToConsumptionPerInGameSecond(rawRate);
// → 2.5 / 3600
```

**Core PR 완료 전:** `CaravanCalculator.GetRemainingFood`는 아직 `progress01 × totalSeconds`(현실 초) 기준이다.  
식량 판정 연동은 Core 후속 PR에서 `elapsedInGameSeconds`를 사용하도록 변경 예정이다.

### 5.5 Debug — 배율 변경 (Editor / Development Build만)

runtime 배율 변경은 **Editor 또는 Development Build**에서만 적용된다. Release에서는 `TrySetInGameTimeMultiplier`가 false를 반환하고 무시된다.

```csharp
// 방법 1: DebugCommands (권장)
bool applied = FrameworkRoot.Instance.DebugCommands.SetInGameTimeMultiplier(60f);

// 방법 2: GameTimeService 직접 호출
bool applied = FrameworkRoot.Instance.GameTime.TrySetInGameTimeMultiplier(60f);

// config 기본값으로 복원
FrameworkRoot.Instance.DebugCommands.ResetInGameTimeMultiplier();
```

InGame scene UI 버튼 연결 예:

```csharp
// InGameSceneController public method → Button OnClick
FrameworkRoot.Instance.DebugCommands.SetInGameTimeMultiplier(60f);
FrameworkRoot.Instance.DebugCommands.PauseGameTime();
FrameworkRoot.Instance.DebugCommands.ResumeGameTime();
```

`FrameworkDebugBridge` Inspector 버튼으로도 동일 API를 호출할 수 있다.

### 5.6 Debug — Unity TimeScale (연출 전용)

```csharp
FrameworkRoot.Instance.DebugCommands.SetTimeScale(2f);
```

`Time.timeScale`은 **인게임 배율과 분리**된다. gameplay elapsed 계산에 사용하지 않는다.

### 5.7 Pause / Resume

```csharp
FrameworkRoot.Instance.GameTime.PauseGameTime();
FrameworkRoot.Instance.GameTime.ResumeGameTime();
```

Pause 중 `TradeProgressCoordinator.CheckProgressAndCompletion()`은:

- `progress01` 갱신 **안 함**
- `elapsedInGameSeconds` 저장 **안 함**

UI에서 elapsed를 직접 `CurrentUtc`로 계산하면 Pause 중에도 숫자가 증가할 수 있다.  
Pause 반영 UI가 필요하면 `IsGameTimePaused`를 함께 확인한다.

---

## 6. Public API — `IInGameTimeProvider`

- **파일:** `Assets/_Project/11.CoreServices/Scripts/Time/IInGameTimeProvider.cs`
- **구현:** `GameTimeService`

### 상태 조회

```csharp
float InGameTimeMultiplier { get; }     // 현재 runtime 배율
bool IsGameTimePaused { get; }            // pause 여부
InGameTimeUnit ElapsedTimeDisplayUnit { get; }
InGameTimeUnit FoodConsumptionUnit { get; }
```

### 변환·포맷

```csharp
double GetElapsedInGameSeconds(DateTime startUtc, DateTime endUtc, float multiplier);

double GetElapsedInGameSecondsForActiveTrade(
    TradeProgressSaveData progress,
    DateTime endUtc);

string FormatInGameDuration(double inGameSeconds, InGameTimeUnit unit);

float ToConsumptionPerInGameSecond(float rawRate);
```

---

## 7. Public API — `GameTimeService` (추가 메서드)

`IInGameTimeProvider` 외에 `GameTimeService`가 직접 제공하는 API:

```csharp
// IGameTimeProvider
DateTime CurrentUtc { get; }

// Unity time scale (연출/debug)
float TimeScale { get; }
void SetTimeScale(float scale);

// runtime 배율 변경 — Editor/Dev Build만 적용
bool TrySetInGameTimeMultiplier(float multiplier);
void ResetInGameTimeMultiplier();

// pause
void PauseGameTime();
void ResumeGameTime();

// 무역 종료 시각 계산 (UTC)
DateTime CalculateTradeEnd(DateTime startUtc, TimeSpan duration);
TimeSpan GetRemainingTime(DateTime endUtc);

// 오프라인 복구 (CompleteLoadingAndEnterGame → ApplyOfflineProgressOnLoad)
double GetOfflineElapsedInGameSeconds(
    DateTime tradeStartUtc,
    DateTime loadUtc,
    float multiplierAtStart);
```

---

## 8. Public API — `IGameTimeProvider`

- **파일:** `Assets/_Project/11.CoreServices/Scripts/Time/IGameTimeProvider.cs`

```csharp
DateTime CurrentUtc { get; }
```

무역 progress tick 비교, 출발/종료 시각 기록 등 **UTC wall-clock**만 필요할 때 사용한다.

---

## 9. Debug API — `FrameworkDebugCommands`

- **진입점:** `FrameworkRoot.Instance.DebugCommands`
- **파일:** `Assets/_Project/11.CoreServices/Scripts/Debug/FrameworkDebugCommands.cs`

| 메서드 | 설명 |
|--------|------|
| `SetInGameTimeMultiplier(float)` | runtime 배율 변경 (Editor/Dev) |
| `ResetInGameTimeMultiplier()` | config 기본 배율로 복원 |
| `PauseGameTime()` | 인게임 시간 pause |
| `ResumeGameTime()` | 인게임 시간 resume |
| `SetTimeScale(float)` | Unity time scale (연출) |
| `CompleteTradeImmediately()` | active trade 즉시 완료 (debug) |

---

## 10. SaveData 필드

저장 schema version: **5** (`SaveData.CurrentVersion`)

`version 5`부터 `pendingSettlement`(대기 정산 결과)를 포함한다. v4 이하 세이브는 마이그레이션 없이 새 게임으로 복구될 수 있다.

### `TradeProgressSaveData`

| 필드 | 타입 | 설명 |
|------|------|------|
| `tradeStartUtcTick` | `long` | 무역 시작 UTC ticks |
| `expectedTradeEndUtcTick` | `long` | 예상 도착 UTC ticks |
| `inGameTimeMultiplierAtStart` | `float` | 출발 시 고정된 인게임 배율 |

- `JsonSaveService`는 `inGameTimeMultiplierAtStart <= 0`이면 **1**로 정규화한다.
- `TradeProgressRecorder.RecordStartedTrade(...)`가 출발 시 현재 runtime 배율을 스냅샷한다.

### `CaravanSaveData`

| 필드 | 타입 | 설명 |
|------|------|------|
| `totalSeconds` | `float` | 이번 무역 **현실** 총 소요 시간(초) |
| `progress01` | `float` | 도착 진행률 0~1 (현실 UTC 기준) |
| `elapsedInGameSeconds` | `float` | 누적 **인게임** 경과 시간(초) |

`TradeProgressCoordinator`가 traveling 갱신 시 `elapsedInGameSeconds`를 SaveData에 기록한다.

---

## 11. Framework 내부 연동 흐름

```text
무역 출발
  → TradeProgressRecorder.RecordStartedTrade
      · tradeStartUtcTick / expectedTradeEndUtcTick 기록
      · inGameTimeMultiplierAtStart = 현재 runtime 배율
      · elapsedInGameSeconds = 0

주기적 갱신 (InGame / debug ticker)
  → TradeProgressCoordinator.CheckProgressAndCompletion
      · pause 중이면 skip
      · progress01 = f(CurrentUtc, tradeStartUtc, expectedTradeEndUtc)   [현실]
      · JourneyRunner.SetProgress(caravan, progress01)
      · elapsedInGameSeconds = f(CurrentUtc, tradeStartUtc, multiplierAtStart) [인게임]
      · Save
```

---

## 12. feature별 사용 가이드

### Core Gameplay

| 목적 | 사용 API / 데이터 |
|------|-------------------|
| 도착·실패 판정 | `progress01` (Framework가 SetProgress로 주입) |
| 식량 소모 | `CaravanData.elapsedInGameSeconds` + `CaravanConsumptionRateNormalizer` + `ToConsumptionPerInGameSecond` |
| 이동 시간 | `totalSeconds` (현실 초, 출발 시 Core 계산) |
| 시간 출처 직접 참조 금지 | `JourneyRunner`는 UTC/배율을 모름 — progress01만 받음 |

### UI & Data

| 목적 | 사용 API |
|------|----------|
| 경과 인게임 시간 표시 | `GetElapsedInGameSecondsForActiveTrade` + `FormatInGameDuration` |
| 현재 배율 / pause 표시 | `InGameTimeMultiplier`, `IsGameTimePaused` |
| 참고 구현 | `Assets/Scripts/InGameTimeTextDisplay.cs` |

### Progression / World (M2/M3)

| 목적 | 사용 API |
|------|----------|
| 계절·재난·마을 갱신 tick | 인게임 초 축 (`elapsedInGameSeconds` 또는 변환 API) |
| 오프라인 복구 | `ApplyOfflineProgressOnLoad` → `GetOfflineElapsedInGameSeconds` / evaluationUtc (구현됨) |

---

## 13. 현재 연동 상태와 주의사항

### Framework PR — 완료

- `IInGameTimeProvider` / `GameTimeService` 배율·pause·변환 API
- Save v4 (`inGameTimeMultiplierAtStart`, `elapsedInGameSeconds`)
- 출발 시 배율 스냅샷
- `TradeProgressCoordinator.SyncElapsedInGameSeconds` — `SetProgress` **전** active caravan + SaveData 동시 갱신
- `CaravanConsumptionRateNormalizer` — raw 소모율 → 인게임 초당 소모율 (출발 직전·runtime 조립 시)
- Debug API, policy config 로드
- Editor E2E: `RunInGameFoodConsumptionE2E`

### Core 연동 — 완료 (식량 소모 축)

- `CaravanData.elapsedInGameSeconds` 필드 존재
- `CaravanCalculator.GetRemainingFood` → `elapsedInGameSeconds` (인게임 초) 기준
- `CaravanSaveDataMapper` → `elapsedInGameSeconds` runtime 매핑

### 이중 시간 모델 (의도적)

| 축 | 사용처 |
|----|--------|
| 현실 UTC (`progress01`) | 도착 판정, `starveGraceSeconds` 유예 카운트다운 |
| 인게임 경과 (`elapsedInGameSeconds`) | 식량 소모, 월드 시뮬 tick (M2/M3) |

`starveGraceSeconds`는 **현실 초** 기준이다. 식량 소모만 인게임 배율의 영향을 받는다.

### Release 빌드

- runtime 배율 변경 API는 **무시**된다 (`TrySetInGameTimeMultiplier` → false).
- 초기 배율은 `InGameTimePolicyConfig.defaultInGameTimeMultiplier`만 사용된다.
- Pause API는 Release에서도 호출 가능하나, 실제 gameplay UI 노출 여부는 feature별로 결정한다.

---

## 14. 테스트

| 리소스 | 설명 |
|--------|------|
| `Assets/_Project/11.CoreServices/Scenes/InGameTimeMultiplierTest.unity` | 배율·progress 갱신 테스트 scene |
| `Assets/_Project/11.CoreServices/Scripts/Debug/TimeScaleProgressTicker.cs` | 주기적 `CheckProgressAndCompletion` 호출 |
| `Assets/Scripts/InGameTimeTextDisplay.cs` | multiplier / elapsed / pause HUD |
| `Assets/_Project/11.CoreServices/Editor/FrameworkM1LoopE2EEditorTests.cs` | loop + Economy + 인게임 식량 E2E (Editor/batchmode) |
| `Assets/_Project/11.CoreServices/Scripts/Debug/TradeStartDebugHarness.cs` | Play mode smoke (`Run InGame Food Consumption Smoke`) |

Debug 배율 변경 → 무역 출발 → ticker 또는 InGame scene에서 progress·elapsed 확인.

---

## 15. 자주 묻는 질문

### Q. TimeScale을 올리면 식량이 더 빨리 줄어드나요?

아니요. gameplay 시간 축은 `inGameTimeMultiplier`이다.  
`Time.timeScale`은 Unity 연출/debug 전용이다.

### Q. 무역 중 배율을 바꾸면 진행 중 무역에 적용되나요?

아니요. 출발 시 `inGameTimeMultiplierAtStart`만 사용한다.  
변경된 runtime 배율은 **다음 출발**부터 스냅샷된다.

### Q. progress01과 elapsedInGameSeconds가 다른 이유는?

도착(이동·거리)은 **현실 시간**, 식량·월드 시뮬은 **인게임 시간** 축을 사용하는 이중 모델 때문이다.

### Q. `totalSeconds`는 인게임 초인가요?

아니요. **현실 UTC** 기준 총 소요 시간(초)이다. Core `CaravanCalculator.GetTravelSeconds`가 출발 시 계산한다.

### Q. Pause 중 UI elapsed가 계속 증가합니다.

coordinator는 pause 중 SaveData 갱신을 멈추지만, UI가 `CurrentUtc`로 직접 계산하면 표시는 증가할 수 있다.  
`IsGameTimePaused == true`일 때 마지막 저장값을 표시하거나 갱신을 skip한다.

### Q. config asset을 수정했는데 Release 빌드에 반영되지 않습니다.

Release에서는 **defaultInGameTimeMultiplier**만 초기값으로 사용된다.  
`Resources/InGameTimePolicyConfig.asset`을 빌드에 포함했는지, Player build에서 Resources 로드가 되는지 확인한다.

---

## 16. 관련 파일

| 파일 | 설명 |
|------|------|
| `Assets/_Project/11.CoreServices/Scripts/Time/IInGameTimeProvider.cs` | 인게임 시간 Public API 계약 |
| `Assets/_Project/11.CoreServices/Scripts/Time/IGameTimeProvider.cs` | UTC 시간 Public API 계약 |
| `Assets/_Project/11.CoreServices/Scripts/Time/GameTimeService.cs` | 구현체 |
| `Assets/_Project/11.CoreServices/Scripts/Time/InGameTimeConversionPolicy.cs` | 변환 공식 (오프라인 포함) |
| `Assets/_Project/11.CoreServices/Scripts/Time/InGameTimePolicyConfig.cs` | config ScriptableObject |
| `Assets/_Project/11.CoreServices/Scripts/Time/InGameTimeUnit.cs` | 단위 enum·helper |
| `Assets/_Project/11.CoreServices/Resources/InGameTimePolicyConfig.asset` | runtime config |
| `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs` | 서비스 조립·config 로드 |
| `Assets/_Project/11.CoreServices/Scripts/Debug/FrameworkDebugCommands.cs` | debug API |
| `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs` | progress·elapsed 갱신 · ApplyOfflineProgressOnLoad |
| `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressRecorder.cs` | 출발 시 배율 스냅샷 |
| `Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs` | 저장 schema v5 (`pendingSettlement` 포함) |
| `Docs/Personal_Documents/CSU/0712_m3-offline-progress-pipeline.md` | 오프라인 복구 로직·테스트 |
| `Assets/Scripts/InGameTimeTextDisplay.cs` | UI 표시 예시 |

---

## 17. 문의

- **API, 정책, SaveData 필드, coordinator 동작:** Framework & Integration 담당
- **식량·CaravanData elapsed 연동:** Core Gameplay 담당 (후속 PR)
- **월드·계절 인게임 tick:** Progression / World 담당 (M2/M3)
