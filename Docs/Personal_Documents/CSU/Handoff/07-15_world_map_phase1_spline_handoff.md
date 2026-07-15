# World Map Phase 1 + Spline Handoff

**작성일:** 2026-07-15  
**브랜치:** `feature/world-map-trade-progress`  
**Feature 루트:** `Assets/_Project/05.UI/04_WorldMap/`  
**관련 문서:**
- `Docs/Personal_Documents/CSU/Handoff/07-15_World_Map_Implementation_Handoff.md` (설계 핸드오프)
- `Docs/Personal_Documents/CSU/0715_world_map_phase1_spline_implementation.md` (구현 로그)
- `Docs/Guide/Framework_World_Map_API_Guide.md`

---

## 1. 작업 목표

- 목표: 논리 Town/Route·Shared·TradeProgress와 분리된 월드맵 Phase 1(직선) + Spline 비주얼, Coordinator 읽기 스냅샷 API, `WorldMapTest` 예시 씬/Prefab 패널 구조 구현
- 완료 기준:
  - `TryGetMapProgress` / `TradeMapProgressSnapshot`(UTC ticks + ProgressPercent) 동작
  - `05.UI/04_WorldMap` Presenter·RouteVisual(Straight/Spline)·Panel·Prefab·테스트 씬 존재
  - Sandbox 전역 `SaveData` 충돌 없이 컴파일
  - Guide + 개인 구현 로그 작성
- 제외 범위:
  - `RouteRuntimeState` / Save schema 확장
  - Trade Prepare UI 실제 연동
  - `InGame.unity`에 Prefab 배치
  - Sandbox `TownData`/`RouteData` 필드 추가
  - commit / push / PR (요청 전)

---

## 2. 현재 상태

| 구분 | 내용 |
|------|------|
| 완료 | Coordinator 맵 progress 읽기 API, WorldMap 스크립트(Straight/Spline), namespace 충돌 수정, batch 씬 베이크, `WorldMapRoot`/`WorldMapPanel` Prefab, Guide·구현 로그 |
| 진행 중 | 없음 |
| 미완료 | InGame 씬에 `WorldMapPanel` Prefab 배치, Trade Prepare `TownClicked` 연결, Play Mode 수동 E2E(무역 활성 시 %·캐러밴) |
| 차단됨 | 없음 |

---

## 3. 핵심 결정 사항

- 논리 데이터: 기존 Sandbox `TownData`/`RouteData` + `ISharedGameDataProvider` 재사용 (신규 World SO 금지)
- Progress: `TradeProgressCoordinator.TryGetMapProgress` (ticks 포함, %는 `ProgressPercent`)
- Risk 표시: `SharedRouteDefinition.BaseRiskLevel`만
- RouteRuntimeState: 1차 제외, `RouteMapPresentationResolver`로 확장 지점만 확보
- 마을 클릭: `TownClicked` 이벤트만 (무역 준비 UI 미연결)
- InGame 표시: Prefab 패널 방식 (`WorldMapPanel` + Traveling Show/Hide)
- Spline: `com.unity.splines` 2.9.0, 누락 시 직선 폴백
- 맵 좌표: 씬/Prefab Transform이 source of truth

---

## 4. 변경 파일

### 생성
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeMapProgressSnapshot.cs` (+ `.meta`)
- `Assets/_Project/05.UI/04_WorldMap/Scripts/` — `RoutePathType`, `RouteVisual`, `TownWorldView`, `CaravanMapMarker`, `RouteMapPresentationResolver`, `WorldMapPresenter`, `WorldMapPanel`, `WorldMapTestEntry` (+ `.meta`)
- `Assets/_Project/05.UI/04_WorldMap/Editor/` — `WorldMapTestSceneBuilder`, `WorldMapTestSceneBuilderBatch` (+ `.meta`)
- `Assets/_Project/05.UI/04_WorldMap/Prefabs/WorldMapRoot.prefab`, `WorldMapPanel.prefab` (+ `.meta`)
- `Assets/_Project/05.UI/04_WorldMap/Scene/WorldMapTest.unity` (+ `.meta`)
- `Assets/_Project/05.UI/04_WorldMap/Art/` — 배경/마커 PNG, `RouteLine.mat` (+ `.meta`)
- `Docs/Guide/Framework_World_Map_API_Guide.md`
- `Docs/Personal_Documents/CSU/0715_world_map_phase1_spline_implementation.md`
- `Docs/Personal_Documents/CSU/Handoff/07-15_World_Map_Implementation_Handoff.md` (설계 원본, 세션 전 존재·작업 중 유지)

### 수정
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs` — `TryGetMapProgress` 추가
- `Docs/Guide/Framework_CoreServices_Team_Usage_Guide.md` — 월드맵 섹션·링크
- `Packages/manifest.json`, `Packages/packages-lock.json` — `com.unity.splines` 2.9.0

