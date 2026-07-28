# 월드맵 네이티브 다중 캐러밴 마커 구현

## 브랜치 정보

- 브랜치: `feature/ui/world-map-multi-caravan-markers`
- 베이스: `dev2` (`0cc4b373e446f1c72ac4276a5c3c956aec214c90`)
- 변경 범위: `WorldMapPresenter.cs` 단일 파일 (Prefab/Scene/Framework 미변경)

## 목적

기존 `WorldMapPresenter`는 Framework의 선택 캐러밴 진행(`TryGetMapProgress`)만 읽어 **마커 1개**를 갱신했다.  
다중 캐러밴이 동시에 `Traveling` / `SettlementPending`일 때도 맵에 모두 표시하려면, 물리 마커와 선택 UI 표시를 분리해야 한다.

이번 구현의 목표는 다음과 같다.

1. `GetMapProgressSnapshots()`로 활성 캐러밴 전체를 읽어 **네이티브 `CaravanMapMarker`를 CaravanId별로 동기화**한다.
2. 진행률 라벨, Risk 라벨, 선택 루트 하이라이트는 **선택된 캐러밴만** 반영한다.
3. 선택 변경이 다른 캐러밴 마커를 숨기거나 재생성하지 않도록 한다.
4. Prefab/Scene/Framework API를 바꾸지 않고 UI Presenter만으로 완료한다.

---

## 변경 파일

| 영역 | 파일 | 역할 |
|------|------|------|
| Presenter | `Assets/_Project/05.UI/04_WorldMap/Scripts/WorldMapPresenter.cs` | 다중 마커 풀 + 선택 표시 분리 |

의도적으로 건드리지 않은 파일:

- `TradeProgressCoordinator.cs` / `TradeMapProgressSnapshot.cs`
- `CaravanMapMarker.cs` / `RouteVisual.cs`
- `MinimapMultiCaravanMarkers.cs` (임시 스크립트)
- `WorldMapRenderRootV2.prefab`, Scene, SaveData

---

## 책임 분리

```text
[물리 마커]
GetMapProgressSnapshots()
  └─ SyncCaravanMarkers()
       └─ CaravanId별 CaravanMapMarker 풀 생성/갱신/숨김

[선택 표시]
TryGetMapProgress()   ← SaveData.tradeProgress = selectedCaravanId 엔트리
  └─ RefreshSelectedProgressFromCoordinator()
       ├─ ProgressPercentLabel
       ├─ RiskLabel
       └─ RouteVisual.SetActiveVisual (선택 루트만)
```

핵심 규칙:

- 마커 위치/활성 상태는 **스냅샷 전체** 기준이다.
- 라벨·루트 하이라이트는 **선택된 캐러밴** 기준이다.
- 선택 캐러밴이 맵 스냅샷에 없어도, 다른 캐러밴 마커는 유지된다.

---

## 전체 갱신 흐름

```text
OnEnable / LoadCompleted / InGameScreenChanged
  └─ RefreshAll()
       ├─ BuildLookups()
       ├─ RefreshPresentationFromSave()   // 마을 unlock + 선택 루트 기반 resolver
       ├─ SyncCaravanMarkers()            // 다중 마커
       └─ RefreshSelectedProgressFromCoordinator()

Update (refreshEveryFrameWhileTraveling == true)
  ├─ SyncCaravanMarkers()
  └─ RefreshSelectedProgressFromCoordinator()
```

`RefreshAll()`은 룩업 재구축까지 포함하므로, `OnEnable`에서 중복 collect를 피하기 위해 별도 auto-collect 호출을 두지 않는다.

---

## 1. SyncCaravanMarkers — 다중 마커 동기화

### 입력

`TradeProgressCoordinator.GetMapProgressSnapshots()`

각 스냅샷에서 사용하는 필드:

- `CaravanId`
- `ActiveRouteId`
- `State`
- `Progress01`

### 표시 대상 State

