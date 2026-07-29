# Building Popup External Integration Request

- 작성일: 2026-07-30
- 작성자: UI & Data / LJH
- 기준 브랜치: `feature/building-ui-presenter`
- 상태: 외부 연동 요청

## 1. 목적

현재 `BuildingAddPopup`의 카탈로그 버튼은
`VillageBuildingRegistry.AddOrUpgrade(int)`를 직접 호출하여 비용 검증 없이
건설 또는 증축한다.

이를 다음 흐름으로 변경한다.

```text
카탈로그 건물 선택
→ 선택한 BuildData와 현재 레벨로 Detail Popup 표시
→ 건설/증축 요청 시 Confirm Popup 표시
→ 최종 확인 이벤트
→ 실제 비용 재검증 및 원자적 저장
→ 저장 성공 후 Village_Home Scene 반영
→ 건물 목록 갱신
```

건물 선택과 Popup 표시는 SaveData, 인벤토리 또는 Scene 건물을 변경하지 않는다.

## 2. 현재 UI에서 제공하는 공개 기능

다음 기능은 `BuildingPopupRuntimeBinding`에 구현되어 있다.
외부 연동 코드는 이를 새로 구현하거나 변경하지 않고 호출하거나 구독한다.

경로:

`Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/BuildingPopupRuntimeBinding.cs`

### 2.1 Detail Popup 표시

```csharp
public void OpenDetail(
    BuildData data,
    int currentLevel);
```

- `data`: 선택한 건물의 `BuildData`
- `currentLevel`: 저장된 현재 건물 레벨
- 아직 건설하지 않은 건물은 `0`

### 2.2 최종 확인 이벤트

```csharp
public event Action<string> BuildConfirmed;
```

- payload는 최종 확인한 건물의 `buildId`다.
- 이 이벤트는 실제 건설 성공 이벤트가 아니라 실행 요청이다.
- 실제 실행 계층은 이벤트를 받은 뒤 현재 SaveData와 비용을 다시 검증한다.

## 3. 이번 연동의 데이터 기준

### 3.1 UI 비용 원본

UI가 표시하는 건설·증축 비용은 목표 레벨의 `BuildRequirement`다.

```text
BuildData
└─ DataPerLevels
   └─ level == currentLevel + 1
      └─ buildRequirements
         ├─ requireCurrency
         └─ requireItems
```

- `DataPerLevels`의 배열 위치가 아니라 `DataPerLevel.level`로 검색한다.
- Confirm 이후 실제 실행도 동일한 요구 조건을 사용해야 한다.
- 같은 비용을 `BuildData`와 `BuildingCostCatalog`에 각각 입력하여 이중 관리하지
  않는다.

### 3.2 보유량과 차감 원본

| 비용 | 보유량 및 차감 원본 |
|---|---|
| Gold | `SaveData.player.tradingCurrency` |
| 건설 아이템 | `SaveData.player.homeInventory` |
| Caravan Cargo | 건설 비용에 포함하지 않음 |

Caravan의 물품은 먼저 기존 이전 절차를 통해 `homeInventory`로 옮긴 뒤 건설에
사용한다. Basecamp에 없는 Caravan의 Cargo를 원격으로 차감하지 않는다.

### 3.3 기존 계약과 달라지는 부분

`Docs/Personal_Documents/JJH/0720_Construction_Material_Building_Cost_Contract.md`
는 HomeInventory 사용을 이미 확정했지만 다음 항목은 현재 UI 요구와 다르다.

- 기존 계약의 비용 정의 원본은 `BuildingCostCatalog`다.
- 기존 계약은 Material 아이템 요구만 정의한다.
- 현재 UI 데이터는 `BuildData.buildRequirements`와 선택적 Gold 요구를 포함한다.

따라서 Progression/Framework 구현에서는 다음 중 하나를 확정해야 한다.

1. `BuildRequirement`를 실제 Command의 단일 비용 입력으로 채택한다.
2. `BuildRequirement`를 기존 계산 입력으로 변환하되 별도 비용 에셋에 수치를
   중복 저장하지 않는다.

Gold 요구를 유지한다면 실제 Command도 Gold 검증·차감·rollback을 같은 저장
경계에서 지원해야 한다. 실제 Command에서 Gold를 지원하지 않기로 결정한다면
production `BuildData`의 `requireCurrency.isRequired`를 사용해서는 안 된다.

## 4. 전체 외부 작업 대상

### 4.1 기존 스크립트 수정

