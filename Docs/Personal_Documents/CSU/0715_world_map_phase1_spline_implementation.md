# 0715 World Map Phase 1 + Spline Implementation Log

> **통합 로그로 이관:** 이후 버튼 Show/Hide · Input System · `WorldMapUi` · Usage Guide · catalog 경로 정정까지 포함한 최종 정리는  
> [`0715_world_map_implementation_log.md`](./0715_world_map_implementation_log.md) 를 본다.  
> 아래는 초기 Phase1+Spline 세션 시점의 원본 기록이다.

---

## 1. 작업 요약

월드맵 Phase 1(직선 루트 + 캐러밴 progress %)과 Spline 경로 표시를 구현했다.

- 브랜치: `feature/world-map-trade-progress`
- 논리 데이터: 기존 Sandbox `TownData` / `RouteData` + `ISharedGameDataProvider` 재사용 (Q1-A)
- 진행 읽기: `TradeProgressCoordinator.TryGetMapProgress` + `TradeMapProgressSnapshot` (ticks 포함)
- 비주얼: `Assets/_Project/05.UI/04_WorldMap`
- 예시 씬: `Assets/_Project/05.UI/04_WorldMap/Scene/WorldMapTest.unity`
- Spline 패키지: `com.unity.splines` 2.9.0 (사전 설치됨)

---

## 2. 결정 사항 (Q1–Q6)

| 항목 | 결정 |
|------|------|
| Q1 논리 소스 | 기존 SO + Shared 재사용. 맵 좌표는 씬 Transform |
| Q2 범위 | Phase 1 + Spline |
| Q3 RouteRuntimeState | 1차 제외. Resolver로 확장 지점만 확보 |
| Q4 Progress | Coordinator 읽기 API + ticks + ProgressPercent |
| Q5 마을 클릭 → 준비 UI | 미연결. `TownClicked` 이벤트만 노출 |
| Q6 Risk | `BaseRiskLevel` 표시만 |

---

## 3. 구현 파일

### CoreServices

- `Scripts/TradeProgress/TradeMapProgressSnapshot.cs`
- `Scripts/TradeProgress/TradeProgressCoordinator.cs` — `TryGetMapProgress` 추가

### World Map (`05.UI/04_WorldMap`)

- `Scripts/RoutePathType.cs`
- `Scripts/RouteVisual.cs` — Straight / Spline, LineRenderer, fallback
- `Scripts/TownWorldView.cs`
- `Scripts/CaravanMapMarker.cs`
- `Scripts/RouteMapPresentationResolver.cs`
- `Scripts/WorldMapPresenter.cs`
- `Scripts/WorldMapPanel.cs` — InGame 패널 show/hide · Traveling 연동
- `Scripts/WorldMapTestEntry.cs` — 레거시 런타임 생성(베이크 후 씬에서는 Prefab 패널 사용)
- `Editor/WorldMapTestSceneBuilder.cs` — 메뉴/배치 베이크 + Prefab 저장
- `Editor/WorldMapTestSceneBuilderBatch.cs` — batch 진입점
- `Scene/WorldMapTest.unity` — Camera + WorldMapPanel
- `Prefabs/WorldMapRoot.prefab`
- `Prefabs/WorldMapPanel.prefab`

### Docs

- `Docs/Guide/Framework_World_Map_API_Guide.md`
- 본 문서
- `Docs/Guide/Framework_CoreServices_Team_Usage_Guide.md` 링크 보강

---

## 4. 데이터 흐름

```text
SaveData.tradeProgress (UTC ticks, state, activeRouteId)
        │
        ▼
TradeProgressCoordinator.TryGetMapProgress
        │  Progress01 / ProgressPercent / ticks
        ▼
WorldMapPresenter
        ├─ RouteVisual.EvaluatePosition(progress01)
        ├─ CaravanMapMarker
        └─ Progress % label + BaseRiskLevel label
```

진행률 공식(Coordinator 내부와 동일):

```text
Progress01 = (CurrentUtc - startUtc) / (endUtc - startUtc)
ProgressPercent = Progress01 * 100
```

Pause 중 Traveling이면 `ActiveCaravan.progress01`을 사용해 화면이 멈추도록 했다.

---

## 5. 예시 씬 사용법