- `Traveling`
- `SettlementPending`

그 외 상태는 스킵한다. `SettlementPending`의 `Progress01`은 Framework가 `1`로 제공하며, Presenter는 재계산하지 않는다.

### 처리 순서

1. `seenCaravanIds`를 비운다.
2. 스냅샷을 순회한다.
3. 유효한 스냅샷마다:
   - `GetOrCreateCaravanMarker(caravanId)`
   - `marker.SetRoute(routeVisual)`
   - `marker.SetProgress(progress01)`
   - `seenCaravanIds`에 추가
4. 루프 종료 후 `HideUnseenCaravanMarkers()`로 이번 프레임에 보이지 않은 마커만 `SetRoute(null)`로 숨긴다.

`Destroy`는 호출하지 않는다. 숨긴 마커는 풀에 남아 재사용된다.

---

## 2. 마커 풀 — GetOrCreateCaravanMarker

| 상황 | 동작 |
|------|------|
| 동일 `CaravanId`가 풀에 있음 | 기존 마커 반환 |
| 첫 활성 캐러밴 | 직렬화된 `caravanMarker`(ActiveCaravanMarker) 재사용 |
| 이후 추가 캐러밴 | `Instantiate(template, template.parent)`로 1회 클론 |
| 템플릿 미할당 | 경고 1회 후 null |

런타임 이름 규칙:

```text
CaravanMapMarker_<caravanId>
```

동일 캐러밴이 새 `tradeId`/다른 `routeId`로 다시 출발해도 **같은 풀 엔트리**를 재사용하고, `SetRoute` / `SetProgress`만 갱신한다.

---

## 3. 방어 로직 (경고는 ID당 1회)

| 조건 | 동작 | 경고 가드 |
|------|------|-----------|
| `CaravanId` 공백 | 스냅샷 스킵 | `warnedEmptyCaravanId` |
| 동일 `CaravanId` 중복 | 첫 스냅샷만 사용, 이후 스킵 | `warnedDuplicateCaravanIds` |
| `ActiveRouteId` 공백 | 해당 마커만 숨김 | `warnedEmptyRouteCaravanIds` |
| `RouteVisual` 없음 | 해당 마커만 숨김 | `warnedMissingRouteIds` |
| 템플릿 null | 마커 생성 불가 | `warnedNullMarkerTemplate` |

경고는 매 프레임 반복하지 않도록 HashSet/bool로 제한한다.

---

## 4. 선택 표시 — RefreshSelectedProgressFromCoordinator

### 성공 시 (`TryGetMapProgress` true)

- Progress 라벨: `Progress {ProgressPercent:0.#}%`
- Risk 라벨: Shared route의 `BaseRiskLevel`
- 모든 `RouteVisual.SetActiveVisual`를 돌며 **선택 루트만** true

### 실패 시 (선택 캐러밴에 활성 진행 없음)

- Progress: `Progress --`
- Risk: `Risk --`
- 모든 루트 하이라이트 해제

이 경로는 마커 풀을 건드리지 않는다.  
따라서 “선택 캐러밴은 Preparing, 다른 캐러밴은 Traveling”인 경우에도 다른 마커는 그대로 남는다.

`TryGetMapProgress`는 내부적으로 `SaveData.tradeProgress`를 읽으며, 이는 `selectedCaravanId`에 대한 `tradeProgressEntries` 호환 접근자다.

---

## 5. 기존 API와의 관계

| API | Presenter 사용처 | 의미 |
|-----|------------------|------|
| `GetMapProgressSnapshots()` | `SyncCaravanMarkers` | 맵에 올릴 전체 활성 진행 |
| `TryGetMapProgress()` | `RefreshSelectedProgressFromCoordinator`, `RefreshPresentationFromSave` | 선택 캐러밴 진행 |

기존 단일 마커 시절에는 두 책임이 한 경로에 섞여 있었다.  
이번 변경은 **읽기 API를 역할별로 분리**한 것이다.

