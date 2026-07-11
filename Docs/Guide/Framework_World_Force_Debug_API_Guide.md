# Framework World Force Debug API 가이드

## 목적

이 문서는 M2 통합·수동 smoke를 위해 Framework가 제공하는 **월드 Force\* debug API** 사용법을 설명한다.

대상 API:

| API | 역할 |
|-----|------|
| `ForceSeason` | 현재 계절 ID를 강제 변경하고 저장한다 |
| `ForceDisaster` | 현재 재난 ID를 강제 변경하고 저장한다 |
| `ForceRouteEvent` | Traveling 무역에 route event 1회 주입 hook을 등록한다 |

소유: **Framework & Integration** (`Assets/_Project/11.CoreServices/`)  
용도: **개발·통합 검증 전용** (플레이어용 프로덕션 UI 아님)

---

## 1. 언제 쓰는가

| 팀 / 목적 | 추천 API |
|-----------|----------|
| Economy — 계절·재난 가격 차이 smoke | `ForceSeason`, `ForceDisaster` |
| Framework / Core — Traveling 중 이벤트 주입 가정 | `ForceRouteEvent` |
| UI — 월드 상태를 바꿔 정산·표시 확인 | Season / Disaster 후 기존 정산 흐름 |

이 API가 없어도 각자 SaveData를 직접 수정해 진행할 수 있다.  
다만 **InGame Inspector ContextMenu만으로** 동일 상태를 맞출 때 사용한다.

---

## 2. 사전 조건

1. Play 시작 씬: `Assets/_Project/07.Scenes/01_Boot/Boot.unity`
2. 경로: **Boot → Title → New Game(또는 Continue) → Loading → InGame**
3. InGame Hierarchy에서 아래 중 하나를 선택한다.
   - `FrameworkDebugBridge`
   - `TradeStartDebugHarness` (무역 출발까지 한 오브젝트에서 할 때)

둘 다 같은 Force\* ContextMenu를 제공한다.  
무역을 시작하려면 **`TradeStartDebugHarness`**를 사용한다.

Console를 열고 Framework 로그를 확인한다.

---

## 3. Inspector에서 ID 바꾸기

선택한 컴포넌트 Inspector에서:

| 필드 | 기본값 | 설명 |
|------|--------|------|
| `Debug Season Id` | `winter` | Force Season에 사용 |
| `Debug Disaster Id` | `drought` | Force Disaster에 사용. **빈 문자열 = 재난 없음** |
| `Debug Route Event Id` | `debug_route_event_001` | Force Route Event에 사용 |

Content 테이블에 없는 ID여도 Force\*는 문자열을 그대로 기록한다.  
가격·이벤트가 기대와 다르면 **Content/Economy ID**를 확인한다.

---

## 4. ContextMenu 사용법

컴포넌트 우클릭(또는 ⋮) → ContextMenu:

| 메뉴 | 호출 |
|------|------|
| `Framework/Force Season` | `ForceSeason(debugSeasonId)` |
| `Framework/Force Disaster` | `ForceDisaster(debugDisasterId)` |
| `Framework/Force Route Event` | `ForceRouteEvent(debugRouteEventId)` |

저장 확인(선택):

| 메뉴 | 위치 |
|------|------|
| `Framework/Print Save Data` | `TradeStartDebugHarness` |
| `Framework/Print Full Save Data` | `SaveDataDebugPrinter` (씬에 있는 경우) |

JSON에서 확인할 필드:

```text
world.currentSeasonId
world.currentDisasterId
```

---

## 5. 시나리오별 순서

### 5-1. 계절 / 재난만 바꾸기 (무역 불필요)

```text
1. Boot → Title → New Game → InGame
2. FrameworkDebugBridge 선택
3. (필요 시) Inspector에서 Season / Disaster ID 수정
4. Framework/Force Season
5. Framework/Force Disaster
6. Print Save Data 로 world 필드 확인
```

기대 로그 예:

```text
ForceSeason applied. TradeId: (none), SeasonId: winter
ForceDisaster applied. TradeId: (none), DisasterId: 'drought'
```

재난을 끄려면 `Debug Disaster Id`를 비운 뒤 `Force Disaster`를 다시 실행한다.

