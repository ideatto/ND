# Framework 공용 게임 데이터(Shared Game Data) 가이드

## 목적

이 문서는 Framework가 제공하는 **공용 기준 데이터(Shared Game Data)** 와 **SandboxSharedGameDataCatalog** 의 역할, 팀원 사용 방법, Public API를 설명한다.

공용 데이터 로드와 검증은 Framework & Integration이 소유한다.  
다른 feature 담당자는 Sandbox ScriptableObject를 직접 참조하기보다 `ISharedGameDataProvider` 계약을 통해 ID 기반으로 기준 데이터를 조회해야 한다.

---

## 1. 이 기능이 무엇인가

**Shared Game Data(공용 게임 데이터)** 는 게임 전역에서 공통으로 참조하는 **기준/정의 데이터**이다.

예시:

- 마을(Town) 정의
- 시장(Market) 정의
- 무역품(TradeItem) 정의
- 마차(Wagon) 정의
- 견인 동물(DraftAnimal) 정의
- 무역로(Route) 정의

플레이어의 현재 상태는 **SaveData**에 저장되고, SaveData에 들어 있는 ID가 무엇을 의미하는지는 **Shared Game Data**에서 조회한다.

```text
SaveData: selectedWagonId = "Wagon_S"
    ↓ ID lookup
Shared Game Data: wagon.MaxLoad, wagon.BaseEfficientLoad, ...
    ↓
UI / Core / Progression에서 표시·계산에 사용
```

### SaveData와의 차이

| 구분 | Shared Game Data | SaveData |
|------|------------------|----------|
| 성격 | 변하지 않는 기준 데이터 | 플레이어 진행 상태 |
| 예시 | 마차 최대 적재량, 상품 기본 가격 | 보유 재화, 선택한 마차 ID, 진행 중 trade |
| 저장 | 저장하지 않음 (ScriptableObject에서 읽기) | JSON 등으로 저장/로드 |
| 조회 API | `ISharedGameDataProvider` | `SaveData`, `FrameworkRoot.Instance.CurrentSaveData` |

---

## 2. SandboxSharedGameDataCatalog란

`SandboxSharedGameDataCatalog`는 Framework가 공용 데이터를 **어떤 ScriptableObject asset에서 읽을지** 정의하는 catalog asset이다.

- **타입:** `ND.Framework.SandboxSharedGameDataCatalog`
- **생성 메뉴:** `Assets > Create > ND > Framework > Sandbox Shared Game Data Catalog`
- **Runtime catalog 경로:** `Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset`
- **Resources 로드 이름:** `SandboxSharedGameDataCatalog` (확장자 제외)

Catalog는 Project/Sandbox SO를 이동하지 않고, Framework가 읽을 asset 참조 목록만 보관한다.  
1차 빌드 seed는 `Assets/_Project/02.Data/01_ScriptableObjects` 의 SO를 Resources catalog에 등록한다.

### Catalog가 포함하는 배열

| 배열 | SO 타입 |
|------|---------|
| `towns` | `TownData` |
| `markets` | `MarketData` |
| `tradeItems` | `TradeItemData` |
| `wagons` | `WagonData` |
| `draftAnimals` | `DraftAnimalData` |
| `routes` | `RouteData` |

### 1차 빌드 seed 권장 구성

| 배열 | 포함 asset |
|------|------------|
| Towns | `Town_BaseCamp`, `Town_RiverTown` |
| Markets | `Market_BaseCamp`, `Market_RiverTown` |
| TradeItems | `TradeItem_Apple`, `TradeItem_Bread`, `TradeItem_Wheat`, `TradeItem_Fish`, `TradeItem_Cloth` |
| Wagons | `Wagon_WagonM`, `Wagon_WagonS` |
| DraftAnimals | `DraftAnimal_donkey`, `DraftAnimal_Horse` |
| Routes | `Route_BaseToRiver` |

경로: `Assets/_Project/02.Data/01_ScriptableObjects/`

### Catalog 작성 시 주의사항

