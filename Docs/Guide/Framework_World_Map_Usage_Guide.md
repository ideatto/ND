# Framework World Map 사용 가이드

처음 월드맵을 붙이거나 마을·루트를 추가하는 사람을 위한 설명서입니다.  
기술 API 상세는 [`Framework_World_Map_API_Guide.md`](./Framework_World_Map_API_Guide.md)를 참고하세요.

---

## 0. 한 줄로 이해하기

월드맵은 **무역을 시작하지 않습니다.**  
이미 Framework에 있는 마을·루트·무역 진행 정보를 **지도 위에 보여 주는 UI**입니다.

```text
논리 데이터 (게임플레이)          시각 맵 (표시만)
─────────────────────          ─────────────────
TownData / RouteData           TownWorldView Transform 위치
SharedGameData                 RouteVisual (직선/곡선)
TradeStart.TryStartTrade       WorldMapPanel Show/Hide
TradeProgress (UTC 진행률)      캐러밴 마커 · % 라벨
```

**중요:** 맵에 그린 선의 길이나 Spline 곡선 길이는 여행 거리·시간과 **무관**합니다.  
거리·시간은 항상 catalog에 등록된 `RouteData` / Shared의 논리 값을 씁니다.

---

## 1. 논리 데이터·카탈로그·맵 (경로를 헷갈리지 않기)

월드맵은 `99.Sandbox` 폴더만 스캔하지 **않습니다.**  
런타임에는 Framework **Shared catalog에 등록된 SO**만 ID로 조회합니다.

| 구분 | 경로 | 역할 |
|------|------|------|
| SO **타입(스크립트)** | `Assets/99.Sandbox/...` 의 `TownData`, `RouteData` 등 | 클래스 정의. 폴더 이름이 Sandbox여도 “데이터 범위=Sandbox만”은 아님 |
| **1차 빌드용 SO 에셋** | `Assets/_Project/02.Data/01_ScriptableObjects/` | 실제 게임에 쓸 Town/Route 등 내용 seed |
| **더미·레거시 테스트 SO** | `Assets/99.Sandbox/_LJH/02.SO/` 등 | 테스트용. ProjectData와 별도 watch root |
| **Shared catalog** | `Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset` | Framework가 읽을 SO **등록 목록** (이름이 Sandbox여도 1차 빌드 SO를 여기에 넣음) |
| **Watch inventory** | `Assets/_Project/11.CoreServices/Resources/SharedGameDataWatchInventory.asset` | catalog 누락 감시 |
| 맵이 런타임에 보는 것 | `ISharedGameDataProvider` (`FrameworkRoot.SharedGameData`) | catalog SO → `SharedTownDefinition` / `SharedRouteDefinition` |

정리:

- **“카탈로그”**라고 부를 대상 → Resources의 `SandboxSharedGameDataCatalog` (및 watch inventory).  
- **1차 빌드 마을/루트 에셋** → `02.Data/01_ScriptableObjects`.  
- **맵 Prefab의 `townId` / `routeId`** → Shared에 올라온 ID와 일치시킬 것.  
- 상세: [`Framework_Shared_Game_Data_Guide.md`](./Framework_Shared_Game_Data_Guide.md)

---

## 2. 어디에 무엇이 있나 (월드맵 Feature)

| 구분 | 경로 |
|------|------|
| Feature 루트 | `Assets/_Project/05.UI/04_WorldMap/` |
| 맵 Prefab | `Prefabs/WorldMapUi.prefab` (InGame 배치용 루트), `WorldMapPanel.prefab`, `WorldMapRoot.prefab` |
| 예시 씬 | `Scene/WorldMapTest.unity` |
| 논리 마을/루트 조회 | Shared (`TryGetTown` / `TryGetRoute`) — SO는 catalog 경유 |
| 진행 읽기 | `TradeProgressCoordinator.TryGetMapProgress` |
| 무역 출발 | `FrameworkRoot.Instance.TradeStart.TryStartTrade` (맵 밖) |

주요 스크립트:

| 이름 | 하는 일 |
|------|---------|
| `WorldMapPanel` | 맵 패널 열기(`Show`) / 닫기(`Hide`) |
| `WorldMapPanelControls` | 열기·닫기 버튼을 Show/Hide에 연결 |
| `WorldMapPresenter` | Shared·Save·진행률을 읽어 맵 갱신 |
| `TownWorldView` | 마을 마커 + 클릭 → `townId` 전달 |
| `RouteVisual` | 직선/곡선 루트 그리기 + 캐러밴 위치 계산 |
| `CaravanMapMarker` | 진행률에 따라 캐러밴 위치 표시 |

