# Progression Requested — All Teams

- 작성일: 2026-07-20
- 기준 브랜치: `dev2`
- 상태: Progression 공통 구현 요청
- 목적: 무역 도착과 상품 거래를 분리하고, 도착지에서 명시적으로 구매·판매한 뒤 다음 이동을 준비할 수 있게 한다.
- 관련 문서: `0720_Progression_Economy_M0_Contract.md`
- 관련 문서: `0720_Construction_Material_Building_Cost_Contract.md`

## 1. 확정 구조

```text
현재 도시에서 무역 준비
-> Traveling
-> SettlementPending
-> 이동 결과 Claim
-> 현재 도시를 목적지로 변경
-> Town 화면
-> 목적지 시장에서 명시적 구매·판매
-> 다음 목적지 또는 본기지로 출발
```

- 도착만으로 적재 상품을 자동 판매하지 않는다.
- 이동 결과 Claim은 이동 비용·손실·보상만 확정한다.
- 판매 전 상품은 `SaveData.caravan.cargo`에 유지한다.
- 목적지와 본기지 모두 도착 후 `Town` 화면으로 진입한다.
- `AtTown`을 `JourneyState` 또는 `TradeProgressState`에 추가하지 않는다.
  - `JourneyState`는 한 번의 여정 수명주기만 표현한다.
  - `TradeProgressState`는 Claim 성공 시 기존 `Completed` 또는 `Failed`로 끝낸다.
  - 현재 위치는 기존 `player.currentTownId`가 담당한다.
  - 도착 후 화면은 `InGameScreenState.Town` 또는 `Market` 추가로 표현한다.

## 2. Progression / Economy 요청

### 자동 판매 제거 지점

자동 판매는 Claim 단계가 아니라 정산 입력과 결과 생성 단계에서 이미 발생한다. 다음 코드를 우선 변경한다.

- `FrameworkEconomyM1InputBuilder.TryBuild`
  - cargo에서 임의 판매 상품과 `sellQuantity`를 선택하지 않는다.
- `EconomyM1LoopCalculator.Execute`
  - 이동 정산 결과에서 `ItemSaleRevenue`와 `SoldItems`를 만들지 않는다.

### 계산 계약 분리

```csharp
TravelSettlementResult CalculateTravelSettlement(TravelSettlementInput input);
MarketTransactionResult CalculateMarketTransaction(MarketTransactionInput input);
```

- 이동 정산: 여행 비용, 고용 비용, 수리·내구도, 이벤트 손익, 이동 중 상품 손실, 발전 재화 보상만 계산한다.
- 시장 거래: 플레이어가 명시한 품목과 수량만 구매·판매한다.
- 계산기는 `SaveData`를 직접 변경하지 않는다.
- 시장 가격 공식을 UI나 Core에 복제하지 않는다.

### 완료 조건

- 동일 cargo로 도착해도 판매 수익은 0이다.
- 이동 중 손실만 cargo에 반영된다.
- 시장 거래 Command 성공 후에만 cargo와 거래 재화가 변한다.

## 3. Core Gameplay 요청

- `JourneyState`에 `AtTown` 또는 `Arrived`를 추가하지 않는다.
- Claim 이후 여정은 기존 `Completed` 또는 `Failed`로 종료한다.
- 기존 `JourneyState.Prepare` 진입 조건이 깨지지 않도록 한다.
- 도착·Claim만으로 cargo를 clear하지 않는다.
- 현재 도시에서 출발하는 route만 선택할 수 있도록 검증한다.
- 본기지에서는 cargo의 Material을 건설 Command가 직접 소비할 수 있다.

### Cargo 규칙

- 이동 중 실제 손실 수량만 차감한다.
- 시장 판매 성공 시 판매 수량만 차감한다.
- 시장 구매 성공 시 구매 수량만 추가한다.
- 본기지 건설 성공 시 요구 Material 수량만 cargo에서 차감한다.

## 4. Framework / Save 요청

### 목적지와 상태

