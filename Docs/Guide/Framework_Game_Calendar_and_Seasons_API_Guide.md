# Framework 게임 달력 · 계절 API 가이드

## 목적

이 문서는 Framework가 제공하는 **게임 달력**(년/월/일)과 **계절·월별 재난** API의 핵심 개념과 사용법을 설명한다.

달력의 권위(날짜를 누가 정하는가)는 **Framework & Integration**(`11.CoreServices`)이 가진다.  
UI, Economy, Progression 등 다른 feature는 날짜를 직접 계산하거나 Save 필드를 임의로 고치지 말고, 아래 API로 **조회**하거나 **이벤트**로 변화를 받는다.

**네임스페이스:** `ND.Framework`  
**진입점:** `FrameworkRoot.Instance.GameCalendar`  
**예시 UI:** `Assets/_Project/05.UI/10_Calendar/`

상세 구현·공식·오프라인 병합 로직:  
[`Docs/Personal_Documents/CSU/0731_game_calendar_and_seasons_logic.md`](../Personal_Documents/CSU/0731_game_calendar_and_seasons_logic.md)

---

## 1. 한 줄로 이해하기

```text
현실 UTC가 흐르면 → 게임 일수가 쌓이고 → 년/월/일·계절·재난이 정해진다.
```

| 질문 | 답 |
|------|-----|
| 누가 날짜를 정하나? | `GameCalendarService` (Framework) |
| 다른 팀은 무엇을 쓰나? | `TryGetCurrent` 조회 + `FrameworkEvents` 구독 |
| 저장에 무엇이 남나? | 지난 **완전한** 게임 일수와 그 날의 UTC 앵커 |
| 계절은 어디서 오나? | 월에서 자동 계산 (별도 수동 설정 불필요) |

---

## 2. 핵심 개념

### 2.1 달력 축 vs 인게임 배율 축 (서로 다름)

이 프로젝트에는 **두 개의 시간 축**이 있다. 혼동하지 않는다.

| 축 | 담당 | 쓰임 |
|----|------|------|
| **달력 축** | `GameCalendarService` | 년/월/일, 계절, 월별 재난 |
| **인게임 배율 축** | `GameTimeService` / `IInGameTimeProvider` | 무역 식량 소모, 인게임 초 경과 |

- 달력의 **1게임 일** = 현실 UTC **120초** (고정)
- 인게임 배율의 **1 Day 단위** = 인게임 **86400초** (식량·배율용) → 달력 일과 **무관**

배율·Pause 상세: [`Framework_InGame_Time_Multiplier_API_Guide.md`](./Framework_InGame_Time_Multiplier_API_Guide.md)

### 2.2 달력 규칙 (고정)

| 항목 | 값 |
|------|-----|
| 1게임 일 | 현실 UTC 120초 |
| 1게임 월 | 30게임 일 (고정) |
| 1게임 년 | 12게임 월 |
| 시작점 (Epoch) | 지난 일수 `0` → **1년 3월 1일** (봄) |

월이 바뀌면 날짜·계절·재난이 함께 따라간다.

### 2.3 계절

| 월 | 계절 | `SeasonId` |
|----|------|------------|
| 12, 1, 2 | Winter | `winter` |
| 3, 4, 5 | Spring | `spring` |
| 6, 7, 8 | Summer | `summer` |
| 9, 10, 11 | Autumn | `autumn` |

enum: `GameSeason` (`Spring`, `Summer`, `Autumn`, `Winter`)

### 2.4 월별 재난

매 게임 월마다, 그 월의 계절과 `worldSeed`로 **결정적**으로 재난 ID가 정해진다.

| 계절 | 가능한 결과 | 기본 확률 |
|------|-------------|-----------|
| Spring / Autumn | 없음 (`""`) | — |
| Summer | `flood` 또는 없음 | 홍수 0.25 |
| Winter | `drought` 또는 없음 | 가뭄 0.25 |

같은 시드·같은 월이면 항상 같은 결과다.  
Economy 등은 재난 ID를 **읽어 쓰는** 쪽이고, 발생 판정은 달력이 한다.

### 2.5 저장에 남는 것 / 안 남는 것