---

## 3. 전체 사용 흐름 (처음 붙일 때)

```text
① 논리 데이터 준비
   02.Data에 TownData / RouteData SO 작성(또는 수정)
   → Resources의 SandboxSharedGameDataCatalog에 등록
   → (필요 시) Watch Inventory refresh
   → Shared 로드 후 TryGetTown / TryGetRoute 로 확인

② 맵 Prefab에 시각 배치
   WorldMapRoot에 마을 Transform + townId
   루트 RouteVisual + routeId (직선 또는 Spline)

③ InGame UI에 월드맵 배치
   WorldMapUi Prefab을 InGame 월드맵 패널(또는 UI 계층)에 붙인다
   → WorldMapPanelControls에 열기/닫기 버튼·panel 참조를 확인·연결한다

④ (후속) 마을 클릭 → Trade Prepare UI 연결
   panel.TownClicked 구독

⑤ 무역은 기존 파이프라인으로 출발
   TradeStart.TryStartTrade(..., routeId)

⑥ 맵은 진행만 표시
   TryGetMapProgress / Traveling 화면 이벤트로 감지·갱신
```

예시 동작 확인만 할 때:

1. `WorldMapTest.unity` 연다.  
2. Play → **Open Map** → 맵 확인 → **Close Map**.  
3. EventSystem은 **Input System UI Input Module**이어야 한다 (`StandaloneInputModule` 금지).

---

## 4. 맵에 새 도시(마을)를 추가하려면

마을은 **데이터가 하나**, **맵 위 표시가 하나**입니다. 둘 다 맞추어야 합니다.

### 4-1. 논리 데이터 (필수)

1. 1차 빌드용이면 `Assets/_Project/02.Data/01_ScriptableObjects/`에 `TownData` 에셋을 만든다 (또는 기존 SO를 수정한다).  
2. `townId`를 정한다. 예: `riverside`.  
3. `Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset`의 towns 배열에 이 에셋을 **등록**한다.  
4. Watch inventory를 refresh해 ProjectData 누락이 없는지 확인한다.  
5. Boot/Loading으로 Shared가 로드된 뒤  
   `FrameworkRoot.Instance.SharedGameData.TryGetTown("riverside", out _)` 가 true인지 확인한다.  
6. **맵 좌표용 필드를 SO에 추가하지 않는다.** 위치는 Prefab Transform만 사용한다.  
7. 더미 테스트만 필요하면 Sandbox Legacy SO를 쓸 수 있으나, 1차 빌드 데이터는 `02.Data` + catalog 등록을 쓴다.

### 4-2. 맵 Prefab에 표시 (필수)

1. `WorldMapRoot.prefab` (또는 production용 복제 Prefab)을 연다.  
2. `TownLayer` 아래에 마을용 GameObject를 만든다.  
3. `TownWorldView`를 붙이고 Inspector의 **Town Id**에 논리 ID와 **완전히 같은 문자열**을 넣는다.  
   - 예: `riverside`  
4. Transform으로 맵 위 위치를 잡는다. (이게 맵 좌표의 source of truth)  
5. 아이콘 SpriteRenderer / 선택 링이 있으면 `TownWorldView`에 연결한다.  
6. Prefab을 저장한다.

`WorldMapPresenter`는 자식의 `TownWorldView`를 모아 `townId`로 찾습니다.  
ID가 중복되면 Console에 `[WorldMap] Duplicate townId` Error가 납니다.

### 4-3. unlock 표시

- Save의 `unlockedTownIds`에 있으면 해금 색.  
- 없더라도 Shared `UnlockedByDefault`이면 해금으로 본다.  
- 맵은 unlock을 **바꾸지 않고** 색만 표시한다.

---

## 5. 맵에 새 루트를 추가하려면

루트도 **논리 RouteData** + **맵 RouteVisual**이 한 쌍입니다.

### 5-1. 논리 데이터 (필수)

1. 1차 빌드용이면 `02.Data/01_ScriptableObjects/`에 `RouteData` 에셋을 만든다.  
2. 설정할 것:

| 필드 | 의미 |
|------|------|
| `routeId` | 맵·무역이 공유하는 ID. 예: `riverside_route` |
| `fromTown` | 출발 마을 `TownData` |
| `toTown` | 도착 마을 `TownData` |
| `distance` | 게임플레이 거리 |
| `defaultElapsedTime` | 기본 소요 시간 |
| `baseRiskLevel` | 맵 Risk 라벨에 표시 |

3. Shared catalog(`SandboxSharedGameDataCatalog`)의 routes 배열에 등록한다. From/To Town도 catalog towns에 있어야 한다.  
4. **시각 선 길이로 `distance`를 계산하지 않는다.**

### 5-2. 맵에 직선 루트 배치

1. `WorldMapRoot`의 `RouteLayer`에 GameObject를 만든다.  
2. `RouteVisual` + `LineRenderer`를 붙인다.  
3. Inspector:

| 항목 | 값 |
|------|-----|
| Route Id | 논리 `routeId`와 동일 |
| Path Type | `Straight` |
| Start Point | 출발 마을 Transform (또는 끝점용 빈 Transform) |
| End Point | 도착 마을 Transform |

4. Prefab 저장.

무역이 이 `routeId`로 Traveling이면 Presenter가 해당 루트를 활성 색으로 바꾸고, 캐러밴을 이 선 위에 올립니다.

---

## 6. 곡선형(Spline) 루트는 어떻게 배치하나

패키지: `com.unity.splines` (프로젝트에 설치됨).

### 절차

1. 루트 GameObject에 `SplineContainer`를 추가한다.  
2. Spline 편집으로 중간 곡선을 만든다. (출발·도착 근처를 마을 Transform에 맞추면 보기 좋음)  
3. 같은 오브젝트(또는 지정 오브젝트)에 `RouteVisual`을 둔다.  
4. Inspector:

| 항목 | 값 |
|------|-----|
| Route Id | Shared `routeId` |
| Path Type | `Spline` |
| Spline Container | 방금 만든 `SplineContainer` |
| Start / End Point | 폴백용으로 마을 Transform 연결 권장 |

5. Spline이 없거나 비어 있으면 **직선으로 폴백**하고 Warning이 납니다. 게임플레이는 그대로입니다.

### 기억할 것

- 곡선은 **예쁨용**입니다.  
- 캐러밴은 Spline을 `progress01`(0~1)로 따라갑니다.  
- `progress01`은 UTC 무역 시간에서 오며, Spline 길이에서 오지 않습니다.

---

## 7. 무역 출발지점 데이터는 어디서 연결하나

맵 Prefab 안이 **아닙니다.**

### 출발·도착의 기준

| 역할 | 위치 |
|------|------|
| 출발 마을 | `RouteData.fromTown` → `FromTownId` |
| 목적 마을 | `RouteData.toTown` → `ToTownId` |
| 어떤 길로 가는지 | `RouteData.routeId` |
| 실제 출발 실행 | `FrameworkRoot.Instance.TradeStart.TryStartTrade(caravan, distanceKm, tradeId, routeId)` |

`TryStartTrade`에 넘기는 **`routeId`**가 Save의 `tradeProgress.activeRouteId`로 기록됩니다.  
월드맵 Presenter는 이 `activeRouteId`와 같은 `RouteVisual`을 찾아 캐러밴을 그립니다.

```csharp
// 무역 준비 UI / 기존 출발 코드에서 (맵이 아님)
var result = FrameworkRoot.Instance.TradeStart.TryStartTrade(
    caravan,
    distanceKm,
    tradeId,
    routeId);   // ← 맵 RouteVisual.routeId 와 동일한 문자열

// canDepart 와 TradeStart.LastRecordSucceeded 를 함께 확인
```

정리:

- **출발지·목적지 도시 연결** = `RouteData`의 From/To Town.  
- **맵 마을 Transform** = 그 ID를 지도에 그려 줄 위치일 뿐.  
- 맵 스크립트는 `TryStartTrade`를 호출하지 않습니다.

---

## 8. 목적 도시는 어떻게 고르나

현재 단계 기준으로 역할이 나뉩니다.

### 8-1. 맵에서 하는 일 (이미 있음)

플레이어가 마을을 클릭하면:

1. `TownWorldView`가 `townId`로 클릭 이벤트를 낸다.  
2. `WorldMapPresenter`가 선택 표시(링/색)를 바꾼다.  
3. `WorldMapPanel.TownClicked`로 **문자열 townId**가 밖으로 나간다.

```csharp
// InGame에서 패널을 배치한 뒤
worldMapPanel.TownClicked += townId =>
{
    // 예: Trade Prepare UI에 목적지 후보로 넘긴다
    // 아직 프로젝트에 기본 연결은 없음 — 구독 측에서 구현
};
```

맵은 **목적지를 Save에 쓰지 않고**, “이 마을을 골랐다”는 신호만 줍니다.

### 8-2. 실제 무역 목적지로 확정하는 일 (맵 밖)

목적 도시가 확정되는 곳은 Trade Prepare / 출발 UI입니다.

1. 선택한 `townId`(또는 고른 `routeId`)로 어떤 `RouteData`를 쓸지 정한다.  
2. 그 루트의 `ToTownId`가 게임플레이상 목적 도시이다.  
3. `TryStartTrade(..., routeId)`로 출발한다.

지금은 **맵 클릭 → Trade Prepare 자동 연결은 미구현**입니다.  
UI 팀에 `TownClicked` / `RouteClicked`를 구독해 연결하면 됩니다.

---

## 9. UI 배치 방법 (InGame)

Scene Owner와 일정을 맞춘 뒤, **InGame 씬의 월드맵 패널(UI 계층)** 에 `WorldMapUi` Prefab을 붙인다.

권장 Prefab:

`Assets/_Project/05.UI/04_WorldMap/Prefabs/WorldMapUi.prefab`

이 Prefab 안에 이미 포함되는 것:

```text
WorldMapUi
  ├─ WorldMapPanelControls   ← panel / open / close 참조
  ├─ HudCanvas / OpenMapButton
  └─ WorldMapPanel (startHidden)
        ├─ PanelCanvas / CloseMapButton
        └─ MapHost / WorldMapRoot
```

### 배치 절차

1. InGame 씬에서 월드맵을 둘 UI 부모(월드맵 패널 슬롯)를 연다.  
2. `WorldMapUi.prefab`을 그 아래에 인스턴스한다.  
3. `WorldMapPanelControls` Inspector를 확인한다.  
   - `panel` → 자식 `WorldMapPanel`  
   - `openMapButton` → `HudCanvas/OpenMapButton` (또는 InGame HUD의 맵 열기 버튼)  
   - `closeMapButton` → 패널 쪽 Close 버튼 (또는 InGame 닫기 버튼)  
4. InGame 전용 버튼을 쓰려면 Prefab 기본 버튼 대신 **해당 버튼 참조만 다시 연결**하면 된다. 열기 버튼은 패널이 꺼져 있어도 눌려야 하므로 **패널 밖(항상 활성)** 에 둔다.  
5. EventSystem은 **Input System UI Input Module**을 쓴다.  
6. Canvas sorting이 다른 UI와 겹치면 sortingOrder를 조정한다.

표시 규칙:

- 열기 → `WorldMapPanel.Show()` → 패널 GameObject 활성  
- 닫기 → `WorldMapPanel.Hide()` → 패널 GameObject 비활성  
- Traveling / Preparation 화면 전환으로 **자동 열리지 않음**

참고:

- `WorldMapPanel.prefab` / `WorldMapRoot.prefab`은 `WorldMapUi` 내부 구성·콘텐츠용이다. InGame에는 보통 **`WorldMapUi`만** 붙인다.  
- 테스트: `WorldMapTest.unity`에도 동일 `WorldMapUi` 구조가 있다.

---

## 10. UI·데이터 연결 후, 무역 시작을 어떻게 감지하나

맵 Prefab은 출발을 하지 않으므로, **감지**는 Framework 쪽 API·이벤트를 씁니다.

### 방법 A — 진행 스냅샷 (맵·HUD 공통, 권장)

무역이 Traveling 또는 SettlementPending이면 `true`입니다.

```csharp
var coordinator = FrameworkRoot.Instance.TradeProgressCoordinator;
if (coordinator.TryGetMapProgress(out var snapshot))
{
    // 무역이 “진행 중(또는 도착 대기)”으로 감지됨
    string routeId = snapshot.ActiveRouteId;
    float percent = snapshot.ProgressPercent; // 0~100
    var state = snapshot.State;               // Traveling / SettlementPending
}
else
{
    // 활성 무역 맵 표시 대상 없음 (Preparation 등)
}
```

