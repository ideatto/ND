# 0715 World Map 구현 로그

**작성일:** 2026-07-15  
**브랜치:** `feature/world-map-trade-progress`  
**Feature 루트:** `Assets/_Project/05.UI/04_WorldMap/`  
**관련 공용 가이드:**
- `Docs/Guide/Framework_World_Map_Usage_Guide.md` (사용법)
- `Docs/Guide/Framework_World_Map_API_Guide.md` (API)
- `Docs/Guide/Framework_Shared_Game_Data_Guide.md` (catalog / 02.Data)

---

## 1. 작업 목표와 완료 기준

### 목표

무역 논리 데이터·Shared·TradeProgress와 **분리된** 월드맵 UI를 제공한다.

- Phase 1: 직선 루트 + 캐러밴 progress % 표시
- Spline: 곡선 경로 시각화 (`com.unity.splines`)
- Coordinator 읽기 전용 스냅샷 API
- 예시 씬·Prefab·InGame 배치용 `WorldMapUi`
- 팀 공용 사용/API 가이드

### 완료 기준 (구현 시점)

- [x] `TryGetMapProgress` / `TradeMapProgressSnapshot` (UTC ticks + ProgressPercent)
- [x] `05.UI/04_WorldMap` Presenter · RouteVisual(Straight/Spline) · Panel · Controls · Prefab · 테스트 씬
- [x] 표시 정책: **버튼 Show/Hide** (Traveling 자동 Show 아님)
- [x] EventSystem: **Input System UI Input Module**
- [x] Guide: Usage + API + CoreServices 링크 · catalog/`02.Data` 경로 정정
- [ ] InGame.unity에 `WorldMapUi` 실배치 (Scene Owner 일정 — 가이드만 반영)
- [ ] Trade Prepare ↔ `TownClicked` 실연동
- [ ] Play Mode E2E (활성 무역 + 캐러밴 %) — InGame 소유권 이슈로 일부 스킵

### 제외 / 금지 (유지)

- Sandbox/`02.Data` SO에 `mapPosition` 필드 추가
- 시각 Line/Spline 길이로 gameplay distance·duration 계산
- 캐러밴 Transform Save 저장
- Presenter/RouteVisual에서 무역 출발·정산·Save 쓰기
- 승인 없는 production Scene 무단 수정
- 요청 전 commit / push / PR

---

## 2. 결정 사항

| 항목 | 결정 |
|------|------|
| 논리 소스 | `TownData` / `RouteData` SO + Shared catalog. 맵 좌표는 Prefab Transform |
| SO 위치 | 1차 빌드: `02.Data/01_ScriptableObjects`. 타입·더미: `99.Sandbox`. 등록: Resources `SandboxSharedGameDataCatalog` |
| 범위 | Phase 1 + Spline |
| RouteRuntimeState | 1차 제외. `RouteMapPresentationResolver`만 확장 지점 |
| Progress | `TryGetMapProgress` + ticks + `ProgressPercent` |
| 마을 클릭 | `TownClicked` 이벤트만 (Trade Prepare 미연결) |
| Risk | `SharedRouteDefinition.BaseRiskLevel`만 |
| 패널 표시 | **열기/닫기 버튼** → `Show`/`Hide`. Traveling 자동 연동 제거 |
| InGame 배치 | `WorldMapUi.prefab`을 월드맵 패널 계층에 붙이고 Controls로 버튼 연결 |

---

## 3. 구현·변경 파일

### CoreServices (승인된 외부 수정)

| 파일 | 내용 |
|------|------|
| `Scripts/TradeProgress/TradeMapProgressSnapshot.cs` | 스냅샷 값 객체 |
| `Scripts/TradeProgress/TradeProgressCoordinator.cs` | `TryGetMapProgress` 추가 |

### World Map (`05.UI/04_WorldMap`)

**Scripts**

