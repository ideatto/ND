# Shared Game Data Catalog Sync Menu · 로직 정리

**작성일:** 2026-07-31  
**작성:** Framework & Integration (천성욱)  
**브랜치:** `feature/framework/shared-data-catalog-sync-menu`  
**HEAD (문서화 시점):** `10186e4` — `feat(framework): 공유 데이터 카탈로그 수동 동기화 메뉴 추가`  
**베이스:** `dev2`  
**Feature root:** `Assets/_Project/11.CoreServices/Editor/`  
**관련 선행:** [`0713_shared_game_data_catalog_drift_check.md`](./0713_shared_game_data_catalog_drift_check.md), [`0731_builddata_catalog_watch_inventory_integration.md`](./0731_builddata_catalog_watch_inventory_integration.md)

**공식 가이드:** [`Docs/Guide/Framework_Shared_Game_Data_Guide.md`](../../Guide/Framework_Shared_Game_Data_Guide.md)

---

## 1. 목적

watch root 아래 ProjectData SO가 production Catalog에 등록되지 않은 **drift** 를, Editor에서 **Preview → 확인 → Sync** 흐름으로 안전하게 해소한다.

이번 브랜치가 추가하는 것:

- Catalog에 미등록 ProjectData SO를 **Add** (타입별 배열 끝 append)
- Catalog의 null/해석 불가 참조를 **Remove**
- Sync 성공 시 **Watch Inventory 스냅샷 자동 refresh**

이번 브랜치가 하지 않는 것:

- 기존 Catalog 항목 **reorder**
- SandboxLegacy 미등록 SO의 Catalog 자동 편입
- ID 자동 Trim / data-quality 정규화
- 런타임 SharedGameData DTO·Provider API 변경

---

## 2. diff 요약 (10186e4)

| 파일 | 변경 |
|------|------|
| `SharedGameDataCatalogSynchronizer.cs` | **신규** (+515) — Preview/Sync, BuildPlan, 적용 |
| `SharedGameDataTypeCatalog.cs` | **신규** (+93) — 타입 디스크립터 레지스트리 |
| `SharedGameDataWatchScanner.cs` | **신규** (+105) — watch root 공통 스캔 |
| `SharedGameDataCatalogDriftChecker.cs` | **축소** (−554 net) — refresh/미등록 수집만 유지 |
| `SandboxSharedGameDataCatalog.asset` | `builds[]` +1 (`Build_Test`) |
| `SharedGameDataWatchInventory.asset` | Build_Test entry + 스캔 정렬 reorder |

**통계:** 9 files, +838 / −554 lines

---

## 3. 관련 에셋 · 경로

| 역할 | 경로 |
|------|------|
| Production Catalog | `Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset` |
| Watch Inventory | `Assets/_Project/11.CoreServices/Resources/SharedGameDataWatchInventory.asset` |
| ProjectData watch root | `Assets/_Project/02.Data/01_ScriptableObjects` |
| Sandbox Legacy watch root | `Assets/99.Sandbox/_LJH/02.SO` |

### Catalog 필드 ↔ SO 타입 매핑

| Catalog 필드 | SO 타입 | ID 읽기 |
|---|---|---|
| `towns` | `TownData` | `TownId` |
| `markets` | `MarketData` | `MarketId` |
| `tradeItems` | `TradeItemData` | `ItemId` |
| `wagons` | `WagonData` | `WagonId` |
| `draftAnimals` | `DraftAnimalData` | `DraftAnimalId` |
| `routes` | `RouteData` | `RouteId` |
| `quests` | `QuestData` | `QuestId` |
| `builds` | `BuildData` | `BuildId` |

---

## 4. 파일 구조 · 역할 분리

```text
Assets/_Project/11.CoreServices/Editor/
├── SharedGameDataTypeCatalog.cs        ← 타입 8종 단일 레지스트리
├── SharedGameDataWatchScanner.cs       ← watch root 스캔 (공통)
├── SharedGameDataCatalogSynchronizer.cs← Preview/Sync/BuildPlan
└── SharedGameDataCatalogDriftChecker.cs← 미등록 수집 + Inventory refresh
```

**DriftChecker 리팩터링**

- **이전:** 스캔·타입 매핑·drift 검사·inventory refresh가 한 파일에 혼재
- **이후:** 스캔/타입 정의를 `WatchScanner` / `TypeCatalog`로 분리
- DriftChecker는 `CollectUnregisteredAssets`, `TryRefreshWatchInventory` 전담

---

## 5. 컴포넌트 상세

### 5.1 `SharedGameDataTypeCatalog`

지원 SO 8종에 대한 descriptor 배열.

각 `SharedGameDataTypeDescriptor`:

- `AssetType`, `TypeName`
- `AssetFilter` — `t:TypeName` (`FindAssets`용)
- `CatalogFieldName` — Catalog SerializedProperty 이름
- `ReadDataId` — SO에서 data ID 읽기 delegate

