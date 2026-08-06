# Transport Inventory · Caravan Set InGame 조립 문서

## 1. 목적과 범위

이 문서는 `InGame.unity`와 `TradePrepareUI.prefab`의 조립 변경을 제거한 뒤에도 다음 상태를 다시 만들기 위한 문서다.

- Main UI의 건설된 `목장 Lv.N` 행을 누르면 Transport Inventory가 열린다.
- Transport Inventory는 Framework의 현재 SaveData와 SharedGameData를 표시한다.
- Caravan Set은 소유 중인 마차를 `contentId`별로 묶어 표시하고, 펼친 목록에서는 `instanceId`별 개체를 선택한다.
- Caravan에 배정된 마차와 동물은 Transport Inventory에 남아 있으며 `어느 Caravan에서 사용 중인지` 표시된다.
- 무역 저장 과정에서도 선택한 마차와 동물의 `instanceId`가 유지된다.

이 문서는 조립 전용이다. 구매 재화 차감, 퀘스트 보상 지급, 무역 실패 정산 삭제는 각 기능 담당 영역이다.

## 2. 조립 전에 존재해야 하는 에셋과 스크립트

### Transport Inventory

- `Assets/_Project/08.Prefabs/UI/TransportInventory/TransportInventoryPopup.prefab`
- `TransportInventoryPopupController`
- `TransportInventoryPresentation`
- `TransportInventorySlotView`
- `TransportInventoryTooltipView`
- `TransportInventoryMainUiEntry`

`TransportInventoryMainUiEntry.cs`를 제거했다면 먼저 복구해야 한다. 이 컴포넌트가 없으면 목장 행 클릭 이벤트와 팝업을 Inspector 연결만으로 이어줄 수 없다.

파일 경로는 다음으로 고정한다.

`Assets/_Project/05.UI/04_InGame/YHY/Scripts/TransportInventory/TransportInventoryMainUiEntry.cs`

파일이 없는 경우에는 이 문서의 **부록 A**에 있는 전체 소스로 복구한 뒤 Unity 컴파일이 끝날 때까지 기다린다. 컴파일 전에는 Inspector의 Add Component 목록에 나타나지 않는다.

### Caravan Set 마차 선택

- `Assets/_Project/08.Prefabs/UI/Trade/WagonSelectPopup.prefab`
- `Assets/_Project/08.Prefabs/UI/Trade/WagonInstanceRow.prefab`
- `WagonSelectPopup`
- `WagonInstanceRowView`
- `AnimalInventoryPanel`
- `TransportSelectPanel`

### Caravan 저장 연결

- `CaravanSettingRuntimeBridge`
- `CaravanSettingApplicationService`
- `CaravanSaveDataMapper`
- `CaravanOverviewEditBinding`
- `CaravanOverviewCreationBinding`

## 3. `WagonInstanceRow.prefab` 자체 참조

프리팹 경로:

`Assets/_Project/08.Prefabs/UI/Trade/WagonInstanceRow.prefab`

루트 오브젝트 `WagonInstanceRow`에는 다음 컴포넌트가 있어야 한다.

- `RectTransform`: 기준 크기 `530 × 40`
- `Image`
- `Button`
- `LayoutElement`: 높이 `40`
- `WagonInstanceRowView`

자식 `Label`에는 `TextMeshProUGUI`가 있어야 한다.

`WagonInstanceRowView`의 직렬화 참조:

| 필드 | 연결 대상 |
| --- | --- |
| `button` | 루트 `WagonInstanceRow`의 `Button` |
| `label` | 자식 `Label`의 `TextMeshProUGUI` |

이 프리팹은 씬에 미리 배치하지 않는다. `WagonSelectPopup`이 처음 필요한 수량만 생성하고 이후 비활성화·활성화하여 재사용한다.

## 4. `WagonSelectPopup.prefab` 자체 참조

프리팹 경로:

`Assets/_Project/08.Prefabs/UI/Trade/WagonSelectPopup.prefab`

루트 `WagonPopup`의 `WagonSelectPopup` 필드는 다음과 같이 연결한다.

| 필드 | 연결 대상 |
| --- | --- |
| `listContainer` | 그룹 버튼과 개체 행이 들어갈 목록 Transform |
| `buttonPrefab` | `contentId`별 그룹 제목에 사용하는 Button 프리팹 또는 내부 템플릿 |
| `instanceRowPrefab` | `WagonInstanceRow.prefab`의 `WagonInstanceRowView` |
| `cancelButton` | 팝업 내부 취소 버튼 |