| 파일 | 역할 |
|------|------|
| `RoutePathType.cs` | Straight / Spline |
| `RouteVisual.cs` | 경로 LineRenderer, EvaluatePosition, Spline 폴백 |
| `TownWorldView.cs` | townId · 클릭 · unlock/selected 색 |
| `CaravanMapMarker.cs` | progress01로 위치·방향 |
| `RouteMapPresentationResolver.cs` | unlock/active/completed 표시 상태 |
| `WorldMapPresenter.cs` | Shared/Save/스냅샷 연결, 룩업, %·Risk |
| `WorldMapPanel.cs` | Prefab 호스트, `Show`/`Hide` (패널 GameObject 활성) |
| `WorldMapPanelControls.cs` | Open/Close 버튼 → Show/Hide |
| `WorldMapTestEntry.cs` | 레거시 런타임 조립 (베이크 후 씬에서는 미사용) |

**Editor**

| 파일 | 역할 |
|------|------|
| `WorldMapTestSceneBuilder.cs` | 메뉴 베이크, Prefab 저장, InputSystemUIInputModule |
| `WorldMapTestSceneBuilderBatch.cs` | batch 진입점 |

**Prefabs / Scene / Art**

| 경로 | 역할 |
|------|------|
| `Prefabs/WorldMapUi.prefab` | **InGame 배치용 루트** (Controls + Open/Close + Panel + Root) |
| `Prefabs/WorldMapPanel.prefab` | 패널 (`startHidden`) |
| `Prefabs/WorldMapRoot.prefab` | 맵 콘텐츠 |
| `Scene/WorldMapTest.unity` | 예시·수동 테스트 씬 |
| `Art/` | 배경·마커 PNG, `RouteLine.mat` |

### Packages

- `com.unity.splines` 2.9.0 (`manifest.json` / lock)

### Docs

| 문서 | 내용 |
|------|------|
| `Docs/Guide/Framework_World_Map_Usage_Guide.md` | 팀 사용 가이드 (마을/루트 추가, Spline, 출발, 감지, WorldMapUi 배치) |
| `Docs/Guide/Framework_World_Map_API_Guide.md` | API · 테스트 씬 · Prefab |
| `Docs/Guide/Framework_CoreServices_Team_Usage_Guide.md` | §13 월드맵 + 링크 |
| `Docs/Personal_Documents/CSU/Handoff/07-15_world_map_phase1_spline_handoff.md` | 세션 핸드오프 |
| 본 문서 | 구현 로그 |

---

## 4. 아키텍처·데이터 흐름

### 4-1. 논리 vs 시각

```text
논리 (게임플레이)                          시각 (맵 UI)
─────────────────────────────────          ─────────────────────────────
TownData / RouteData (02.Data seed)        TownWorldView Transform
SandboxSharedGameDataCatalog               RouteVisual Straight/Spline
ISharedGameDataProvider                    CaravanMapMarker
TradeStart.TryStartTrade(routeId)          WorldMapPanel Show/Hide
SaveData.tradeProgress (UTC)               Progress % / Risk 라벨
```

### 4-2. 진행 표시

```text
SaveData.tradeProgress
        │
        ▼
TradeProgressCoordinator.TryGetMapProgress
        │  Progress01 / ProgressPercent / ActiveRouteId / State
        ▼
WorldMapPresenter
        ├─ RouteVisual.EvaluatePosition(progress01)
        ├─ CaravanMapMarker
        └─ 라벨 + 활성 루트 하이라이트
```

진행률 (Coordinator와 동일 계열):

```text
Traveling: Progress01 ≈ (CurrentUtc - start) / (end - start)
Pause 중: ActiveCaravan.progress01 우선
SettlementPending: Progress01 = 1
ProgressPercent = Progress01 * 100
```

갱신 트리거:

- Traveling 중 `Update`에서 `TryGetMapProgress` 폴링 (주 경로)
- `FrameworkEvents.InGameScreenChanged` / `LoadCompleted` → `RefreshAll`
- 패널 `Show()` → `RefreshAll`