### 5.2 `SharedGameDataWatchScanner`

```text
Scan()
  ├─ ScanRoot(ProjectDataRoot)
  └─ ScanRoot(SandboxLegacyRoot)
       └─ Descriptor별 FindAssets → GUID dedupe → ScannedAsset
```

`SharedGameDataScannedAsset`: `Guid`, `AssetPath`, `Descriptor`, `DataId`, `RootKind`, `Asset`, `LoadError`

정렬: RootKind → Type Kind → Path → GUID

### 5.3 `SharedGameDataCatalogSynchronizer`

Preview/Sync 메뉴 + **BuildPlan → Apply → VerifyAppliedPlan → TryRefreshWatchInventory** 파이프라인.

### 5.4 `SharedGameDataCatalogDriftChecker` (축소 후)

- `CollectUnregisteredAssets(catalog)` — Catalog GUID 집합에 없는 watch SO
- `TryRefreshWatchInventory(catalog)` — Scanner + Catalog GUID → Inventory.ReplaceSnapshot
- 메뉴: `ND/Framework/Refresh Shared Game Data Watch Inventory`

---

## 6. CatalogSyncAction (계획 분류)

| Action | 의미 | Sync 적용 |
|---|---|---|
| `Keep` | Catalog 등록 유지, **기존 순서 보존** | target 배열에 포함 |
| `Add` | ProjectData 미등록 → append | target 배열 끝 추가 |
| `Remove` | null/해석 불가 참조 | target 배열에서 제외 |
| `Ignore` | SandboxLegacy 미등록 | Catalog에 넣지 않음 |
| `Warning` | 동작 가능하나 주의 (보조 레코드) | — |
| `Error` | **Sync 전체 차단** | 변경 없음 |

`CatalogSyncPlan` 판정:

- `HasErrors` → Error ≥ 1
- `HasSemanticChanges` → Add ≥ 1 또는 Remove ≥ 1

---

## 7. BuildPlan 알고리즘

```text
BuildPlan()
│
├─ 1. Catalog 로드 (없으면 Error 후 종료)
│
├─ 2. SharedGameDataWatchScanner.Scan()
│     └─ LoadError → Error
│
├─ 3. Catalog 기존 배열 순회 (타입별, index 순)
│     ├─ null 참조              → Remove (+ Warning)
│     ├─ SO 타입 불일치         → Error
│     ├─ GUID 해석 실패         → Remove (+ Warning)
│     ├─ 같은 배열 GUID 중복    → Error
│     ├─ 다른 배열 GUID 충돌    → Error
│     └─ 정상                   → target.Assets/Guids에 Keep
│
├─ 4. AddScannedAssets()
│     ├─ 이미 Catalog 등록      → skip
│     ├─ SandboxLegacy 미등록   → Ignore (+ Warning)
│     └─ ProjectData 미등록     → target.Assets에 Add
│
├─ 5. ValidateTargetIds()
│     ├─ IsNullOrWhiteSpace(id) → Error
│     └─ 같은 타입 내 ID 중복   → Error
│
└─ 6. SortRecords() → Plan 반환
```

**순서 보존:** 기존 Catalog 항목은 원래 array index 순서 유지. Add는 해당 타입 배열 **끝에 append**.

---

## 8. Sync 실행 흐름

```text
Sync()
│
├─ BuildPlan() + PrintPlan("Sync")
│
├─ HasErrors?
│   └─ YES → LogError "Sync blocked" → return (변경 없음)
│
├─ !HasSemanticChanges?
│   └─ YES → Log "No-op success" → return (변경 없음)
│
├─ DisplayDialog 확인
│   └─ Cancel → Log "Sync cancelled" → return
│
├─ Undo.RecordObject(Catalog)
├─ SerializedObject로 target 배열 크기/참조 반영
├─ SaveAssets
│
├─ VerifyAppliedPlan()
│   └─ FAIL → LogError, Inventory refresh 생략
│
└─ TryRefreshWatchInventory()
    ├─ FAIL → CatalogApplied=true, InventoryRefreshed=false
    └─ OK  → CatalogApplied=true, InventoryRefreshed=true
```

**Sync 메뉴 활성 조건** (`ValidateSync`):

- 컴파일/AssetDatabase 업데이트 중 아님
- Play Mode / Play Mode 전환 중 아님

---

## 9. 검증 규칙

### 9.1 Catalog 참조 무결성

| 조건 | Action | Sync |
|---|---|---|
| 배열 요소 null | Remove + Warning | Remove 적용 가능 |
| SO 타입 ≠ 배열 기대 타입 | Error | 차단 |
| GUID 중복 (같은 배열) | Error | 차단 |
| GUID 중복 (다른 배열) | Error | 차단 |
| watch root 밖 등록 SO | Keep + Warning | 유지 |

