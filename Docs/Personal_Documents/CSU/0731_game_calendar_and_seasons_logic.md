# Game Calendar & Seasons · 구현 로직 정리

**작성일:** 2026-07-31  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `feature/framework/add-calendar-and-seasons`  
**HEAD (문서화 시점):** `e230392` — `fix(ui): refresh calendar panel after calendar initialization`  
**베이스:** `dev2`  
**Feature root:** `Assets/_Project/11.CoreServices/Scripts/Time/`  
**UI root:** `Assets/_Project/05.UI/10_Calendar/`

관련 선행:

- [`0712_m3-offline-progress-pipeline.md`](./0712_m3-offline-progress-pipeline.md) — 오프라인 evaluationUtc·상한·역행
- [`0710_InGame-TimeScale.md`](./0710_InGame-TimeScale.md) — 인게임 시간 축
- [`0711_world-force-debug-commands.md`](./0711_world-force-debug-commands.md) — ForceSeason / ForceDisaster (레거시 캐시)

---

## 1. 목적

현실 UTC 경과를 **권위 있는 게임 달력**(년/월/일/계절/월별 재난)으로 변환하고,  
온라인 tick·오프라인 복구·저장·이벤트를 Framework 계층에서 일관되게 제공한다.

이번 브랜치가 추가하는 것:

1. **저장 계약** — `world.calendar` (`totalElapsedDays` + `dayAnchorUtcTicks`)와 `world.worldSeed`
2. **순수 날짜 계산** — 30일 고정 월, Year 1 March epoch, 월→계절 매핑
3. **온라인 진행** — UTC 샘플 누적 → 완전 일 단위 전진 → 저장 성공 후에만 이벤트 발행
4. **결정적 월별 재난** — `worldSeed` + `AbsoluteMonthIndex` 해시로 Summer 홍수 / Winter 가뭄
5. **오프라인 병합 트랜잭션** — 달력·무역이 동일 `OfflineRestoreContext`를 쓰고, dirty면 **한 번만** Save
6. **표시 UI** — 12개월 예시 패널 (`GameCalendarPanel`)이 snapshot/이벤트로 갱신
7. **디버그 커맨드** — scale / 일·월 전진 / 오프라인 시뮬레이션 / 상태 로그

이번 브랜치가 하지 않는 것:

- 무역 duration·식량 소모 공식 변경 (무역은 기존 multiplier 축 유지)
- 재난이 가격/루트에 미치는 이코노미 효과 구현 (ID만 저장·노출)
- 공용 Scene에 Calendar Prefab 배치 (Prefab·스크립트만 제공)
- `ForceSeason` / `ForceDisaster`를 달력 권위로 승격 (레거시 캐시로 격하, 다음 달력 mutation이 덮어씀)

---

## 2. diff 요약

| 영역 | 주요 변경 |
|------|-----------|
| Time (신규) | `GameCalendarService`, `GameCalendarDate`, `GameCalendarSaveData`, `GameCalendarSnapshot`, `GameSeason`, `MonthlyDisasterResolver/Policy`, `OfflineRestoreContext`, `CalendarRestoreResult`, `MonthlyWorldState`, `GameCalendarSeed` |
| Bootstrap | `FrameworkRoot` — `GameCalendar` 소유, online tick에 달력 포함, `ExecuteOfflineRestore` 병합 저장 |
| Trade | `TradeProgressCoordinator.PrepareOfflineProgressOnLoad` — 저장·이벤트 deferred / rollback |
| Save | `WorldSaveData.worldSeed` + `calendar`, 기본 계절 `spring`, NormalizeData 보정 |
| Events | `CalendarInitialized`, `CalendarRestored`, `Year/Month/Season/DisasterChanged` |
| Debug | Calendar ContextMenu + `AdvanceDebugDays` / `SimulateCalendarOffline` |
| UI | `05.UI/10_Calendar` — View/Presenter/MonthSlot + Prefab Generator + Edit Mode 테스트 |
| Tests | Date / SaveContract / Service / Disaster / OfflineRestore / Panel |