1. **같은 asset을 배열에 두 번 넣지 않는다.** duplicate ID error가 발생한다.
2. Market, Town, Route가 참조하는 ID의 asset은 **catalog 해당 배열에 포함**해야 한다.
3. 참조 무결성 검증 실패 시 **InGame 진입이 차단**된다.
4. `DraftAnimalData.IncreaseMaxLoad > 0`이면 **경고만** 출력한다. InGame 진입은 차단하지 않는다.
5. watch root에 SO를 추가·삭제한 뒤에는 catalog 등록과 **Watch Inventory refresh**를 함께 수행한다.

---

## 2.1 Catalog drift 검사

Framework는 catalog에 등록되지 않은 watch SO를 감지한다.

### Watch roots

| Root | 경로 | Player에서 미등록 시 |
|------|------|----------------------|
| ProjectData | `Assets/_Project/02.Data/01_ScriptableObjects` | **Error + InGame 차단** |
| SandboxLegacy | `Assets/99.Sandbox/_LJH/02.SO` | Warning만 (진입 허용) |

스캔 대상 타입: `TownData`, `MarketData`, `TradeItemData`, `WagonData`, `DraftAnimalData`, `RouteData`  
제외: `SandboxSharedGameDataCatalog`, `InGameTimePolicyConfig`

비교 기준은 **에셋 GUID**이다. ID 문자열만 같다고 동일 에셋으로 취급하지 않는다.

### 환경별 동작

| 환경 | 검사 방식 | ProjectData 미등록 | SandboxLegacy 미등록 |
|------|-----------|--------------------|----------------------|
| Unity Editor Play | `AssetDatabase` 실시간 스캔 | Warning, 진입 허용 | Warning, 진입 허용 |
| Player 빌드 | `SharedGameDataWatchInventory` 스냅샷 | Error, 진입 차단 | Warning, 진입 허용 |

### Watch Inventory 갱신 (Player 빌드 전 필수)

메뉴: `ND/Framework/Refresh Shared Game Data Watch Inventory`

- 경로: `Assets/_Project/11.CoreServices/Resources/SharedGameDataWatchInventory.asset`
- watch root 스냅샷과 현재 catalog 등록 GUID를 기록한다.
- **inventory를 갱신하지 않고 빌드하면** 신규 ProjectData SO drift를 Player가 감지하지 못할 수 있다.

---

## 3. 로드 흐름

```text
Loading 완료
  → SaveData 준비
  → SharedGameDataService.LoadInitialData()
      1. Catalog 로드 (Resources 우선)
      2. SO → Framework DTO 변환
      3. ID / 참조 무결성 검증
      4. Catalog drift 검사 (Editor 실시간 / Player inventory)
  → FrameworkEvents.SharedGameDataLoaded 발행
  → FrameworkEvents.LoadCompleted 발행
  → InGame 진입
```

### 데이터 소스 우선순위

```text
1. 생성자로 주입된 explicitCatalog (테스트용)
2. Resources.Load<SandboxSharedGameDataCatalog>("SandboxSharedGameDataCatalog")
3. Unity Editor 전용 Sandbox asset path fallback (Player build 불가)
```

Player build에서는 `Resources` 폴더 하위의 catalog와 `SharedGameDataWatchInventory`가 필요하다.

### 이벤트 순서

| 순서 | 이벤트 | 의미 |
|------|--------|------|
| 1 | `SharedGameDataLoaded` | 공용 기준 데이터 준비 완료 |
| 2 | `LoadCompleted` | SaveData 준비 완료, InGame 진입 직전 |

공용 데이터는 SaveData보다 먼저 준비된다.

---

## 4. 팀원 사용 방법

### 4.1 이벤트 구독 (권장)

공용 데이터가 필요한 시스템은 `SharedGameDataLoaded`를 구독한다.

```csharp
using ND.Framework;
using UnityEngine;

public sealed class MyFeatureBootstrap : MonoBehaviour
{
    private ISharedGameDataProvider sharedData;

    private void OnEnable()
    {
        FrameworkEvents.SharedGameDataLoaded += OnSharedGameDataLoaded;
    }

    private void OnDisable()
    {
        FrameworkEvents.SharedGameDataLoaded -= OnSharedGameDataLoaded;
    }

    private void OnSharedGameDataLoaded(ISharedGameDataProvider provider)
    {
        sharedData = provider;

        if (provider.TryGetWagon("dummywagonwithanimals", out var wagon))
        {
            Debug.Log($"Wagon max load: {wagon.MaxLoad}");
        }
    }
}
```

