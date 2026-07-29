# feat(village+minimap): 마을·무역마을·미니맵 v2 + 격자 좌표계·바람·구름 날씨 레이어 통합 (프리팹화)

> 이전 PR #260(닫힘)의 내용 전체 + 이후 작업(격자 좌표계/지형, 기압 바람 시스템, 구름/먹구름 날씨 레이어, 이벤트 배치, 에디터 디버그 툴) + **dev2 최신화 머지**를 한 브랜치에 담았습니다.

## 1. 개요
거점 마을 · 무역마을 · 미니맵 v2를 하나로 통합하고, 그 위에 **미니맵 격자 좌표계 → 기압 기반 바람 → 구름/먹구름 날씨 레이어**를 프로토타입으로 얹었습니다. 전부 재사용 프리팹으로 패키징. **공유 씬/프리팹(InGame.unity · MainUICanvas.prefab)은 변경하지 않았고**, 프리팹 드롭 방식으로 얹도록 설계했습니다. dev2 최신화 머지 포함(충돌 0, 컴파일·런타임 에러 0 확인).

## 2. 추가된 기능
- **거점 마을**: 그리드 건물 배치, 카메라 팬/줌, 건물 드래그·회전·겹침 방지, 니즈 기반 NPC(건물이 있는 니즈만 타겟, 빈터 상호작용 제거)
- **무역마을 (한 맵)**: 거점 + 무역마을(RiverTown/MountTown/WindyTown)을 `Village_Home` 한 씬에 좌표를 떨어뜨려 배치. VillageCamera 이동으로 전환 → 나중에 하나의 큰 맵으로 확장 가능
- **미니맵 v2**: 실제 맵 아트 배경, 확대/축소·드래그(콘텐츠 기반 경계 자동 산출), 무역마을 클릭 진입, 거점 항상 접근, 상단 이름표, 다중 캐러밴 이동 시연 테스트 패널
- **🆕 미니맵 격자 좌표계 + 지형**: 배경 위 24×16 격자, 셀↔월드 좌표 변환, 셀별 지형(평지·풀·숲·논밭·강·강변·다리·산·호수·구름). ASCII 지형 텍스트맵 로드/저장, 페인트 툴로 편집. 격자 디버그 오버레이(셀 지형색 + `A1` 좌표 라벨)
- **🆕 기압 기반 바람 시스템(프로토)**: 바람 = `-∇P`(고→저기압) + swirl 회전. 기압원 = **계절(여름↔겨울 주풍 반전)** + 떠도는 배경 + **이벤트(큰불=저기압/메테오=고기압)** + **산 지형(고기압 → 바람 우회)**. 화살표 시각화 디버그
- **🆕 구름/먹구름 날씨 레이어(프로토)**: 코드 베이킹한 임시 구름이 바람을 따라 흐름. **강/호수 위 → 수분↑ → 먹구름**(진한색·불투명·아래 정렬·빠른 이동), **먹구름이 물 벗어나면 비 뿌리며 소멸**. 4방향 + 물 위 지속 생성, SpriteMask로 격자 밖 클리핑, 현실적 속도(셀 통과 ~20초)
- **🆕 이벤트 클릭 배치**: 큰불/메테오 버튼 무장 후 미니맵 클릭한 좌표에 기압 이벤트 발생 → 바람 실시간 반응
- **🆕 에디터 디버그 툴 프리팹**: 페인트 툴·이벤트 배치 툴을 런타임(에디터)에서 미니맵에 얹는 설치기 프리팹