1. `WorldMapTest.unity` 연다.
2. Play — `WorldMapTestEntry`가 예시 맵을 생성한다.
3. (선택) Edit 모드 베이크: `ND → World Map → Build WorldMapTest Scene`  
   - Unity가 다른 인스턴스로 열려 batchmode가 막힌 경우 이 메뉴를 사용한다.

예시 ID:

| 종류 | ID | Shared |
|------|-----|--------|
| Town | `basecamp`, `dummytown` | 있음 |
| Town | `demo_waypoint` | 데모 전용 |
| Route | `dummyroute` | 있음 (Straight) |
| Route | `demo_spline_route` | 데모 Spline (고아 ID Warning 가능) |

무역이 활성일 때(`dummyroute`) 캐러밴과 %가 갱신된다. Framework Boot 루프와 함께 검증하는 것을 권장한다.

---

## 6. 검증 체크리스트

- [ ] `TryGetMapProgress`가 Traveling / SettlementPending에서 true
- [ ] Progress % 라벨 갱신
- [ ] `dummyroute` Straight LineRenderer 표시
- [ ] `demo_spline_route` Spline 표시
- [ ] Spline 제거 시 직선 폴백 Warning
- [ ] 씬 재진입 시 Transform 저장 없이 progress로 위치 재구성
- [ ] 마을 클릭 시 선택 링 + `TownClicked` (무역 출발 없음)
- [ ] Console에 예상치 못한 오류 없음

Unity Editor 컴파일/Play는 이 작업 환경에서 열린 Editor 인스턴스 때문에 batch 검증을 완료하지 못했다. Editor에서 위 체크리스트를 확인한다.

---

## 7. 이번 범위에서 하지 않은 것

- Sandbox `TownData` / `RouteData` 필드 추가
- `SaveData` / `RouteRuntimeState`
- Trade Prepare UI 연동
- zoom/pan, road mesh, pathfinding
- 다른 production 씬 수정

---

## 8. 추후 변경 권장 API 구조

아래는 **아직 구현하지 않은** 권장안이다.

### 8.1 `ITradeMapProgressProvider`

Coordinator에 직접 의존하지 않도록 UI 어댑터 인터페이스를 둘 수 있다.

```csharp
public interface ITradeMapProgressProvider
{
    bool TryGetMapProgress(out TradeMapProgressSnapshot snapshot);
}
```

`TradeProgressCoordinator`가 구현하거나, thin wrapper가 `FrameworkRoot`에서 위임한다.

### 8.2 `RouteRuntimeState` + Save

```csharp
[Serializable]
public sealed class RouteRuntimeState
{
    public string routeId;
    public bool isDiscovered;
    public bool isBlocked;
    public float dangerMultiplier = 1f;
    public float speedMultiplier = 1f;
}
```

`WorldSaveData.routeStates`에 목록을 두고, `RouteMapPresentationResolver.GetDisplayState`가 blocked/highlight를 반영한다. `RouteVisual`은 결과 플래그만 받는다.

### 8.3 Shared 표시 메타

아트 파이프라인이 데이터 주도 배치를 원하면:

- `SharedTownDefinition`에 Icon / 선택적 mapPosition
- 또는 Feature 전용 `WorldMapVisualCatalog` SO (`townId` → sprite/좌표 override)

논리 `TownData`에 runtime 상태를 넣지 않는다.

### 8.4 World Map → Trade Prepare bridge

```csharp
public interface IWorldMapTradePrepareBridge
{
    void FocusTown(string townId);
    void FocusRoute(string routeId);
}
```

`WorldMapPresenter.TownClicked` / `RouteClicked`를 브리지가 구독해 기존 `TownRoutePanel` 선택 상태로 연결한다. Presenter는 계속 읽기·표시만 담당한다.

### 8.5 화면 상태

맵이 Traveling overlay를 넘어 독립 화면이 되면 `InGameScreenState` 확장 또는 별도 Map panel 상태를 검토한다. 1차에서는 `InGameScreenChanged` 구독만으로 충분하다.

---

## 9. 관련 문서

- Handoff: `Docs/Personal_Documents/CSU/Handoff/07-15_World_Map_Implementation_Handoff.md`
- Guide: `Docs/Guide/Framework_World_Map_API_Guide.md`
- 기존 인게임 패널 라우팅: `Docs/Personal_Documents/CSU/0710_InGame_Scene_Route_UI_Guide.md` (월드맵은 additive)