**통계:** 57 files, +8105 / −52 lines (Prefab YAML 비중 큼)

커밋 흐름:

```text
1f611db  add calendar save and date contract
39abe36  advance game calendar from utc time
79b1fcf  add deterministic monthly disasters
efc6630  restore calendar from offline elapsed time
508d044  verify merged offline restore transactions
d24e13e  add calendar events and debug commands
cc0cebf  add example twelve-month calendar prefab
e230392  refresh calendar panel after calendar initialization
```

---

## 3. 달력 규칙 (순수 계산)

### 3.1 단위

| 개념 | 값 |
|------|-----|
| 1 게임 일 | 현실 UTC **120초** (`GameCalendarService.TicksPerGameDay`) |
| 1 게임 월 | **30** 게임 일 (고정) |
| 1 게임 년 | **12** 게임 월 |
| Epoch | `totalElapsedDays = 0` → **Year 1 / Month 3 (March) / Day 1** |
| `AbsoluteMonthIndex` | epoch March를 **0**으로 하는 단조 월 인덱스 (`totalElapsedDays / 30`) |

### 3.2 `GameCalendarDate.FromElapsedDays`

```text
normalizedDays = max(0, totalElapsedDays)
monthOffset    = normalizedDays / 30          → AbsoluteMonthIndex
calendarMonthOffset = monthOffset + 2         → March = offset 0 → month 3
Year  = 1 + calendarMonthOffset / 12
Month = (calendarMonthOffset % 12) + 1        → 1..12
Day   = (normalizedDays % 30) + 1             → 1..30
Season = FromMonth(Month)
```

### 3.3 월 → 계절

| 월 | 계절 | SeasonId |
|----|------|----------|
| 12, 1, 2 | Winter | `winter` |
| 3, 4, 5 | Spring | `spring` |
| 6, 7, 8 | Summer | `summer` |
| 9, 10, 11 | Autumn | `autumn` |

`currentSeasonId` 캐시는 날짜에서 유도한 `SeasonId`와 동기화한다.  
Normalize / BeginOnline / Advance / Restore 시 갱신.

---

## 4. 저장 계약

### 4.1 권위 필드 (`GameCalendarSaveData`)

| 필드 | 의미 | 단위 |
|------|------|------|
| `totalElapsedDays` | 시작일부터 지난 **완전한** 게임 일수 | game day |
| `dayAnchorUtcTicks` | **현재 게임 일의 시작**에 대응하는 UTC `DateTime.Ticks` | UTC ticks |

부분 일 진행은 저장하지 않는다. 온라인 세션의 `pendingGameTicks`는 메모리 전용이다.

### 4.2 World 캐시

| 필드 | 역할 |
|------|------|
| `world.worldSeed` | 월별 재난 결정성 루트. `0` = 미초기화 → Normalize 시 생성 |
| `world.currentSeasonId` | Economy 등 소비용 캐시 (달력이 권위) |
| `world.currentDisasterId` | 현재 월 재난 ID (`""` / `flood` / `drought`) |

### 4.3 New Game / Normalize

**CreateNewGameData**

- `worldSeed = GameCalendarSeed.Create()` (GUID FNV-1a, 0 금지 → 1)
- `calendar.totalElapsedDays = 0`
- `calendar.dayAnchorUtcTicks = createdUtcTicks` (`lastSavedUtcTicks`와 동일 시각)
- 기본 계절: **spring** (기존 기본값 summer에서 변경)

**NormalizeData**

- `worldSeed == 0` → 시드 생성
- `calendar == null` / 음수 일수 / 비양수 앵커 → 보정
- `currentSeasonId`를 `FromElapsedDays` 결과와 맞춘다
- `currentDisasterId == null` → `""` (재난 **재계산은 Normalize에서 하지 않음**)

---

## 5. 컴포넌트 구조