팝업 루트는 기본적으로 비활성화 상태여야 한다. 열고 닫을 때 새 팝업을 생성·삭제하지 않고 기존 인스턴스의 활성 상태만 변경한다.

그룹 버튼과 개체 행 풀의 기본 생성 수량은 모두 `0`이다. 처음 필요한 만큼만 만들며, 이후 목록 갱신에서는 기존 객체를 재사용한다.

## 5. `TradePrepareUI.prefab`에 마차 선택 팝업 연결

대상 프리팹:

`Assets/_Project/08.Prefabs/UI/Maps/TradePrepareUI.prefab`

조립 순서:

1. Prefab Mode로 `TradePrepareUI.prefab`을 연다.
2. 기존에 직접 만들어진 `WagonPopup`이 있다면 제거한다.
3. `WagonSelectPopup.prefab`을 `TradePrepareUI` 루트 아래에 중첩 프리팹으로 한 번 배치한다.
4. 오브젝트 이름은 `WagonPopup`으로 유지한다.
5. 기존 팝업과 동일한 형제 순서, 앵커, 위치와 크기를 적용한다.
6. `WagonPopup`을 비활성화한다.
7. `TradePrepareUI/S3_Animal` 오브젝트의 `AnimalInventoryPanel`을 선택한다.
8. `AnimalInventoryPanel.wagonPopup`에 새 `WagonPopup`의 `WagonSelectPopup` 컴포넌트를 연결한다.
9. Prefab을 저장한다.

정상 결과:

- 마차 선택 화면에는 소유 마차가 `displayName [종류] ×수량` 형태로 `contentId`별 집계되어 나타난다.
- 그룹 버튼을 누르면 바로 아래에 `1. displayName 내구도 현재/최대` 형태의 개체 행이 나타난다.
- 같은 그룹을 다시 누르면 접힌다.
- 다른 그룹을 누르면 이전 그룹은 접히고 새 그룹만 펼쳐진다.
- 개체 행을 선택하면 화면에 보이지 않는 실제 `instanceId`가 선택 값으로 전달된다.
- 다른 Caravan에서 사용 중인 개체는 선택할 수 없다.

나오면 안 되는 결과:

- `instanceId` 문자열이 사용자용 이름이나 툴팁에 표시됨
- 그룹 클릭 후 개체 행이 나타나지 않음
- 그룹을 전환할 때 이전 그룹의 행이 남음
- 클릭할 때마다 행을 무조건 생성하고 이전 행을 파괴함
- 다른 Caravan에서 사용 중인 마차를 선택할 수 있음
- 동일 `contentId`의 다른 소유 개체로 자동 재연결함

개체 행이 나오지 않으면 가장 먼저 `WagonSelectPopup.instanceRowPrefab`과 `AnimalInventoryPanel.wagonPopup`이 `None`인지 확인한다.

## 6. InGame에 Transport Inventory 배치

대상 씬:

`Assets/_Project/07.Scenes/04_InGame/InGame.unity`

조립 순서:

1. `MainUICanvas`의 팝업 계층 아래에 `TransportInventoryPopup.prefab`을 한 번 배치한다.
2. 이름은 `TransportInventoryPopup`으로 유지한다.
3. 화면 전체 Stretch 앵커를 사용한다.
4. 시작 상태는 비활성화한다.
5. Main UI의 `BuildingListPanel`이 붙은 오브젝트에 `TransportInventoryMainUiEntry`를 추가한다.
6. 다음 직렬화 참조를 직접 연결한다.

| `TransportInventoryMainUiEntry` 필드 | 연결 대상 |
| --- | --- |
| `buildingListPanel` | 같은 Main UI의 `BuildingListPanel` |
| `popup` | 배치한 `TransportInventoryPopup`의 `TransportInventoryPopupController` |

런타임 `Find`로 연결하지 않는다.

여기서 `MainUICanvas.prefab` 원본에 Apply하지 않는다. 현재 조립 기준은 `InGame.unity` 안의 `MainUICanvas` 프리팹 인스턴스에 Popup 인스턴스와 Entry 컴포넌트를 추가하는 방식이다. 다른 씬까지 공통 적용하려는 별도 합의가 있을 때만 `MainUICanvas.prefab`에 Apply한다.

`TransportInventoryMainUiEntry`는 다음 역할만 담당한다.

