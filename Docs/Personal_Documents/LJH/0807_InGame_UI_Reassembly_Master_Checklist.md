# InGame UI 현재 형태 재조립 총괄 체크리스트

## 1. 사용 목적

이 문서는 현재 기능 개발 브랜치에서 충돌 가능성이 큰 `MainUICanvas.prefab`, `TradePrepareUI.prefab`, `InGame.unity`의 조립 diff를 제외해 전달한 뒤, 대상 브랜치의 최신 파일에 현재 기능을 다시 조립할 때 사용하는 최상위 기준이다. 이 문서 한 개만으로 필수 기능과 데이터 흐름을 복구할 수 있어야 하며, 기능별 문서는 외형 수치와 원인 분석을 위한 보충 자료로 사용한다.

> **브랜치 적용 범위:** 현재 기능 개발 브랜치에서는 다른 브랜치와의 Scene/Prefab 충돌을 줄이기 위해 `MainUICanvas.prefab`과 `InGame.unity`의 조립 diff를 discard한다. 이후 기능을 받아 조립하는 대상 브랜치에서는 이 문서대로 두 파일을 수정·저장하고 그 조립 결과를 정상적으로 커밋해야 한다. 두 파일을 항상 discard하라는 공통 규칙이 아니다.

## 2. 기능별 문서

| 순서 | 기능 | 상세 문서 |
| ---: | --- | --- |
| 1 | Caravan Setting 런타임 서비스, Wagon 개체 선택, Animal 묶음 표시 | `0806_Caravan_Set_UI_Reassembly.md` |
| 2 | 목장 클릭, Transport Inventory, Logs/Stone 아이콘, 동물 탭 스크롤, 테스트 지급 버튼 | `0806_Transport_Inventory_InGame_Assembly.md` |
| 3 | Caravan Overview, Rename 버튼, Treadmill, 상태 아이콘, 말 애니메이션 | `0807_Caravan_Overview_Current_Reassembly.md` |
| 4 | 최신 dev2 Scene/Prefab 조립, 실패 Claim 전손, 손실 Popup, 순차 정산 | `0807_Dev2_InGame_Reassembly_and_Failed_Trade_Loss.md` |
| 5 | BaseCamp 레벨 상한, 건물 현황 Popup, InGame 정적 조립 | `0810_BaseCamp_Level_Gate_and_Overview_UI_Assembly.md` |
| 6 | 오두막 UTC 생산, 받기 Popup, 생산 완료 Badge, 최신 dev2 MainUI 조립 | `0810_Cottage_Production_MainUI_Reassembly.md` |
| 7 | 빵집 UTC 생산, 전체/부분 수령, 생산 완료 Badge, 0원/유료 Bread 가격 묶음 | `0811_Bakery_Production_MainUI_Reassembly.md` |
| 참고 | 기존 Overview/Treadmill 요구사항과 Scene 참조 배경 | `0805_Caravan_Overview_Treadmill_Binding_Request.md` |
| 참고 | Transport Inventory 원인 및 수정 근거 | `0806_Transport_Inventory_PlayMode_Issue_Report.md` |
| 참고 | 창고 가격 묶음, 수량 Modal과 Backdrop 입력 계약 | `Warehouse_Runtime_Connection_2026-08-03.md`, `Warehouse_Inventory_UI_Implementation_Spec.md` |

## 3. discard 범위와 보존 범위

### 현재 기능 개발 브랜치에서만 discard할 조립 파일

- `Assets/_Project/08.Prefabs/MainUICanvas.prefab`

> 주의: `InGame.unity`의 MainUICanvas 인스턴스가 실제 참조하는 원본은 위 경로다. `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab`에 조립하면 Scene의 건물 Block 이벤트와 연결되지 않는다.
- `Assets/_Project/08.Prefabs/UI/Maps/TradePrepareUI.prefab`
- `Assets/_Project/07.Scenes/04_InGame/InGame.unity`

현재 기능 개발 브랜치에서 discard 후 사라지는 항목이며, 대상 브랜치에서는 반드시 다시 조립해 저장할 항목:

- `MainUICanvas.prefab`의 `BaseCampMainUiEntry` 컴포넌트와 `BaseCampOverviewPopup` 자식 인스턴스
- `MainUICanvas.prefab`의 `CottageProductionMainUiEntry` 컴포넌트와 `CottageProductionPopup` 자식 인스턴스
- 위 두 파일에 저장된 BaseCamp 관련 Inspector 참조와 sibling override

`WorldMapRenderRootV2.prefab` 변경은 위 UI 재조립과 직접 관련 없는 좌표/라인 변경이 섞일 수 있으므로 별도 검토 후 처리한다.

### 반드시 보존할 기능 코드와 에셋