```text
FrameworkRoot
  ├─ GameTimeService          ← UTC provider + offline cap
  ├─ GameCalendarService      ← 달력 권위 / tick / restore / debug advance
  ├─ TradeProgressCoordinator ← 무역 오프라인 prepare/publish/rollback
  └─ SaveService              ← 병합 저장 / 일일 저장

GameCalendarService
  ├─ GameCalendarDate         ← 순수 Y/M/D/Season
  ├─ MonthlyDisasterResolver  ← seed+month → disasterId
  └─ MonthlyDisasterPolicy    ← SummerFlood / WinterDrought 확률

UI (표시 전용)
  GameCalendarPanelPresenter → FrameworkEvents / TryGetCurrent
  GameCalendarPanelView      → snapshot을 TMP·월 슬롯에 반영
```

---

## 6. 온라인 진행 로직

### 6.1 세션 시작 — `BeginOnlineSession`

```text
TryGetCalendar(save) 실패 → ResetSession, false
세션 바인딩:
  lastSampleUtcTicks = currentUtc
  pendingGameTicks   = max(0, currentUtc - dayAnchor)   ← 앵커 이후 벽시계를 초기 pending으로
캐시 수리 (이 시점에는 저장하지 않음):
  currentSeasonId / currentDisasterId 재계산
Current snapshot 설정
최초 초기화 시에만 CalendarInitialized 발행
```

로딩 완료 후 restore가 dirty가 아니고 `LastCalendarRestoreResult == null`인 경우  
`CompleteLoadingAndEnterGame`에서 `BeginOnlineSession`을 호출한다.  
Restore가 이미 세션을 잡았으면 별도 Begin이 필요 없다.

### 6.2 Tick — `TickOnline` (0.2s polling, 무역과 동일 gate)

```text
Update (isOnlineProgressTickEnabled)
  ├─ GameCalendar.TickOnline(save, SaveService)   ← 일 단위면 Save 1회
  └─ TradeProgress.CheckProgress...(save:false)  ← 중간 프레임 무저장
```

```text
TickOnline
  ├─ 세션 불일치 → BeginOnlineSession
  ├─ UTC 역행 (current < lastSample) → ignore
  ├─ pendingGameTicks += (deltaUtc * debugScale)   ← scale ∈ {0,1,2,4}, 비영속
  └─ AdvancePendingWholeDays
        days = pending / TicksPerGameDay
        days ≤ 0 → Unchanged
        mutate: totalElapsedDays, dayAnchor, season/disaster caches, Current
        Save 실패 → 필드·snapshot 롤백, pending은 유지(재시도)
        Save 성공 → pending = remainder, RaiseCalendarTransition
```

**앵커 갱신 공식 (온라인):**

```text
newAnchorUtcTicks = currentUtcTicks - remainderTicks
remainderTicks    = pendingGameTicks - daysAdvanced * TicksPerGameDay
```

월이 바뀌면 재난을 다시 Resolve하고, 같은 월이면 기존 `currentDisasterId`를 유지한다.

### 6.3 전환 이벤트 순서

`RaiseCalendarTransition(previous, current)`:

1. YearChanged (Year 변경 시)
2. MonthChanged (`AbsoluteMonthIndex` 변경 시)
3. SeasonChanged
4. DisasterChanged

모두 `(previous, current)` snapshot 쌍. snapshot은 값 복사.

---

## 7. 결정적 월별 재난

### 7.1 정책 기본값

| 계절 | 결과 | 기본 확률 |
|------|------|-----------|
| Spring / Autumn | 항상 `""` (None) | — |
| Summer | `flood` 또는 None | 0.25 |
| Winter | `drought` 또는 None | 0.25 |

### 7.2 Resolve

```text
hash = FNV-1a(worldSeed || "monthly_disaster_occur" || absoluteMonthIndex)
roll = hash / (uint.MaxValue + 1)   ∈ [0, 1)
Occurs = roll < chance
```

동일 `(worldSeed, AbsoluteMonthIndex, season, policy)` → 항상 동일 ID.  
온라인·오프라인·디버그 월 전진이 같은 Resolver를 사용한다.

---

## 8. 오프라인 복구 (달력 + 무역 병합)

### 8.1 공통 UTC 구간 — `OfflineRestoreContext`

`InGameTimeConversionPolicy.ResolveOfflineRestoreContext`:

