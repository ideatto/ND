# World Map · Minimap 캐러밴 마커 정산 대기 위치 수정 구현 로직

**작성일:** 2026-07-30  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `fix/ui/world-map-caravan-maker-settlement-position-edit-csu`  
**베이스:** `dev2`  
**범위:** `SettlementPending` 상태 캐러밴의 지도 마커를 정산 등급에 맞는 **정확한 마을 좌표**에 표시

관련 선행:

- [`0728_world_map_native_multi_caravan_markers.md`](./0728_world_map_native_multi_caravan_markers.md) — 다중 `CaravanMapMarker` 풀·스냅샷 동기화
- World Map Post-Travel Caravan Position 조사 결과 — `SettlementPending`에서 durable `caravan.currentTownId`(출발지)로 폴백하는 것이 근본 원인

---

## 1. 목적

무역 완료 후 `SettlementPending` 상태가 되면, Claim 전까지 durable `caravan.currentTownId`는 **출발 마을**에 머문다.  
기존 지도 표시는 이 값 또는 `ActiveRouteId` 공백을 이유로 마커를 숨기거나 출발지에 두어, **도착·실패 위치와 시각이 어긋났다.**

이번 변경의 목표:

1. **저장 데이터·Claim·durable town 갱신 타이밍은 바꾸지 않는다.**
2. 지도 표시 계층만에서 `SettlementPending`의 **의미적 위치**(성공→목적지, 실패→출발지)를 계산한다.
3. `WorldMapPresenter`와 `MinimapMultiCaravanMarkers`가 **동일한 표시 정책**을 공유한다.
4. 데이터 불일치 시 폴백·경고를 일관되게 기록한다.

의도적으로 건드리지 않은 영역:

- `TradeProgressCoordinator` / `TradeMapProgressSnapshot` 생성 규칙
- SaveData 스키마, Claim, `caravan.currentTownId` durable 갱신 시점
- Prefab / Scene

---

## 2. 변경 파일

| 영역 | 파일 | 역할 |
|------|------|------|
| Framework (신규) | `Assets/_Project/11.CoreServices/Scripts/MapPresentation/CaravanMapDisplayResolver.cs` | 저장 상태 → 지도 표시 모드 변환 |
| Framework (신규) | `Assets/_Project/11.CoreServices/Editor/CaravanMapDisplayResolverTests.cs` | Edit Mode 단위 테스트 |
| World Map UI | `Assets/_Project/05.UI/04_WorldMap/Scripts/WorldMapPresenter.cs` | Resolver 연동, Route/Town 분기 |
| World Map UI | `Assets/_Project/05.UI/04_WorldMap/Scripts/CaravanMapMarker.cs` | `SetWorldPosition` 추가 |
| Minimap (임시) | `Assets/_Project/01.Core/08_Minimap/MinimapMultiCaravanMarkers.cs` | 동일 Resolver로 위치 계산 |

---

## 3. 핵심 설계 — 표시 계층 분리

```text
[저장·런타임 진실]
SaveData
  ├─ caravans[].currentTownId          ← Claim 전까지 출발지 유지
  ├─ tradeProgressEntries[].state
  ├─ pendingSettlements[]              ← 정산 등급 (Success / PartialSuccess / Failed)
  └─ tradePreparationCommits[]         ← 출발지·목적지·routeId

[지도 표시 정책 — 읽기 전용]
CaravanMapDisplayResolver.TryResolve(...)
  └─ CaravanMapDisplayState
       ├─ Mode: Route | Town
       ├─ RouteId + Progress01          (Traveling)
       └─ TownId + Issue                (정박·정산 대기·폴백)

[소비자]
WorldMapPresenter.SyncCaravanMarkers()
  ├─ Route  → marker.SetRoute + SetProgress
  └─ Town   → marker.SetWorldPosition(town.position)

MinimapMultiCaravanMarkers.TryResolvePosition()
  ├─ Route  → route.EvaluatePosition(progress01)
  └─ Town   → town.transform.position
```

Resolver는 **전달받은 caravan + progress만** 사용하며 SaveData를 수정하지 않는다.

---

## 4. CaravanMapDisplayResolver 로직

### 4.1 공개 API

```csharp
bool TryResolve(
    SaveData saveData,
    ISharedGameDataProvider sharedGameData,
    CaravanSaveData caravan,
    TradeProgressSaveData progress,
    float travelingProgress01,
    out CaravanMapDisplayState state)
```

반환 `CaravanMapDisplayState` 필드:

| 필드 | 의미 |
|------|------|
| `Mode` | `Route` = 경로 위 보간, `Town` = 마을 월드 좌표 |
| `RouteId` / `Progress01` | `Traveling`일 때 사용. `Progress01`은 0~1 clamp |
| `TownId` | `Town` 모드일 때 배치할 마을 ID |
| `Issue` | 폴백·데이터 누락 사유 (`None`이면 정상) |

### 4.2 상태별 분기