- `CaravanSlotView.cs`의 Rename/Selling/Animator 제어 코드
- `AnimalInventoryPanel.cs`
- `TransportSelectPanel.cs`
- `WagonSelectPopup.cs`
- `TransportInventoryPanelView.cs`
- `TransportInventoryMainUiEntry.cs`와 `.meta`
- Wagon/Animal 선택용 재사용 Prefab과 View 스크립트
- `HorseCycle.png`와 `.meta`
- `CaravanSlotTravelingHorse.anim`과 `.meta`
- `JourneyStateDisplay.controller`
- Rename 아이콘 에셋
- 관련 Editor/PlayMode 계약 테스트 변경
- `FailedTradeTransportLoss.cs`와 `.meta`
- `ReusableMessagePopup.cs`와 상위 `Common.meta`
- `TradeFailureLossPopup.prefab`과 `.meta`
- `BaseCampBuildingLevelPolicy.cs`와 `.meta`
- `BaseCampOverviewPopupController.cs`, `BaseCampMainUiEntry.cs`와 `.meta`
- `BaseCampOverviewPopup.prefab`과 `.meta`
- `BuildingMaterialTestButton.cs`, `BuildingMaterialTestButton.prefab`과 각 `.meta`
- BaseCamp 레벨 정책 및 UI 계약 테스트와 각 `.meta`
- `FailedTradeTransportLossTests.cs`, `TradeFailureLossPopupWiringTests.cs`와 각 `.meta`
- 오두막 생산 코드, SaveData/이벤트 연동, `CottageProductionData.asset`
- `CottageProductionPopup.prefab`과 View/Presenter/ViewData
- `CottageProductionMainUiEntry.cs`와 `CottageProductionPopupPrefabBuilder.cs`
- 빵집 생산 코드, SaveData/이벤트 연동, `BakeryProductionData.asset`
- `BakeryProductionPopup.prefab`과 View/Presenter/ViewData
- `BakeryProductionMainUiEntry.cs`와 빵집 MainUI 설치 도구
- Warehouse 가격 묶음 보존 및 수량 Modal Backdrop 처리 코드와 Prefab

현재 기능 개발 브랜치에서 Prefab/Scene diff를 discard할 때 보존 목록의 파일을 함께 제거하지 않는다. 특히 untracked 파일은 `git restore`로 복구할 수 없으므로 먼저 백업 또는 추적 상태를 확보한다. 대상 브랜치에서는 재조립한 `MainUICanvas.prefab`과 `InGame.unity`를 discard하지 말고 기능 변경으로 커밋한다.

필수 파일의 권위 경로:

| 용도 | 경로 |
| --- | --- |
| 마차 개체 행 | `Assets/_Project/08.Prefabs/UI/Trade/WagonInstanceRow.prefab` |
| 마차 선택 Popup | `Assets/_Project/08.Prefabs/UI/Trade/WagonSelectPopup.prefab` |
| Transport Inventory Popup | `Assets/_Project/08.Prefabs/UI/TransportInventory/TransportInventoryPopup.prefab` |
| 실패 손실 Popup | `Assets/_Project/08.Prefabs/UI/Trade/TradeFailureLossPopup.prefab` |
| 실패 손실 View | `Assets/_Project/05.UI/04_InGame/YHY/Scripts/Common/ReusableMessagePopup.cs` |
| 실패 전손 처리 | `Assets/_Project/11.CoreServices/Scripts/TradeProgress/FailedTradeTransportLoss.cs` |
| 정산 UI Adapter | `Assets/_Project/11.CoreServices/Scripts/UI/Settlement/SettlementUiDataAdapter.cs` |
| BaseCamp 현황 Popup | `Assets/_Project/08.Prefabs/UI/Building/BaseCampOverviewPopup.prefab` |
| BaseCamp 레벨 정책 | `Assets/_Project/11.CoreServices/Scripts/Building/BaseCampBuildingLevelPolicy.cs` |
| 통나무 테스트 지급 버튼 | `Assets/99.Sandbox/_LJH/Prefab/BuildingMaterialTestButton.prefab` |
| 통나무 TradeItem | `Assets/_Project/02.Data/01_ScriptableObjects/TradeItem/Material/TradeItem_Logs.asset` |
| 오두막 생산 설정 | `Assets/_Project/11.CoreServices/Resources/CottageProductionData.asset` |
| 오두막 생산 Popup | `Assets/_Project/08.Prefabs/UI/Cottage/CottageProductionPopup.prefab` |
| 오두막 MainUI 설치 도구 | `Assets/99.Sandbox/_LJH/Editor/CottageProductionPopupPrefabBuilder.cs` |
| 빵집 생산 설정 | `Assets/_Project/11.CoreServices/Resources/BakeryProductionData.asset` |
| 빵집 생산 Popup | `Assets/_Project/08.Prefabs/UI/Bakery/BakeryProductionPopup.prefab` |
| 빵집 조립 상세 | `Docs/Personal_Documents/LJH/0811_Bakery_Production_MainUI_Reassembly.md` |

## 4. 권장 재조립 순서

### A. 코드와 에셋 준비

- [ ] Unity 컴파일 오류 0건 확인
- [ ] `TransportInventoryMainUiEntry` 타입이 Inspector Add Component에 노출됨
- [ ] `CaravanSlotView`에 `renameButton`, `sellingStateIcon` 필드가 보임
- [ ] `WagonSelectPopup`에 `instanceRowPrefab` 필드가 보임
- [ ] HorseCycle 12개 Sprite와 Traveling Animation Clip이 존재함
- [ ] `JourneyStateDisplay.controller`에 `IsTraveling` Bool이 존재함
- [ ] `SettlementUiDataAdapter`에 `failureLossPopup` 필드가 보임
- [ ] `TradeFailureLossPopup.prefab`과 `ReusableMessagePopup` 타입이 존재함
- [ ] `BaseCampOverviewPopup.prefab`, `BaseCampMainUiEntry`, `BaseCampBuildingLevelPolicy`가 존재함
- [ ] `BuildingMaterialTestButton.prefab`의 `items[0]`이 `TradeItem_Logs.asset`, `grantQuantity`가 `40`임
- [ ] `CottageProductionData.asset`, `CottageProductionPopup.prefab`, `CottageProductionMainUiEntry`가 존재함
- [ ] 오두막 설정의 `Wagon_M`, `Horse`가 런타임 카탈로그에 존재함
- [ ] `BakeryProductionData.asset`, `BakeryProductionPopup.prefab`, `BakeryProductionMainUiEntry`가 존재함
- [ ] 빵집 생산품 `Bread`가 런타임 카탈로그에 존재하고 수령품 구매가는 `0`으로 보존됨