| 상황 | EvaluationUtc | AcceptedElapsed | 플래그 |
|------|----------------|-----------------|--------|
| `lastSavedUtcTicks ≤ 0` | loadUtc | 0 | — |
| loadUtc < lastSaved | loadUtc | 0 | `ClockRollbackDetected` |
| 정상 + 상한 초과 | lastSaved + maxOffline | capped | `WasClamped` |
| 정상 | loadUtc | load − lastSaved | — |

달력과 무역이 **같은 EvaluationUtc**를 소비한다.

### 8.2 `GameCalendarService.RestoreOffline`

```text
역행 또는 evaluation < dayAnchor → daysAdvanced = 0 (상태 유지 가능)
그 외:
  daysAdvanced = (evaluationUtc - dayAnchor) / TicksPerGameDay
  nextTotalDays / nextAnchor 갱신
  final disaster Resolve
  PassedMonths = (startingMonth, finalMonth] 구간의 MonthlyWorldState 목록
    bound: requested ≤ daysAdvanced/30 + 2, 초과 시 빈 목록 + Warning
세션 rebase:
  lastSampleUtcTicks = loadUtc.Ticks
  pendingGameTicks   = evaluationUtc - nextAnchor   ← 복구 구간 이중 계산 방지
  debugScale = 1
저장·이벤트는 호출하지 않음 → FrameworkRoot 트랜잭션이 담당
```

### 8.3 `FrameworkRoot.ExecuteOfflineRestore`

```text
snapshot = JsonUtility.ToJson(saveData)
calendarResult = calendar.RestoreOffline(save, context)
tradeRestore   = trade.PrepareOfflineProgressOnLoad(save, context)

!calendarDirty && !tradeDirty
  → RaiseCalendarRestored + trade.Publish (저장 없음)
  → Succeeded

dirty
  → Save 1회
  → 실패: FromJsonOverwrite + trade.RollbackRuntime + calendar.RebuildRuntimeAfterFailedRestore
         CalendarResult = null, 이벤트 미발행
  → 성공: RaiseCalendarRestored + trade.Publish
```

무역 쪽 `PrepareOfflineProgressOnLoad`는 기존 오프라인 처리와 동일하되,  
저장·Settlement/Route 이벤트는 `Publish`로 미룬다. 실패 시 runtime caravan·LastSettlement·economy pending을 되돌린다.

`ApplyOfflineProgressOnLoad`는 단독 호출 경로에서도 같은 prepare→save→publish/rollback 패턴을 유지한다 (레거시/테스트 호환).

---

## 9. UI 표시 로직

```text
OnEnable → Subscribe + RefreshFromCurrentState
  ├─ TryGetCurrent → view.Refresh(snapshot)
  └─ 없음 → HideUntilInitialized

구독:
  CalendarInitialized / CalendarRestored
  YearChanged / MonthChanged / SeasonChanged / DisasterChanged
  → Refresh(current)

View.Refresh:
  "Y년 M월", 계절 한글, 재난 행(있을 때만), 12 MonthSlot 중 현재 월 하이라이트
```

표시 전용. SaveData를 수정하지 않는다.  
로딩 직후 세션이 늦게 잡히는 경우를 위해 `CalendarInitialized`에서도 갱신한다 (`e230392`).

---

## 10. 디버그 API

| ContextMenu / API | 동작 |
|-------------------|------|
| `TrySetCalendarDebugScale` | 0/1/2/4만 허용, **비영속** |
| `AdvanceOneGameDay` / `Month` | `AdvanceDebugDays` + Save 1회 + Transition |
| `AdvanceToMonth(m)` | 다음 해당 월까지 일수 전진 (같으면 +12개월) |
| `SimulateCalendarOffline(sec)` | 앵커 기준 가짜 context로 Restore + Save |
| `LogCalendarState` / `LogCalendarRestoreTimeline` | 현재·PassedMonths 로그 |

`ForceSeason` / `ForceDisaster`는 캐시만 덮고, 로그에 **calendar queries remain authoritative**를 명시한다.

---

## 11. 전체 런타임 흐름

