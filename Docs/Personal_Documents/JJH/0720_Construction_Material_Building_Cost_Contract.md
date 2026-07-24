# Construction Material and Building Cost Contract

- 작성일: 2026-07-20
- 담당: Progression & System / Economy
- 기준 브랜치: `dev2`
- 상태: M0 구현 계약
- 상위 문서: `0720_Progression_Economy_M0_Contract.md`

## 1. 목적

건축 재료를 일반 무역품으로 구매·판매·운송하면서, 플레이어 거점에서는 건물 신축·강화 비용으로 소비하는 계산 및 상태 변경 계약을 고정한다.

이 계약은 건축 재료 전용 화폐나 별도 재료 인벤토리를 만들지 않는다. Caravan으로 운송한 재료는 먼저 거점 도시의 `homeInventory`로 이전하며, 건설 시점에는 `homeInventory`를 재료 원본으로 사용한다.

## 2. 확정 정책

- 건축 재료는 일반 무역품과 동일한 상품이다.
- 도시 시장에서 다른 상품처럼 구매·판매한다.
- Caravan 화물로 적재·운송한다.
- 건설·강화 비용은 `homeInventory`에 보관된 `TradeItemCategory.Material` 상품만 소비한다.
- `SaveData.caravan.cargo`에 남은 재료는 건설에 직접 사용할 수 없다.
- Caravan으로 운송한 재료를 사용하려면 먼저 재료 이전 Command로 `homeInventory`에 이전해야 한다.
- 재료 이전은 Caravan cargo와 homeInventory 변경을 하나의 transaction으로 저장한다.
- 이전 요청은 중복 itemId 합계, Caravan 적재 수량, 이전 가능 상태, homeInventory 용량을 검증한다.
- 시장 재고에 있거나 이동 중인 재료는 건설에 사용할 수 없다.
- 건설 비용은 하나 이상의 `itemId + quantity` 요구 목록이다.
- `developmentCurrency`는 건축 재료가 아니며 건설 비용으로 사용하지 않는다.
- 건물 진행 상태는 안정적인 `VillageBuildingSaveData.buildingId + level`로 저장한다.
- 표시 이름과 비용 정의는 Shared Data에서 `buildingId`로 조회한다.
- homeInventory 재료 차감과 건물 레벨 변경은 한 번의 즉시 저장 작업이다.

## 3. 팀 책임

| 영역 | 담당 | 책임 |
|---|---|---|
| 비용·검증 | Progression/Economy | 레벨별 요구 재료, 보유량 검사, 소비 결과, 실패 코드 |
| 상품 데이터 | Content/Tools | 건축 재료 상품 ID, 가격, 도시별 공급량, 건물별 요구 수량 |
| 이전 가능 상태 검증 | Core | Caravan cargo 수량, 본기지·비이동 상태, homeInventory 수용 가능 여부 검증 |
| 저장·복구 | Framework | 이전 시 `caravan.cargo`·`homeInventory`, 건설 시 `homeInventory`·`villageBuildings` 원자적 저장과 rollback |
| 화면 | UI & Data | 재료 요구량·homeInventory 보유량·부족량 표시, 이전·건설 요청, 실패 표시 |
| 씬 반영 | Core/Village | 저장 성공 후 건물 생성·레벨·효과 반영 |

UI와 Core는 요구 재료 계산을 복제하지 않는다. `VillageBuildingRegistry`는 저장 성공 전에 건물 레벨이나 씬 오브젝트를 확정하지 않는다.

## 4. 건축 재료 식별

건축 재료는 기존 `TradeItemData.Category == TradeItemCategory.Material`인 상품으로 정의한다.

- `TradeItemData`의 serialized `category` 필드와 공개 `Category` property를 단일 분류 기준으로 사용한다.
- `TradeItemSaveData`에 `isConstructionMaterial` 같은 중복 필드를 추가하지 않는다.
- `TradeItemSaveData`에는 category가 없으므로 계산 입력 adapter가 `itemId`로 `TradeItemData`를 조회해 category를 복원한다.
- `Material`이 아닌 Food, Valuable, LuxuryGoods, DraftAnimalsFood는 건설 재료 보유량에서 제외한다.
- 건물 요구 목록의 모든 `itemId`는 Content 검증 시 `TradeItemCategory.Material`인지 확인한다.
- UI의 건축 재료 필터도 `TradeItemData.Category == Material`을 사용한다.
- 표시 이름이 아니라 안정적인 `itemId`로 비교한다.