### A-1. 오두막 생산 MainUI 조립

상세 수치와 참조 계약은 `0810_Cottage_Production_MainUI_Reassembly.md`를 따른다.

- [ ] `Tools > LJH > Install Cottage Production Popup To Main UI` 실행
- [ ] MainUI 루트의 `CottageProductionMainUiEntry`가 정확히 1개
- [ ] `CottageProductionPopup`이 정확히 1개이며 `NoticeUI` 바로 앞, 기본 비활성화
- [ ] Entry의 BuildingListPanel/Popup/느낌표 아이콘과 Presenter의 View/NoticeUI 연결
- [ ] 이 기능만을 위해 `InGame.unity`에 override를 만들지 않음

### A-2. 빵집 생산 MainUI 조립

상세 수치와 참조 계약은 `0811_Bakery_Production_MainUI_Reassembly.md`를 따른다.

- [ ] `MainUICanvas` 최상위 루트에 `BakeryProductionMainUiEntry`가 정확히 1개
- [ ] `BakeryProductionPopup`이 정확히 1개이며 기본 비활성화
- [ ] Entry의 BuildingListPanel/Popup/`알림 UI ICON.png`와 Presenter의 View/NoticeUI 연결
- [ ] 빵집 행 Badge는 별도 런타임 오브젝트를 만들지 않고 `BuildingListPanel.SetBuildingBadge()`로 표시
- [ ] 보관량이 현재 레벨 최대치일 때만 Badge가 표시되고 수령 후 즉시 숨겨짐
- [ ] 오두막·빵집 공용 Badge가 우상단 Anchor 기준 `(-120, -6)`, `28 x 28`로 건물 이름·레벨 오른쪽에 표시되고 패널 밖으로 잘리지 않음
- [ ] 빵집 수령 `Bread + 0원`과 상점 구매 `Bread + 실제 구매가`를 서로 다른 가격 묶음으로 보존
- [ ] 이 기능만을 위해 `InGame.unity`에 override를 만들지 않음

### B. TradePrepareUI Prefab 조립

> 코드/문서 전달 브랜치는 기존 `TradePrepareUI.prefab`의 조립 결과를 커밋하지 않는다. 따라서 최신 dev2에 반영한 직후 `instanceRowPrefab`이 비어 있는 것은 예상 상태이며, 아래 절차로 연결한 뒤 Scene/Prefab 담당자가 저장한다.

- [ ] `WagonSelectPopup.buttonPrefab` 연결
- [ ] `WagonSelectPopup.instanceRowPrefab`에 `WagonInstanceRow.prefab` 연결
- [ ] `AnimalInventoryPanel.wagonPopup` 연결
- [ ] Animal Content/Viewport/ScrollRect 연결 및 실제 슬롯 수 기반 Content 높이 로직 확인
- [ ] Popup은 기본 비활성화

`WagonSelectPopup` 필드의 정확한 대상:

| 필드 | 대상 |
| --- | --- |
| `listContainer` | 그룹 버튼과 개체 행이 들어갈 기존 Content |
| `buttonPrefab` | `TradePrepareUI/Templates/TownBtn`의 Button |
| `instanceRowPrefab` | `WagonInstanceRow.prefab`의 `WagonInstanceRowView` |
| `cancelButton` | 기존 취소 Button |

현재 조립은 기존 내장 `WagonPopup` 유지 방식이다. 별도 Popup을 추가해 기존 Popup과 중복 활성화하지 않는다. `tradeScreenPresenter: null` 같은 기본값 기록은 Caravan Set 기능 연결이 아니지만 `instanceRowPrefab`까지 함께 제거하면 안 된다.

저장 후 Prefab Mode를 닫았다 다시 열고 다음 세 참조가 모두 유지되는지 확인한다.

- `AnimalInventoryPanel.wagonPopup`
- `WagonSelectPopup.buttonPrefab`
- `WagonSelectPopup.instanceRowPrefab`

### C. MainUICanvas Prefab 조립

> 이 단계는 대상 브랜치의 최신 `MainUICanvas.prefab`에서 시작한다. 현재 기능 개발 브랜치의 조립된 Entry/Popup을 파일째 덮어쓰거나 Apply하는 절차가 아니라, 보존된 `BaseCampOverviewPopup.prefab`과 스크립트를 사용해 대상 브랜치에서 새로 연결하고 커밋하는 절차다.

BaseCamp 조립은 메뉴 `ND > UI > Install BaseCamp Overview Into Main UI` 사용을 권장한다. 수동 조립 시 `BuildingListPanel` 컴포넌트가 있는 기존 오브젝트에 `BaseCampMainUiEntry`를 추가하고 MainUICanvas root 직접 자식으로 Popup Prefab을 배치한다. Entry의 `buildingListPanel`, `popup`, `noticeUI`는 같은 MainUICanvas 내부 컴포넌트로 연결하며 현재 기능 개발 브랜치의 MainUI 파일을 대상 브랜치에 덮어쓰지 않는다.