```text
[New Game]
  CreateNewGameData → seed + calendar(0, createdUtc) → Loading → CompleteLoading

[Continue]
  Load + Normalize → Loading → CompleteLoadingAndEnterGame
    ├─ ResolveOfflineRestoreContext(lastSaved, loadUtc)
    ├─ ExecuteOfflineRestore (calendar + trade, merged save)
    ├─ pending settlement cache / InGameScreenRouter / RaiseLoadCompleted
    ├─ (restore empty) BeginOnlineSession
    └─ isOnlineProgressTickEnabled = true → InGame

[InGame Update @ 0.2s]
  GameCalendar.TickOnline → (whole days) Save + events
  TradeProgress.CheckProgress → settlement 시 자체 Save
```

---

## 12. 주요 파일 맵

| 경로 | 역할 |
|------|------|
| `11.CoreServices/Scripts/Time/GameCalendarService.cs` | 온라인/오프라인/디버그 달력 권위 |
| `11.CoreServices/Scripts/Time/GameCalendarDate.cs` | Y/M/D/Season 순수 계산 |
| `11.CoreServices/Scripts/Time/MonthlyDisasterResolver.cs` | 결정적 재난 ID |
| `11.CoreServices/Scripts/Time/OfflineRestoreContext.cs` | 공유 UTC 구간 |
| `11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs` | tick gate + 병합 restore |
| `11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs` | trade offline prepare/publish |
| `11.CoreServices/Scripts/Save/SaveData.cs` / `JsonSaveService.cs` | 스키마·Normalize·NewGame |
| `11.CoreServices/Scripts/Events/FrameworkEvents.cs` | 달력 이벤트 |
| `05.UI/10_Calendar/Scripts/*` | 표시 Presenter/View |
| `05.UI/10_Calendar/Prefabs/GameCalendarPanel.prefab` | 12개월 예시 패널 |
| `11.CoreServices/Editor/*Calendar*Tests.cs` | Edit Mode 검증 |

---

## 13. 검증·리스크

### 검증 (코드/테스트 기준)

- Edit Mode: Date, SaveContract, Service tick/save-fail rollback, Disaster hash, Offline restore + merged transaction, Panel refresh
- Unity Play Mode / Scene 배치 연동은 Prefab 제공 수준이며, 공용 InGame Scene 삽입은 후속

### 리스크·주의

- **무역 시간 축과 달력 일 축이 다름** — 무역은 multiplier 기반 인게임 초, 달력은 고정 120s/day UTC. 의도적 분리.
- **Normalize는 재난을 재굴리지 않음** — 손상된 `currentDisasterId`는 BeginOnline/Restore/월 전환 때 교정.
- **오프라인 PassedMonths 상한** — 비정상적으로 긴 구간은 타임라인 목록을 비우고 Warning.
- **Save 실패 시** — 온라인은 pending 유지·상태 롤백; 오프라인 병합은 JSON 스냅샷으로 SaveData 전체 롤백.
- **레거시 ForceSeason/Disaster** — UI/Economy가 캐시만 보면 달력과 어긋날 수 있음. 다음 달력 mutation이 권위를 회복.

---

## 14. 소비자 가이드 (요약)

```csharp
// 현재 날짜·계절·재난
if (FrameworkRoot.Instance.GameCalendar.TryGetCurrent(out var snap))
{
    // snap.Year, Month, Day, Season, SeasonId, ActiveDisasterId
}

// 이벤트
FrameworkEvents.CalendarInitialized += s => { };
FrameworkEvents.CalendarRestored += r => { /* r.PassedMonths, r.DaysAdvanced */ };
FrameworkEvents.SeasonChanged += (prev, cur) => { };
FrameworkEvents.DisasterChanged += (prev, cur) => { };

// 저장 캐시 (읽기 전용 권장)
save.world.currentSeasonId;
save.world.currentDisasterId;
```

Economy `PriceCalculationInput.SeasonId` / `DisasterId`는 기존처럼 world 캐시를 쓸 수 있으나,  
실시간 UI는 `GameCalendarSnapshot` 또는 달력 이벤트를 구독하는 편이 안전하다.
)