## 5. 건물 비용 정의

```csharp
[Serializable]
public sealed class BuildingMaterialRequirement
{
    public string ItemId = string.Empty;
    public int Quantity;
}

[Serializable]
public sealed class BuildingLevelCost
{
    public int TargetLevel;
    public List<BuildingMaterialRequirement> Materials = new List<BuildingMaterialRequirement>();
}

[Serializable]
public sealed class BuildingCostDefinition
{
    public string BuildingId = string.Empty;
    public int MaxLevel = 1;
    public List<BuildingLevelCost> LevelCosts = new List<BuildingLevelCost>();
}
```

`BuildingId`를 저장 및 정의 조회의 안정적인 계약 키로 사용한다. 표시 이름과 localization key는 Shared Data의 표시 정보이며 저장 식별자로 사용하지 않는다.

정의 검증:

- `BuildingId`는 비어 있지 않아야 한다.
- `MaxLevel >= 1`이어야 한다.
- 각 `TargetLevel`은 1~`MaxLevel` 범위에서 중복되지 않아야 한다.
- 신축은 `TargetLevel = 1` 비용을 사용한다.
- 강화는 `TargetLevel = currentLevel + 1` 비용을 사용한다.
- 요구 `ItemId`는 비어 있지 않아야 한다.
- 요구 수량은 0보다 커야 한다.
- 한 레벨 비용에서 같은 `ItemId`를 중복 정의하지 않는다.
- 요구 상품 ID가 Content 상품 카탈로그에 존재해야 한다.
- 요구 상품의 `TradeItemData.Category`가 `Material`이어야 한다.
- 요구 목록이 비어 있는 무료 건설은 허용하지 않는다.

## 6. 인벤토리 계산 입력

Progression 계산기는 Framework DTO를 직접 변경하지 않고 다음 snapshot을 입력으로 받는다.

```csharp
[Serializable]
public sealed class InventoryItemAmount
{
    public string ItemId = string.Empty;
    public TradeItemCategory Category;
    public int Quantity;
}
```

- 동일 `ItemId` 항목이 여러 개면 합산해 보유량을 계산한다.
- `Category == TradeItemCategory.Material`인 항목만 건설 재료 보유량으로 집계한다.
- adapter는 homeInventory의 `itemId`로 `TradeItemData`를 조회해 `Category`를 채운다.
- 저장된 `itemId`에 대응하는 `TradeItemData`를 찾지 못하면 손상된 입력으로 처리한다.
- null 항목과 빈 ID는 유효하지 않은 입력으로 보고한다.
- 음수 수량은 유효하지 않은 저장 상태다.
- 수량 합산 시 `int` overflow를 검사한다.
- 결과 계산은 입력 리스트와 항목을 변경하지 않는다.

## 7. 건설 가능 여부 계산

### 입력

```csharp
public sealed class BuildingCostInput
{
    public string BuildingId = string.Empty;
    public int CurrentLevel;
    public int MaxLevel;
    public List<BuildingLevelCost> LevelCosts = new List<BuildingLevelCost>();
    public List<InventoryItemAmount> HomeInventory = new List<InventoryItemAmount>();
}
```

### 결과

```csharp
public sealed class BuildingCostResult
{
    public bool Success;
    public BuildingCostFailureReason FailureReason;
    public string BuildingId = string.Empty;
    public int PreviousLevel;
    public int TargetLevel;
    public List<BuildingMaterialDelta> Materials = new List<BuildingMaterialDelta>();
}

public sealed class BuildingMaterialDelta
{
    public string ItemId = string.Empty;
    public int RequiredQuantity;
    public int QuantityBefore;
    public int QuantityAfter;
    public int MissingQuantity;
}
```

계산:

```text
targetLevel = currentLevel + 1
quantityAfter = quantityBefore - requiredQuantity
missingQuantity = max(0, requiredQuantity - quantityBefore)
```

모든 재료의 `MissingQuantity == 0`일 때만 `Success = true`다.

Preview와 실제 건설 Command는 동일한 계산기와 같은 정의를 사용한다.

## 8. 실패 코드

```csharp
public enum BuildingCostFailureReason
{
    None,
    InvalidInput,
    InvalidDefinition,
    BuildingNotFound,
    AlreadyMaxLevel,
    LevelCostNotFound,
    InventoryCorrupted,
    InsufficientMaterials,
    Overflow
}
```