### 4-3. InGame 배치 계약 (최종)

```text
[InGame 월드맵 패널 슬롯]
  └─ WorldMapUi.prefab
        ├─ WorldMapPanelControls
        ├─ HudCanvas / OpenMapButton   (또는 InGame 버튼 재연결)
        └─ WorldMapPanel
              ├─ CloseMapButton
              └─ WorldMapRoot
```

---

## 5. 세션 중 주요 변경 이력

| 순서 | 내용 |
|------|------|
| 1 | Phase1 + Spline + `TryGetMapProgress` + 테스트 씬/Prefab 초기 구현 |
| 2 | Traveling 자동 Show/Hide → **버튼 Show/Hide** (`WorldMapPanelControls`) |
| 3 | `StandaloneInputModule` → **`InputSystemUIInputModule`** (프로젝트 Input System only) |
| 4 | 사용 가이드 작성 · Shared를 “Sandbox만”으로 오해하지 않도록 **02.Data + catalog** 문구 정정 |
| 5 | InGame 배치를 **`WorldMapUi.prefab`** 기준으로 가이드 갱신 |

초기 핸드오프 §10(InGame Prefab 배치·E2E)은 Scene Owner 일정으로 후속.  
표시 정책은 핸드오프 초안의 Traveling 자동 Show와 다르며, **버튼 제어가 최종**이다.

---

## 6. 검증

| 항목 | 결과 |
|------|------|
| 코드 검토 | WorldMap namespace · Framework SaveData alias |
| batch 베이크 | 씬·Prefab 생성 성공 (재베이크 시 Input System 모듈 유지) |
| Play — Input 예외 | `InputSystemUIInputModule`로 해소 |
| Play — Open/Close | 맵 Show/Hide 동작 확인 (수동) |
| Shared `[WorldMap]` Warning | WorldMapTest 단독 Play 시 Shared 미로드 → 로그 0은 정상 가능 |
| InGame E2E 캐러밴·% | 미실시 (InGame Scene 소유권) |
| commit / push / PR | 미실시 (요청 전) |

---

## 7. 알려진 리스크·주의

- `demo_spline_route` / `demo_waypoint`는 Shared에 없을 수 있음 (데모 Warning 허용)
- Sandbox 전역 `SaveData`와 Framework 타입 공존 — WorldMap은 `ND.Framework` alias 필수
- Overlay Canvas가 InGame UI와 겹칠 수 있음 — sorting/카메라 정리 필요
- `WorldMapUi`만으로는 InGame에 자동 표시되지 않음 — Scene에 인스턴스 배치 필요
- catalog 미등록 `02.Data` SO는 Shared에 안 올라감 → 맵 ID만 맞추면 부족

---

## 8. 후속 작업

1. InGame Scene Owner와 `WorldMapUi.prefab` 배치 · 버튼 재연결  
2. `TownClicked` → Trade Prepare UI  
3. Traveling 상태 Play Mode E2E (캐러밴·ProgressPercent)  
4. (선택) production용 `WorldMapRoot`에 실마을/실루트 ID·Transform 배치  
5. 요청 시 commit / PR (`base: dev2`)

---

## 9. 빠른 참조

| 하고 싶은 일 | 어디를 보나 |
|--------------|-------------|
| 마을/루트 추가 | Usage Guide §4–§6 |
| Spline 배치 | Usage Guide §6 |
| 출발·목적지 데이터 | Usage Guide §7–§8 (`RouteData` + `TryStartTrade`) |
| 무역 시작 감지 | Usage Guide §10 (`TryGetMapProgress` / `InGameScreenChanged`) |
| InGame에 붙이기 | Usage Guide §9 (`WorldMapUi`) |
| Shared catalog | `Framework_Shared_Game_Data_Guide.md` |

메뉴 베이크:

```text
ND → World Map → Build WorldMapTest Scene
```