패널을 연 동안 `WorldMapPresenter`가 이 API로 캐러밴·%를 갱신합니다.  
직접 감지할 때도 같은 API를 쓰면 맵과 숫자가 일치합니다.

### 방법 B — 인게임 화면 상태 이벤트

출발이 기록되면 화면이 Traveling으로 바뀌는 흐름입니다.

```csharp
void OnEnable()
{
    FrameworkEvents.InGameScreenChanged += OnScreenChanged;
}

void OnDisable()
{
    FrameworkEvents.InGameScreenChanged -= OnScreenChanged;
}

void OnScreenChanged(InGameScreenState state)
{
    if (state == InGameScreenState.Traveling)
    {
        // 무역 이동 화면으로 전환됨 → 필요하면 맵을 Show() 하거나 알림
        // (기본 월드맵은 자동 Show하지 않음)
    }
}
```

### 방법 C — Save 상태 직접 확인

```csharp
var progress = FrameworkRoot.Instance.CurrentSaveData?.tradeProgress;
if (progress != null && progress.state == TradeProgressState.Traveling)
{
    // activeRouteId, tradeStartUtcTick, expectedTradeEndUtcTick
}
```

### 무엇을 쓰지 않나

- 캐러밴 Transform 위치를 Save에 저장해 “출발했는지” 판단 → **금지**  
- LineRenderer / Spline 길이 변화로 출발 감지 → **금지**  
- `WorldMapPresenter` / `RouteVisual` 안에서 `TryStartTrade` 호출 → **금지**

---

## 11. 데이터·표시가 맞는지만 빠르게 점검

| 확인 | 방법 |
|------|------|
| townId / routeId 철자 | Prefab Inspector ↔ `02.Data`(또는 catalog 등록 SO)의 `TownId`/`RouteId` 동일 |
| catalog 등록 | `SandboxSharedGameDataCatalog` towns/routes 배열에 에셋 포함 |
| Shared에 있는지 | Boot/Loading으로 Shared 로드 후 `TryGetTown` / `TryGetRoute`, Console `[WorldMap]` Warning |
| 데모 ID | `demo_waypoint` / `demo_spline_route`는 catalog/Shared에 없을 수 있음 (Warning 허용) |
| Open/Close | 버튼으로만 열림/닫힘 |
| 캐러밴 | Traveling + `activeRouteId`가 맵에 있는 route일 때만 경로 위에 표시 |
| Input | EventSystem = Input System UI Input Module |

`WorldMapTest`만 Play하면 Shared가 아직 안 로드되어 `[WorldMap]` Warning이 **안 나올 수 있습니다.**  
Error만 없으면 표시 테스트로는 Pass로 보면 됩니다.

---

## 12. 하지 말 것

- `TownData` / `RouteData` SO(및 Shared DTO)에 `mapPosition` 같은 맵 좌표 필드 추가  
- 시각 선·Spline 길이로 `distance` / 여행 시간 계산  
- 캐러밴 Transform을 SaveData에 저장  
- 월드맵 스크립트에서 무역 출발·정산·Save 쓰기  
- 1차 빌드 SO를 catalog에 넣지 않은 채 맵 ID만 맞추기 (`02.Data` 에셋은 Resources catalog 등록 필수)  
- “Sandbox 폴더만 논리 데이터의 전부다”고 가정하기 (런타임 범위는 **catalog 등록분**)  
- 승인 없이 `InGame.unity` 등 다른 팀 Scene 수정  

---

## 13. 관련 문서

| 문서 | 내용 |
|------|------|
| [`Framework_World_Map_API_Guide.md`](./Framework_World_Map_API_Guide.md) | API·스냅샷·테스트 씬 구조 |
| [`Framework_CoreServices_Team_Usage_Guide.md`](./Framework_CoreServices_Team_Usage_Guide.md) | TradeStart · 진행 · 정산 통합 |
| [`Framework_Shared_Game_Data_Guide.md`](./Framework_Shared_Game_Data_Guide.md) | Shared catalog · `02.Data` seed · watch |

Feature 구현 로그(개인):

- `Docs/Personal_Documents/CSU/0715_world_map_phase1_spline_implementation.md`
- `Docs/Personal_Documents/CSU/Handoff/07-15_world_map_phase1_spline_handoff.md`
