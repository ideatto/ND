# World Map First-Build Prefab Handoff

**작성일:** 2026-07-15  
**브랜치:** `ui/prefab/creating-map-prefabs-based-on-the-first-build`  
**Feature 루트:** `Assets/_Project/05.UI/04_WorldMap/`  
**관련 문서:**
- `Docs/Guide/Framework_World_Map_Usage_Guide.md` (사용법 · WorldMapUi 배치 · Spline)
- `Docs/Guide/Framework_World_Map_API_Guide.md`
- `Docs/Personal_Documents/CSU/0715_world_map_implementation_log.md` (통합 구현 로그)
- `Docs/Personal_Documents/CSU/Handoff/07-15_world_map_phase1_spline_handoff.md` (초기 Phase1 핸드오프, 일부 구식)
- 선행 머지: PR `#112` (`feature/world-map-trade-progress` → `dev2`)

---

## 1. 작업 목표

- 목표: 1차 빌드용 `02.Data` Town/Route ID에 맞춰 `WorldMapRoot` Prefab에 실마을·실루트를 배치하고, 맵 UI(`WorldMapUi`)와 가이드·진행 API가 이 데이터와 맞게 동작하도록 한다.
- 완료 기준:
  - `WorldMapRoot`에 1차 빌드 townId/routeId 배치 (Transform이 맵 좌표)
  - Shared catalog 등록 ID와 Prefab Inspector ID 일치
  - Spline 루트는 Tangent Mode Auto/Bezier로 곡선 표시 가능
  - InGame 배치는 Scene Owner 일정 (가이드는 `WorldMapUi.prefab` 기준 반영됨)
- 제외 범위:
  - `TownData`/`RouteData`에 `mapPosition` 필드 추가
  - 시각 길이로 gameplay distance/duration 계산
  - 캐러밴 Transform Save 저장
  - 승인 없는 `InGame.unity` 수정
  - Trade Prepare ↔ `TownClicked` 실연동 (후속)

---

## 2. 현재 상태

| 구분 | 내용 |
|------|------|
| 완료 | Phase1+Spline·`TryGetMapProgress`·버튼 Show/Hide·InputSystemUIInputModule·Usage/API 가이드·`WorldMapUi` Prefab·PR `#112` 머지 |
| 진행 중 | 1차 빌드 기준 `WorldMapRoot` Prefab에 실마을/실루트 배치 (현재 브랜치, **미커밋**) |
| 미완료 | Prefab ID 검수·오타 수정, Spline Tangent Mode 정리, InGame `WorldMapUi` 배치, Traveling E2E, Trade Prepare 연결 |
| 차단됨 | InGame Scene Owner 일정 (배치·E2E) |

---

## 3. 핵심 결정 사항

- 논리 데이터: `02.Data/01_ScriptableObjects` seed + Resources `SandboxSharedGameDataCatalog` (맵이 `99.Sandbox` 폴더만 보지 않음)
- 맵 좌표: Prefab Transform only
- Progress: `TryGetMapProgress` (폴링) + `InGameScreenChanged`/`LoadCompleted`로 Refresh
- 패널 표시: **열기/닫기 버튼** → `WorldMapPanel.Show`/`Hide` (Traveling 자동 Show 아님)
- InGame 배치: **`WorldMapUi.prefab`** 을 월드맵 패널 계층에 붙인 뒤 `WorldMapPanelControls`로 버튼 연결
- Spline: knot만 추가하면 Linear(꺾인 직선)일 수 있음 → **Auto/Bezier** 필요
- Risk: `BaseRiskLevel`만
- RouteRuntimeState: 1차 제외

---

## 4. 변경 파일

### 이미 머지됨 (`feature/world-map-trade-progress` / PR `#112`)

- `05.UI/04_WorldMap/` 전체 (Scripts, Editor, Prefabs, Scene, Art)
- `TradeMapProgressSnapshot.cs`, `TradeProgressCoordinator.TryGetMapProgress`
- Guide·개인 로그, `com.unity.splines`

### 현재 브랜치 워킹트리 (미커밋 — git status 기준)

**수정**

- `Assets/_Project/05.UI/04_WorldMap/Prefabs/WorldMapRoot.prefab` — 1차 빌드 마을/루트로 교체·확장 (대량 diff)
- `Assets/_Project/05.UI/04_WorldMap/Prefabs/WorldMapUi.prefab`
- `Assets/_Project/05.UI/04_WorldMap/Scene/WorldMapTest.unity`
- `Assets/_Project/05.UI/04_WorldMap/Art/*.png.meta` (importer/meta)
- `Assets/_Project/02.Data/01_ScriptableObjects/Routes/Route_RiverToBase.asset` — 외부 데이터 수정 (승인 범위 확인 필요)

**생성 / 삭제**

- 이번 워킹트리 기준 신규/삭제 없음 (위 수정만)

### 승인된 외부 수정

- CoreServices TradeProgress 읽기 API — 선행 PR에서 승인·머지
- `WorldMapTest.unity` — Feature 테스트 씬 허용
- `02.Data` Route SO 수정 — **미커밋. 의도·승인 여부 다음 세션에서 확인**