`OnEnable` / `OnDisable`에서 반드시 구독과 해제를 수행해야 한다.

### 4.2 FrameworkRoot 직접 조회

Loading 이후, InGame 런타임에서 직접 조회할 수 있다.

```csharp
var provider = FrameworkRoot.Instance?.SharedGameData;
if (provider != null && provider.IsLoaded)
{
    provider.TryGetTown("basecamp", out var town);
}
```

### 4.3 SaveData ID와 함께 사용하는 패턴

```csharp
// SaveData에는 ID만 저장
string wagonId = saveData.SelectedWagonId;

// 기준값은 Shared Game Data에서 조회
if (sharedData.TryGetWagon(wagonId, out var wagon))
{
    float maxLoad = wagon.MaxLoad;
    float efficientLoad = wagon.BaseEfficientLoad;
}
```

SaveData의 ID를 직접 해석하지 말고 Shared Game Data lookup을 사용한다.

---

## 5. Public API — `ISharedGameDataProvider`

- **Namespace:** `ND.Framework`
- **파일:** `Assets/_Project/11.CoreServices/Scripts/Data/ISharedGameDataProvider.cs`

### 상태 조회

```csharp
bool IsLoaded { get; }          // 검증 통과 후 true
string Summary { get; }         // 디버그용 요약 문자열

int TownCount { get; }
int MarketCount { get; }
int TradeItemCount { get; }
int WagonCount { get; }
int DraftAnimalCount { get; }
int RouteCount { get; }
```

### ID 목록

```csharp
IReadOnlyList<string> TownIds { get; }
IReadOnlyList<string> MarketIds { get; }
IReadOnlyList<string> TradeItemIds { get; }
IReadOnlyList<string> WagonIds { get; }
IReadOnlyList<string> DraftAnimalIds { get; }
IReadOnlyList<string> RouteIds { get; }
```

### ID 기반 lookup

```csharp
bool TryGetTown(string id, out SharedTownDefinition town);
bool TryGetMarket(string id, out SharedMarketDefinition market);
bool TryGetTradeItem(string id, out SharedTradeItemDefinition tradeItem);
bool TryGetWagon(string id, out SharedWagonDefinition wagon);
bool TryGetDraftAnimal(string id, out SharedDraftAnimalDefinition draftAnimal);
bool TryGetRoute(string id, out SharedRouteDefinition route);
```

- ID가 없거나 비어 있으면 `false`를 반환한다.
- 반환 객체는 **읽기 전용 스냅샷**으로 취급해야 한다. 호출자가 값을 수정하지 않아야 한다.

---

## 6. DTO 필드 참조

**파일:** `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataView.cs`

### SharedTownDefinition

| 필드 | 설명 |
|------|------|
| `Id` | 마을 ID |
| `DisplayName` | 표시 이름 |
| `UnlockedByDefault` | 기본 해금 여부 |
| `MarketId` | 연결된 시장 ID |
| `AvailableRouteIds` | 이용 가능 무역로 ID 배열 |
| `CanContribute` | 기부 가능 여부 |
| `MaximumContributionLimit` | 최대 기부 한도 |

### SharedMarketDefinition

| 필드 | 설명 |
|------|------|
| `Id` | 시장 ID |
| `ItemMaxQuantity` | 아이템 최대 수량 |
| `ItemRenewalCycle` | 아이템 갱신 주기 |
| `TradeItemIds` | 판매 무역품 ID 배열 |
| `DraftAnimalIds` | 판매 견인 동물 ID 배열 |
| `WagonIds` | 판매 마차 ID 배열 |
| `LocalSpecialtyItemIds` | 지역 특산품 ID 배열 |

### SharedTradeItemDefinition

| 필드 | 설명 |
|------|------|
| `Id`, `DisplayName` | ID, 표시 이름 |
| `Rarity`, `Category` | 희귀도, 카테고리 |
| `BaseBuyPrice`, `BaseSellPrice` | 기본 매수/매도가 |
| `CanStack`, `MaxCount` | 스택 가능 여부, 최대 수량 |
| `Weight` | 무게 |
| `IsConsumable`, `LocalSpecialty` | 소비품 여부, 지역 특산품 여부 |

