# Transport Inventory · Caravan Set InGame 조립 문서

> 전체 InGame UI 재조립 순서와 완료 판정은 `0807_InGame_UI_Reassembly_Master_Checklist.md`에서 관리한다. 최신 dev2의 실패 Claim 전손과 손실 Popup 조립은 `0807_Dev2_InGame_Reassembly_and_Failed_Trade_Loss.md`를 따른다. 이 문서는 목장 진입, Transport Inventory, RuntimeBridge, 테스트 지급 기능의 상세 조립 절차로 사용한다.

## 1. 목적과 범위

이 문서는 `InGame.unity`와 `TradePrepareUI.prefab`의 조립 변경을 제거한 뒤에도 다음 상태를 다시 만들기 위한 문서다.

- Main UI의 건설된 `목장 Lv.N` 행을 누르면 Transport Inventory가 열린다.
- Transport Inventory는 Framework의 현재 SaveData와 SharedGameData를 표시한다.
- Caravan Set은 소유 중인 마차를 `contentId`별로 묶어 표시하고, 펼친 목록에서는 `instanceId`별 개체를 선택한다.
- Caravan에 배정된 마차와 동물은 Transport Inventory에 남아 있으며 `어느 Caravan에서 사용 중인지` 표시된다.
- 무역 저장 과정에서도 선택한 마차와 동물의 `instanceId`가 유지된다.

이 문서는 조립 전용이다. 구매 재화 차감, 퀘스트 보상 지급, 무역 실패 정산 삭제는 각 기능 담당 영역이다. 실패 Claim 이후 Transport Inventory에는 제거가 저장된 뒤의 소유 자산만 표시되어야 하며, 삭제 자체를 이 UI가 수행해서는 안 된다.

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

현재 독립 `WagonSelectPopup.prefab` 원본에는 `buttonPrefab`이 내장되어 있지 않다. `TradePrepareUI.prefab`에 중첩한 뒤 해당 인스턴스의 `buttonPrefab` override를 `TradePrepareUI/Templates/TownBtn`에 연결해야 한다. 이 연결은 `instanceRowPrefab`과 별개이며 둘 중 하나라도 비면 완전한 선택 목록을 만들 수 없다.

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
9. `WagonSelectPopup.buttonPrefab`에 `TradePrepareUI/Templates/TownBtn`의 `Button`을 연결한다. 이 참조가 비면 소유 마차 데이터가 전달되어도 그룹 버튼이 생성되지 않는다.
10. `WagonSelectPopup.instanceRowPrefab`에 `WagonInstanceRow.prefab`의 `WagonInstanceRowView`가 연결되어 있는지 확인한다.
11. Prefab을 저장한다.

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

마차 그룹이 하나도 나오지 않으면 `WagonSelectPopup.buttonPrefab`을, 개체 행만 나오지 않으면 `instanceRowPrefab`을 먼저 확인한다. 또한 `AnimalInventoryPanel.wagonPopup`이 `None`인지 확인한다.

## 6. InGame에 Transport Inventory 배치

대상 씬:

`Assets/_Project/07.Scenes/04_InGame/InGame.unity`

조립 순서:

1. `MainUICanvas`의 팝업 계층 아래에 `TransportInventoryPopup.prefab`을 한 번 배치한다.
2. 이름은 `TransportInventoryPopup`으로 유지한다.
3. 화면 전체 Stretch 앵커를 사용한다. Prefab을 Scene 부모 아래에 배치한 직후 `RectTransform`을 `Anchor Min (0,0)`, `Anchor Max (1,1)`, `Pivot (0.5,0.5)`, `Anchored Position (0,0)`, `Size Delta (0,0)`, `Scale (1,1,1)`로 명시적으로 맞춘다. 부모를 변경하며 `Anchored Position (-960,-540)`, `Size Delta (-1920,-1080)` 같은 Canvas 크기 보정값이 남으면 화면 위치와 크기가 어긋난다.
4. 시작 상태는 비활성화한다.
5. 항상 활성화되는 Scene 조립 오브젝트에 `TransportInventoryMainUiEntry`를 추가한다. 현재 기준 위치는 `CaravanSettingUiConnector`다.
6. 다음 직렬화 참조를 직접 연결한다.

| `TransportInventoryMainUiEntry` 필드 | 연결 대상 |
| --- | --- |
| `buildingListPanel` | 같은 Main UI의 `BuildingListPanel` |
| `popup` | 배치한 `TransportInventoryPopup`의 `TransportInventoryPopupController` |