- `BuildingListPanel.BuildingClicked` 이벤트 구독
- 클릭된 건물명이 `TransportInventoryFunction.BuildingDisplayName`인 `목장`인지 검사
- Framework의 SaveData와 SharedGameData가 준비될 때까지 열기 요청 유지
- 준비 완료 시 현재 데이터를 공급하는 `TransportInventoryDataProvider`로 팝업 열기
- 비활성화 시 이벤트 구독 해제

정상 결과:

- 건설된 `목장 Lv.N` 행을 누를 때만 팝업이 열린다.
- 팝업을 처음 열면 마차 탭이 선택되어 있고 탭 피드백이 보인다.
- 닫기 버튼과 Backdrop으로 닫을 수 있다.
- Inventory Card 내부 빈 영역 클릭으로 닫히지 않는다.
- 탭 전환, 슬롯 갱신과 스크롤은 `Update` 폴링 없이 이벤트와 명시적 Refresh로 동작한다.
- 목장 레벨에 따라 마차는 레벨당 10칸, 동물은 레벨당 20칸이 열린다.
- 표시 수량은 `사용 중인 칸 / 현재 열린 칸`이다.
- 장착 중인 개체도 슬롯 한 칸을 차지하고 Caravan 표시가 남는다.

나오면 안 되는 결과:

- 목장이 건설되지 않았는데 팝업에 진입함
- 창고나 다른 건물 행을 눌렀는데 Transport Inventory가 열림
- 팝업을 열 때마다 팝업 자체를 새로 생성함
- 비활성 슬롯 전체를 매 갱신마다 삭제·재생성함
- 임시 초과 반환 슬롯이 일반 보유 슬롯처럼 UI에 표시됨
- 장착 개체가 인벤토리에서 사라짐

## 7. Caravan Setting RuntimeBridge 연결

InGame의 Caravan 설정 연결 오브젝트에 `CaravanSettingRuntimeBridge`를 한 번 배치한다. 권장 계층은 다음과 같다.

```text
InGame
└─ CaravanSettingUiConnector
   ├─ CaravanSettingRuntimeBridge
   ├─ CaravanOverviewEditBinding
   └─ CaravanOverviewCreationBinding
```

`CaravanOverviewEditBinding`의 다음 네 필드는 같은 `CaravanSettingRuntimeBridge`를 참조해야 한다.

- `settingProviderBehaviour`
- `settingCommandBehaviour`
- `loadSettingProviderBehaviour`
- `loadSettingCommandBehaviour`

기존 임시 서비스의 `cargoCatalog`에 연결되어 있던 `TradeItemData` 에셋은 `CaravanSettingRuntimeBridge.tradeItemAssets`에 그대로 연결한다. 이 배열이 비면 Transport Inventory가 아니라 Caravan Cargo UI의 상품 이름, 아이콘과 표시 정보가 누락될 수 있다.

기존 `TestCaravanSettingService`와 RuntimeBridge를 동시에 명령 처리자로 연결하지 않는다. 저장 명령이 두 번 등록될 수 있다.

기존 `TestCaravanSettingService`를 교체할 때는 다음 순서를 지킨다.

1. 기존 `cargoCatalog`의 모든 `TradeItemData` 참조를 기록한다.
2. 같은 GameObject에 `CaravanSettingRuntimeBridge`를 추가한다.
3. 기록한 에셋을 순서 그대로 `tradeItemAssets`에 연결한다.
4. Overview Binding의 네 Provider/Command 필드를 RuntimeBridge로 교체한다.
5. 네 필드가 모두 교체된 것을 확인한 뒤에만 `TestCaravanSettingService`를 제거한다.

중간에 임시 서비스부터 제거하면 `cargoCatalog` 참조를 잃을 수 있다.

정상 결과:

- Caravan Set 저장 시 선택한 `wagonInstanceId`와 동물 `instanceId`가 SaveData에 유지된다.
- 다른 Caravan의 선택 후보에서는 이미 배정된 개체가 차단된다.
- Transport Inventory를 다시 열면 동일 개체에 해당 Caravan의 표시 이름이 나타난다.
- 무역 출발과 저장 과정에서 다른 `instanceId`로 덮어쓰지 않는다.

나오면 안 되는 결과:

- `wagonName` 또는 `animalName`을 개체 식별자로 저장함
- 동일 `contentId`라는 이유로 다른 개체를 자동 선택함
- 무역 출발 후 장착 중 표시가 사라짐
- 내구도 0으로 삭제된 개체가 선택 상태에 남음
- 같은 동물이 두 Caravan에 동시에 배정됨

## 8. 개발 검증 버튼 — 선택 사항

