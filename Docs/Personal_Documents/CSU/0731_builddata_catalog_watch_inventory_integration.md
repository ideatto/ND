# BuildData Catalog · Watch Inventory 연동 구현 로직

**작성일:** 2026-07-31  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `feature/framework/buildso-add-field-shared-data-catalog`  
**HEAD (문서화 시점):** `425361f` — `feat(framework): add BuildData to catalog watch inventory`  
**베이스:** `dev2`  
**범위:** `BuildData` ScriptableObject를 Shared Game Data Catalog / Drift Checker / Watch Inventory 파이프라인에 등록. **런타임 SharedGameData DTO·Provider 조회에는 포함하지 않음.**

관련 선행·후속:

- [`0730_Building_Popup_External_Integration_Request.md`](../LJH/0730_Building_Popup_External_Integration_Request.md) — 건물 Popup은 `VillageBuildingRegistry`의 직접 `BuildData` 참조 경로 사용
- [`0729_village_building_runtime_placement_save_restore.md`](./0729_village_building_runtime_placement_save_restore.md) — 마을 건물 런타임·저장 경로 (본 변경과 독립)

---

## 1. 목적

watch root 아래 존재하는 `BuildData` SO가 `SandboxSharedGameDataCatalog`에 등록되어 있는지 Editor/Player 양쪽에서 drift 검사할 수 있게 한다.

달성 계약:

```text
BuildData SO (ProjectData / SandboxLegacy)
  → SandboxSharedGameDataCatalog.builds[] 등록
  → SharedGameDataCatalogDriftChecker 스캔·GUID 비교
  → SharedGameDataWatchInventory 스냅샷 갱신
  → SharedGameDataService.LoadInitialData() drift 검증
```

의도적으로 하지 않은 것:

- `SharedGameDataView` / `ISharedGameDataProvider`에 Build API 추가
- `TryGetBuild`, `BuildCount`, Summary에 Build 수량 추가
- `SharedGameDataService.BuildView()`에서 BuildData → DTO 변환
- `VillageBuildingRegistry` → SharedGameData 경로로 건물 조회 리다이렉트
- `BuildData` 에셋 내용, Scene, Prefab, SaveData 스키마 변경
- Watch root 분류 정책·drift severity 정책 변경

**BuildData는 Catalog/Inventory에서 “등록 여부”만 감시하고, 런타임 공용 데이터 스냅샷에는 넣지 않는다.**

---

## 2. 변경 파일

| 영역 | 파일 | 역할 |
|------|------|------|
| Catalog 타입 | `Assets/_Project/11.CoreServices/Scripts/Data/SandboxSharedGameDataCatalog.cs` | `builds[]` 직렬화 필드·방어적 복사 property |
| Catalog 에셋 | `Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset` | 8개 BuildData 참조 등록 |
| Editor drift | `Assets/_Project/11.CoreServices/Editor/SharedGameDataCatalogDriftChecker.cs` | `t:BuildData` 스캔, GUID 수집, `BuildId` identity |
| Inventory 스냅샷 | `Assets/_Project/11.CoreServices/Resources/SharedGameDataWatchInventory.asset` | Refresh 결과 (48 entries, Build 8건 포함) |

**diff 통계:** 4 files, +105 / −1 lines

---

## 3. 전체 아키텍처

### 3.1 Catalog / Inventory / Drift (이번 변경)

```text
[Watch Roots]
  Assets/_Project/02.Data/01_ScriptableObjects/...     → ProjectData
  Assets/99.Sandbox/_LJH/02.SO/...                     → SandboxLegacy
        │
        ▼
SharedGameDataCatalogDriftChecker.ScanWatchedAssets()
  · AssetDatabase.FindAssets("t:BuildData", root)
  · TryReadWatchedIdentity → typeName=BuildData, dataId=BuildId
  · TryResolveWatchRootKind(assetPath)
        │
        ├─► SharedGameDataWatchInventory (Player 스냅샷)
        │     entries[] + catalogRegisteredGuids[]
        │
        └─► CollectUnregisteredAssets(catalog)
              watched GUID ∉ CollectCatalogGuids(catalog)
              · ProjectData  → BlocksPlayerBuild = true
              · SandboxLegacy → BlocksPlayerBuild = false
```

### 3.2 Runtime Shared Game Data (변경 없음)