런타임 `Find`로 연결하지 않는다.

`Size Delta (-1920,-1080)`은 1920×1080 Canvas에서 Stretch 크기를 0으로 축소하는 잘못된 override다. 이 상태에서는 카드 자식은 보이더라도 전체 화면 Backdrop이 보이지 않거나 클릭 영역이 사라질 수 있다. 조립 후 Scene Inspector에서 반드시 `Size Delta (0,0)`을 다시 확인하고, `TransportInventoryPopupController.backdropButton`이 Prefab의 `Backdrop/Button`을 참조하는지 확인한다. Backdrop Image는 전체 Stretch, Raycast Target On이며 `OnEnable()`에서 등록되는 `Close()`로 현재 팝업을 닫는다.

Popup은 이름만 보고 첫 Canvas를 선택하지 않는다. 실제 `BuildingListPanel`과 함께 활성화되는 `MainUICanvas` 아래에 배치하고, 비활성 레거시 `InGameCanvas` 아래에는 배치하지 않는다. 닫힌 상태는 Popup 자신의 `activeSelf == false`여야 하며 비활성 부모에 의존하지 않는다.

여기서 `MainUICanvas.prefab` 원본에 Apply하지 않는다. 현재 조립 기준은 `InGame.unity` 안의 `MainUICanvas` 프리팹 인스턴스에 Popup 인스턴스를 추가하고, Scene 조립 오브젝트에 Entry 컴포넌트를 추가하는 방식이다. 다른 씬까지 공통 적용하려는 별도 합의가 있을 때만 `MainUICanvas.prefab`에 Apply한다.

같은 원칙으로 실패 손실 Popup도 `InGame.unity`의 활성 MainUICanvas 아래에 Scene prefab instance로 배치한다. Transport Inventory Popup과 실패 손실 Popup은 서로 다른 컴포넌트와 책임을 가지며 한 Popup으로 합치지 않는다. 실패 Popup의 상세 sibling 순서와 `SettlementUiDataAdapter.failureLossPopup` 연결은 최신 dev2 실패 전손 문서를 따른다.

실패 Claim 저장 성공 후 Transport Inventory를 다시 열었을 때 파괴된 장착 Wagon/Animal은 목록에서 사라지고 예비 자산만 남아야 한다. 저장 실패 후에는 기존 자산이 그대로 보여야 한다. UI가 빈 `instanceId`를 보고 임의의 동일 콘텐츠 개체를 삭제하거나 대체하면 안 된다.

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

기존 임시 서비스의 `cargoCatalog`에 연결되어 있던 `TradeItemData` 에셋은 `CaravanSettingRuntimeBridge.tradeItemAssets`에 옮긴다. 단, 기존 배열만 그대로 복사하는 것으로 완료 처리하지 않는다. RuntimeBridge는 상점 목록뿐 아니라 Caravan Cargo에 이미 적재된 품목도 표시하므로, 현재 저장 데이터와 SharedGameData에서 Cargo로 들어올 수 있는 모든 `itemId`의 표시 에셋이 필요하다.

현재 시연 데이터 기준 최소 확인 대상은 다음 여섯 개다.

| itemId | TradeItemData 에셋 |
| --- | --- |
| `Apple` | 프로젝트의 Apple `TradeItemData` |
| `Wheat` | 프로젝트의 Wheat `TradeItemData` |
| `Cloth` | 프로젝트의 Cloth `TradeItemData` |
| `Stover` | 프로젝트의 Stover `TradeItemData` |
| `Logs` | `Assets/_Project/02.Data/01_ScriptableObjects/TradeItem/TradeItem_Logs.asset` |
| `Stone` | `Assets/_Project/02.Data/01_ScriptableObjects/TradeItem/TradeItem_Stone.asset` |

배열 순서는 식별 기준이 아니다. 각 `TradeItemData.itemId`로 조회되므로 `itemId` 중복과 누락이 없어야 한다. `Logs` 또는 `Stone`이 빠지면 저장 데이터의 수량과 중량은 남아 있어도 적재 슬롯 아이콘이 비어 보인다.

기존 `TestCaravanSettingService`와 RuntimeBridge를 동시에 명령 처리자로 연결하지 않는다. 저장 명령이 두 번 등록될 수 있다.

기존 `TestCaravanSettingService`를 교체할 때는 다음 순서를 지킨다.