- [ ] 네 Caravan Slot의 기존 직렬화 참조 유지
- [ ] Display Name 폭 축소와 Auto Size/Ellipsis 설정
- [ ] 네 슬롯에 RenameButton을 Prefab 오브젝트로 배치
- [ ] 네 RenameButton 아이콘과 `CaravanSlotView.renameButton` 연결
- [ ] 네 슬롯의 정적 `CreateButton`/Label과 `LockOverlay`/Button을 각 `CaravanSlotView`에 연결
- [ ] `CaravanOverviewPresenter.noticeUI`를 같은 MainUICanvas의 기존 `NoticeUI`에 연결
- [ ] 네 JourneyState Icon Image/Animator 연결
- [ ] Prepare/Traveling/Selling/Settling/Completed Sprite 연결
- [ ] `CaravanOverviewRenameBinding`의 Presenter/Popup 연결
- [ ] Transport Inventory Popup과 개발용 지급 버튼은 `MainUICanvas.prefab` 원본에 넣지 않음
- [ ] `BaseCampOverviewPopup.prefab`을 MainUICanvas의 비활성 정적 자식으로 한 번 배치
- [ ] 기존 `BuildingListPanel`에 `BaseCampMainUiEntry`를 한 개 추가
- [ ] Entry의 `buildingListPanel`, `popup`, 기존 `NoticeUI` 참조 연결
- [ ] Popup의 BaseCamp 포함 7개 행, Backdrop, X 버튼이 Prefab 오브젝트로 존재

각 Slot의 필수 순서와 동작:

```text
JourneyStateDisplay → DisplayName → RenameButton → SettingButton → CargoButton
```

- DisplayName 클릭: Treadmill만 호출
- RenameButton 클릭: Rename Popup만 호출
- RenameButton Label: 빈 문자열, 비활성. Icon은 활성
- Prepare/Traveling: `HorseCycle_0`
- Traveling Animator: `IsTraveling=true`, `HorseCycle_0~3`, Sprite track만 사용
- Selling: 느낌표
- Settling/Completed: 체크
- RenameButton과 상태 Icon은 네 Slot에 미리 배치하며 런타임 생성하지 않음
- `JourneyStateDisplay`는 각 Slot의 첫 번째 자식으로 배치한다. 상태 아이콘은 DisplayName 바로 왼쪽에 표시한다.
- `JourneyStateDisplay` 루트 Image는 배경 용도이므로 알파를 `0`으로 설정한다. 자식 `Icon` Image의 알파는 `1`을 유지하여 아이콘만 보이게 한다.
- `LockOverlay`는 항상 마지막 자식으로 유지한다.

Caravan Slot 해금 및 생성 계약:

- 슬롯 인덱스 `0~3`은 BaseCamp Lv.`1~4`에서 차례대로 해금한다. BaseCamp Lv.0의 해금 슬롯은 0개다.
- Occupied: 이름/Rename/Set/Cargo/상태 표시만 활성화한다.
- Empty: 정적 `CreateButton`만 활성화하며, 클릭 시 해당 `slotIndex` 생성 명령을 한 번만 전달하고 저장 완료 전 중복 입력을 막는다.
- Locked: 정적 `LockOverlay`만 활성화한다. 클릭 시 기존 `NoticeUI`에 `베이스 캠프 레벨이 부족하여 캐러밴 슬롯을 해금할 수 없습니다. 필요 레벨: Lv.N`을 표시한다.
- Unknown: Occupied/Create/Lock 동작을 모두 비활성화한다.
- 슬롯 해금 권위는 `SaveData.player.villageBuildings`의 BaseCamp 레벨이다. UI 활성 상태나 현재 슬롯 점유 여부를 권위 데이터로 사용하지 않는다.

현재 MainUICanvas 외형 재현값:

| 대상 | 설정 |
| --- | --- |
| DisplayName LayoutElement | Min Width `173`, Preferred Width `173` |
| DisplayName TMP | Auto Size On, Font Size `14~20`, Overflow Ellipsis, 좌우 Margin `6` |
| RenameButton LayoutElement | Min/Preferred Width `72`, Height `44` |
| RenameButton 배경 | `RGB(212,170,93)` 황갈색 |
| RenameButton Label | 빈 문자열, GameObject 비활성 |
| RenameButton Icon | Rename Sprite, GameObject/Image 활성, Raycast Target Off |

각 `CaravanSlotView`의 필수 직렬화 필드:

- `displayNameText`
- `renameButton`
- `settingButton`
- `cargoButton`
- `journeyStateDisplay`
- `journeyStateText`
- `journeyStateIconImage`
- `prepareStateIcon`
- `travelingStateIcon`
- `sellingStateIcon`
- `settlingStateIcon`
- `completedStateIcon`
- `journeyStateIconAnimator`
- `travelingAnimatorParameter = IsTraveling`

Trading Currency HUD 계약:

- [ ] `CurrencyHUD`의 `CurrencyHudRuntimeBinding.currencyChangedChannel`을 `Assets/_Project/02.Data/01_ScriptableObjects/EventChannels/EventChannel_CurrencyChanged.asset`에 연결
- [ ] `CurrencyHudPivot`의 `CurrencyHudPresenter.currencyChangedChannel`도 같은 Asset에 연결
- [ ] 두 컴포넌트가 동일한 채널을 사용하고 `FrameworkEvents.TradingCurrencyChanged` 발생 직후 HUD가 갱신되는지 확인
- [ ] HUD 갱신을 위한 `Update()` 폴링을 추가하지 않음

### D. InGame Scene 조립