- 재료 하나라도 부족하면 `InsufficientMaterials`다.
- 실패 결과도 모든 요구 재료의 보유·필요·부족 수량을 UI에 제공한다.
- 저장 실패는 계산 실패가 아니므로 `BuildingCostFailureReason`에 넣지 않는다.

## 9. 건설 Command 상태 변경 순서

구현 API:

```csharp
SaveResult HomeInventoryBuildingConstructionCommand.Execute(
    SaveData saveData,
    ISaveService saveService,
    IEnumerable<TradeItemData> tradeItemCatalog,
    BuildingCostDefinition definition,
    out BuildingCostResult costResult);
```

- Command 반환값은 기존 결정대로 `SaveResult`를 직접 사용한다.
- 비용 계산 상세와 UI preview 값은 `out BuildingCostResult`로 제공한다.
- 씬 건물 생성·효과 적용은 `SaveResult.Succeeded` 이후 호출자가 수행한다.

```text
1. 구조 대출 제한 모드와 건물 정의·현재 레벨 검증
2. 현재 `SaveData.player.homeInventory`와 대상 건물 level snapshot
3. `HomeInventoryBuildingCostInputFactory`로 homeInventory와 상품 카탈로그를 `BuildingCostInput`에 변환
4. `BuildingCostCalculator` 계산
5. 계산 실패면 상태 변경·Save 호출 없음
6. 성공 결과의 요구 수량만 homeInventory에서 차감 stage
7. 기존 건물이면 level을 targetLevel로 stage
8. 신축이면 buildingId + level 1 항목 추가 stage
9. 즉시 Save
10. 저장 성공 후 씬 건물 생성·레벨 갱신·효과 적용 및 event/UI refresh
11. 저장 실패면 homeInventory와 building snapshot rollback
```

현재 `VillageBuildingRegistry.AddOrUpgrade(int)`는 비용 검증 없이 즉시 씬과 SaveData 레벨을 변경한다. UI는 이 메서드를 직접 호출하지 않고 새 건설 Command를 호출하도록 전환해야 한다.

호환 어댑터가 필요하면 `AddOrUpgrade`는 새 Command 성공 후의 씬 반영 전용 경로로 축소한다. 동일 요청을 두 번 적용하지 않는다.

## 10. homeInventory 차감 규칙

- 요구 재료는 `SaveData.player.homeInventory`에서 `itemId`로 찾는다.
- 조회한 `TradeItemData.Category`가 `Material`인 entry만 재료 차감 대상이다.
- 같은 ID가 여러 entry에 있으면 리스트 순서대로 차감하거나 먼저 정규화해 하나로 합친다.
- 차감 후 수량이 0이면 해당 entry를 제거할 수 있다.
- 요구하지 않은 상품은 변경하지 않는다.
- `TradeItemSaveData`의 이름·무게·가격 snapshot은 재료 계산 기준이 아니다.
- 재료 차감 중 일부만 적용한 상태로 실패해서는 안 된다.

## 11. 재료 이전과 건설 상태 검증

- Caravan에서 homeInventory로 이전할 때 플레이어의 `currentTownId`가 본기지 ID와 일치해야 한다.
- Caravan이 `Traveling` 또는 `SettlementPending`이면 재료 이전을 거부한다.
- 시장 거래 draft가 확정되지 않은 상태에서는 재료 이전을 거부한다.
- 중복 itemId 요청은 먼저 합산한 뒤 cargo 보유량과 homeInventory 용량을 검증한다.
- 이전 검증은 UI가 아니라 재료 이전 Command에서 수행한다.
- 건설 Command는 Caravan cargo를 재료 원본으로 사용하지 않으며, 구조 대출 제한 모드와 homeInventory 보유량을 검증한다.

## 12. 시장 매매와 정산

- 건축 재료 구매는 기존 시장 구매·화물 적재 계약을 그대로 사용한다.
- 가격은 일반 `PriceCalculator`와 시장 `unitPrice` 정책을 따른다.
- 판매 시 일반 상품과 동일하게 판매 수익을 정산한다.
- homeInventory로 이전한 재료는 Caravan cargo에서 제거되므로 이후 Caravan 판매·정산 대상에 포함되지 않는다.
- 건설에 소비한 재료는 homeInventory에서 제거한다.
- 건설 소비는 무역 정산 비용으로 기록하지 않는다.
- 건설 UI에서는 거래 당시 매입가가 아니라 요구 수량을 핵심 비용으로 표시한다.

