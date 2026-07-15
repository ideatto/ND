# Framework World Map API Guide

처음 월드맵을 붙이는 경우 먼저 [`Framework_World_Map_Usage_Guide.md`](./Framework_World_Map_Usage_Guide.md)를 읽는다.

## 목적

월드맵 표시 계층이 **논리 무역 데이터**와 **시각 경로**를 분리한 채 Framework 진행 상태를 읽기 전용으로 소비하는 방법을 설명한다.

- 논리 데이터: `TownData` / `RouteData` SO → Resources **Shared catalog** 등록 → `ISharedGameDataProvider`
- 1차 빌드 SO seed: `Assets/_Project/02.Data/01_ScriptableObjects/`
- catalog: `Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset`
- `99.Sandbox`는 타입·더미/레거시용이며, 맵이 폴더를 직접 스캔하지 않는다
- 진행 읽기: `TradeProgressCoordinator.TryGetMapProgress`
- 시각 구현: `Assets/_Project/05.UI/04_WorldMap/`

---

## 1. 핵심 분리 규칙

```text
Gameplay route distance / travel duration
!=
Visual LineRenderer or Spline length
```

금지:

- Transform 거리로 여행 거리 계산
- Spline 길이로 여행 시간 계산
- 캐러밴 Transform 위치를 SaveData에 저장
- `RouteVisual` / `TownWorldView`에서 무역 출발·정산·Save 쓰기

---

## 2. 진행 스냅샷 API

**위치:** `Assets/_Project/11.CoreServices/Scripts/TradeProgress/`

```csharp
var coordinator = FrameworkRoot.Instance.TradeProgressCoordinator;
if (coordinator.TryGetMapProgress(out var snapshot))
{
    string routeId = snapshot.ActiveRouteId;
    float progress01 = snapshot.Progress01;          // 0~1
    float percent = snapshot.ProgressPercent;        // 0~100
    long startTick = snapshot.TradeStartUtcTick;
    long endTick = snapshot.ExpectedTradeEndUtcTick;
    TradeProgressState state = snapshot.State;
}
```

### `TradeMapProgressSnapshot` 필드

| 필드 | 의미 |
|------|------|
| `HasActiveTrade` | Traveling 또는 SettlementPending이면 true |
| `ActiveTradeId` | 활성 무역 ID |
| `ActiveRouteId` | 활성 route ID |
| `State` | `TradeProgressState` |
| `Progress01` | UTC 기반 진행률 0~1 |
| `TradeStartUtcTick` | 시작 UTC ticks |
| `ExpectedTradeEndUtcTick` | 예상 종료 UTC ticks |
| `ProgressPercent` | `Progress01 * 100` |

### 동작 메모

- 읽기 전용이다. 저장·정산·출발을 변경하지 않는다.
- `Traveling` + pause 중에는 `ActiveCaravan.progress01`을 우선해 화면 진행이 멈춘다.
- `SettlementPending`이면 `Progress01 = 1`.
- `%` 표시는 `ProgressPercent` 또는 Presenter 라벨을 사용한다.

---

## 3. 월드맵 스크립트 (`05.UI/04_WorldMap/Scripts`)

| 스크립트 | 역할 |
|----------|------|
| `WorldMapPresenter` | Shared/Save/스냅샷 연결, 캐러밴·%·risk 갱신, `TownClicked` 이벤트 |
| `WorldMapPanel` | Prefab 호스트. `Show`/`Hide`로 패널 GameObject 활성·비활성 |
| `WorldMapPanelControls` | 열기/닫기 버튼을 `Show`/`Hide`에 연결 (패널 밖에 배치) |
| `TownWorldView` | townId 표시·클릭 입력만 |
| `RouteVisual` | Straight / Spline 경로, `EvaluatePosition`, LineRenderer |
| `CaravanMapMarker` | progress01로 위치·방향 재구성 |
| `RouteMapPresentationResolver` | unlock/completed/active 표시 상태 (후속 RouteRuntimeState 확장 지점) |
| `WorldMapTestEntry` | 레거시 런타임 생성 (베이크 후에는 Prefab 패널 사용) |

Risk 표시는 `SharedRouteDefinition.BaseRiskLevel`만 사용한다.

표시 정책:

- `InGameScreenState`(Traveling/Preparation)로 자동 Show/Hide하지 않는다.
- 맵 열기 버튼 → `WorldMapPanel.Show()`
- 맵 닫기 버튼 → `WorldMapPanel.Hide()`
- 열기 버튼은 패널이 꺼져 있어도 동작하도록 **패널 밖**에 둔다.

---

## 4. 예시 씬

**경로:** `Assets/_Project/05.UI/04_WorldMap/Scene/WorldMapTest.unity`

### 4.1 Play 직후 정상 상태