> 이 단계는 대상 브랜치의 최신 `InGame.unity`에서 시작한다. 먼저 C 단계의 `MainUICanvas.prefab` 조립을 저장하고 Prefab Mode를 닫은 뒤 Scene을 연다. BaseCamp Popup/Entry는 Prefab 상속으로 받아야 하며 Scene에 다시 중복 생성하지 않는다. 대상 브랜치에서 완성된 Scene 변경은 정상 조립 결과이므로 저장·커밋한다.

BaseCamp 조립에서는 기존 `BuildingConstructionRuntimeHandler`에 추가 Inspector 연결이 없다. `BuildingLogsDebugButton`은 기본 조립 대상이 아니며 InGame Scene에 배치하지 않는다. 다른 최신 dev2 Scene 오브젝트와 override는 유지한다.

- [ ] `CaravanSettingUiConnector` 또는 별도 명확한 Scene 조립 루트 사용
- [ ] `TestCaravanSettingService` 제거
- [ ] `CaravanSettingRuntimeBridge` 정확히 1개 추가
- [ ] Overview Setting/Cargo Binding의 Provider/Command 참조를 RuntimeBridge로 연결
- [ ] RuntimeBridge `tradeItemAssets`에 Apple/Wheat/Cloth/Stover/Logs/Stone 연결
- [ ] `TransportInventoryMainUiEntry` 정확히 1개 추가
- [ ] Entry `buildingListPanel` 연결
- [ ] `InGame.unity`의 활성 MainUICanvas 인스턴스 아래에 `TransportInventoryPopup.prefab`을 한 번 배치하고 기본 비활성화
- [ ] Entry `popup`에 위 Scene Popup 인스턴스의 `TransportInventoryPopupController` 연결
- [ ] 개발용 운송 수단 지급 버튼(`TransportInventoryRewardDebugButton.prefab`)을 활성 MainUICanvas Scene 인스턴스에 한 개만 배치
- [ ] `CaravanOverviewPresenter.treadmillPanel`에 Scene TreadmillPanel 연결
- [ ] 활성 MainUICanvas Scene 인스턴스 아래에 `TradeFailureLossPopup.prefab`을 한 번 배치하고 기본 비활성화
- [ ] Scene에서 사용하는 모든 `SettlementUiDataAdapter.failureLossPopup`에 같은 Popup instance 연결
- [ ] 실패 Popup은 일반 Town/Trade UI보다 뒤 sibling에 두되 Scene 전용 참조를 MainUICanvas 원본에 Apply하지 않음
- [ ] Scene 전용 참조는 Prefab 에셋에 Apply하지 않고 Scene override로 저장
- [ ] BaseCamp UI는 MainUICanvas Prefab 상속으로 반영하고 InGame에 불필요한 Scene override를 만들지 않음
- [ ] InGame 실인스턴스에서 BaseCamp Entry 1개, Popup 1개, 건설 Handler 1개 확인
- [ ] InGame Scene에 `BuildingLogsDebugButton`이 배치되지 않았는지 확인

Scene 배치 세부 기준:

- `TransportInventoryPopup`: 활성 MainUICanvas 아래, 기본 비활성
- 운송 수단 지급 버튼과 통나무 지급 버튼은 서로 다른 Prefab/컴포넌트다. 한쪽으로 다른 쪽을 대체하지 않는다.
- 운송 수단 지급 버튼: 좌하단 anchor/pivot `(0,0)`, 위치 `(20,20)`, 크기 `(260,56)`
- 두 버튼은 가로 20px 간격으로 배치한다. 둘 다 `InfoPanel` 다음 영역에 두고 일반 Popup/NoticeUI보다 낮은 sibling index를 사용해 Popup을 가리지 않는다.
- `TradeFailureLossPopup`: 활성 MainUICanvas 아래, 기본 비활성, 일반 Popup/활동 로그보다 뒤이고 `NoticeUI` 바로 앞 sibling
- 모든 Scene `SettlementUiDataAdapter.failureLossPopup`: 동일한 `TradeFailureLossPopup`의 `ReusableMessagePopup` 참조
- Popup Button Persistent `OnClick`: 별도 Listener를 넣지 않음
- `ReusableMessagePopup`: 배치된 instance의 내용과 활성 상태만 변경

BaseCamp 재조립 경계:

| 대상 | 저장 위치 | 현재 브랜치 discard / 대상 브랜치 조치 |
| --- | --- | --- |
| BaseCamp 레벨 정책·건설 검증 | C# 파일 | 보존, Scene 연결 불필요 |
| BaseCamp Popup 원본 | `BaseCampOverviewPopup.prefab` | 보존 |
| Popup/Entry 배치와 참조 | `MainUICanvas.prefab` | C 단계에서 재조립 |
| 통나무 지급 버튼 원본 | `BuildingMaterialTestButton.prefab` | 보존 |
| 통나무 지급 버튼 인스턴스 | `InGame.unity` | D 단계에서 재조립 |
| BaseCamp 저장 데이터 | `SaveData.player.villageBuildings` | 기존 저장 흐름 사용, 별도 Scene 필드 추가 없음 |

### E. 계약 테스트와 수동 검증