- 새 `destinationTownId` 필드를 추가하지 않는다.
- 기존 `TradePreparationCommitSaveData.destinationTownId`를 사용한다.
- Claim 시 commit의 목적지와 route의 `ToTownId`가 같은지 검증한다.
- Claim 성공 시 `player.currentTownId = destinationTownId`로 갱신한다.
- `TradeProgressState.AtTown`을 추가하지 않는다.
- `tradeProgress.state`는 Claim 결과에 따라 기존 `Completed` 또는 `Failed`로 확정한다.
- 이후 화면 진입은 `InGameScreenState.Town` 또는 `Market`이 담당한다.

### Claim 처리 순서

```text
1. SettlementPending 및 Trade ID 검증
2. tradePreparationCommit 조회
3. commit.destinationTownId와 route.ToTownId 검증
4. 이동 결과 및 잔존 cargo stage
5. player.currentTownId stage
6. tradeProgress 완료 상태와 pendingSettlement clear stage
7. SaveData 저장
8. SaveResult.Succeeded 확인
9. 성공 후에만 commit 완료·cache clear·Town 화면 요청
10. 실패 시 currency·cargo·currentTownId·state·pending·commit rollback
```

### 기존 코드 수정 요구

- `TradeProgressCoordinator.ClaimSettlementAndReset()`에서 `saveService.Save(saveData)` 반환값을 반드시 검사한다.
- 저장 실패 시 `RequestScreen`, cache clear, commit clear, 성공 event를 실행하지 않는다.
- Claim 성공 후 `Preparation`으로 즉시 이동하지 않고 `Town` 화면을 요청한다.
- 중복 Claim이 위치 이동이나 보상을 다시 적용하지 않게 한다.

## 5. 시장 거래 Command 요청

기존 `MarketInventorySession.WriteCargo()`와 `CancelPreparation()`은 cargo 전체를 clear하므로 목적지 거래에 그대로 사용하지 않는다.

권장 입력:

```csharp
public sealed class MarketTransactionLine
{
    public string ItemId;
    public int BuyQuantity;
    public int SellQuantity;
}
```

### 필수 규칙

- 거래는 `itemId`별 증감(delta)으로 적용한다.
- 판매: cargo 감소, 시장 재고 증가, 거래 재화 증가.
- 구매: cargo 증가, 시장 재고 감소, 거래 재화 감소.
- 관계없는 cargo 항목은 그대로 유지한다.
- 수량이 0이 된 항목만 제거한다.
- 음수 수량, 보유량 초과 판매, 재고 초과 구매, 자금·중량 초과를 거부한다.
- 저장 성공 전에는 실제 cargo, 시장 재고, 재화를 확정하지 않는다.
- 저장 실패 시 모든 변경을 rollback한다.
- 취소는 draft만 폐기하고 실제 cargo를 변경하지 않는다.

### 조건부 구현 정리

- 현재 시장 저장 경로는 `ND_MARKET_SAVE_SCHEMA_VNEXT` 조건부 컴파일 상태다.
- 위 delta 계약, `SaveResult` 검사, rollback을 먼저 반영한 뒤 실제 빌드 경로로 활성화하거나 정식 코드로 이전한다.

## 6. UI 요청

### 건설 UI 연결 요청

- `BuildingAddPopup.cs`는 UI 팀 관할이며 Progression에서 직접 수정하지 않는다.
- `VillageBuildingRegistry.cs`는 Core Gameplay 팀 관할이며 Progression에서 직접 수정하지 않는다.
- Progression은 `CaravanBuildingConstructionCommand.Execute(...)`까지만 제공한다.
- 기존 `VillageBuildingRegistry.AddOrUpgrade(idx)`를 Command 성공 전에 호출하지 않는다.
- `SaveResult.Succeeded`일 때만 성공 callback과 팝업 Close를 수행한다.
- 비용 부족·본기지 외 위치·이동 중·저장 실패 시 팝업을 유지하고 안정적인 실패 사유를 표시한다.
- 중복 클릭으로 동일 건설 요청이 두 번 실행되지 않도록 처리 중 상태를 둔다.
- 재료별 요구량·cargo 보유량·부족량은 `BuildingCostResult` 기반으로 표시한다.

### 건설 Command 호출 방법

필요 의존성:

```csharp
FrameworkRoot root = FrameworkRoot.Instance;
SaveData saveData = root.CurrentSaveData;
ISaveService saveService = root.SaveService;
IEnumerable<TradeItemData> tradeItemCatalog = /* Content 상품 카탈로그 */;
BuildingCostDefinition definition = /* 선택 건물의 레벨별 비용 */;
string baseTownId = "BaseCamp";
```

호출:

```csharp
SaveResult saveResult = CaravanBuildingConstructionCommand.Execute(
    saveData,
    saveService,
    tradeItemCatalog,
    definition,
    baseTownId,
    out BuildingCostResult costResult);
```

결과 처리:

```csharp
if (saveResult == null || !saveResult.Succeeded)
{
    // costResult가 있으면 FailureReason과 재료별 부족량 표시
    // costResult가 null이면 위치·진행 상태·카탈로그·저장 계층 실패 표시
    // 씬 건물, 성공 callback, 팝업 Close 금지
    return;
}

// 이 시점에는 cargo 차감과 VillageBuildingSaveData.targetLevel 저장이 완료되어 있다.
// Registry는 저장된 결과를 기준으로 씬만 반영한다.
ApplySavedBuildingLevel(definition.DisplayName, costResult.TargetLevel);
RefreshBuildingList();
CloseBuildingPopup();
```

### VillageBuildingRegistry 연결 제한

- Command 성공 후 기존 `AddOrUpgrade()`를 호출하면 레벨이 다시 증가하거나 SaveData를 중복 변경할 수 있으므로 금지한다.
- Registry 소유 팀은 다음과 같은 **씬 반영 전용** 메서드를 별도로 제공한다.

```csharp
void ApplySavedBuildingLevel(string displayName, int targetLevel);
```

- 위 메서드는 prefab 생성 또는 기존 씬 건물의 level 적용만 수행한다.
- `SaveData.player.villageBuildings`를 다시 변경하거나 `Save()`를 다시 호출하지 않는다.
- 씬 반영에 실패해도 이미 성공한 저장 데이터를 역으로 변경하지 않고, 재진입 시 `RestoreFromSaveData()`로 복구한다.

### 비용 Preview 연결

실제 저장 없이 요구량·보유량·부족량을 표시할 때는 같은 입력과 계산기를 사용한다.

```csharp
if (CaravanBuildingCostInputFactory.TryCreate(
        saveData,
        tradeItemCatalog,
        definition,
        currentLevel,
        out BuildingCostInput input))
{
    BuildingCostResult preview = BuildingCostCalculator.Calculate(input);
}
```

Preview 결과를 실제 차감에 직접 적용하지 않는다. 최종 버튼 입력에서는 반드시 `CaravanBuildingConstructionCommand.Execute(...)`가 현재 cargo를 다시 검증해야 한다.

### BuildingCostCatalog 제공 및 설정 요청

Progression 제공 완료:

```csharp
BuildingCostCatalog.TryGetDefinition(displayName, out definition);
BuildingCostCatalog.Validate(tradeItemCatalog, out findings);
```

Content/Tools 팀 요청:

1. `Assets > Create > ND > Economy > Building Cost Catalog`로 실제 catalog asset을 생성한다.
2. `VillageBuildingRegistry`에서 사용하는 것과 동일한 `displayName`을 등록한다.
3. 각 건물의 `MaxLevel`과 1~`MaxLevel` 모든 레벨 비용을 입력한다.
4. 각 비용은 `TradeItemCategory.Material` 상품의 `itemId + quantity`로 입력한다.
5. 빈 레벨 비용이나 무료 건설 정의를 두지 않는다.
6. Content 검수 또는 빌드 전 `Validate(...)` 결과가 성공인지 확인한다.

Framework/Core 연결 요청:

```csharp
if (!buildingCostCatalog.TryGetDefinition(buildingDisplayName, out BuildingCostDefinition definition))
{
    // 알 수 없는 건물 또는 중복 정의: 건설 요청 차단
    return;
}

SaveResult result = CaravanBuildingConstructionCommand.Execute(
    root.CurrentSaveData,
    root.SaveService,
    tradeItemCatalog,
    definition,
    baseTownId,
    out BuildingCostResult costResult);
```

빈 catalog이나 조회 실패를 무료 건설로 대체하지 않는다.