1. 기존 `cargoCatalog`의 모든 `TradeItemData` 참조를 기록한다.
2. 같은 GameObject에 `CaravanSettingRuntimeBridge`를 추가한다.
3. 기록한 에셋과 현재 Cargo에 들어올 수 있는 추가 품목(`Logs`, `Stone` 포함)을 `tradeItemAssets`에 연결한다.
4. Overview Binding의 네 Provider/Command 필드를 RuntimeBridge로 교체한다.
5. `tradeItemAssets`의 각 에셋이 고유한 `itemId`를 가지며 누락이 없는지 확인한다.
6. 네 필드가 모두 교체된 것을 확인한 뒤에만 `TestCaravanSettingService`를 제거한다.

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

## 8. 개발 검증 버튼 — 현재 개발 씬 필수

상점과 보상 지급 경로가 아직 완성되지 않은 현재 개발 씬에서는 `TransportInventoryRewardDebugButton.prefab`을 활성 `MainUICanvas` 좌하단에 정확히 한 번 배치한다. 운영 빌드 전환 시 제거 또는 비활성화 여부를 별도로 결정한다.

배치 계약:

- 부모: InGame Scene의 활성 `MainUICanvas` 인스턴스
- Hierarchy: `InfoPanel` 바로 다음 sibling에 둔다. 현재 기준 sibling index는 `3`이다.
- 렌더 순서: `TransportInventoryPopup`, `CaravanActivityLogPanel`, `MenuPopup`, `NoticeUI`보다 반드시 앞 sibling이어야 한다. Canvas의 마지막 자식으로 넣으면 모든 팝업 위를 덮으므로 금지한다.
- RectTransform: Anchor Min/Max `(0, 0)`, Pivot `(0, 0)`, Anchored Position `(20, 20)`, Size Delta `(260, 56)`
- 이 버튼은 Scene에 미리 배치한 prefab instance이며 런타임 생성 대상이 아니다.

기본 지급 예시:

- `Wagon_M` 1개
- `Wagon_S` 1개
- `Horse` 2마리

버튼은 `contentId`와 수량만 전달해야 한다. `PlayerMainManager`가 각 개체의 고유 `instanceId`를 발급한다. 운영 빌드의 정상 획득 경로로 사용하지 않는다.

### 8.1 무역 실패 손실 안내 Popup

- 에셋: `Assets/_Project/08.Prefabs/UI/Trade/TradeFailureLossPopup.prefab`
- 구성: 전체 화면 입력 차단 배경, 중앙 `Panel`, `MessageText`, `ConfirmButton/Label`
- 공용 View: `ReusableMessagePopup` (`Assets/_Project/05.UI/04_InGame/YHY/Scripts/Common/ReusableMessagePopup.cs`)
- 문구는 Claim 직전 실제 장착 구성에 따라 마차+동물, 마차만, 동물만, 적재물만 네 경우로 선택한다.
- prefab root는 기본 비활성 상태로 유지한다.
- 런타임 생성하지 않고 활성 `MainUICanvas`에 prefab instance로 미리 배치한다.
- 실제 손실 데이터 저장이 성공한 뒤 활성화하고, `ConfirmButton`으로 닫도록 연결한다.
- 재사용 시 `Show(message, buttonText, confirmed)`로 메시지, 버튼 문구, 일회성 확인 콜백을 전달한다. 표시 중 문구만 바꿀 때는 `SetContent(message, buttonText)`를 사용한다.
- InGame Scene에서는 `MainUICanvas`의 일반 Popup과 활동 로그보다 뒤, `NoticeUI` 바로 앞 sibling에 둔다. 현재 조립 기준 index는 `22`이며 `NoticeUI`는 `23`이다.
- `SettlementUiDataAdapter.failureLossPopup`에 이 Scene instance를 연결한다. 현재 실제 S8/S9 Adapter인 `UIManager`와 호환용 `TradeTestPannel` 양쪽 참조가 연결되어 있다.
- S9 Claim은 다음 Pending 표시를 보류한 채 저장한다. 실패 결과면 Popup 확인 콜백에서 `ContinuePendingSettlementPresentation()`을 호출하고, 성공/부분 성공이면 Popup 없이 즉시 호출한다.
- 다음 실패 Pending이 있으면 컬렉션 순서대로 S8을 표시하고, 없으면 Claim이 전환한 Town 화면을 유지한다.