## 13. ViewData 계약

UI에 최소 다음 값을 제공한다.

- 건물 표시 이름
- 현재 레벨과 목표 레벨
- 최대 레벨 여부
- 재료별 표시 이름 또는 localization key
- 요구 수량
- homeInventory 보유 수량
- 부족 수량
- 건설 가능 여부
- 안정적인 실패 코드

시장 재고와 Caravan cargo는 참고 정보로 표시할 수 있지만 건설 가능 수량에는 포함하지 않는다. 건설 가능 수량은 현재 homeInventory만 사용한다.

## 14. Event 계약

저장 성공 후 event 후보:

- `HomeInventoryChanged`
- `BuildingConstructed`
- `BuildingUpgraded`

event payload에는 `itemId`, 변경 전후 수량, `buildingId`, 이전·새 레벨과 저장 revision을 가능한 범위에서 포함한다.

event 수신자는 재료나 레벨을 다시 변경하지 않고 UI·씬을 갱신하는 데만 사용한다.

## 15. 테스트 매트릭스

### 비용 정의

- 신축은 target level 1 비용 사용
- 강화는 current level + 1 비용 사용
- 최대 레벨이면 거부
- 대상 레벨 비용 누락 시 거부
- 빈 ID·0 이하 수량·중복 재료 정의 거부

### 보유량 계산

- 동일 itemId 복수 entry 합산
- `TradeItemCategory.Material`만 보유 재료에 포함하고 다른 category는 제외
- 저장 itemId의 `TradeItemData` 조회 실패 시 손상된 입력으로 거부
- 정확한 수량 보유 시 성공 및 잔량 0
- 재료 하나 부족 시 전체 실패
- 여러 재료의 부족량을 모두 반환
- 음수 inventory 수량과 합산 overflow 거부
- 입력 리스트와 entry 불변

### 저장·rollback

- 성공 시 재료 차감과 레벨 증가를 한 번만 적용
- 신축 성공 시 level 1 추가
- 저장 실패 시 재료와 레벨 원복
- 저장 실패 시 씬 건물 생성·효과·성공 event 없음
- 동일 요청 연속 입력으로 중복 건설되지 않음

### 위치·상태 검증

- 거점 외 위치에서 재료 이전 거부
- Traveling·SettlementPending 중 재료 이전 거부
- 보유량 초과 소비 거부
- 관계없는 homeInventory 항목 유지

### 재실행

- 건설 후 재실행 시 재료 잔량과 건물 레벨 유지
- 기존 null `homeInventory`와 `villageBuildings` 정규화 유지

## 16. 기존 코드 영향

| 위치 | 현재 상태 | 후속 작업 |
|---|---|---|
| `03.Economy` | 건설 비용 계산기 없음 | 모델·순수 계산기·Editor 테스트 추가 |
| `SaveData.player.homeInventory` | 거점 도시 인벤토리 | Framework 어댑터 입력 및 건설 Command 차감 원본으로 사용 |
| 재료 이전 Command | 연결 필요 | cargo와 homeInventory를 원자적으로 변경하고 이전 상태·수량·용량 검증 |
| `HomeInventoryBuildingCostInputFactory` | 전환 필요 | homeInventory와 `TradeItemData`를 `BuildingCostInput` snapshot으로 변환 |
| `HomeInventoryBuildingConstructionCommand` | 전환 필요 | homeInventory 재료 차감, buildingId 기반 레벨 upsert, SaveResult 검사와 rollback |
| `VillageBuildingRegistry.AddOrUpgrade` | 비용 없이 즉시 레벨·씬 변경 | UI 직접 호출 제거, 저장 성공 후 씬 반영으로 전환 |
| `BuildingAddPopup` | `AddOrUpgrade` 직접 호출 | ViewData·Command 기반 요청으로 변경 |
| `SaveData` | homeInventory와 기존 displayName+level 존재 | stable buildingId 추가 및 기존 저장 마이그레이션 |
| `JsonSaveService` | 관련 리스트 null 정규화 존재 | 재료 entry·수량 정규화 정책 확인 |

## 17. 완료 기준