- [ ] Scene 계약 테스트에서 RuntimeBridge 1개, Test Service 0개
- [ ] BaseCamp Entry 1개, Popup 1개, `BuildingLogsDebugButton` 0개
- [ ] 운송 수단 지급 버튼과 통나무 지급 버튼의 컴포넌트·Prefab·기능이 서로 뒤바뀌지 않음
- [ ] 목장 Lv.1 생성 후 목장 블록 클릭 시 Transport Inventory가 열림
- [ ] Wagon_M 1, Wagon_S 1, Horse 2 지급 후 목록에 표시됨
- [ ] Logs/Stone 아이콘과 이름, 수량, 중량이 표시됨
- [ ] 동물 슬롯이 늘어나도 스크롤이 잠금 영역까지 내려감
- [ ] Wagon 종류 그룹과 개체별 내구도가 올바르게 표시됨
- [ ] 다른 Caravan이 사용 중인 Wagon 개체는 선택 불가
- [ ] BaseCamp Lv.0~4에서 슬롯 0~3이 Locked → Empty 순서로 해금됨
- [ ] Empty 슬롯 CreateButton이 Caravan을 한 번만 생성하고 저장 후 Occupied로 갱신됨
- [ ] Locked 슬롯 클릭 시 기존 NoticeUI에 필요한 BaseCamp 레벨이 한국어로 표시됨
- [ ] 이름 클릭/Treadmill과 Rename 버튼 동작이 분리됨
- [ ] Prepare/Traveling/Selling/Settling/Completed 아이콘이 상태표와 일치함
- [ ] Missing Script, Missing Reference, 중복 이벤트 등록 오류가 없음
- [ ] 코드/Prefab 집중 테스트 통과 후, Scene 조립이 끝난 상태에서 Explicit `InGameScene_SettlementAdaptersReferencePlacedPopupInstance`도 별도 실행하여 통과
- [ ] 실패 S8 → S9 Claim → Town 위 손실 Popup → 확인 → 다음 실패 Pending 또는 Town 순서 확인
- [ ] Claim 저장 실패 시 장착/소유 인벤토리와 Pending이 모두 복구됨
- [ ] BaseCamp Lv.0에서 일반 건물 Lv.1 건설이 차단되고 재료가 유지됨
- [ ] BaseCamp Lv.1에서 일반 건물 Lv.1은 성공하고 Lv.2는 차단됨
- [ ] BaseCamp Lv.2 증축 후 일반 건물 Lv.2가 성공함
- [ ] BaseCamp 블록 클릭 시 현황 Popup의 저장 레벨, 현재 해금 슬롯 수, 다음 슬롯/EndingItem 해금 안내가 일치함
- [ ] BaseCamp Lv.0~4에서 EndingItem은 비활성 표시되고 Lv.5에서만 활성화됨
- [ ] EndingItem Lv.1 건설 후 추가 건설과 증축이 모두 차단됨
- [ ] Prepare 아이콘은 네 슬롯 모두 `HorseCycle_0`이며 점 세 개 구형 아이콘이 남아 있지 않음
- [ ] 수량 0 이하 Cargo 행은 로드/저장 및 UI 목록에서 제거됨
- [ ] 보이지 않는 0개 Cargo가 슬롯을 차지하거나 여유 공간이 있는 Wagon 교체를 차단하지 않음
- [ ] Warehouse 수량 Modal Backdrop 클릭 시 수량 Modal만 닫히고 Warehouse Popup과 선택 Caravan은 유지됨
- [ ] Warehouse Caravan 선택 목록은 BaseCamp 저장 레벨을 권위로 사용하여 Lv.1~4에서 1~4개 행을 표시하고, 미생성 행은 `캐러밴 없음`으로 표시함
- [ ] `Bread + 0원`과 `Bread + 유료 구매가`가 동시에 있으면 가격 선택 Modal에 구매가 오름차순으로 모두 표시됨
- [ ] Backdrop/X 닫기, 카드 내부 클릭 유지, NoticeUI 한국어 실패 안내 확인
- [ ] 대상 브랜치 조립 후 Explicit `MainUiPrefab_HasOneWiredEntryAndPopup`, `InGameScene_InheritsBaseCampUiThroughMainUiPrefab` 통과

실패 Popup 문구는 Claim 직전 실제 구성으로 선택한다.

| 장착 구성 | 손실 문구 대상 |
| --- | --- |
| Wagon + Animal | 마차, 동물, 적재 물품 |
| Wagon만 | 마차, 적재 물품 |
| Animal만 | 동물, 적재 물품 |
| 둘 다 없음 | 적재 물품 |

## 5. 기능 완료 판정표