상점과 보상 지급 경로가 아직 없을 때만 `TransportInventoryRewardDebugButton.prefab`을 Main UI 아래에 한 번 배치한다.

기본 지급 예시:

- `Wagon_Wagon_M` 1개
- `Wagon_Wagon_S` 1개
- `DraftAnimal_Horse` 2마리

버튼은 `contentId`와 수량만 전달해야 한다. `PlayerMainManager`가 각 개체의 고유 `instanceId`를 발급한다. 운영 빌드의 정상 획득 경로로 사용하지 않는다.

## 9. 권장 조립 순서

1. `WagonInstanceRow.prefab` 내부 참조 확인
2. `WagonSelectPopup.prefab` 내부 참조 확인
3. `TradePrepareUI.prefab`에 `WagonSelectPopup.prefab` 배치
4. `AnimalInventoryPanel.wagonPopup` 연결
5. InGame에 `TransportInventoryPopup.prefab` 배치
6. `TransportInventoryMainUiEntry` 추가 및 두 필드 연결
7. `CaravanSettingRuntimeBridge`와 Overview Binding 연결
8. `tradeItemAssets` 이전
9. 필요할 때만 테스트 지급 버튼 배치
10. Unity Console 오류가 없는지 확인 후 PlayMode 검증

Unity Test Runner가 PlayMode 테스트를 `0 tests`로 반환하면 성공으로 간주하지 않는다. 이 경우 위 체크리스트를 수동 PlayMode로 실행하거나 PlayMode test assembly 설정을 먼저 복구한다.

## 10. PlayMode 최종 검증 순서

1. 목장을 건설하고 `목장 Lv.1` 행이 생기는지 확인한다.
2. 테스트 지급 또는 정상 획득 경로로 마차 2종과 말 여러 마리를 지급한다.
3. 목장 행을 눌러 Transport Inventory가 열리고 지급 개체가 보이는지 확인한다.
4. 툴팁이 화면 경계 안에서 이름, 기본 구매 가격, 설명과 내구도를 표시하는지 확인한다.
5. Caravan 1에서 특정 마차 개체와 동물을 선택해 저장한다.
6. Caravan 2에서는 같은 개체가 선택 불가능하고 남은 수량만 선택 가능한지 확인한다.
7. 마차 그룹을 펼치고 접으며 개체별 현재/최대 내구도가 맞는지 확인한다.
8. Transport Inventory에서 선택한 마차와 동물에 Caravan 사용 중 표시가 남는지 확인한다.
9. 무역을 출발시킨 뒤에도 같은 `instanceId`와 사용 중 표시가 유지되는지 확인한다.
10. 플레이 모드를 종료한 뒤 Missing Reference 및 이벤트 이중 등록 오류가 없는지 확인한다.

## 11. Scene diff에서 재현하지 않아야 하는 변경

수동 조립 과정에서 Scene을 저장하면 작업과 관계없는 월드 오브젝트 좌표, 라인 렌더러 위치 또는 RectTransform override가 함께 기록될 수 있다. 다음 변경은 이 기능의 필수 조립 사항이 아니다.

- 지형, 풀, 바위 등 월드 장식물의 위치 배열 변경
- Transport Inventory와 무관한 Main UI 앵커 변경
- 기존 팝업 또는 패널의 의미 없는 빈 직렬화 필드 추가
- Prefab instance의 이름 외 불필요한 Transform override

재조립 후 `git diff InGame.unity`를 확인하고, 필수 오브젝트 배치·컴포넌트·참조 외 변경은 되돌린다.

## 부록 A. `TransportInventoryMainUiEntry.cs` 전체 소스

```csharp
using System;
using ND.Framework;
using UnityEngine;

namespace ND.UI.InGame.TransportInventory
{
    [DisallowMultipleComponent]
    public sealed class TransportInventoryMainUiEntry : MonoBehaviour
    {
        [SerializeField] private BuildingListPanel buildingListPanel;
        [SerializeField] private TransportInventoryPopupController popup;
        private bool openWhenReady;

        private void Awake()
        {
            if (popup != null) popup.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (buildingListPanel != null)
                buildingListPanel.BuildingClicked += HandleBuildingClicked;
            FrameworkEvents.SharedGameDataLoaded += HandleFrameworkReady;
            FrameworkEvents.LoadCompleted += HandleFrameworkReady;
        }

        private void OnDisable()
        {
            if (buildingListPanel != null)
                buildingListPanel.BuildingClicked -= HandleBuildingClicked;
            FrameworkEvents.SharedGameDataLoaded -= HandleFrameworkReady;
            FrameworkEvents.LoadCompleted -= HandleFrameworkReady;
            openWhenReady = false;
        }

        private void HandleBuildingClicked(string buildingName)
        {
            if (!string.Equals(buildingName, TransportInventoryFunction.BuildingDisplayName,
                    StringComparison.Ordinal))
                return;

            openWhenReady = true;
            TryOpen();
        }

        private void HandleFrameworkReady(ISharedGameDataProvider _) => TryOpen();
        private void HandleFrameworkReady(ND.Framework.SaveData _) => TryOpen();

        private void TryOpen()
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            if (!openWhenReady || root == null || popup == null || root.CurrentSaveData == null
                || root.SharedGameData == null || !root.SharedGameData.IsLoaded)
                return;

            openWhenReady = false;
            popup.Open(new TransportInventoryDataProvider(
                () => FrameworkRoot.Instance?.CurrentSaveData,
                () => FrameworkRoot.Instance?.SharedGameData));
        }
    }
}
```