## 9. 권장 조립 순서

1. 기능 스크립트 버전을 확인하고 누락 또는 롤백된 파일을 복구한다.
2. Unity 컴파일 완료와 Console 오류 0건을 확인한다.
3. `WagonInstanceRow.prefab`과 `WagonSelectPopup` 내부 참조를 확인한다.
4. 기존 내장 Popup 유지 또는 독립 Popup 중첩 중 한 방식을 선택하고 `AnimalInventoryPanel.wagonPopup`을 연결한다.
5. `TransportInventoryMainUiEntry.cs` 존재 여부를 확인하고 다시 컴파일한다.
6. 활성 `MainUICanvas`에 `TransportInventoryPopup.prefab`을 배치하고 시작 상태를 비활성화한다.
7. 활성 Scene 조립 오브젝트에 `TransportInventoryMainUiEntry`를 추가하고 두 필드를 연결한다.
8. 임시 서비스의 카탈로그를 보존한 채 `CaravanSettingRuntimeBridge`와 Overview Binding을 먼저 연결한다.
9. `Logs`, `Stone`을 포함한 `tradeItemAssets`를 확인한 뒤에만 `TestCaravanSettingService`를 제거한다.
10. 개발 검증에서 지급 수단이 필요한 경우에만 활성 `MainUICanvas` 좌하단에 테스트 지급 버튼을 한 번 배치한다. 운영 조립에서는 생략한다.
11. Scene과 Prefab을 저장 후 다시 열어 직렬화 참조를 확인한다.
12. 조립 계약 테스트와 PlayMode Smoke Test를 갱신하고 실행한다.

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
- RuntimeBridge의 `tradeItemAssets`에 현재 Cargo 표시 대상인 `Logs`와 `Stone`이 포함되어 있다.
- `TransportInventoryPopupController`가 정확히 1개 있고 시작 시 비활성화다.
- `TransportInventoryMainUiEntry`가 정확히 1개 있다.
- Entry의 `buildingListPanel`과 `popup`이 모두 연결되어 있다.
- 현재 개발 씬에는 테스트 지급 버튼이 정확히 1개 배치되어 있다.
- Popup의 부모 Canvas는 활성 `MainUICanvas`이며 비활성 `InGameCanvas`가 아니다.
- `TradePrepareUI/S3_Animal`의 `AnimalInventoryPanel.wagonPopup`이 새 중첩 프리팹을 참조한다.
- `WagonSelectPopup.instanceRowPrefab`이 비어 있지 않다.
- `WagonSelectPopup.buttonPrefab`이 비어 있지 않다.
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
# 2026-08-10 실제 재조립 확인 사항

최신 미조립 `InGame.unity`에서 다음 구성으로 정상 재현됨을 확인했다.

```text
MainUICanvas (Scene의 활성 Prefab instance)
└─ TransportInventoryPopup [Prefab instance, inactive]

CaravanSettingUiConnector
├─ CaravanSettingRuntimeBridge
└─ TransportInventoryMainUiEntry
```

필수 연결:

| 대상 | 값 |
| --- | --- |
| `TransportInventoryMainUiEntry.buildingListPanel` | Scene의 기존 `BuildingListPanel` |
| `TransportInventoryMainUiEntry.popup` | 배치한 Popup의 `TransportInventoryPopupController` |

완료 판정은 Hierarchy 이름만이 아니라 타입 개수와 직렬화 참조로 한다.

- `TransportInventoryMainUiEntry`: 정확히 1개
- `TransportInventoryPopupController`: 정확히 1개
- Popup root: 시작 시 inactive
- Entry의 두 필드: 모두 None 아님
- 목장 행 클릭: Popup이 열림
- 동물 탭: 현재 해금 슬롯의 마지막 행까지 스크롤 가능

`TransportInventoryPopup`은 `MainUICanvas.prefab` 원본에 Apply하지 않는 Scene 전용 조립물이다. 따라서 기능 전달 브랜치에서 `InGame.unity`를 discard할 경우 사라지는 것이 정상이며, 통합 브랜치에서 이 문서대로 다시 배치하고 Scene 변경으로 저장해야 한다.

개발용 지급 버튼은 기능 필수가 아니다. 검증에 필요한 경우에만 Scene 전용으로 `TransportInventoryRewardDebugButton.prefab`을 좌하단 `(20,20)`, `260x56`으로 1개 두며 마지막 sibling으로 배치하지 않는다.