- 건설 preview와 실제 Command가 같은 비용 계산기를 사용한다.
- 건축 재료는 일반 상품처럼 매매·운송된다.
- 본기지에 도착한 Caravan의 재료를 homeInventory로 이전한 뒤 건설에 사용한다.
- 재료 차감과 건물 레벨 변경이 한 저장 경계에서 처리된다.
- 저장 실패 시 재료·건물·씬 상태가 확정되지 않는다.
- UI가 재료별 요구·보유·부족 수량을 표시할 수 있다.

## 18. 2026-07-20 구현 반영

- 기존 `BuildingCostInput.CaravanCargo`는 기획 의도와 어긋나므로 `HomeInventory`로 전환한다.
- 기존 `CaravanBuildingCostInputFactory`와 `CaravanBuildingConstructionCommand`는 직접 사용하지 않고 homeInventory 기반으로 교체한다.
- 본기지 위치 및 Traveling·SettlementPending 검증은 재료 이전 Command에 적용한다.
- 중복 이전 요청 합산, cargo 차감, homeInventory 추가와 용량 검증을 재료 이전 Command에 적용한다.
- 건물 `displayName + level` 저장은 `buildingId + level`로 마이그레이션한다.
- 저장 실패 시 재료 이전은 cargo·homeInventory, 건설은 homeInventory·건물 목록을 각각 rollback한다.
- 성공 전 씬·UI를 변경하지 않는 경계 적용
- 독립 계산·Command 검사 통과
- `VillageBuildingRegistry`는 Core Gameplay 관할이므로 Progression 작업에서 수정하지 않음
- `BuildingAddPopup`은 UI 팀 관할이므로 Progression 작업에서 수정하지 않음
- 저장 성공 후 씬 건물 생성·레벨 갱신 연결은 관할 팀 요청으로 이관
- Popup의 실패 표시·유지 및 성공 callback·Close 정책은 UI 팀 연결 요청으로 이관
- Unity runtime·Editor assembly 컴파일 성공

## 19. 타 팀 연결 계약

Progression 제공 경계는 다음 세 API까지다.

- `HomeInventoryBuildingCostInputFactory.TryCreate(...)`
- `BuildingCostCalculator.Calculate(...)`
- `HomeInventoryBuildingConstructionCommand.Execute(...)`

`VillageBuildingRegistry`와 `BuildingAddPopup`은 변경하지 않는다. 자세한 호출 예제와 연결 제한 사항은 `0720_Progression_Requested_All_Teams.md`의 건설 Command 연결 요청을 따른다.

## 20. BuildingCostCatalog 구현 계약

구현 위치:

- `Assets/_Project/03.Economy/09_Building/BuildingCostCatalog.cs`
- Unity 생성 메뉴: `Assets > Create > ND > Economy > Building Cost Catalog`

조회 API:

```csharp
bool TryGetDefinition(
    string buildingId,
    out BuildingCostDefinition definition);
```

- stable `buildingId`를 조회 키로 사용한다.
- 동일 이름이 두 개 이상이면 조회를 거부한다.
- 반환되는 정의는 깊은 복제본이다.
- 호출자가 반환값의 레벨이나 수량을 바꿔도 catalog asset은 변경되지 않는다.

검증 API:

```csharp
bool Validate(
    IEnumerable<TradeItemData> tradeItemCatalog,
    out List<BuildingCostCatalogFinding> findings);
```

검증 범위:

- 빈 건물 catalog
- 빈 건물 이름, `MaxLevel < 1`, null 비용 목록
- 중복 `buildingId`
- 1~`MaxLevel` 사이의 누락·중복 레벨 비용
- 빈 재료 목록
- 빈 `itemId`, 0 이하 수량, 같은 레벨의 중복 재료
- null·빈 ID·중복 ID가 포함된 상품 catalog
- 상품 catalog에 없는 재료 ID
- `TradeItemCategory.Material`이 아닌 비용 상품

`BuildingCostCatalogFinding`은 건물 이름, 목표 레벨, 상품 ID, 안정적인 실패 enum과 진단 메시지를 제공한다.

2026-07-20 반영 상태:

- ScriptableObject catalog 구현 완료
- 깊은 복제 조회 구현 완료
- 전체 정의 및 상품 분류 검증 구현 완료
- Editor 검사 추가 완료
- 독립 검사 `[Building Cost Catalog Checks] Success`
- Unity runtime·Editor assembly 컴파일 성공