1. `Assets/_Project/01.Core/07_Village/YHY/VillageBuildingRegistry.cs`
2. `Assets/_Project/05.UI/04_InGame/YHY/Scripts/BuildingAddPopup.cs`
3. `Assets/_Project/05.UI/04_InGame/YHY/Scripts/BuildingListPanel.cs`
4. `Assets/_Project/03.Economy/09_Building/BuildingConstructionModels.cs`
5. `Assets/_Project/03.Economy/09_Building/BuildingConstructionCalculator.cs`

### 4.2 신규 또는 교체 구현

기존 HomeInventory 계약에 이름이 정의되어 있지만 현재 프로젝트에서 파일을
찾을 수 없는 다음 구현이 필요하다.

1. `HomeInventoryBuildingCostInputFactory`
2. `HomeInventoryBuildingConstructionCommand`
3. 실제 건설 실행 Binding 또는 Controller
4. HomeInventory 건설 Command Editor 테스트

### 4.3 수정 에셋

1. `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab`
2. `Assets/_Project/07.Scenes/04_InGame/Village_Home.unity`

### 4.4 직접 수정하지 않을 레거시 구현

다음 파일은 Cargo 기반 레거시 구현이므로 이번 연동의 production 경로로
확장하지 않는다.

1. `Assets/_Project/11.CoreServices/Scripts/Building/CaravanBuildingCostInputFactory.cs`
2. `Assets/_Project/11.CoreServices/Scripts/Building/CaravanBuildingConstructionCommand.cs`

새 UI 흐름이 위 레거시 Command를 호출하지 않도록 한다. 기존 테스트 보존이나
제거 시점은 Progression/Framework 담당 범위에서 결정한다.

## 5. 스크립트 변경 요청

### 5.1 `VillageBuildingRegistry.cs`

`CatalogEntry`에 대응하는 `BuildData` 참조를 추가한다.

```csharp
[System.Serializable]
public class CatalogEntry
{
    public string displayName;
    public GameObject prefab;
    public BuildData buildData;
}
```

다음 조회 기능을 제공한다.

- Catalog index에 대응하는 `BuildData`
- `buildId`에 대응하는 `BuildData`
- Catalog 건물의 현재 레벨
- 저장 성공 후 Scene 반영에 필요한 표시 이름

메서드명은 기존 Core 규칙에 맞출 수 있지만 다음 동작이 가능해야 한다.

```csharp
BuildData GetCatalogBuildData(int index);
BuildData FindBuildData(string buildId);
int GetCatalogLevel(int index);
```

현재 SaveData는 `VillageBuildingSaveData.displayName + level` 구조다.
`buildingId` 저장 전환은
`Docs/Contract/Village_Building_ID_Migration_Plan.md`의 별도 migration
단계이므로 완료된 것으로 가정하지 않는다.

이번 연결에서는 `BuildData.buildId`로 카탈로그 항목을 찾은 후 기존
`displayName` 저장 키와 안전하게 연결하는 compatibility 경계가 필요하다.
알 수 없거나 중복된 ID는 즉시 건설로 우회하지 않고 요청을 거부한다.

`AddOrUpgrade(int)`는 새 Popup 흐름에서 호출하지 않는다. 저장 성공 후 Scene
반영에는 기존 `ApplySavedBuildingLevel(displayName, targetLevel)`을 사용한다.

### 5.2 `BuildingAddPopup.cs`

카탈로그 버튼에서 다음 직접 호출을 제거한다.

```csharp
reg.AddOrUpgrade(idx);
```

`BuildingPopupRuntimeBinding` 참조를 직렬화한다.

```csharp
[SerializeField]
private BuildingPopupRuntimeBinding buildingPopupRuntimeBinding;
```

버튼 클릭 시 다음 정보만 조회하여 Detail Popup을 연다.

```csharp
BuildData buildData =
    reg.GetCatalogBuildData(idx);

int currentLevel =
    reg.GetCatalogLevel(idx);

if (buildData == null ||
    buildingPopupRuntimeBinding == null)
{
    return;
}

buildingPopupRuntimeBinding.OpenDetail(
    buildData,
    currentLevel);

Close();
```

다음 fallback은 금지한다.

- `BuildData`가 없을 때 `AddOrUpgrade()` 호출
- Binding 참조가 없을 때 즉시 건설
- Popup을 여는 시점에 목록 갱신 callback 호출

### 5.3 `BuildingListPanel.cs`

현재 `BuildingAddPopup.Open(Rebuild)`는 카탈로그 선택 즉시 건설된다는 전제를
가진다.

목록 갱신 시점을 다음과 같이 변경한다.

```text
Command와 Save 성공
→ Scene 반영
→ BuildingListPanel.Rebuild()
```