## 3. 사용한 프레임워크 API (전부 읽기 전용 — 남의 코드 미수정)
| API | 용도 |
|---|---|
| `FrameworkRoot.Instance.CurrentSaveData` | SaveData 접근 |
| `TradeProgressCoordinator.TryGetMapProgress(out TradeMapProgressSnapshot)` | 진행 스냅샷(HasActiveTrade/ActiveRouteId/State/Progress01) |
| `GameTime.CurrentUtc` | 진행률 시계(== `DateTime.UtcNow`, 실시간) |
| `SharedGameData.TryGetTown(id, out SharedTownDefinition)` | 마을 DisplayName |
| `SaveData.caravans` / `SaveData.tradeProgressEntries` | 캐러밴·진행 조회 |
| `ND.UI.WorldMap`: `WorldMapPresenter` / `CaravanMapMarker` / `RouteVisual` / `SlidePanel` | 미니맵 표시·경로·패널 토글 |
| 🆕 `WorldSaveData.currentSeasonId` (읽기) | 바람 계절 기본 기압 결정(여름/겨울 반전). 값 변경은 디버그 버튼으로만 |

**진행 애니메이션 원리**: `WorldMapPresenter`가 매 프레임 `TryGetMapProgress`를 읽어 마커를 `RouteVisual` 위로 이동. `progress = (CurrentUtc - tradeStartUtcTick) / (expectedTradeEndUtcTick - tradeStartUtcTick)`

## 4. 프레임워크에서 수정/논의가 필요한 부분 (정헌님/성욱님)
1. **다중 캐러밴 정식 표시** — 현재 정식 미니맵은 `selectedCaravanId` 1대만. `Coordinator`에 `GetAllMapProgress()`(state==Traveling 전부) 추가 + `WorldMapPresenter` 다중 마커화 필요. (임시로 이 PR의 `MinimapMultiCaravanMarkers`가 클라이언트 측에서 대행 — 정식 반영 시 제거 가능)
2. 🆕 **계절 자동 진행 없음** — `currentSeasonId`는 저장 문자열이고 시간→계절 전환 로직이 없음(디버그로만 변경). 바람/날씨가 계절에 자동 반응하려면 계절 회전 시스템 필요. `GameTimeService`는 "인게임 시간 배율"이라 하루/낮밤/계절 개념 없음.
3. 🆕 **날씨→실제 효과 API 없음** — `RouteEvent.Weather`는 stub(효과 없음), route 이벤트 데이터도 비어 있음. "먹구름/비 밑 셀 = 실제 이벤트(식량↓·지연 등)"로 연결하려면 Core에 위치/날씨 이벤트 효과 API 신설 필요. (지금은 표시/연출만, `ForceRouteEvent` 훅만 존재)
4. **부가**: 마을 한글 이름은 `SharedGameData.DisplayName` 채우면 이름표에 자동 반영.

## 5. 프리팹 목록
| 프리팹 | 경로 | 비고 |
|---|---|---|
| `WorldMapRenderRootV2` | `08.Prefabs/UI/Maps/` | 미니맵 렌더 소스. **격자·바람·구름 컴포넌트 전부 포함** |
| `WorldMapPanelV2` (패널 교체형) | `08.Prefabs/UI/Maps/` | RawImage=V2 RT, 카메라·라우터 내장 |
| `HomeButton` / `TownNameLabel` / `MinimapCaravanTestPanel` | `08.Prefabs/UI/Maps/` | 선택 UI·테스트 |
| 🆕 `MinimapEditorTools` | `08.Prefabs/UI/Maps/` | **에디터 전용** 디버그 툴(페인트·이벤트배치) 설치기 |
| `VillageWorldTowns` | `08.Prefabs/Village/` | 무역마을 배치 |

관련 스크립트: `01.Core/07_Village/YHY/` — `BuildingPlacementController`, `VillageNpc`, `MinimapCameraController`, `MinimapTownClickRouter`, `MinimapMultiCaravanMarkers`, `TradeTownCameraMover`, `HomeViewButton`, `CurrentTownNameLabel`, `MinimapCaravanTestPanel`, 🆕 `MinimapGrid`·`MinimapCell`·`MinimapGridDebug`·`MinimapGridPainter`·`MinimapWind`·`MinimapWindDebug`·`MinimapClouds`·`MinimapEventPlacer`·`MinimapDebugToolsInstaller`