### 삭제
- 없음 (테스트 씬에서 `WorldMapTestEntry` 오브젝트는 베이크 시 제거, 스크립트 파일은 레거시로 유지)

### 승인된 외부 수정
- `11.CoreServices` TradeProgress 읽기 API (세션에서 승인·구현)
- `WorldMapTest.unity` Scene 수정 (이번 작업에 한해 허용)
- Splines 패키지 (사용자 사전 설치)

### Scene / Prefab / Meta / Package / 데이터 변경 여부
- Scene 변경: Yes (`WorldMapTest.unity`만)
- Prefab 변경: Yes (`WorldMapRoot`, `WorldMapPanel`)
- Meta 변경: Yes (신규 에셋 `.meta`)
- Package 변경: Yes (`com.unity.splines`)
- ScriptableObject 또는 데이터 변경: No

---

## 5. 구현된 동작

- `TryGetMapProgress`: Traveling/SettlementPending 스냅샷, pause 시 caravan `progress01` 우선, SettlementPending 시 progress=1
- `WorldMapPresenter`: Shared/Save unlock, active route 하이라이트, 캐러밴 위치, Progress % / Risk 라벨, `TownClicked`/`RouteClicked`
- `RouteVisual`: Straight Lerp / Spline `EvaluatePosition`(월드), LineRenderer, spline 누락 폴백
- `WorldMapPanel`: Prefab 호스트, Traveling 시 Show·그 외 Hide(옵션), 클릭 이벤트 중계
- 베이크: `ND → World Map → Build WorldMapTest Scene` 또는 batch `WorldMapTestSceneBuilderBatch.Run`
- 예시 ID: towns `basecamp`/`dummytown`/`demo_waypoint`, routes `dummyroute`(straight)·`demo_spline_route`(spline demo)

---

## 6. 검증 결과

- 코드 검토: 완료 (namespace alias로 Sandbox `SaveData` 충돌 해소)
- Unity 컴파일: batch 로그 기준 WorldMap `error CS` 없음 (기존 프로젝트 warning만 존재)
- 테스트한 항목: batch 씬·Prefab 베이크 성공, Prefab 경로 존재 확인
- 테스트하지 못한 항목: Editor Play Mode에서 활성 무역 연동 E2E, InGame Prefab 배치 후 Traveling 전환 확인

---

## 7. 알려진 리스크

- `demo_spline_route` / `demo_waypoint`는 Shared에 없어 Presenter Warning 가능 (의도된 데모 ID)
- Sandbox 전역 `SaveData`와 Framework 타입이 공존 — WorldMap은 alias 필수, 신규 스크립트도 `ND.Framework.*` 한정 권장
- `WorldMapPanel`의 Overlay Canvas가 InGame UI와 겹칠 수 있음 — InGame 배치 시 sorting/카메라 정리 필요
- `InGame.unity` 미수정 — Prefab만으로는 런타임 InGame에 자동 표시되지 않음

---

## 8. 하지 말 것 (Do Not)

- 핸드오프·구현 문서 외로 Sandbox `TownData`/`RouteData`에 `mapPosition` 등 필드 추가하지 말 것
- 시각 Spline/Line 길이로 gameplay distance·duration 계산하지 말 것
- 캐러밴 Transform을 SaveData에 저장하지 말 것
- `WorldMapPresenter`/`RouteVisual`에서 무역 출발·정산·Save 쓰기 하지 말 것
- `InGame.unity` / 다른 production Scene을 승인 없이 수정하지 말 것
- 요청 전 commit / push / PR / merge 하지 말 것

---

## 9. 다음 세션 읽기 순서

1. `@Docs/Personal_Documents/CSU/Handoff/07-15_world_map_phase1_spline_handoff.md`
2. `@Docs/Guide/Framework_World_Map_API_Guide.md`
3. `@Docs/Personal_Documents/CSU/0715_world_map_phase1_spline_implementation.md` (§8 추후 API 권장)
4. `@Assets/_Project/05.UI/04_WorldMap/Scripts/WorldMapPanel.cs`
5. `@Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs` (`TryGetMapProgress`)

---

## 10. 다음 단계 (단일 작업)

> `WorldMapPanel.prefab`을 InGame Traveling UI 계층에 배치하고, Play Mode에서 Traveling 전환 시 맵 Show/Hide와 `TryGetMapProgress` 기반 Progress %가 갱신되는지 확인한다.

**완료 조건:** InGame에서 Traveling 진입 시 월드맵 패널 표시, Preparation/Settlement에서 숨김(또는 정책에 맞는 표시), Console에 WorldMap 관련 예기치 않은 오류 없음

**검증 방법:** Boot→InGame→무역 출발(또는 debug harness)→Traveling→맵·%·캐러밴 확인→정산 화면 전환 시 Hide 확인