| 저장됨 (권위) | 의미 |
|---------------|------|
| `world.calendar.totalElapsedDays` | 시작부터 지난 **완전한** 게임 일수 |
| `world.calendar.dayAnchorUtcTicks` | 현재 게임 일이 시작된 UTC 시각 |
| `world.worldSeed` | 재난 결정에 쓰는 월드 시드 |

| 캐시 (편의용) | 의미 |
|---------------|------|
| `world.currentSeasonId` | 현재 계절 문자열 |
| `world.currentDisasterId` | 현재 월 재난 (`""` / `flood` / `drought`) |

- **하루의 일부 진행**은 메모리에만 두고, 저장하지 않는다.
- 라이브 UI·게임플레이는 가능하면 **스냅샷/이벤트**를 쓰고, 캐시 필드는 Economy 입력 등 읽기 전용으로 쓴다.

---

## 3. 진입점과 데이터

```text
FrameworkRoot.Instance
  └─ GameCalendar  (GameCalendarService)
        ├─ TryGetCurrent → GameCalendarSnapshot
        └─ (내부) 온라인 tick / 오프라인 복구 / 디버그 전진
```

### `GameCalendarSnapshot` (읽기용 스냅샷)

소비자가 들고 있어도 Save가 바뀌지 않는 **값 복사**다.

| 필드 | 설명 |
|------|------|
| `Year` / `Month` / `Day` | 게임 달력 날짜 |
| `Season` / `SeasonId` | 계절 enum / 문자열 |
| `ActiveDisasterId` | 현재 월 재난 ID |
| `TotalElapsedDays` | 시작부터 지난 완전한 일수 |
| `AbsoluteMonthIndex` | 시작 월(3월)을 0으로 하는 월 인덱스 |

---

## 4. 팀원 사용법

다른 feature가 해야 할 일은 거의 두 가지뿐이다. **조회**와 **구독**.

### 4-1. 지금 날짜·계절 조회

```csharp
var calendar = FrameworkRoot.Instance?.GameCalendar;
if (calendar != null && calendar.TryGetCurrent(out var snap))
{
    // snap.Year, snap.Month, snap.Day
    // snap.Season / snap.SeasonId
    // snap.ActiveDisasterId
}
```

세션이 아직 잡히기 전이면 `TryGetCurrent`가 false일 수 있다.  
그때는 UI를 숨기거나, 아래 `CalendarInitialized`를 기다린다.

### 4-2. 변화 알림 구독

```csharp
void OnEnable()
{
    FrameworkEvents.CalendarInitialized += OnInit;
    FrameworkEvents.CalendarRestored += OnRestored;
    FrameworkEvents.SeasonChanged += OnSeason;
    FrameworkEvents.DisasterChanged += OnDisaster;
    // 필요 시 YearChanged / MonthChanged 도 동일 패턴
}

void OnDisable()
{
    FrameworkEvents.CalendarInitialized -= OnInit;
    FrameworkEvents.CalendarRestored -= OnRestored;
    FrameworkEvents.SeasonChanged -= OnSeason;
    FrameworkEvents.DisasterChanged -= OnDisaster;
}

void OnInit(GameCalendarSnapshot snap) { /* 최초 표시 */ }
void OnRestored(CalendarRestoreResult result) { /* result.Current, result.PassedMonths */ }
void OnSeason(GameCalendarSnapshot prev, GameCalendarSnapshot cur) { /* 계절 UI·로직 */ }
void OnDisaster(GameCalendarSnapshot prev, GameCalendarSnapshot cur) { /* 재난 표시 */ }
```

**전환 이벤트 발행 순서** (해당할 때만):

1. `YearChanged`
2. `MonthChanged`
3. `SeasonChanged`
4. `DisasterChanged`

모두 `(이전 스냅샷, 현재 스냅샷)` 쌍이다.  
오프라인으로 여러 날이 한꺼번에 지나면 `CalendarRestored`가 오고, `PassedMonths`로 지나간 월 목록을 볼 수 있다.

참고 구현: `GameCalendarPanelPresenter` (`05.UI/10_Calendar`)

### 4-3. Save 캐시만 읽는 경우 (Economy 등)

