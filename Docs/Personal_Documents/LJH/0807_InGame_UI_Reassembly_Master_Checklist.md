# InGame UI 현재 형태 재조립 총괄 체크리스트

## 1. 사용 목적

이 문서는 `MainUICanvas.prefab`, `TradePrepareUI.prefab`, `InGame.unity`의 작업 내용을 discard한 뒤 현재 기능을 다시 조립할 때 사용하는 총괄 순서표다. 세부 수치와 필드 연결은 기능별 문서를 따른다.

## 2. 기능별 문서

| 순서 | 기능 | 상세 문서 |
| ---: | --- | --- |
| 1 | Caravan Setting 런타임 서비스, Wagon 개체 선택, Animal 묶음 표시 | `0806_Caravan_Set_UI_Reassembly.md` |
| 2 | 목장 클릭, Transport Inventory, Logs/Stone 아이콘, 동물 탭 스크롤, 테스트 지급 버튼 | `0806_Transport_Inventory_InGame_Assembly.md` |
| 3 | Caravan Overview, Rename 버튼, Treadmill, 상태 아이콘, 말 애니메이션 | `0807_Caravan_Overview_Current_Reassembly.md` |
| 참고 | 기존 Overview/Treadmill 요구사항과 Scene 참조 배경 | `0805_Caravan_Overview_Treadmill_Binding_Request.md` |
| 참고 | Transport Inventory 원인 및 수정 근거 | `0806_Transport_Inventory_PlayMode_Issue_Report.md` |

## 3. discard 범위와 보존 범위

### discard 예정 조립 파일

- `Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab`
- `Assets/_Project/08.Prefabs/UI/Maps/TradePrepareUI.prefab`
- `Assets/_Project/07.Scenes/04_InGame/InGame.unity`

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

Prefab/Scene을 discard하는 명령에 위 파일을 포함하지 않는다. 특히 untracked 파일은 `git restore`로 복구할 수 없으므로 먼저 백업 또는 추적 상태를 확보한다.

## 4. 권장 재조립 순서

### A. 코드와 에셋 준비

- [ ] Unity 컴파일 오류 0건 확인
- [ ] `TransportInventoryMainUiEntry` 타입이 Inspector Add Component에 노출됨
- [ ] `CaravanSlotView`에 `renameButton`, `sellingStateIcon` 필드가 보임
- [ ] `WagonSelectPopup`에 `instanceRowPrefab` 필드가 보임
- [ ] HorseCycle 12개 Sprite와 Traveling Animation Clip이 존재함
- [ ] `JourneyStateDisplay.controller`에 `IsTraveling` Bool이 존재함

### B. TradePrepareUI Prefab 조립

- [ ] `WagonSelectPopup.buttonPrefab` 연결
- [ ] `WagonSelectPopup.instanceRowPrefab`에 `WagonInstanceRow.prefab` 연결
- [ ] `AnimalInventoryPanel.wagonPopup` 연결
- [ ] Animal Content/Viewport/ScrollRect 연결 및 실제 슬롯 수 기반 Content 높이 로직 확인
- [ ] Popup은 기본 비활성화

### C. MainUICanvas Prefab 조립

- [ ] 네 Caravan Slot의 기존 직렬화 참조 유지
- [ ] Display Name 폭 축소와 Auto Size/Ellipsis 설정
- [ ] 네 슬롯에 RenameButton을 Prefab 오브젝트로 배치
- [ ] 네 RenameButton 아이콘과 `CaravanSlotView.renameButton` 연결
- [ ] 네 JourneyState Icon Image/Animator 연결
- [ ] Prepare/Traveling/Selling/Settling/Completed Sprite 연결
- [ ] `CaravanOverviewRenameBinding`의 Presenter/Popup 연결
- [ ] `TransportInventoryPopup.prefab`을 활성 MainUICanvas 아래 배치하고 기본 비활성화
- [ ] 개발용 운송 수단 지급 버튼이 필요하면 MainUICanvas 좌하단에 한 개만 배치

### D. InGame Scene 조립

- [ ] `CaravanSettingUiConnector` 또는 별도 명확한 Scene 조립 루트 사용
- [ ] `TestCaravanSettingService` 제거
- [ ] `CaravanSettingRuntimeBridge` 정확히 1개 추가
- [ ] Overview Setting/Cargo Binding의 Provider/Command 참조를 RuntimeBridge로 연결
- [ ] RuntimeBridge `tradeItemAssets`에 Apple/Wheat/Cloth/Stover/Logs/Stone 연결
- [ ] `TransportInventoryMainUiEntry` 정확히 1개 추가
- [ ] Entry `buildingListPanel` 연결
- [ ] Entry `popup`에 활성 MainUICanvas의 Transport Inventory Popup 연결
- [ ] `CaravanOverviewPresenter.treadmillPanel`에 Scene TreadmillPanel 연결
- [ ] Scene 전용 참조는 Prefab 에셋에 Apply하지 않고 Scene override로 저장

### E. 계약 테스트와 수동 검증