다음 경우에는 목록을 갱신하지 않는다.

- 카탈로그 항목 선택
- Detail Popup 닫기
- Confirm Popup 취소
- 비용 부족
- Save 실패

### 5.4 `BuildingConstructionModels.cs`

기존 모델은 `DisplayName`, `BuildingCostDefinition.LevelCosts`,
`BuildingCostInput.CaravanCargo`를 사용한다.

HomeInventory 경로를 재사용할 경우 최소한 다음 의미로 정리한다.

- 비용 및 결과의 안정적인 식별값은 가능한 범위에서 `buildingId`
- 입력 재고는 `HomeInventory`
- `CaravanCargo`라는 필드에 HomeInventory snapshot을 넣지 않음
- `BuildRequirement`에서 변환된 목표 레벨 요구 목록을 계산 입력에 전달
- Gold를 계산기에서 처리한다면 보유·요구·차감 결과 모델 추가

SaveData의 building ID migration은 별도 단계이므로 현재 DTO에 존재하지 않는
필드를 완료된 것처럼 참조하지 않는다.

### 5.5 `BuildingConstructionCalculator.cs`

`BuildingCostCalculator`가 HomeInventory snapshot과 목표 레벨 요구 목록을
계산하도록 변경한다.

- `TradeItemCategory.Material`만 건설 아이템으로 인정
- 동일 item ID의 여러 entry 합산
- 잘못된 ID, 음수 수량, 중복 요구, overflow 거부
- 요구 아이템별 보유·요구·부족 수량 결과 제공
- 입력 목록과 SaveData를 변경하지 않음

Gold까지 계산기가 담당한다면 다음도 포함한다.

- `requireCurrency.isRequired == false`이면 Gold 요구 없음
- 활성화된 경우 현재 Gold와 요구 Gold 비교
- Gold 부족 실패 결과
- 성공 결과에 차감할 Gold 포함

Gold 검증을 Command에서 담당한다면 계산기와 Command의 판정이 달라지지 않도록
공통 변환 규칙을 한 곳에 둔다.

## 6. 신규 또는 교체 구현 요청

### 6.1 `HomeInventoryBuildingCostInputFactory`

`SaveData.player.homeInventory`와 TradeItem catalog를 순수 계산 입력 snapshot으로
변환한다.

- `itemId`로 `TradeItemData` 조회
- `TradeItemData.Category` 복원
- null entry, 빈 ID, 음수 수량, catalog 누락 거부
- Caravan Cargo를 읽지 않음
- SaveData나 inventory entry를 변경하지 않음

### 6.2 `HomeInventoryBuildingConstructionCommand`

Confirm 이후 실제 건설을 처리하는 유일한 production Command로 사용한다.

실행 시점에 다음을 다시 검증한다.

1. `buildId`에 대응하는 `BuildData`
2. SaveData의 현재 건물 레벨
3. `targetLevel = currentLevel + 1`
4. 목표 레벨의 `BuildRequirement`
5. HomeInventory 보유 재료
6. Gold 요구가 활성화된 경우 현재 Gold

하나의 저장 경계에서 다음 상태를 처리한다.

```text
SaveData.player.homeInventory
SaveData.player.tradingCurrency
SaveData.player.villageBuildings
```

처리 순서:

```text
검증
→ inventory/currency/building snapshot
→ 재료와 Gold 차감 stage
→ 건물 레벨 stage
→ Save
→ 성공 결과 반환
```

비용 부족, 상태 변경, Save 실패 또는 예외 발생 시 세 상태를 모두 원복한다.
Save 성공 전에는 Scene을 변경하거나 성공 이벤트를 발생시키지 않는다.

### 6.3 실제 건설 실행 Binding 또는 Controller

`BuildingPopupRuntimeBinding.BuildConfirmed`를 구독한다.

```text
BuildConfirmed(buildId)
→ Registry에서 BuildData 조회
→ HomeInventoryBuildingConstructionCommand 실행
→ Save 성공 확인
→ ApplySavedBuildingLevel(displayName, targetLevel)
→ BuildingListPanel.Rebuild()
```

- 중복 클릭 또는 처리 중 재요청을 막는 실행 중 상태가 필요하다.
- 실패 시 Scene과 목록을 갱신하지 않는다.
- 실패 사유를 기존 오류 표시 경로 또는 명시적인 로그로 전달한다.
- 기존 프로젝트에 있는 FrameworkRoot, SaveData, SaveService 접근 방식을
  사용하고 별도 Resolver나 새로운 전역 검색 방식을 임의로 추가하지 않는다.