Unity가 `.meta` 파일을 자동 생성하도록 Project 창 갱신을 기다린다. 기존 `.meta`를 임의로 복사하거나 GUID를 직접 작성하지 않는다.

## 부록 B. 조립 완료 판정 체크리스트

다음 항목이 모두 참이어야 조립 완료다.

- InGame Scene에 `CaravanSettingRuntimeBridge`가 정확히 1개 있다.
- InGame Scene에 `TestCaravanSettingService`가 남아 있지 않다.
- Overview Binding의 네 서비스 참조가 모두 같은 RuntimeBridge다.
- RuntimeBridge의 `tradeItemAssets`가 비어 있지 않고 중복 ItemId가 없다.
- `TransportInventoryPopupController`가 정확히 1개 있고 시작 시 비활성화다.
- `TransportInventoryMainUiEntry`가 정확히 1개 있다.
- Entry의 `buildingListPanel`과 `popup`이 모두 연결되어 있다.
- 테스트 지급 버튼을 사용한다면 정확히 1개만 배치되어 있다.
- `TradePrepareUI/S3_Animal`의 `AnimalInventoryPanel.wagonPopup`이 새 중첩 프리팹을 참조한다.
- `WagonSelectPopup.instanceRowPrefab`이 비어 있지 않다.
- Console에 Missing Script, Missing Reference, 이벤트 이중 등록 오류가 없다.

## 부록 C. 조립 계약 테스트 갱신 기준

RuntimeBridge 조립을 커밋할 때는 Scene 계약 테스트도 같은 커밋에 포함한다. 기존 테스트가 `TestCaravanSettingService` 1개를 기대하면 정상 조립을 실패로 판정한다.

`CaravanSettingSceneContractTests`의 InGame 계약은 다음을 검사하도록 갱신한다.

- `InGame.unity`: `TestCaravanSettingService` 0개, `CaravanSettingRuntimeBridge` 1개
- `InGame_Test.unity`: 기존 임시 서비스 구조를 유지한다면 `TestCaravanSettingService` 1개, RuntimeBridge 0개
- InGame RuntimeBridge의 `tradeItemAssets`가 비어 있지 않고 ItemId 중복이 없음
- Overview Binding의 네 서비스 필드가 모두 RuntimeBridge를 참조함
- Transport Inventory Entry, Popup, 테스트 지급 버튼이 각각 1개
- Entry의 `buildingListPanel`, `popup` 참조가 연결됨
- Popup의 시작 상태가 비활성화임

`CaravanSettingPlayModeSmokeTests`는 다음 흐름을 추가한다.

1. Fixture에 `목장`, 레벨 1 건물 데이터를 추가한다.
2. InGame의 서비스 타입을 `CaravanSettingRuntimeBridge`로 검사한다.
3. 테스트 지급 버튼을 눌러 마차 2개와 동물 2마리가 추가되는지 검사한다.
4. `BuildingListPanel`에서 `목장`으로 시작하는 버튼을 눌러 Popup이 열리는지 검사한다.
5. 동물 탭 버튼을 눌러 동물 패널이 활성화되는지 검사한다.
6. Popup을 닫은 뒤 기존 Caravan Setting smoke flow를 계속 실행한다.

테스트 갱신은 런타임 기능을 만드는 단계가 아니라, 조립 결과가 이후 실수로 끊기지 않게 보호하는 단계다. Scene 조립만 임시 검증하고 버릴 경우에는 테스트 파일을 수정하지 않아도 되지만, 조립 변경을 커밋할 경우에는 반드시 함께 갱신한다.