- [ ] Scene 계약 테스트에서 RuntimeBridge 1개, Test Service 0개
- [ ] Entry 1개, Popup 1개, 개발용 지급 버튼 1개 이하
- [ ] 목장 Lv.1 생성 후 목장 블록 클릭 시 Transport Inventory가 열림
- [ ] Wagon_M 1, Wagon_S 1, Horse 2 지급 후 목록에 표시됨
- [ ] Logs/Stone 아이콘과 이름, 수량, 중량이 표시됨
- [ ] 동물 슬롯이 늘어나도 스크롤이 잠금 영역까지 내려감
- [ ] Wagon 종류 그룹과 개체별 내구도가 올바르게 표시됨
- [ ] 다른 Caravan이 사용 중인 Wagon 개체는 선택 불가
- [ ] 이름 클릭/Treadmill과 Rename 버튼 동작이 분리됨
- [ ] Prepare/Traveling/Selling/Settling/Completed 아이콘이 상태표와 일치함
- [ ] Missing Script, Missing Reference, 중복 이벤트 등록 오류가 없음

## 5. 기능 완료 판정표

| 기능 묶음 | 완료 조건 | 상태 |
| --- | --- | --- |
| Caravan Runtime Setting | RuntimeBridge 1개, Test Service 0개, Overview Binding 연결 | [ ] |
| Wagon 개체 선택 | 그룹/개체 행, 내구도, 다른 Caravan 사용 제한 정상 | [ ] |
| Animal 선택 | contentId 묶음 수량과 Wagon 허용 타입 제한 정상 | [ ] |
| Transport Inventory 진입 | 목장 블록 클릭으로 Popup 열림 | [ ] |
| Transport Inventory 데이터 | Wagon/Animal/Cargo와 Logs/Stone 아이콘 정상 | [ ] |
| Animal Scroll | 실제 슬롯 수 기반 높이와 최하단 접근 정상 | [ ] |
| Debug 지급 | Wagon 2종 각 1개와 Horse 2마리 지급 | [ ] |
| Rename | 별도 아이콘 버튼으로 팝업 열림 | [ ] |
| Treadmill | Display Name 클릭으로 해당 Caravan 표시 | [ ] |
| Journey Icon | Selling 느낌표, Settling/Completed 체크 | [ ] |
| Horse Animation | Traveling에서만 0~3 프레임 반복 | [ ] |

## 6. 조립 원칙

- UI 버튼, 아이콘, 팝업 루트는 Prefab에 명시적으로 배치한다.
- 코드가 Rename 버튼이나 상태 아이콘 GameObject를 런타임에 생성하게 만들지 않는다.
- 반복 슬롯은 기존 Pool/재사용 구조를 유지하고 부족한 수량만 생성한다.
- Scene 객체를 참조해야 하는 연결은 Scene override로 둔다.
- Prefab의 외형 작업과 기능 코드 변경이 충돌하면 기능 코드를 유지하고 Inspector 참조만 다시 연결한다.
- 조립이 끝나면 Play Mode뿐 아니라 Prefab Mode를 닫았다 다시 열어 참조가 직렬화되었는지 확인한다.

## 7. 문서 역검증 결과

2026-08-07 현재 Prefab/Scene diff와 기능 코드를 다시 대조한 결과:

- MainUICanvas의 기능성 변경인 RenameButton 4개, Display Name 폭/텍스트 설정, Journey Sprite/Animator 필드는 Overview 재조립 문서에 대응되어 있다.
- TradePrepareUI의 기능성 변경인 `WagonSelectPopup.instanceRowPrefab`은 Caravan Set 문서와 총괄 B 단계에 대응되어 있다.
- InGame Scene의 기능성 변경인 RuntimeBridge 교체, 여섯 TradeItem 에셋, TransportInventoryMainUiEntry, Popup, BuildingListPanel, TreadmillPanel Scene 참조는 총괄 D 단계와 Transport Inventory 문서에 대응되어 있다.
- 문서에서 명시한 `Assets/...` 경로는 모두 실제 파일 존재 여부를 확인했다.
- `renameButton`, `sellingStateIcon`, `journeyStateIconAnimator`, `instanceRowPrefab`, `buildingListPanel`, `popup`, `treadmillPanel` 직렬화 필드가 현재 코드에 존재함을 확인했다.
- 관련 EditMode 계약 테스트 17개가 통과했다.

PlayMode smoke 실행은 Unity Test Runner가 `0 tests`를 반환했기 때문에 성공 근거로 사용하지 않는다. 재조립 완료 판정에는 반드시 이 문서 E 단계의 수동 PlayMode 검증을 포함한다. 현재 테스트 인프라에서 PlayMode fixture가 실제로 발견되도록 수정되기 전까지는 자동 검증만으로 완료 처리하지 않는다.

이 검증은 실제 Prefab/Scene을 discard하지 않고 `HEAD 조립 상태 + 보존 예정 코드/에셋 + 현재 diff`를 대조한 비파괴 검증이다. 따라서 재조립 직후에는 아래 계약 테스트를 다시 실행해야 한다.

- `CaravanRenamePopupPrefabTests`
- `CaravanSettingSceneContractTests`
- `TransportInventoryPresentationTests`
- PlayMode에서 목장 진입부터 지급, 선택, 출발, Selling/Settling 표시까지의 수동 시나리오