- 도착 결과 화면과 목적지 시장 화면을 분리한다.
- `InGameScreenState.Town` 또는 `Market`을 추가한다.
- Claim 저장 성공 후에만 목적지 Town 화면으로 전환한다.
- `CargoLoadingPanelController.loadedLines`는 저장 인벤토리가 아니라 UI draft로만 사용한다.
- 실제 인벤토리 원본은 `SaveData.caravan.cargo`다.
- 기존 cargo, 구매 draft, 판매 draft를 구분한다.
  - 권장: `savedCargoSnapshot`, `buyDraft`, `sellDraft`
  - 또는 각 line에 거래 방향을 명시한다.
- 취소 시 draft만 초기화하고 실제 cargo는 유지한다.
- 거래 성공 결과를 받은 뒤에만 화면의 cargo·재화·시장 재고를 갱신한다.

### 표시 항목

- 현재 도시
- Caravan 보유 cargo
- 시장 판매 재고와 잔여량
- 구매 수량·비용
- 판매 수량·수익
- 거래 후 예상 재화
- 적재 중량·여유 공간
- 거래 불가 사유

## 7. Content / Tools 요청

- 각 도시의 시장 ID, 상품, 초기 재고, 가격 데이터를 제공한다.
- 목적지 시장에 `TradeItemCategory.Material` 상품을 포함할 수 있어야 한다.
- 건축 재료는 일반 무역품과 동일하게 사고팔 수 있다.
- 본기지 건물 업그레이드는 `SaveData.caravan.cargo`의 Material을 직접 요구한다.

## 8. QA 요청

### 정상 흐름

1. BaseCamp에서 일반 상품을 적재하고 출발한다.
2. TradeTown 도착 후 이동 결과를 Claim한다.
3. cargo가 자동 판매되지 않았는지 확인한다.
4. `currentTownId == TradeTown`인지 확인한다.
5. 시장에서 일부 상품만 판매하고 Material을 구매한다.
6. 관계없는 cargo가 유지되는지 확인한다.
7. BaseCamp로 귀환하고 Claim한다.
8. Material cargo가 유지되는지 확인한다.
9. cargo의 Material을 사용해 건물을 업그레이드한다.
10. 요구 수량만 cargo에서 차감됐는지 확인한다.

### 실패·복구

- Claim 저장 실패 시 도시·cargo·상태·pending·commit이 모두 원복된다.
- 앱 재시작 후 SettlementPending 및 완료된 현재 도시를 각각 정상 복구한다.
- 시장 거래 저장 실패 시 재화·시장 재고·cargo가 모두 원복된다.
- 거래 취소 시 실제 cargo가 바뀌지 않는다.
- 중복 Claim과 중복 시장 거래를 차단한다.
- 이동 중 Material 일부 손실 시 손실 수량만 감소한다.

## 9. 구현 순서

1. Progression/Economy: `FrameworkEconomyM1InputBuilder`와 `EconomyM1LoopCalculator`에서 자동 판매 제거
2. Framework: 기존 commit 목적지를 사용하는 Claim 저장·rollback 계약 적용
3. Scene/UI: `InGameScreenState.Town` 또는 `Market` 추가 및 도착 화면 연결
4. Economy/Framework: cargo delta 기반 시장 구매·판매 Command 구현
5. UI: 기존 cargo와 구매·판매 draft 분리
6. Framework/Progression: cargo 어댑터, 원자적 건설 Command, `BuildingCostCatalog` 조회·검증 제공 완료. Core의 Registry 씬 반영 전용 API, UI의 Popup 호출 연결, Content의 실제 catalog asset·비용 수치 설정 필요
7. QA: 왕복 무역, Material 구매, cargo 직접 소비, 건물 업그레이드 E2E 검증

## 10. 공통 완료 조건

```text
BaseCamp 출발
-> TradeTown 도착
-> 자동 판매 없이 cargo 유지
-> 목적지 시장에서 명시적 판매 및 Material 구매
-> BaseCamp 귀환
-> cargo 유지
-> cargo에서 건설 재료 직접 소비
-> 건물 업그레이드
```

위 흐름이 저장·재실행을 포함해 한 번 이상 성공하고, 각 중요 저장 실패 시 화면·상태·재화·cargo·시장 재고가 확정되지 않아야 한다.