### SharedWagonDefinition

| 필드 | 설명 |
|------|------|
| `Id`, `DisplayName`, `WagonType` | ID, 이름, 타입 |
| `MaxDurability` | 최대 내구도 |
| `BaseEfficientLoad` | 기본 적정 적재량 |
| `MaxLoad` | 최대 적재량 |
| `BaseMoveSpeed` | 기본 이동 속도 |
| `InventorySlotCount` | 인벤토리 슬롯 수 |
| `MaxPullAnimals`, `MinRequireAnimals` | 최대/최소 견인 동물 수 |
| `EligibleAnimalTypes` | 허용 견인 동물 타입 |
| `BaseBuyPrice`, `CanStack`, `MaxCount` | 구매가, 스택 정보 |

### SharedDraftAnimalDefinition

| 필드 | 설명 |
|------|------|
| `Id`, `DisplayName`, `AnimalType` | ID, 이름, 동물 타입 |
| `FoodConsumptionPerSecond` | 초당 식량 소모 |
| `BaseMoveSpeed` | 기본 이동 속도 |
| `AdditionalEfficientLoad` | 추가 효율 적재량 |
| `BaseBuyPrice`, `CanStack`, `MaxCount` | 구매가, 스택 정보 |

Sandbox `IncreaseMaxLoad`는 GDD상 사용하지 않으며 Framework DTO에 포함되지 않는다.

### SharedRouteDefinition

| 필드 | 설명 |
|------|------|
| `Id`, `DisplayName` | ID, 이름 |
| `FromTownId`, `ToTownId` | 출발/도착 마을 ID |
| `UnlockedByDefault` | 기본 해금 여부 |
| `Distance`, `DefaultElapsedTime` | 거리, 기본 소요 시간 |
| `BaseRequiredFoodQuantity` | 기본 필요 식량 |
| `BaseRequiredMercenaryPower` | 기본 필요 용병 전투력 |
| `BaseRiskLevel` | 기본 위험도 |
| `MaxEventCount` | 최대 이벤트 수 |

---

## 7. 이벤트 API — `FrameworkEvents`

**파일:** `Assets/_Project/11.CoreServices/Scripts/Events/FrameworkEvents.cs`

```csharp
// 구독
FrameworkEvents.SharedGameDataLoaded += handler;

// Framework 내부 발행. 외부에서 직접 Invoke하지 않는다.
FrameworkEvents.RaiseSharedGameDataLoaded(provider);
```

| 이벤트 | 인자 | 발생 시점 |
|--------|------|-----------|
| `SharedGameDataLoaded` | `ISharedGameDataProvider` | 공용 데이터 검증 성공 직후 |
| `LoadCompleted` | `SaveData` | SaveData와 SharedGameData 준비 후, InGame 직전 |

---

## 8. Catalog asset 관리

### Catalog 수정 방법

1. Unity에서 `Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset`를 연다.
2. Inspector에서 각 배열에 `02.Data/01_ScriptableObjects` ScriptableObject asset을 등록한다.
3. 메뉴 `ND/Framework/Refresh Shared Game Data Watch Inventory`를 실행한다.
4. Play Mode 또는 `SharedDataTest` scene으로 검증한다.

### 검증 실패 시

Console에 Error가 출력되고 InGame 진입이 차단된다.

| 로그 예시 | 원인 |
|-----------|------|
| `duplicate ID: BaseCamp` | 동일 Town asset 중복 등록 |
| `wagon reference is missing: Wagon_S` | Market이 참조하는 Wagon이 catalog에 없음 |
| `Shared game data asset is not registered in catalog: ... (root=ProjectData)` | ProjectData SO가 catalog에 없음 (Player에서는 진입 차단) |
| `InGame entry blocked because shared game data validation failed.` | 검증 실패 결과 |

### 디버그 확인

- `FrameworkDebugBridge` → ContextMenu → `Framework/Log Shared Game Data Summary`
- 또는 `FrameworkRoot.Instance.DebugCommands.LogSharedGameDataSummary()`

---

## 9. 테스트 Scene

**경로:** `Assets/_Project/11.CoreServices/Scenes/SharedDataTest.unity`