| 기능 묶음 | 완료 조건 | 상태 |
| --- | --- | --- |
| Caravan Runtime Setting | RuntimeBridge 1개, Test Service 0개, Overview Binding 연결 | [ ] |
| Wagon 개체 선택 | 그룹/개체 행, 내구도, 다른 Caravan 사용 제한 정상 | [ ] |
| Animal 선택 | contentId 묶음 수량과 Wagon 허용 타입 제한 정상 | [ ] |
| Transport Inventory 진입 | 목장 블록 클릭으로 Popup 열림 | [ ] |
| Transport Inventory 데이터 | Wagon/Animal/Cargo와 Logs/Stone 아이콘 정상 | [ ] |
| Animal Scroll | 실제 슬롯 수 기반 높이와 최하단 접근 정상 | [ ] |
| 운송 수단 Debug 지급 | Wagon_M 1개, Wagon_S 1개, Horse 2마리 지급. `TransportInventoryRewardDebugButton.prefab` 사용 | [ ] |
| Rename | 별도 아이콘 버튼으로 팝업 열림 | [ ] |
| Treadmill | Display Name 클릭으로 해당 Caravan 표시 | [ ] |
| Journey Icon | Selling 느낌표, Settling/Completed 체크 | [ ] |
| Horse Animation | Traveling에서만 0~3 프레임 반복 | [ ] |
| 실패 정산 손실 | 실패 Claim 저장 성공 시 해당 Caravan의 마차·동물·화물·식량 전량 제거, 예비 자산 유지 | [ ] |
| 실패 정산 롤백 | Claim 저장 실패 시 운송 구성과 소유 인벤토리 모두 복구 | [ ] |
| 실패 손실 Popup | S9 종료 후 실패 결과에만 표시하며 런타임 생성 없이 Scene prefab instance 사용 | [ ] |
| 순차 정산 | Popup 확인 전 다음 Pending을 보류하고, 확인 후 다음 Caravan S8 또는 Town 유지 | [ ] |
| 실패 플래그 소비 | 손실 처리 후 새 마차를 장착·저장해도 재삭제되지 않음 | [ ] |
| BaseCamp 레벨 상한 | 일반 건물 목표 레벨이 BaseCamp 레벨을 넘으면 재료 차감 전에 차단 | [ ] |
| BaseCamp 현황 UI | BaseCamp 포함 건물 레벨과 상한 표시, Backdrop/X 닫기, 정적 Prefab 조립 | [ ] |
| Caravan Slot 해금 | BaseCamp Lv.1~4에 따라 슬롯 1~4 해금, Locked Notice와 Empty 생성 정상 | [ ] |
| EndingItem 해금 | BaseCamp Lv.5에서만 활성, Lv.1 건설 후 추가 건설·증축 차단 | [ ] |
| BaseCamp 저장 복원 | BaseCamp 및 일반 건물 레벨이 재진입 후 동일하게 복원 | [ ] |
| 오두막 최초 지급 | 최초 Lv.1 건설 때만 마차 1/말 2가 내부 보관함에 즉시 생성 | [ ] |
| 오두막 생산/복원 | UTC 주기 생산, 보관 한도 정지, 종료 후 경과분 복원 정상 | [ ] |
| 오두막 받기 | 목장 검증, 개별/모두 받기, 저장 실패 롤백 정상 | [ ] |
| 오두막 UI/Badge | 공용 건물 행 클릭으로 Popup 진입, 양쪽 Full일 때만 느낌표 | [ ] |
| 빵집 생산/복원 | UTC 생산과 오프라인 경과분 복원, 레벨별 보관 한도 정상 | [ ] |
| 빵집 전체/부분 수령 | 창고 검증, Slider 수량, 저장 실패 롤백 정상 | [ ] |
| 빵집 UI/Badge | 빵집 Popup 진입, 최대 보관량일 때만 알림 아이콘 표시 | [ ] |
| Bread 가격 묶음 | 생산 0원/상점 구매가 묶음이 저장·이동·판매 선택까지 분리 유지 | [ ] |
| Warehouse 수량 Backdrop | 수량 Modal만 취소하고 Warehouse와 Caravan 선택은 유지 | [ ] |

## 6. 조립 원칙

- UI 버튼, 아이콘, 팝업 루트는 Prefab에 명시적으로 배치한다.
- BaseCamp 현황 UI는 MainUICanvas Prefab 안에 정적으로 배치하고 런타임 생성하지 않는다.
- BaseCamp 레벨은 `SaveData.player.villageBuildings`를 권위로 하며 Scene 건물 레벨로 건설 가능 여부를 판정하지 않는다.
- BaseCamp 제한은 UI 사전 안내와 실제 `TryStage` 변경 경계에서 재검증한다.
- Caravan Slot 잠금/생성은 `SaveData.player.villageBuildings`의 BaseCamp 레벨과 슬롯 저장 데이터를 재조회하여 판정한다.
- 가격 묶음 권위는 `itemId + purchaseUnitPrice`이며, 비구매 생산품의 `purchaseUnitPrice = 0`도 유효한 묶음으로 취급한다.
- Warehouse 수량 Modal Backdrop은 현재 선택만 취소하며 상위 Warehouse Popup 닫기 이벤트로 전파하지 않는다.
- Scene에만 필요한 Popup과 두 종류의 개발용 지급 버튼은 원본 MainUICanvas Prefab을 수정하지 않고 `InGame.unity`의 활성 MainUICanvas 인스턴스 아래에 정적 Prefab 인스턴스로 배치한다.
- 손실 Popup은 런타임 생성하지 않으며 `ReusableMessagePopup`은 이미 배치된 instance의 내용과 활성 상태만 변경한다.
- 실패 전손은 S8 표시 시점이 아니라 S9 Claim Save transaction 안에서 수행한다.
- 코드가 Rename 버튼이나 상태 아이콘 GameObject를 런타임에 생성하게 만들지 않는다.
- 반복 슬롯은 기존 Pool/재사용 구조를 유지하고 부족한 수량만 생성한다.
- Scene 객체를 참조해야 하는 연결은 Scene override로 둔다.
- Prefab의 외형 작업과 기능 코드 변경이 충돌하면 기능 코드를 유지하고 Inspector 참조만 다시 연결한다.
- 조립이 끝나면 Play Mode뿐 아니라 Prefab Mode를 닫았다 다시 열어 참조가 직렬화되었는지 확인한다.

### 실패 정산의 권위 데이터 흐름

```text
Traveling 치명 실패
→ JourneyRunner.Settle에서 Failed snapshot 생성
→ 실패는 Selling을 건너뛰고 S8
→ S9 Claim 입력
→ Claim 직전 SaveData에서 장착 Wagon/Animal instanceId 확정
→ Economy 적용 및 Reset stage
→ FailedTradeTransportLoss.Apply
→ 장착 Wagon/Animal 소유 인벤토리 제거
→ Caravan wagon/animals/cargo/food/durability/실패 참조 정리
→ matching Pending 제거
→ Save 1회
→ 성공 시 Town 전환 및 실패 손실 Popup
→ Popup 확인 후 다음 Failed Pending S8 또는 Town 유지
```