```text
progress == null
  OR state ∈ { None, Preparing, Completed, Failed }
  → Town: caravan.currentTownId

progress.caravanId ≠ caravan.caravanId
  → Town: caravan.currentTownId, Issue = InvalidIdentity

state == Traveling
  → activeRouteId 존재 AND sharedGameData에 route 존재
       ? Route(activeRouteId, travelingProgress01)
       : Town(currentTownId), Issue = MissingRoute

state == SettlementPending
  → pendingSettlements에서 (caravanId, activeTradeId) 1건 조회
       ├─ 0건 / hasResult=false → Town(currentTownId), MissingPendingSettlement
       ├─ 2건 이상            → Town(currentTownId), AmbiguousPendingSettlement
       └─ 1건 + hasResult
            ├─ grade == Failed
            │    origin = commit.currentTownId ?? caravan.currentTownId
            │    → Town(origin), commit 없으면 MissingPreparationCommit
            ├─ grade ∈ { Success, PartialSuccess }
            │    destination = commit.destinationTownId
            │    비어 있으면 routeId + origin으로 shared route 반대편 마을 역산
            │    → Town(destination) 또는 UnresolvedDestination / MissingPreparationCommit
            └─ 기타 grade → Town(currentTownId), InvalidSettlementGrade

state == SettlementPending 이외 (위에서 걸러지지 않은 잔여)
  → Town(currentTownId), Issue = InvalidIdentity
```

### 4.3 SettlementPending 위치 결정 요약

| 정산 등급 | 표시 마을 | 우선 데이터 소스 |
|-----------|-----------|------------------|
| `Success` | 목적지 | `tradePreparationCommits.destinationTownId` |
| `PartialSuccess` | 목적지 | 동일 |
| `Failed` | 출발지 | `tradePreparationCommits.currentTownId` → 없으면 `caravan.currentTownId` |
| pending 없음 | durable 현재 마을 | 폴백 + `MissingPendingSettlement` |

목적지 ID가 commit에 없을 때:

```text
routeId = commit.routeId ?? pending.routeId
origin  = commit.currentTownId ?? caravan.currentTownId
destination = shared route에서 origin의 반대편 마을
```

### 4.4 Issue enum

| Issue | 발생 조건 | 소비자 동작 |
|-------|-----------|-------------|
| `None` | 정상 해석 | 표시 진행 |
| `InvalidIdentity` | caravan/progress 불일치, 비정상 state | 마커 숨김 (WorldMap) / 위치 미반환 (Minimap) |
| `MissingRoute` | Traveling인데 route 없음 | Town 폴백 + 경고 |
| `MissingPendingSettlement` | SettlementPending인데 pending 없음 | currentTown 폴백 + 경고 |
| `AmbiguousPendingSettlement` | pending 2건 이상 | currentTown 폴백 + 경고 |
| `MissingPreparationCommit` | commit 없거나 destination 역산 실패 | currentTown 또는 origin 폴백 + 경고 |
| `AmbiguousPreparationCommit` | commit 2건 이상 | currentTown 폴백 + 경고 |
| `InvalidSettlementGrade` | Success/PartialSuccess/Failed 외 grade | currentTown 폴백 + 경고 |
| `UnresolvedDestination` | TownId를 특정할 수 없음 | 마커 숨김 + 경고 |

---

## 5. WorldMapPresenter 변경

### 5.1 SyncCaravanMarkers 흐름 (변경 후)

기존: 스냅샷의 `ActiveRouteId`가 비면 마커 숨김 → `SettlementPending`에서 전부 사라짐.

변경 후:

```text
GetMapProgressSnapshots() 순회
  └─ state ∈ { Traveling, SettlementPending } 만 처리
       ├─ SaveDataLookup으로 caravan + progress identity 검증
       │    (progress.activeTradeId == snapshot.ActiveTradeId)
       ├─ CaravanMapDisplayResolver.TryResolve(..., snapshot.Progress01)
       ├─ display.Issue != None → WarnDisplayIssue (1회)
       ├─ Mode == Route
       │    → routesById[RouteId] 존재 시 SetRoute + SetProgress
       │    → 없으면 Hide + MissingRoute 경고
       └─ Mode == Town
            → townsById[TownId] 존재 시 SetWorldPosition
            → 없으면 Hide + UnresolvedDestination 경고
```

`SettlementPending` 스냅샷의 `Progress01 = 1`은 **Route 모드가 아닐 때는 사용하지 않는다.**  
Town 모드에서는 Resolver가 등급 기반 `TownId`를 결정한다.

### 5.2 경고 통합

기존 `warnedEmptyRouteCaravanIds`, `warnedMissingRouteIds`를 제거하고  
`warnedDisplayIssues` HashSet 하나로 통합:

```text
key = caravanId + "\n" + tradeId + "\n" + issue
RefreshAll() 시 Clear
64건 초과 시 Clear (로그 폭주 방지)
```

메시지 접두사: `[WorldMap] Caravan map placement used a fallback or was skipped.`

### 5.3 CaravanMapMarker.SetWorldPosition

```csharp
public void SetWorldPosition(Vector3 position)
{
    activeRoute = null;           // route 참조 해제
    transform.position = position;
    gameObject.SetActive(true);
}
```