```text
SandboxSharedGameDataCatalog
  Towns / Markets / TradeItems / Wagons / DraftAnimals / Routes / Quests
        │
        ▼
SharedGameDataService.LoadSource()
  → SharedGameDataSource (Builds 미포함)
        │
        ▼
BuildView() → SharedGameDataView / ISharedGameDataProvider
  Summary: Towns, Markets, TradeItems, Wagons, DraftAnimals, Routes, Quests
```

### 3.3 Building Runtime (변경 없음)

```text
VillageBuildingRegistry.catalog[].buildData  (직접 BuildData 참조)
        │
        ▼
BuildingAddPopup → GetCatalogBuildData(index)
        │
        ▼
BuildingPopupRuntimeBinding.OpenDetail(BuildData, level)
```

`FrameworkRoot.SharedGameData`는 Popup의 **재료/TradeItem 표시** 등 기존 용도로만 참조하며, Build 정의 lookup에는 사용하지 않는다.

---

## 4. SandboxSharedGameDataCatalog

### 4.1 추가 API

기존 Town ~ Quest와 동일한 Catalog convention:

```csharp
[SerializeField] private global::BuildData[] builds;

public global::BuildData[] Builds =>
    builds != null
        ? (global::BuildData[])builds.Clone()
        : new global::BuildData[0];
```

- 원본 `builds` 배열은 public으로 노출하지 않음
- 호출자가 반환 배열을 수정해도 직렬화 원본에 영향 없음

### 4.2 Catalog 에셋 등록 (8건)

| BuildId | Asset | Watch root |
|---------|-------|------------|
| `BaseCamp` | `Build_BaseCamp` | ProjectData |
| `Store` | `Build_Store` | ProjectData |
| `Warehouse` | `Build_Warehouse` | ProjectData |
| `Farm` | `Build_Farm` | ProjectData |
| `Cottage` | `Build_Cottage` | ProjectData |
| `Windmill` | `Build_Windmill` | ProjectData |
| `Bakery` | `Build_Bakery` | ProjectData |
| `dummybuild` | `Build_Dummy` | SandboxLegacy |

Inspector 배열 순서는 기능 계약이 아님. GUID·경로·BuildId·root kind가 검증 기준이다.

---

## 5. SharedGameDataCatalogDriftChecker

### 5.1 WatchedTypeFilters

```csharp
"t:BuildData"   // QuestData 다음에 추가
```

기존 Town / Market / TradeItem / Wagon / DraftAnimal / Route / Quest 필터는 유지.

### 5.2 CollectCatalogGuids

```csharp
AddObjectGuids(catalog.Builds, guids);
```

catalog에 연결된 BuildData asset path → GUID를 `catalogRegisteredGuids` 집합에 포함.

### 5.3 TryReadWatchedIdentity

```csharp
case global::BuildData build:
    typeName = nameof(BuildData);
    dataId = build.BuildId;
    return true;
```

- identity 키는 asset 파일명이 아니라 **`BuildId`**
- root kind는 `SharedGameDataWatchRoots.TryResolveWatchRootKind(path)` — **이번 PR에서 수정하지 않음**

### 5.4 Refresh 메뉴

```text
ND/Framework/Refresh Shared Game Data Watch Inventory
```

1. `ScanWatchedAssets()` — watch root 전체 재스캔  
2. `CollectCatalogGuids(catalog)` — catalog 등록 GUID 스냅샷  
3. `SharedGameDataWatchInventory.ReplaceSnapshot(entries, catalogGuids)`

---

## 6. SharedGameDataWatchInventory Refresh 결과

| 항목 | Refresh 전 | Refresh 후 | 설명 |
|------|-----------|-----------|------|
| Total entries | 36 | 48 | watch root 재스캔 |
| BuildData entries | 0 | 8 | 신규 watched type |
| ProjectData Build | — | 7 | `02.Data/.../Build/` |
| SandboxLegacy Build | — | 1 | `Build_Dummy` |
| `catalogRegisteredGuids` | 35 | 50 | catalog GUID 스냅샷 재생성 |

Refresh로 **함께 보정된 stale 항목** (Build 외):

- `TradeItem_Logs`, `TradeItem_Stone`
- Sandbox `QuestData` 2건 (`quest_dummy_basecamp`, 더미용 리버타운)

이 항목들은 디스크에 존재하는 SO를 inventory가 누락했던 것이므로, Refresh의 정상 결과로 취급한다.