### Scene / Prefab / Meta / Package / 데이터 변경 여부

- Scene 변경: Yes (`WorldMapTest.unity`, InGame 미수정)
- Prefab 변경: Yes (`WorldMapRoot`, `WorldMapUi`)
- Meta 변경: Yes (Art png.meta)
- Package 변경: No (이번 워킹트리)
- ScriptableObject 또는 데이터 변경: Yes (`Route_RiverToBase.asset` 미커밋)

---

## 5. 구현된 동작

- 맵은 무역을 시작하지 않음. `TryGetMapProgress`로 Traveling/SettlementPending 진행·캐러밴·% 표시
- `WorldMapUi`: Controls + Open/Close + Panel(`startHidden`) + Root
- Prefab 배치 흐름: TownLayer에 SpriteRenderer·Collider·`TownWorldView`+townId, RouteLayer에 LineRenderer·`RouteVisual`+routeId
- 현재 `WorldMapRoot`에 배치된 ID(워킹트리 기준):
  - Towns: `MountTown`, `WindyTown`, `RiverTown`, `BaseCampf` ← **오타 의심** (SO는 `BaseCamp`)
  - Routes: `RiverToMount2`, `MountToBase`, `BaseToWindy`, `RiverToMount`, `BaseToRiver`, `RiverToBase`
- catalog/`02.Data`와 맞출 때: `Routes/Route_BaseToRiver.asset`의 `routeId`는 `BaseToRiver`이나, 루트에 남은 `Route_BaseToRiver.asset`은 `BaseRoute`인 중복/레거시 가능성 있음

---

## 6. 검증 결과

- 코드 검토: 선행 PR 기준 Phase1+Spline·버튼 UI·Input System 반영됨
- Unity Console: `WorldMapTest`에서 InputSystemUIInputModule 적용 후 Input 예외 해소 확인
- 테스트한 항목: Open/Close Map, 마을·루트 표시; Spline은 Linear면 꺾인 선 두 개로 보이는 현상 확인·원인(Tangent Mode) 파악
- 테스트하지 못한 항목: InGame `WorldMapUi` 배치, 활성 무역 E2E, Shared 로드 후 1차 빌드 ID Warning/unlock 전수 확인, `BaseCampf` 수정 후 재검증

---

## 7. 알려진 리스크

- `townId: BaseCampf` 오타 → Shared `BaseCamp`와 불일치 가능
- `Route_BaseToRiver` ID 이중 정의 가능성 (`BaseToRiver` vs `BaseRoute`)
- Spline knot 기본 Linear → 곡선이 아닌 꺾인 선으로 보일 수 있음
- `02.Data` Route SO 미커밋 변경의 의도 미확인
- Overlay Canvas vs InGame UI sorting
- 구 핸드오프는 Traveling 자동 Show / `WorldMapPanel`만 배치로 되어 있어 **구식** — Usage Guide·본 문서 우선

---

## 8. 하지 말 것 (Do Not)

- SO에 `mapPosition` 등 맵 좌표 필드 추가하지 말 것
- 시각 Spline/Line 길이로 gameplay distance·duration 계산하지 말 것
- 캐러밴 Transform을 SaveData에 저장하지 말 것
- Presenter/RouteVisual에서 무역 출발·정산·Save 쓰기 하지 말 것
- 승인 없이 `InGame.unity` 수정하지 말 것
- 승인 없이 `02.Data` SO를 추가로 임의 변경하지 말 것 (현재 미커밋 1건 검토 후 처리)
- force-push / `dev2` 직접 커밋하지 말 것

---

## 9. 다음 세션 읽기 순서

1. `@Docs/Personal_Documents/CSU/0715_world_map-first-build-prefab-handoff.md`
2. `@Docs/Guide/Framework_World_Map_Usage_Guide.md` (§1 catalog/`02.Data`, §4–§6 배치·Spline, §9 WorldMapUi)
3. `@Docs/Personal_Documents/CSU/0715_world_map_implementation_log.md`
4. `@Assets/_Project/05.UI/04_WorldMap/Prefabs/WorldMapRoot.prefab` (townId/routeId Inspector)
5. `@Assets/_Project/02.Data/01_ScriptableObjects/Towns/` · `Routes/` (ID 대조)

---

## 10. 다음 단계 (단일 작업)

> `WorldMapRoot` Prefab의 1차 빌드 townId/routeId를 `02.Data` SO·Shared catalog와 전수 대조하고, `BaseCampf` 오타를 `BaseCamp`로 수정한 뒤 Play에서 Open Map·마을 클릭·Spline(Auto) 표시를 확인한다.

**완료 조건:** Prefab의 모든 townId/routeId가 catalog SO와 일치하고, `BaseCamp` 오타 없음, Console에 해당 ID Duplicate/심각한 Error 없음

**검증 방법:** Prefab Inspector ↔ `02.Data` ID 표 대조 → `WorldMapTest` Play → Open Map → 마을 선택·루트 Line/Spline 확인 → (가능하면) Shared 로드 환경에서 `[WorldMap]` Warning 점검
