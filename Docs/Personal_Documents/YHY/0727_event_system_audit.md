# 이벤트(Route Event) 시스템 현황 감사 (0727)

## 목적
"이벤트 트리거가 제대로 만들어졌나, 이벤트 생성은 되나?"를 프로젝트 전체 확인. (미니맵 격자 기반 구름→날씨 이벤트 기획 전 현황 파악)

## 한 줄 결론
**트리거 "엔진"은 잘 만들어져 있고 여행 tick에 연결돼 있으나, 먹일 "이벤트 데이터"가 전부 비어 있고 날씨 효과는 stub이다. 즉 실게임에선 이벤트가 하나도 발생하지 않는다.**

---

## ✅ 만들어진 것 — 트리거 엔진

| 항목 | 위치 | 상태 |
|---|---|---|
| 거리구간 이벤트 판정(결정적) | `11.CoreServices/.../TradeProgress/TradeRouteEventProcessor.cs` | 구현 + 테스트(`TradeRouteEventProcessorTests`) |
| 여행 중 주기 호출 | `FrameworkRoot.cs` L346~351 (`CheckProgressAndCompletion`) | `TradeProgressCheckIntervalSeconds`마다 tick |
| 강제 주입 API | `FrameworkDebugCommands.ForceRouteEvent(eventId)` | 있음(디버그 hook) |
| 전투/약탈 효과 | `TradeRouteEventProcessor.TryApply` → `JourneyRunner.ResolveBanditRaid` | **실제 구현**(화물·사료 약탈 적용) |

- 이벤트 발생 여부/선택/적용을 `StableHash(tradeId, checkIndex, purpose)`로 결정 → 호출 시점과 무관하게 재현 가능(오프라인 복구에도 안전).
- `TradeProgressCoordinator.ProcessRouteEvents`가 route의 `Distance/MaxEventCount`로 interval을 계산해 `Process` 호출.

## ❌ 안 만들어진 것 — "말만 나온" 부분

1. **이벤트 데이터가 전부 비어 있음**
   - `02.Data/01_ScriptableObjects/Routes/`의 route 9개 **전부 `routeEvents: []`** (BaseToRiver, BaseToWindy, MountToRiver, RiverToBase, WindyToBase, ...).
   - 엔진은 `route.Events`가 비면 스킵(`ValidateCommon` → "Route event table is empty") → **실게임에서 이벤트 0건**.

2. **Weather / Lucky 효과: 껍데기(stub)**
   - `TradeRouteEventProcessor.TryApply` L227~230 주석 그대로:
     > "현재 Core에는 Lucky/Weather runtime 효과 API가 없다. 정의된 event 발생 자체만 기록하고 임의의 cargo/재화 효과를 만들지 않는다."
   - 즉 Weather 이벤트를 정의해 발생시켜도 **아무 효과 없음**.

3. **위치(공간) 기반 이벤트: 아예 없음**
   - 계절·재난(`03.Economy/02_SeasonDisaster/JourneyEconomyModifierCalculator`)은 **전역 경제 배율**이지 "어느 위치/셀에 어떤 날씨"라는 공간 개념이 없음.
   - "특정 좌표에서 이벤트"를 지원하는 것은 현재 없음.

## 요약표
| 부분 | 상태 |
|---|---|
| 트리거 엔진(판정·주입·tick) | ✅ 있음(테스트O) |
| 전투/약탈 효과 | ✅ 구현 |
| 이벤트 데이터(콘텐츠) | ❌ 전부 빈 배열 |
| 날씨(Weather) 효과 | ❌ stub(효과 API 없음) |
| 위치/공간 기반 이벤트 | ❌ 없음 |

## 미니맵 격자 구름→이벤트 기획에 주는 시사점
- **트리거를 걸 훅(`ForceRouteEvent`)은 존재** → 우리 격자 시스템에서 "캐러밴이 비구름 밑" 판정 시 호출은 가능.
- 그러나 **"비 맞으면 무슨 일"이라는 효과가 없음(stub)** → 프로토타입은 **자체 이벤트/효과로 시작**하는 것이 현실적. route 이벤트 데이터에 의존하지 않는다.
- 정식으로 게임플레이 효과(식량↓·지연·손실 등)를 붙이려면 **Core에 Weather/위치 이벤트 효과 API 신설이 필요** → 성욱님/정헌님과 협의 항목.

## 다음 액션(제안)
1. 이 문서 팀 공유 → "이벤트 엔진은 있으나 콘텐츠·날씨 효과가 비어있다" 합의.
2. 우리(윤호영)는 미니맵 격자 프로토타입으로 **자체 구름/판정/이벤트 훅**을 만들어 데모(비침습).
3. 실제 효과·데이터 authoring 책임/API는 프레임워크 팀과 역할 정리.

> 근거 파일: `TradeRouteEventProcessor.cs`, `FrameworkDebugCommands.cs`, `FrameworkRoot.cs`, `02.Data/01_ScriptableObjects/Routes/*.asset`, `FrameworkEvents.cs`