```csharp
string seasonId = save.world.currentSeasonId;
string disasterId = save.world.currentDisasterId;
```

- 읽기 전용으로 쓴다.
- 라이브 화면은 `ForceSeason` / `ForceDisaster`로 캐시만 바뀐 뒤 **달력과 어긋날 수 있다**. 다음 달력 갱신이 캐시를 덮어쓴다.
- 권위 있는 값은 항상 `GameCalendar.TryGetCurrent` / 이벤트 스냅샷이다.

### 4-4. 하지 말아야 할 것

- `totalElapsedDays` / `dayAnchorUtcTicks`를 feature 코드에서 직접 수정
- 월→계절 매핑을 feature 쪽에 다시 구현
- 재난 발생 확률을 feature에서 다시 굴리기 (이미 달력이 ID를 정함)
- 달력 일을 인게임 배율 Day(86400초)와 같다고 가정

---

## 5. Framework가 알아서 하는 일

팀원이 직접 호출할 필요가 없는 흐름이다.

| 상황 | 동작 요약 |
|------|-----------|
| New Game | 시드·달력 0일·3월 1일(봄)으로 시작 |
| 온라인 플레이 | UTC를 모아 **하루가 꽉 찰 때만** 날짜 전진 → 저장 성공 후 이벤트 |
| Continue / Load | 달력·무역이 **같은 오프라인 UTC 구간**을 쓰고, 필요 시 **한 번만** Save |
| 저장 실패 | 달력 필드·스냅샷을 되돌리고, 이벤트는 내지 않음 |

온라인 tick·오프라인 병합은 `FrameworkRoot`가 담당한다.

---

## 6. 디버그 (Editor)

`FrameworkDebugBridge` / `FrameworkDebugCommands` ContextMenu 계열.

| 명령 | 용도 |
|------|------|
| Calendar Debug Scale (`0`/`1`/`2`/`4`) | 달력만 빠르게/정지 (세션 한정, 저장 안 됨) |
| Advance One Game Day / Month | 일·월 단위로 강제 전진 |
| Advance To Month | 지정 월까지 전진 |
| Simulate Calendar Offline | 오프라인 복구 시뮬레이션 |
| Log Calendar State | 현재 스냅샷 로그 |

레거시 `ForceSeason` / `ForceDisaster`는 **캐시만** 덮는다. 달력 조회·다음 mutation이 권위다.  
상세: [`Framework_World_Force_Debug_API_Guide.md`](./Framework_World_Force_Debug_API_Guide.md)

---

## 7. 관련 파일

| 경로 | 역할 |
|------|------|
| `11.CoreServices/Scripts/Time/GameCalendarService.cs` | 달력 권위 서비스 |
| `11.CoreServices/Scripts/Time/GameCalendarSnapshot.cs` | 읽기 스냅샷 |
| `11.CoreServices/Scripts/Time/GameCalendarDate.cs` | 순수 날짜·계절 계산 |
| `11.CoreServices/Scripts/Time/GameSeason.cs` | 계절 enum |
| `11.CoreServices/Scripts/Time/MonthlyDisasterResolver.cs` | 월별 재난 결정 |
| `11.CoreServices/Scripts/Events/FrameworkEvents.cs` | 달력 이벤트 |
| `05.UI/10_Calendar/` | 표시 전용 예시 패널 |

---

## 8. 관련 문서

| 문서 | 내용 |
|------|------|
| [`0731_game_calendar_and_seasons_logic.md`](../Personal_Documents/CSU/0731_game_calendar_and_seasons_logic.md) | 구현·공식·오프라인 병합 상세 |
| [`Framework_InGame_Time_Multiplier_API_Guide.md`](./Framework_InGame_Time_Multiplier_API_Guide.md) | 인게임 배율·Pause (다른 시간 축) |
| [`Framework_CoreServices_Team_Usage_Guide.md`](./Framework_CoreServices_Team_Usage_Guide.md) | CoreServices 통합 사용 설명서 |
| [`Framework_World_Force_Debug_API_Guide.md`](./Framework_World_Force_Debug_API_Guide.md) | ForceSeason / ForceDisaster |