| GameObject | Component |
|------------|-----------|
| Controller | `LoadingSceneController` (`completeLoadingOnStart = true`) |
| Listener | `SharedDataTestListener` |

성공 시 Console 예시:

```text
[Framework] Shared game data loaded. Towns: 2, Markets: 2, ...
[SharedDataTest] SharedGameDataLoaded: Towns: 2, Markets: 2, ...
[SharedDataTest] LoadCompleted
```

`InGame` scene은 Build Settings에 등록되어 있어야 `CompleteLoadingAndEnterGame()` 이후 scene 전환이 정상 동작한다.

---

## 10. 자주 묻는 질문

### Q. Sandbox ScriptableObject를 직접 참조해도 되나요?

M0/M1 통합 단계에서는 권장하지 않는다.  
`ISharedGameDataProvider`를 사용하면 Sandbox 타입 변경 시 Framework adapter만 수정하면 된다.

### Q. SharedGameDataLoaded 전에 API를 호출하면?

`FrameworkRoot.Instance.SharedGameData`가 null이거나 `IsLoaded == false`일 수 있다.  
이벤트 구독 후 사용하거나 Loading 완료 이후에 조회해야 한다.

### Q. Catalog를 Tests 폴더에 두면 되나요?

Runtime과 Player build에서는 **`Resources` 폴더 하위**에 있어야 `Resources.Load`로 찾을 수 있다.

### Q. SaveData에 wagon 스펙 전체를 저장해야 하나요?

아니다. SaveData에는 ID만 저장하고, 스펙은 Shared Game Data에서 조회한다.

### Q. Shared `markets`와 SaveData `world.marketInventories`는 같나요?

아니다. 역할이 다르다.

| 구분 | Shared Game Data `markets` | SaveData `world.marketInventories` |
|------|----------------------------|--------------------------------------|
| 의미 | 상점 **정의**(어떤 마을·상품 풀인지) | 플레이어 세션의 상점 **재고 스냅샷** |
| 출처 | Catalog의 `MarketData` SO | `JsonSaveService` 저장/로드 |
| 조회 | `TryGetMarket(id, ...)` | `CurrentSaveData.world.marketInventories` |

품목 스펙(무게·기본가 등)은 Shared `tradeItems`에서 조회하고, 현재 남은 수량·단가·갱신 구간은 SaveData market inventory에 둔다.  
구매 초안·확정 마커는 `world.marketPurchasePreparation`, 적재 품목 자체는 `caravan.cargo`에 둔다.

---

## 11. 관련 파일

| 파일 | 설명 |
|------|------|
| `Assets/_Project/11.CoreServices/Scripts/Data/ISharedGameDataProvider.cs` | Public API 계약 |
| `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataView.cs` | DTO 정의 및 구현 |
| `Assets/_Project/11.CoreServices/Scripts/Data/SandboxSharedGameDataCatalog.cs` | Catalog ScriptableObject 타입 |
| `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataService.cs` | 로드, 변환, 검증, drift 검사 |
| `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataWatchInventory.cs` | Player용 watch 스냅샷 타입 |
| `Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataWatchRoots.cs` | watch root 경로 상수 |
| `Assets/_Project/11.CoreServices/Editor/SharedGameDataCatalogDriftChecker.cs` | Editor 스캔·inventory refresh |
| `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs` | Startup 연동 |
| `Assets/_Project/11.CoreServices/Scripts/Events/FrameworkEvents.cs` | 이벤트 |
| `Assets/_Project/11.CoreServices/Resources/SandboxSharedGameDataCatalog.asset` | Runtime catalog |
| `Assets/_Project/11.CoreServices/Resources/SharedGameDataWatchInventory.asset` | Player drift 검사용 inventory |
| `Assets/_Project/11.CoreServices/Scenes/SharedDataTest.unity` | 테스트 scene |
| `Assets/_Project/02.Data/01_ScriptableObjects/` | 1차 빌드 공용 SO seed |

---

## 12. 문의

- **Framework 구조, API, 로드 순서:** Framework & Integration 담당
- **공용 SO seed (`02.Data`), Catalog asset 내용:** Content & Tools, UI & Data 담당과 협의
- **SaveData ID 필드:** 각 feature SaveData owner와 Framework 담당 협의