- 정산 대기·정박 표시 시 경로 보간을 끊고 **마을 좌표에 고정**한다.
- 이후 `SetRoute` 호출로 다시 Traveling 표시 가능.

---

## 6. MinimapMultiCaravanMarkers 변경

기존 `TryResolvePosition`:

- `Traveling`만 route 위치 계산
- 그 외는 `caravan.currentTownId` → **SettlementPending도 출발지**

변경 후:

- `SaveDataLookup.TryGetTradeProgress` + `CaravanMapDisplayResolver.TryResolve` 사용
- `Traveling`일 때만 `CalcProgress(entry)`를 `travelingProgress01`로 전달
- Resolver 결과 `Route` / `Town`에 따라 `FindRoute` / `FindTown`으로 월드 좌표 결정
- route/town lookup 실패 시 **폴백 없이 false** (0728 문서의 WorldMapPresenter와 동일한 엄격도)
- `WarnDisplayIssue` 패턴은 WorldMap과 동일 (접두사만 `[Minimap]`)

---

## 7. 단위 테스트 (CaravanMapDisplayResolverTests)

| 테스트 | 검증 내용 |
|--------|-----------|
| `Preparing_UsesDurableCurrentTown` | Preparing → currentTown |
| `Traveling_UsesRouteWithClampedProgress` | Route + progress clamp 0~1 |
| `SuccessfulPending_UsesExactDestination` | Success / PartialSuccess → destination |
| `FailedPending_UsesExactOrigin` | Failed → origin (commit.currentTownId) |
| `MissingPending_UsesSameCaravanCurrentTown` | pending 없음 → currentTown + Issue |
| `MismatchedPending_IsNotUsed` | 다른 caravan/trade pending 무시 |
| `TwoCaravans_ResolveTheirOwnPendingGrades` | 캐러밴 간 pending 교차 오염 없음 |
| `DurablePending_ResolvesWithoutRuntimeSettlementAuthority` | 런타임 정산 권한 없이 Save만으로 destination 해석 |

테스트는 `FakeSharedGameDataProvider`로 route `origin ↔ destination` 1개를 제공한다.

---

## 8. 기존 0728 구현과의 관계

| 항목 | 0728 (다중 마커) | 이번 변경 |
|------|------------------|-----------|
| 마커 풀·스냅샷 순회 | 유지 | 유지 |
| Traveling 표시 | `SetRoute` + `Progress01` | Resolver `Route` 모드로 동일 |
| SettlementPending 표시 | route 끝(`Progress01=1`) 가정 | **Town 모드**, 등급별 마을 |
| ActiveRouteId 공백 | 마커 숨김 | Resolver가 Town 모드로 전환 시도 |
| Framework 스냅샷 | 그대로 사용 | identity 검증 추가 |
| Minimap | currentTown 폴백 | Resolver 공유 |

선택 캐러밴 라벨·루트 하이라이트(`RefreshSelectedProgressFromCoordinator`)는 **이번 diff 범위 밖**이며 기존 동작을 유지한다.

---

## 9. 검증 메모

**문서화 시점:** 작업 트리 미커밋. Unity Editor 컴파일·Play Mode 런타임 확인은 **별도 수행 필요**.

권장 수동 시나리오:

1. Traveling — 마커가 route 위에서 progress에 따라 이동
2. SettlementPending + Success — **목적지 마을** 좌표에 고정
3. SettlementPending + Failed — **출발지 마을** 좌표에 고정
4. Claim 완료 후 — durable town 갱신과 표시 일치 확인
5. 다중 캐러밴 — 서로 다른 pending grade가 각각 올바른 town에 표시
6. Minimap + WorldMap 동시 — 동일 캐러밴 위치 정책 일치
7. `MinimapMultiCaravanMarkers.hideFrameworkSingleMarker` — V2 Prefab 검증 시 비활성화 필요 (0728 §6)

Edit Mode 테스트:

```text
CaravanMapDisplayResolverTests (NUnit)
```

---

## 10. 리스크 · 후속

| 리스크 | 설명 |
|--------|------|
| commit / pending 데이터 누락 | 폴백은 currentTown. Issue 경고로 추적 |
| shared route 역산 실패 | `UnresolvedDestination`, 마커 숨김 |
| Framework 스냅샷과 Save 불일치 | `InvalidIdentity`, 마커 숨김 |
| Minimap vs native marker 이중 표시 | Prefab 임시 스크립트 설정에 따름 (0728) |

후속:

1. Unity에서 WorldMap / Minimap 시각 검증
2. PR base `dev2`, 커밋 시 `.meta` 포함
3. V2 Render Root에서 임시 Minimap 스크립트 정리 (Prefab 소유자)

---

## 관련 API · 문서

- `CaravanMapDisplayResolver.TryResolve`
- `CaravanMapDisplayState` / `CaravanMapDisplayMode` / `CaravanMapDisplayIssue`
- `TradeProgressCoordinator.GetMapProgressSnapshots()`
- `SaveData.pendingSettlements`, `SaveData.tradePreparationCommits`
- `CaravanMapMarker.SetWorldPosition`
- [`0728_world_map_native_multi_caravan_markers.md`](./0728_world_map_native_multi_caravan_markers.md)