Presenter는 여전히:

- 무역 출발/정산/Save 쓰기를 하지 않는다.
- Framework 스냅샷의 `Progress01`을 그대로 표시한다.

---

## 6. 임시 스크립트 충돌 — MinimapMultiCaravanMarkers

`WorldMapRenderRootV2.prefab`에는 개발용 `MinimapMultiCaravanMarkers`가 붙어 있으며, 기본값이 다음과 같다.

```text
hideFrameworkSingleMarker = true
```

이 옵션이 켜지면 `LateUpdate`에서 활성 `CaravanMapMarker`를 매 프레임 비활성화한다.  
따라서 네이티브 다중 마커를 눈으로 검증하려면:

1. 해당 컴포넌트 전체를 끄거나
2. `hideFrameworkSingleMarker = false`로 둔다

주의:

- 컴포넌트를 삭제하거나 Prefab Apply / Scene Save로 고정하지 말 것 (검증 중)
- 정식 수락 후 Prefab 소유자가 임시 스크립트를 제거·비활성화해야 한다
- 충돌 해결을 `WorldMapPresenter`에 넣지 않는다

런타임 검증 시점의 InGame 활성 맵은 `WorldMapRenderRoot`(구버전)였고, 여기에는 임시 스크립트가 없었다.  
충돌 자체는 컴포넌트를 런타임에 붙였다 제거하는 방식으로 확인했다.

---

## 7. 런타임 검증 요약 (2026-07-28)

환경: Unity `6000.5.2f1`, InGame, `WorldMapPresenter` + `ActiveCaravanMarker`

| 항목 | 결과 |
|------|------|
| 컴파일 Error | 0 |
| 활성 스냅샷 0개 | 마커 숨김, 라벨 empty |
| 단일 Traveling | 템플릿 재사용, 마커 1개 |
| 서로 다른 루트 2대 | 마커 2개, 위치 독립, 클론 1회 |
| 동일 루트 2대 | 마커 2개, progress 독립 |
| Traveling + SettlementPending | Pending `Progress01=1`, 둘 다 표시 |
| 선택만 inactive | 라벨 clear, 다른 마커 유지 |
| 선택 전환 | 마커 수/신원 유지, 라벨만 변경 |
| 스냅샷 제거 | 해당 마커만 숨김, Destroy 없음 |
| 동일 Caravan 재출발 | 풀 재사용 |
| 맵 Root 닫기/열기 | OnEnable Refresh로 복구, 중복 없음 |
| 임시 스크립트 충돌 | 재현·격리 확인 |
| Git | `WorldMapPresenter.cs`만 수정 |

전체 판정: **CONDITIONAL PASS**  
(Title→Loading→InGame 재진입 다중 마커, Empty/Duplicate CaravanId 픽스처는 미실행)

---

## 8. 후속 작업

1. Prefab 소유자: `WorldMapRenderRootV2`의 `MinimapMultiCaravanMarkers` 제거 또는 비활성화
2. InGame가 실제 사용할 Render Root가 V2인지 구버전인지 확정
3. `WorldMapOverlayLabelBinding.presenter`가 null인 InGame 배선 점검 (라벨 자동 바인딩)
4. PR 생성 시 base는 `dev2`, 변경 파일은 Presenter 스크립트(+필요 시 `.meta`)만 포함

---

## 관련 문서 / API

- `Docs/Guide/Framework_World_Map_API_Guide.md`
- `TradeProgressCoordinator.GetMapProgressSnapshots()`
- `TradeProgressCoordinator.TryGetMapProgress()`
- `SaveData.tradeProgress` (selectedCaravanId 호환 접근자)
- 임시 표시: `Assets/_Project/01.Core/07_Village/YHY/MinimapMultiCaravanMarkers.cs`
- 테스트 패널: `MinimapCaravanTestPanel.cs` (`tradeProgressEntries` 직접 주입)