## 7. Prefab과 Scene 연결

### 7.1 `MainUICanvas.prefab`

다음을 연결한다.

- `BuildingAddPopup.buildingPopupRuntimeBinding`
- 실제 건설 실행 Binding 또는 Controller가 구독할
  `BuildingPopupRuntimeBinding`
- 성공 후 갱신할 `BuildingListPanel`
- 기존 프로젝트 방식으로 제공되는 Save/Framework 관련 참조

검증:

- Missing Script 없음
- Missing Reference 없음
- Detail Popup과 Confirm Popup 초기 숨김
- Confirm Popup이 Detail Popup 위에 표시
- 기존 Main UI 배치와 크기를 불필요하게 변경하지 않음

### 7.2 `Village_Home.unity`

현재 `VillageBuildingRegistry.catalog`의 다음 항목에 대응하는 `BuildData`를
연결한다.

- 상점
- 창고
- 목장
- 오두막
- 풍차
- 빵집

검증:

- Catalog 항목과 BuildData가 같은 건물을 가리킴
- 빈 ID와 중복 `buildId` 없음
- Catalog Prefab과 BuildData의 레벨별 Prefab 대응 확인
- Missing Reference 없음

`MainUICanvas.prefab` 변경으로 반영되는 다른 InGame Scene은 직접 수정하지
않는다.

## 8. 테스트 요청

HomeInventory 기반 신규 Command에 다음 Editor 테스트를 추가한다.

### 8.1 비용과 차감

- HomeInventory 재료가 충분하면 성공
- 재료가 부족하면 전체 실패
- Cargo에만 재료가 있으면 실패
- Cargo가 비어 있어도 HomeInventory가 충분하면 성공
- 성공 후 HomeInventory에서 정확한 수량 차감
- 성공과 실패 모두 Caravan Cargo 불변
- Gold 요구가 비활성화되면 Gold 불변
- Gold 요구가 활성화되고 충분하면 정확한 금액 차감
- Gold 부족 시 아이템과 건물 레벨 불변

### 8.2 저장과 rollback

- 성공 시 재료, Gold, 레벨을 한 번만 적용
- Save 실패 시 HomeInventory 복구
- Save 실패 시 Gold 복구
- Save 실패 시 건물 레벨 복구
- 예외 발생 시 전체 상태 복구
- 실패 시 Scene 반영과 성공 알림 없음

### 8.3 식별과 레벨

- 알 수 없는 `buildId` 거부
- 중복 `buildId` 거부
- 미건설 건물은 level 1 비용 사용
- 증축은 current level + 1 비용 사용
- 목표 레벨 데이터 누락 거부
- 최대 레벨 거부
- `AddOrUpgrade()` 경로를 호출하지 않음

## 9. 완료 조건

- 카탈로그 버튼을 눌러도 즉시 건설되지 않는다.
- 선택한 BuildData와 현재 레벨로 Detail Popup이 열린다.
- Confirm Popup은 Detail Popup 위에 표시되고 취소 시 Detail이 유지된다.
- Confirm 이후 실제 실행 시 비용과 현재 레벨을 다시 검증한다.
- 아이템은 HomeInventory에서만 차감한다.
- Gold 요구를 사용하면 실제 Command도 Gold를 동일한 transaction에서 처리한다.
- Caravan Cargo는 건설 과정에서 변경되지 않는다.
- Save 성공 후에만 Scene과 건물 목록을 갱신한다.
- 실패 시 inventory, Gold, 건물 레벨을 원복한다.
- 비용 없는 `AddOrUpgrade()` production 경로를 호출하지 않는다.
- 비용 수치를 BuildData와 다른 에셋에 중복 저장하지 않는다.
- Missing Script와 Missing Reference가 없다.
- Unity runtime 컴파일 오류가 없다.
- 관련 Editor 테스트가 통과한다.
- 무관한 Scene과 Prefab을 변경하지 않는다.

## 10. 구현 전 확인할 계약 변경

다음 두 항목은 기존 Progression 계약과 다르므로 담당자 간 확인 기록이
필요하다.

1. 실제 비용 정의의 단일 원본을 `BuildData.buildRequirements`로 둘지,
   기존 `BuildingCostCatalog`를 유지하고 중복 없는 adapter를 제공할지
2. optional Gold 요구를 production 건설 비용에 포함할지

이 결정 전에도 Popup 연결과 HomeInventory 전환 작업은 진행할 수 있지만,
실제 Command의 비용 입력과 Gold 차감 구현은 두 항목을 확정한 뒤 완료해야 한다.