정합성 규칙:

- S8에서는 손실 snapshot만 확정하고 Cargo/Food 실제 목록을 지우지 않는다.
- 실패 `cargoLost`, `foodLost`는 이미 잃은 양과 남은 전량의 합이다.
- 실제 전손은 S9 Claim의 SaveData snapshot/rollback 범위에서만 수행한다.
- Save 실패 시 Caravan, 소유 인벤토리, Pending, 준비 Commit을 함께 복구한다.
- `caravanId + full tradeId`가 권위 identity이며 `selectedCaravanId`로 Claim 대상을 추측하지 않는다.
- 성공/부분 성공은 운송 구성을 보존하고 전손은 Failed에만 적용한다.
- 실패 후 동일 `contentId`의 예비 자산을 자동 장착하지 않는다.
- 새 구성을 저장한 뒤 과거 실패 플래그로 다시 삭제하지 않는다.

### 재실행과 순차 정산

- 로드 시 `RestorePendingSettlements`로 durable pending cache를 복구한다.
- 복구 성공 뒤 `SettlementUiBridge.ContinuePendingSettlementPresentation()`을 한 번 호출한다.
- 자동 복구 표시는 Failed Pending만 대상으로 하며 성공 Pending은 Selling 흐름이 소유한다.
- 복구를 `Update()` polling으로 구현하지 않는다.
- 실패 Claim은 다음 Pending 표시를 보류하고 Popup 확인 callback에서 진행한다.
- Popup 확인 전에 종료해도 이미 저장 성공한 Claim은 되돌리지 않는다. 다음 실행은 남은 Failed Pending부터 복구한다.

### 현재 알려진 별도 문제

- S8의 `상품 손실 0G`는 Cargo 수량 전손과 별개의 금액 연결 문제다. 이 표시가 0이어도 실제 삭제가 정상일 수 있지만 완료 판정에서는 미해결로 기록한다.
- `WorldMapRenderRootV2.prefab`의 `townData: null`, `iconWorldSize: 1.5` 기본값 기록은 이 조립에 필요하지 않다.

## 7. 문서 역검증 결과

2026-08-07 현재 Prefab/Scene diff와 기능 코드를 다시 대조한 결과:

- MainUICanvas의 기능성 변경인 RenameButton 4개, Display Name 폭/텍스트 설정, Journey Sprite/Animator 필드는 Overview 재조립 문서에 대응되어 있다.
- TradePrepareUI의 기능성 변경인 `WagonSelectPopup.instanceRowPrefab`은 Caravan Set 문서와 총괄 B 단계에 대응되어 있다.
- InGame Scene의 기능성 변경인 RuntimeBridge 교체, 여섯 TradeItem 에셋, TransportInventoryMainUiEntry, Popup, BuildingListPanel, TreadmillPanel Scene 참조는 총괄 D 단계와 Transport Inventory 문서에 대응되어 있다.
- 실패 전손 코드, `TradeFailureLossPopup.prefab`, Scene instance, Adapter 참조, 재실행과 순차 정산은 최신 dev2 실패 전손 문서에 대응되어 있다.
- 문서에서 명시한 `Assets/...` 경로는 모두 실제 파일 존재 여부를 확인했다.
- `renameButton`, `sellingStateIcon`, `journeyStateIconAnimator`, `instanceRowPrefab`, `buildingListPanel`, `popup`, `treadmillPanel` 직렬화 필드가 현재 코드에 존재함을 확인했다.
- Scene 변경을 discard하기 전 기존 UI 계약 테스트 17개와 실패 전손/Popup 집중 테스트 12/12가 통과했다. Scene 변경을 보존하지 않는 브랜치에서는 Scene wiring 테스트를 Explicit로 두며, 최신 dev2에서 조립한 뒤 명시적으로 실행해야 최종 완료다.

PlayMode smoke 실행은 Unity Test Runner가 `0 tests`를 반환했기 때문에 성공 근거로 사용하지 않는다. 재조립 완료 판정에는 반드시 이 문서 E 단계의 수동 PlayMode 검증을 포함한다. 현재 테스트 인프라에서 PlayMode fixture가 실제로 발견되도록 수정되기 전까지는 자동 검증만으로 완료 처리하지 않는다.

이 검증은 실제 Prefab/Scene을 discard하지 않고 `HEAD 조립 상태 + 보존 예정 코드/에셋 + 현재 diff`를 대조한 비파괴 검증이다. 따라서 재조립 직후에는 아래 계약 테스트를 다시 실행해야 한다.

- 최신 dev2의 Scene/Prefab 조립 및 실패 무역 전손 기준: `0807_Dev2_InGame_Reassembly_and_Failed_Trade_Loss.md`
- `CaravanRenamePopupPrefabTests`
- `CaravanSettingSceneContractTests`
- `TransportInventoryPresentationTests`
- `FailedTradeTransportLossTests`
- `TradeArrivalSellingLifecycleTests`
- `JourneyCargoLossCleanupTests`
- `BakeryProductionServiceTests`
- `WarehouseInventoryTransferTests`
- `TradeFailureLossPopupWiringTests`
- PlayMode에서 목장 진입부터 지급, 선택, 출발, Selling/Settling 표시까지의 수동 시나리오
