# BuildData Catalog / Watch Inventory 연동 요청

- 작성일: 2026-07-30
- 요청 대상: Framework & Integration 담당자
- 요청 상태: 연동 요청
- 관련 경로:
  - `Assets/_Project/02.Data/01_ScriptableObjects/Build`
  - `Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset`
  - `Assets/99.Sandbox/_LJH/02.SO/BuildSO/Build_Dummy.asset`

## 1. 요청 배경

현재 Project Data 경로의 정식 `BuildData` SO 7개와 Sandbox Legacy의 `Build_Dummy` 1개, 총 8개가 등록 대상입니다.

| Asset | Build ID |
|---|---|
| `Build_BaseCamp.asset` | `BaseCamp` |
| `Build_Store.asset` | `Store` |
| `Build_Warehouse.asset` | `Warehouse` |
| `Build_Farm.asset` | `Farm` |
| `Build_Cottage.asset` | `Cottage` |
| `Build_Windmill.asset` | `Windmill` |
| `Build_Bakery.asset` | `Bakery` |
| `Build_Dummy.asset` | `dummybuild` |

하지만 현재 `SandboxSharedGameDataCatalog`에는 다음 타입만 등록할 수 있습니다.

- `TownData`
- `MarketData`
- `TradeItemData`
- `WagonData`
- `DraftAnimalData`
- `RouteData`

`BuildData[]` 필드가 없으므로 위 8개 SO를 Catalog에 등록할 수 없습니다. 또한 `SharedGameDataCatalogDriftChecker`도 `BuildData`를 스캔하지 않기 때문에 다음 메뉴를 실행해도 Build SO가 Watch Inventory 및 drift 추적 대상에 포함되지 않습니다.

```text
ND/Framework/Refresh Shared Game Data Watch Inventory
```

따라서 현재 상태에서는 Build SO의 추가·삭제·GUID 변경·Catalog 누락을 Framework Refresh 과정에서 추적할 수 없습니다.

## 2. 최소 요청 범위

이번 요청의 최소 목표는 `BuildData`를 Framework의 공용 런타임 정의로 변환하는 것이 아니라, 기존 Catalog 및 Watch Inventory 정책에 포함하는 것입니다.

### 2.1 SandboxSharedGameDataCatalog

대상:

`Assets/_Project/11.CoreServices/Scripts/Data/SandboxSharedGameDataCatalog.cs`

다음 직렬화 필드와 복사본 반환 프로퍼티를 추가해 주십시오.

```csharp
[SerializeField] private global::BuildData[] builds;

public global::BuildData[] Builds =>
    builds != null
        ? (global::BuildData[])builds.Clone()
        : new global::BuildData[0];
```

기존 배열과 동일하게 원본 배열을 직접 노출하지 않는 계약을 유지해야 합니다.

### 2.2 Catalog Asset 등록

대상:

`Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset`

새 `builds` 필드에 1절의 Build SO 8개를 모두 등록해 주십시오.

등록 순서는 기능 계약으로 사용하지 않으며, 식별은 각 SO의 `BuildData.BuildId`를 기준으로 해야 합니다.

### 2.3 SharedGameDataCatalogDriftChecker

대상:

`Assets/_Project/11.CoreServices/Editor/SharedGameDataCatalogDriftChecker.cs`

다음 세 지점에 `BuildData`를 포함하고, 파일 상단의 지원 타입 설명도 함께 갱신해 주십시오.

1. Watch root 검색 필터

```csharp
"t:BuildData"
```

2. Catalog GUID 수집

```csharp
AddObjectGuids(catalog.Builds, guids);
```

3. Watch 대상 identity 판독

```csharp
case global::BuildData build:
    typeName = nameof(BuildData);
    dataId = build.BuildId;
    return true;
```

이 처리가 추가되어야 Project Data 경로의 Build SO가 Refresh 결과와 미등록 drift 검사에 함께 포함됩니다.

#### Sandbox Legacy Build_Dummy 등록 기준

`"t:BuildData"` 필터는 Project Data와 Sandbox Legacy를 모두 검색합니다. 따라서 다음 Legacy SO도 정식 `builds` 배열에 함께 등록해 주십시오.

```text
Assets/99.Sandbox/_LJH/02.SO/BuildSO/Build_Dummy.asset
```

이번 요청에서는 별도 제외 정책을 두지 않고 다음 8개를 모두 Catalog 및 Watch Inventory 추적 대상으로 사용합니다.

- Project Data BuildData 7개
- Sandbox Legacy BuildData 1개 (`Build_Dummy`)