> ⚠️ **경로 갱신(0729)**: 위 미니맵·날씨 스크립트는 이후 `01.Core/08_Minimap/`(미니맵)·`01.Core/09_Weather/`(바람·구름·날씨이벤트)로 분리됨(GUID 유지). `BuildingPlacementController`·`VillageNpc`만 `07_Village`에 잔류. 자세히는 [[0729_weather_reorg_and_systems]].

## 6. 실제 씬(InGame) 통합 방법 — 상세 (설정방법)

> ⚠️ `MainUICanvas.prefab`은 공유 프리팹이라 Apply(프리팹 반영) 금지. 아래는 전부 씬 안에서의 작업(씬 오버라이드)입니다.

### (A) 미니맵 렌더 소스 추가 — 빠뜨리면 미니맵이 빈 화면
1. `08.Prefabs/UI/Maps/WorldMapRenderRootV2.prefab` 을 Hierarchy 루트에 드래그.
2. 위치는 아무데나 OK(경계 자동 산출). 이걸 넣어야 `WorldMapRenderTextureV2`에 그림이 그려집니다.
3. 🆕 **격자·바람·구름은 이 프리팹에 이미 포함**되어 있어 추가 배선 불필요. 실행 시 격자/바람/구름 디버그 버튼이 화면에 뜹니다.

### (B) 미니맵 패널 교체 (RectTransform 복사/붙여넣기 포함)
1. `MainUICanvas/WorldMapPanel`(기존) 선택 → RectTransform 헤더 우클릭 → **Copy Component**.
2. `WorldMapPanelV2.prefab` 을 `MainUICanvas`의 자식으로 드래그.
3. 추가된 V2 패널의 RectTransform 헤더 우클릭 → **Paste Component Values** → 기존과 동일 위치·크기·앵커.
4. 기존 `WorldMapPanel`은 비활성화 또는 삭제(참조 있으면 V2로 재연결).
5. 배선 불필요 — V2는 RawImage=V2 RT, 카메라·라우터 내장, 지도버튼→SlidePanel 토글이 프리팹 내부 참조. Router `renderRoot`는 런타임 자동 탐색.

### (C) 빌리지 뷰 상호작용이 안 될 때 (VillageView 중복/가림)
- 증상: 뷰는 보이는데 확대/축소·드래그·건물이동 안 됨 → `VillageView`가 2개라 뒤 sibling이 포인터 가로챔.
- 해결: `BuildingPlacementController` 붙은 것만 남기고 중복 `VillageView` 삭제. 다른 raycast 패널에 안 가리게 sibling 순서 조정. `EventSystem`=InputSystemUIInputModule, GraphicRaycaster, RawImage Raycast Target=ON 확인.

### (D) 선택 UI 추가
- `HomeButton.prefab`(거점) / `TownNameLabel.prefab`(이름표) 을 `MainUICanvas` 하위에 드롭(위치는 Copy→Paste로 맞춤). `MinimapCaravanTestPanel.prefab`은 테스트 전용 — 확인 후 제거.

### (E) 🆕 에디터 디버그 툴 (지형 페인트 · 이벤트 배치) — 선택
- 개발 씬에 `08.Prefabs/UI/Maps/MinimapEditorTools.prefab` 을 하나만 드롭.
- 실행하면(에디터에서만) 미니맵 RawImage에 **페인트 툴·이벤트 배치 툴을 자동 설치**. 공유 프리팹은 안 건드림. 빌드에는 안 뜸(`editorOnly`). 필요 없으면 프리팹만 빼면 됨.

## 7. 주의 / 열린 질문
- `InGame.unity`는 이 PR에서 미변경 → 실제 배선은 위 (A)~(E)로 각자 적용. (개발 검증은 `InGame_Test.unity` 샌드박스에서 함)
- 격자·바람·구름·이벤트는 **표시/연출 프로토타입**(게임플레이 영향 없음, 프레임워크 독립). 실제 효과·계절 진행·다중 캐러밴 정식화는 §4 협의 항목.
- 미니맵 격자는 셀당 월드 0.64u = 아직 실거리 미확정. 구름 속도 등은 실거리 확정 시 재튜닝 가능.