| 항목 | 기대 |
|------|------|
| `DontDestroyOnLoad` | `FrameworkRoot` 존재 (런타임 자동 생성) |
| `WorldMapUi` / `HudCanvas` / `OpenMapButton` | **활성** |
| `WorldMapPanel` 및 `WorldMapRoot` | **비활성** (`startHidden`) |
| Game 뷰 | 맵이 안 보임 → Open Map 전이므로 정상 |

### 4.2 버튼으로 맵 열기/닫기

1. Play 후 **Open Map** 클릭 → `WorldMapPanel`·`WorldMapRoot` 활성, 맵 표시
2. 마을/루트·Progress % / Risk 라벨 확인
3. **Close Map** 클릭 → 패널 비활성, Open Map만 남음

### 4.3 계층·예시 ID

구성:

1. Orthographic Main Camera
2. `EventSystem` + `InputSystemUIInputModule` (`StandaloneInputModule` 사용 금지 — Input System only 프로젝트)
3. `WorldMapUi`
   - `WorldMapPanelControls`
   - `HudCanvas` / `OpenMapButton`
   - `WorldMapPanel` (`startHidden`)
     - `PanelCanvas` / `CloseMapButton`
     - `MapHost` / `WorldMapRoot`
4. Towns: `basecamp`, `dummytown`, `demo_waypoint`
5. Routes: `dummyroute`(Straight), `demo_spline_route`(Spline 데모)

| 종류 | ID | Shared |
|------|-----|--------|
| Town | `basecamp`, `dummytown` | 있음 |
| Town | `demo_waypoint` | 데모(없을 수 있음 → Warning 허용) |
| Route | `dummyroute` | 있음 |
| Route | `demo_spline_route` | 데모 Spline |

### Prefab (InGame 배치용)

| Prefab | 용도 |
|--------|------|
| `05.UI/04_WorldMap/Prefabs/WorldMapUi.prefab` | **InGame에 붙이는 루트.** Controls + Open/Close + Panel + Root 포함 |
| `05.UI/04_WorldMap/Prefabs/WorldMapPanel.prefab` | 패널만 (WorldMapUi 내부/개별 구성용). 기본 `startHidden` |
| `05.UI/04_WorldMap/Prefabs/WorldMapRoot.prefab` | 맵 콘텐츠(마을/루트/캐러밴/라벨) |

### 4.4 InGame 연동

1. InGame 씬의 **월드맵 패널(UI 계층)** 에 `WorldMapUi.prefab`을 붙인다.
2. `WorldMapPanelControls`에서 `panel` / `openMapButton` / `closeMapButton` 참조를 확인한다. InGame HUD 버튼을 쓰면 해당 버튼으로 다시 연결한다.
3. `WorldMapPanel.TownClicked`를 구독해 추후 Trade Prepare와 연결한다.
4. Traveling 여부와 무관하게 버튼으로만 패널을 연다. 진행률·캐러밴은 패널이 열린 동안 Presenter가 `TryGetMapProgress`로 갱신한다.

```text
[InGame 월드맵 패널 슬롯]
  └─ WorldMapUi.prefab
        ├─ WorldMapPanelControls  (open/close → panel.Show/Hide)
        ├─ HudCanvas / OpenMapButton   (또는 InGame 열기 버튼으로 재연결)
        └─ WorldMapPanel               (닫혀 있으면 비활성)
              ├─ CloseMapButton
              └─ WorldMapRoot
```

씬/ Prefab 재생성:

```text
ND → World Map → Build WorldMapTest Scene
```

또는 batch:

```text
-executeMethod ND.UI.WorldMap.Editor.WorldMapTestSceneBuilderBatch.Run
```

---

## 5. Spline

- 패키지: `com.unity.splines` 2.9.0
- `RoutePathType.Spline` + `SplineContainer`
- Spline 누락 시 **직선 폴백** + Warning 로그

---

## 6. 확장 훅 (현재는 미연결)

```csharp
panel.TownClicked += townId => { /* 추후 Trade Prepare 연결 */ };
panel.RouteClicked += routeId => { /* 선택 UI */ };
```

`RouteMapPresentationResolver.GetDisplayState`에 이후 blocked / multiplier를 추가해도 `RouteVisual` API는 유지한다.

---

## 관련 문서

- [`Framework_World_Map_Usage_Guide.md`](./Framework_World_Map_Usage_Guide.md) — **처음 쓰는 사람용 사용 가이드**
- [`Framework_CoreServices_Team_Usage_Guide.md`](./Framework_CoreServices_Team_Usage_Guide.md)
- [`Docs/Personal_Documents/CSU/0715_world_map_phase1_spline_implementation.md`](../Personal_Documents/CSU/0715_world_map_phase1_spline_implementation.md)
- [`Docs/Personal_Documents/CSU/Handoff/07-15_World_Map_Implementation_Handoff.md`](../Personal_Documents/CSU/Handoff/07-15_World_Map_Implementation_Handoff.md)