`Build_Dummy`의 전체 경로는 `Assets/99.Sandbox/_LJH/02.SO/BuildSO/Build_Dummy.asset`이며, 실제 `BuildId`는 `dummybuild`입니다. identity는 다른 Build SO와 동일하게 `BuildData.BuildId`를 사용합니다.

### 2.4 Watch Inventory 갱신

구현 및 Catalog 등록 후 다음 메뉴를 실행해 주십시오.

```text
ND/Framework/Refresh Shared Game Data Watch Inventory
```

대상:

`Assets/_Project/11.CoreServices/Resources/SharedGameDataWatchInventory.asset`

갱신 결과에 Project Data Build SO 7개와 Sandbox Legacy의 `Build_Dummy.asset` 1개, 총 8개의 BuildData Entry가 기록되어야 합니다.

- Asset GUID
- Asset path
- Type name: `BuildData`
- Data ID: 각 `BuildId`
- Watch root kind: Project Data

## 3. 이번 요청에서 제외할 범위

아래 기능은 Catalog/Watch 추적을 위해 반드시 필요하지 않으므로 이번 최소 요청에서는 제외합니다.

- `ISharedGameDataProvider`에 Build 조회 API 추가
- `SharedGameDataView`에 Build Definition Dictionary 추가
- `BuildData`를 별도의 Framework DTO로 변환
- 건축 비용 또는 레벨 데이터를 다른 Catalog로 복제
- 현재 UI 및 `VillageBuildingRegistry`의 BuildData 조회 경로 변경

현재 건축 UI와 건축 실행 로직은 `BuildData`를 직접 비용 및 레벨 데이터의 단일 원본으로 사용합니다. Watch 연동을 이유로 동일 데이터를 별도 DTO나 비용 Catalog에 중복 저장하지 않아야 합니다.

추후 Framework를 통해 `BuildData` 런타임 조회까지 제공할 계획이라면 별도 계약으로 다음 항목을 합의한 뒤 확장하는 편이 안전합니다.

- `TryGetBuild(string buildId, ...)` 공개 여부
- Unity Object 참조를 Framework API에서 직접 노출할지 여부
- `DataPerLevel.buildPrefab` 같은 Scene/Prefab 참조의 변환 범위
- 건물 ID 중복 및 레벨 배열 검증 책임

## 4. 검증 요청

### 4.1 Catalog 표시

- `SandboxSharedGameDataCatalog.asset` Inspector에 Builds 배열이 표시된다.
- Build SO 8개가 Missing Reference 없이 등록된다.
- 빈 `BuildId` 또는 중복 `BuildId`가 없다.

### 4.2 Refresh 및 drift

- Refresh 메뉴 실행 후 Watch Inventory에 BuildData Entry가 총 8개 생성된다.
- Project Data 7개는 `ProjectData`, `Build_Dummy.asset`은 `SandboxLegacy`로 기록된다.
- Catalog에서 Build SO 하나를 임시로 제외했을 때 미등록 drift로 탐지된다.
- 다시 등록하고 Refresh하면 해당 drift가 해소된다.
- 기존 Town/Market/TradeItem/Wagon/DraftAnimal/Route 추적 결과가 변하지 않는다.

### 4.3 빌드 및 런타임

- Unity 컴파일 오류가 없다.
- 기존 `SharedGameDataService.LoadInitialData()` 결과가 변하지 않는다.
- InGame 진입 및 건축 Popup 동작에 회귀가 없다.
- Player Build에서 Watch Inventory 검증이 기존 정책대로 동작한다.

## 5. 완료 조건

- `SandboxSharedGameDataCatalog`가 `BuildData[]`를 직렬화하고 안전한 복사본 프로퍼티를 제공한다.
- Catalog Asset에 Build SO 8개가 등록되어 있다.
- Drift Checker가 `BuildData`를 스캔하고 `BuildId`를 identity로 기록한다.
- Watch Inventory Refresh 결과에 Project Data 7개와 Sandbox Legacy `Build_Dummy` 1개가 포함된다.
- 기존 Shared Game Data 타입의 등록·검증·런타임 로딩에 회귀가 없다.

## 6. 담당 경계

이 요청은 `11.CoreServices`의 Catalog 및 Watch/Drift 정책 변경이므로 Framework & Integration 담당 범위로 요청합니다.

UI/Building 쪽에서는 다음 사항을 유지합니다.

- Build SO 내부 데이터와 Prefab 참조 관리
- `BuildId` 유일성 유지
- `DataPerLevel` 레벨 및 건축 비용 데이터 관리
- Popup 및 건축 실행 시 `BuildData`를 단일 원본으로 사용