### 9.2 Data ID (`ValidateTargetIds`)

| 조건 | Action |
|---|---|
| `string.IsNullOrWhiteSpace(id)` | Error — `"null, empty, or whitespace-only data ID"` |
| 같은 타입 내 동일 ID (다른 GUID) | Error — `"Duplicate target data ID within the same type"` |

**주의:** `" build_01 "` 같이 앞뒤 공백만 있는 **비어 있지 않은 ID**는 whitespace-only가 아니므로 통과한다. 자동 Trim 없음.

### 9.3 RootKind 정책

| RootKind | Catalog Sync | Player drift |
|---|---|---|
| `ProjectData` | 미등록 → **Add** | InGame 진입 **차단** |
| `SandboxLegacy` | 미등록 → **Ignore** | **경고만**, 진입 허용 |

---

## 10. Watch Inventory Refresh

Sync 성공 후 DriftChecker가 수행:

```text
TryRefreshWatchInventory(catalog)
  ├─ WatchScanner.Scan() → entries[]
  │     (assetGuid, assetPath, typeName, dataId, watchRootKind)
  ├─ CollectCatalogGuidSequence(catalog) → catalogRegisteredGuids[]
  └─ inventory.ReplaceSnapshot(entries, catalogRegisteredGuids)
```

Player는 AssetDatabase 없이 Inventory 스냅샷으로 drift 검사.

---

## 11. Editor 메뉴

| 메뉴 | 동작 |
|---|---|
| `ND/Framework/Preview Shared Game Data Catalog Sync` | BuildPlan 출력만, **변경 없음** |
| `ND/Framework/Sync Shared Game Data Catalog` | 검증 통과 + 확인 후 Catalog/Inventory 갱신 |
| `ND/Framework/Refresh Shared Game Data Watch Inventory` | Inventory만 refresh (기존) |

Console prefix: `[CatalogSync]` (Preview/Sync), `[Framework]` (Inventory refresh)

---

## 12. 전체 데이터 흐름

```text
[Watch Roots]
  ProjectData / SandboxLegacy
        │
        ▼
  WatchScanner ──────────────────┐
        │                        │
        ▼                        ▼
  BuildPlan ◄── Catalog    TryRefreshWatchInventory
        │                        │
        ▼                        ▼
  Sync Apply ──► Catalog ──► Watch Inventory
        │                        │
        └──────── Resources.Load ─┴──► Runtime Drift Check
```

---

## 13. 사용 시나리오

### 신규 ProjectData SO 등록

1. watch root 아래 SO 생성, **유효한 data ID** 부여
2. Preview → Add 1, Error 0
3. Sync → 확인 다이얼로그 승인
4. Catalog append + Inventory refresh
5. Sync 재실행 → Add:0 Remove:0 no-op

### whitespace-only / empty ID

1. Preview → Error ≥ 1
2. Sync → 차단, **확인 다이얼로그 없음**, Catalog/Inventory 변경 없음

### 동일 타입 ID 중복

1. Preview → Error `"Duplicate target data ID within the same type"`
2. Sync → 차단

---

## 14. 검증 결과 (2026-07-31 focused reverification)

| 케이스 | 결과 |
|---|---|
| `""`, `"   "`, tab, `\r\n` | Preview Error, Sync blocked, no mutation |
| null (Unity → `""` 직렬화) | empty와 동일하게 차단 |
| `" build_01 "` | Add planned, ID 미변경 (no trim) |
| valid ProjectData Add | append, order preserved, Inventory ProjectData |
| duplicate same-type ID | Error, blocked |
| cleanup restore | Catalog 50 refs, Inventory 48 entries / 50 registered GUIDs |

---

## 15. 설계상 유의점

1. **Preview는 항상 안전** — 읽기 전용
2. **Error 하나라도 있으면 Sync 전체 차단** — 부분 적용 없음
3. **기존 Catalog 순서 보존** — reorder는 Sync 범위 밖
4. **SandboxLegacy는 Catalog 자동 편입 안 함**
5. **VerifyAppliedPlan 실패 시 Inventory refresh 생략**
6. **ID Trim 미적용** — 향후 data-quality 규칙 후보

---

## 16. 관련 런타임 타입 (참고)

| 타입 | 역할 |
|---|---|
| `SharedGameDataWatchRoots` | watch root 경로, RootKind 판별 |
| `SharedGameDataWatchInventory` | Player용 스냅샷 SO |
| `SharedGameDataDriftFinding` | 미등록 1건, `BlocksPlayerBuild` |
| `SharedGameDataService` | 런타임 Catalog 로드/조회 |

---

*이 문서는 `feature/framework/shared-data-catalog-sync-menu` 브랜치 diff (`10186e4`)와 Editor 소스를 기준으로 정리했다.*