### 5-2. Route Event 주입 hook (Traveling 필수)

```text
1. InGame에서 TradeStartDebugHarness 선택
2. Framework/Fill Sample Caravan
3. Framework/Start Trade And Record Time
4. Framework/Force Route Event
```

기대 로그 예:

```text
RouteEventForced event raised. TradeId: ..., EventId: debug_route_event_001
ForceRouteEvent inject hook registered. TradeId: ..., RouteId: ..., EventId: ...
```

무역 시작 전에 Force Route Event를 누르면 **실패가 정상**이다.

```text
ForceRouteEvent skipped because trade state is ... Traveling is required.
```

### 5-3. 권장 한 바퀴 smoke

```text
1. Force Season → Print Save
2. Force Disaster → Print Save
3. Force Route Event (무역 전) → Warning 확인
4. Fill Sample → Start Trade
5. Force Route Event → success 로그 확인
```

---

## 6. 코드에서 호출하기

```csharp
var commands = FrameworkRoot.Instance.DebugCommands;

commands.ForceSeason("winter");
commands.ForceDisaster("drought");
commands.ForceDisaster(""); // 재난 클리어

// Traveling 상태에서만 true
commands.ForceRouteEvent("debug_route_event_001");

if (commands.TryConsumeForcedRouteEvent(tradeId, out var eventId))
{
    // 향후 Core 로드/약탈 적용 연결 지점
}
```

이벤트 구독 (선택):

```csharp
FrameworkEvents.RouteEventForced += (tradeId, eventId) =>
{
    // tradeId, eventId
};

// OnDisable 등에서 반드시 구독 해제
```

---

## 7. 동작 차이 요약

| 항목 | ForceSeason / ForceDisaster | ForceRouteEvent |
|------|-----------------------------|-----------------|
| 저장 | 즉시 `SaveService.Save` | **저장 안 함** |
| 대상 | `WorldSaveData` | 런타임 pending + 이벤트 |
| Traveling 필요 | 아니오 | **예** |
| Continue 후 | 값 유지 | pending **소멸** (정상) |
| Core 적용 | 해당 없음 (Economy는 다음 settle 시 ID 사용) | **미연결 stub** |

Economy는 정산 조립 시 `WorldSaveData`의 season/disaster를 읽는다.  
Force\* 직후 자동 settle은 하지 않으므로, 가격 확인은 **기존 무역 정산 흐름**을 이어서 실행한다.

---

## 8. 실패했을 때

| 로그 / 증상 | 조치 |
|-------------|------|
| `FrameworkRoot save services are not ready` | Boot부터 New Game으로 다시 진입 |
| `ForceSeason skipped because seasonId is empty` | Inspector Season ID 입력 |
| `Traveling is required` | Fill Sample → Start Trade 후 재시도 |
| `eventId is empty` | Route Event ID 입력 |
| Season/Disaster는 바뀌었는데 가격이 안 변함 | Content ID·정산 실행 여부 확인 (Force는 문자열만 기록) |
| Route Event 후 화물/전투 변화 없음 | Core 적용 API 미연결 — 현재는 hook/로그만 |

---

## 9. 하지 말 것

- Release/플레이어 플로우에 Force\*를 넣지 말 것
- Scene / Prefab YAML을 직접 편집해 ID를 박지 말 것
- ForceRouteEvent를 “로드 이벤트 완전 적용”으로 오해하지 말 것
- M3 AtomicSave / Offline / AutoSave와 혼동하지 말 것

---

## 10. 관련 파일 · 문서

| 경로 | 설명 |
|------|------|
| `Scripts/Debug/FrameworkDebugCommands.cs` | API 구현 |
| `Scripts/Debug/FrameworkDebugBridge.cs` | ContextMenu |
| `Scripts/Debug/TradeStartDebugHarness.cs` | ContextMenu + 무역 출발 |
| `Scripts/Events/FrameworkEvents.cs` | `RouteEventForced` |
| `Docs/Personal_Documents/CSU/world-force-debug-commands.md` | 구현 로직 상세 |
| `Docs/Guide/Framework_InGame_Time_Multiplier_API_Guide.md` | 인게임 시간 배율 가이드 |