---

## 7. Drift 검증 · Severity

### 7.1 Editor Play

`SharedGameDataService.ApplyCatalogDriftCheck()` → reflection으로 `CollectUnregisteredAssets(catalog)` 호출.

- **ProjectData 미등록:** warning (Editor는 InGame 차단하지 않음)
- **SandboxLegacy 미등록:** warning

### 7.2 Player Build

`Resources.Load<SharedGameDataWatchInventory>()` → `CollectPlayerDriftFindings(inventory)`.

- inventory `entries` 중 GUID가 `catalogRegisteredGuids`에 없으면 finding
- `finding.BlocksPlayerBuild == (WatchRootKind == ProjectData)`
- **ProjectData Build 미등록 → error, InGame 진입 차단**
- **SandboxLegacy Build 미등록 → warning only**

### 7.3 검증 시나리오 (Editor)

in-memory 임시 Catalog에서 `BaseCamp` 참조 제거:

```text
finding: BaseCamp | BuildData | ProjectData | blocks=True
```

production Catalog(8건 등록)에서는 `prodBuildFindings=0`.

`Build_Dummy`만 Catalog에서 제거:

```text
finding: dummybuild | BuildData | SandboxLegacy | blocks=False
```

---

## 8. SharedGameDataService와의 경계

### 8.1 LoadSource — Builds 미사용

```csharp
return new SharedGameDataSource(
    catalog.Towns,
    catalog.Markets,
    catalog.TradeItems,
    catalog.Wagons,
    catalog.DraftAnimals,
    catalog.Routes,
    catalog.Quests);
    // catalog.Builds 없음
```

### 8.2 Provider Summary (변경 없음)

```text
Towns: 6, Markets: 5, TradeItems: 10, Wagons: 5, DraftAnimals: 3, Routes: 11, Quests: 2
```

Build count / BuildIds / TryGetBuild API 없음.

### 8.3 Player drift consumer

`CollectPlayerDriftFindings`는 `entry.typeName`을 whitelist로 거르지 않고 GUID만 비교한다.  
`"BuildData"` typeName은 Player에서도 별도 처리 없이 generic finding으로 동작한다.

---

## 9. 운영 · PR 체크리스트

새 `BuildData` SO를 watch root에 추가할 때:

1. `SandboxSharedGameDataCatalog.asset`의 `builds[]`에 참조 등록  
2. `ND/Framework/Refresh Shared Game Data Watch Inventory` 실행  
3. `SharedGameDataWatchInventory.asset` diff 확인 (entry + catalogRegisteredGuids)  
4. Unity Editor에서 `LoadInitialData()` / InGame 진입 smoke  
5. Player RC 전 inventory refresh 여부 확인  

Catalog에 등록했는데 inventory를 Refresh하지 않으면, **Editor Play는 통과할 수 있으나 Player build에서 ProjectData drift error**가 날 수 있다.

---

## 10. 검증 요약 (2026-07-31)

| 항목 | 결과 |
|------|------|
| Unity compile (`6000.5.2f1`) | Pass |
| Catalog Builds 8건 | Pass |
| Inventory BuildData 8건 (7 ProjectData + 1 SandboxLegacy) | Pass |
| Drift detection (BaseCamp / dummybuild) | Pass |
| SharedGameData LoadInitialData / Summary | Pass (Build 미포함) |
| Building Popup (`VillageBuildingRegistry` 직접 참조) | Pass |
| Player build | 미실행 |

알려진 pre-existing 이슈 (본 PR 범위 외):

- `Build_Dummy` level-1 `buildPrefab` null  
- `TownQuestFlowService` abstract script console error  

---

## 11. 관련 코드 위치

| 주제 | 경로 |
|------|------|
| Catalog 타입 | `Assets/_Project/11.CoreServices/Scripts/Data/SandboxSharedGameDataCatalog.cs` |
| Drift checker | `Assets/_Project/11.CoreServices/Editor/SharedGameDataCatalogDriftChecker.cs` |
| Watch root 정책 | `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataWatchRoots.cs` |
| Inventory SO | `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataWatchInventory.cs` |
| Load + drift | `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataService.cs` |
| Finding 모델 | `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataDriftFinding.cs` |
| 건물 런타임 | `Assets/_Project/01.Core/07_Village/YHY/VillageBuildingRegistry.cs` |
