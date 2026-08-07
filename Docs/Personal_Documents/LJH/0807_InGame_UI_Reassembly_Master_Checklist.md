# InGame UI 현재 형태 재조립 총괄 체크리스트

## 1. 사용 목적

이 문서는 `MainUICanvas.prefab`, `TradePrepareUI.prefab`, `InGame.unity`의 작업 내용을 discard한 뒤 현재 기능을 다시 조립할 때 사용하는 최상위 조립 기준이다. 이 문서 한 개만으로 필수 기능과 데이터 흐름을 복구할 수 있어야 하며, 기능별 문서는 외형 수치와 원인 분석을 위한 보충 자료로 사용한다.

## 2. 기능별 문서

| 순서 | 기능 | 상세 문서 |
| ---: | --- | --- |
| 1 | Caravan Setting 런타임 서비스, Wagon 개체 선택, Animal 묶음 표시 | `0806_Caravan_Set_UI_Reassembly.md` |
| 2 | 목장 클릭, Transport Inventory, Logs/Stone 아이콘, 동물 탭 스크롤, 테스트 지급 버튼 | `0806_Transport_Inventory_InGame_Assembly.md` |
| 3 | Caravan Overview, Rename 버튼, Treadmill, 상태 아이콘, 말 애니메이션 | `0807_Caravan_Overview_Current_Reassembly.md` |
| 4 | 최신 dev2 Scene/Prefab 조립, 실패 Claim 전손, 손실 Popup, 순차 정산 | `0807_Dev2_InGame_Reassembly_and_Failed_Trade_Loss.md` |
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
- `FailedTradeTransportLoss.cs`와 `.meta`
- `ReusableMessagePopup.cs`와 상위 `Common.meta`
- `TradeFailureLossPopup.prefab`과 `.meta`
- `FailedTradeTransportLossTests.cs`, `TradeFailureLossPopupWiringTests.cs`와 각 `.meta`

Prefab/Scene을 discard하는 명령에 위 파일을 포함하지 않는다. 특히 untracked 파일은 `git restore`로 복구할 수 없으므로 먼저 백업 또는 추적 상태를 확보한다.

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

### B. TradePrepareUI Prefab 조립

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

### C. MainUICanvas Prefab 조립

- [ ] 네 Caravan Slot의 기존 직렬화 참조 유지
- [ ] Display Name 폭 축소와 Auto Size/Ellipsis 설정
- [ ] 네 슬롯에 RenameButton을 Prefab 오브젝트로 배치
- [ ] 네 RenameButton 아이콘과 `CaravanSlotView.renameButton` 연결
- [ ] 네 JourneyState Icon Image/Animator 연결
- [ ] Prepare/Traveling/Selling/Settling/Completed Sprite 연결
- [ ] `CaravanOverviewRenameBinding`의 Presenter/Popup 연결
- [ ] Transport Inventory Popup과 개발용 지급 버튼은 `MainUICanvas.prefab` 원본에 넣지 않음

각 Slot의 필수 순서와 동작:

```text
DisplayName → RenameButton → SettingButton → CargoButton → JourneyStateDisplay
```

- DisplayName 클릭: Treadmill만 호출
- RenameButton 클릭: Rename Popup만 호출
- RenameButton Label: 빈 문자열, 비활성. Icon은 활성
- Prepare/Traveling: `HorseCycle_0`
- Traveling Animator: `IsTraveling=true`, `HorseCycle_0~3`, Sprite track만 사용
- Selling: 느낌표
- Settling/Completed: 체크
- RenameButton과 상태 Icon은 네 Slot에 미리 배치하며 런타임 생성하지 않음

### D. InGame Scene 조립

- [ ] `CaravanSettingUiConnector` 또는 별도 명확한 Scene 조립 루트 사용
- [ ] `TestCaravanSettingService` 제거
- [ ] `CaravanSettingRuntimeBridge` 정확히 1개 추가
- [ ] Overview Setting/Cargo Binding의 Provider/Command 참조를 RuntimeBridge로 연결
- [ ] RuntimeBridge `tradeItemAssets`에 Apple/Wheat/Cloth/Stover/Logs/Stone 연결
- [ ] `TransportInventoryMainUiEntry` 정확히 1개 추가
- [ ] Entry `buildingListPanel` 연결
- [ ] `InGame.unity`의 활성 MainUICanvas 인스턴스 아래에 `TransportInventoryPopup.prefab`을 한 번 배치하고 기본 비활성화
- [ ] Entry `popup`에 위 Scene Popup 인스턴스의 `TransportInventoryPopupController` 연결
- [ ] 개발용 운송 수단 지급 버튼을 활성 MainUICanvas Scene 인스턴스 좌하단에 한 개만 배치
- [ ] `CaravanOverviewPresenter.treadmillPanel`에 Scene TreadmillPanel 연결
- [ ] 활성 MainUICanvas Scene 인스턴스 아래에 `TradeFailureLossPopup.prefab`을 한 번 배치하고 기본 비활성화
- [ ] Scene에서 사용하는 모든 `SettlementUiDataAdapter.failureLossPopup`에 같은 Popup instance 연결
- [ ] 실패 Popup은 일반 Town/Trade UI보다 뒤 sibling에 두되 Scene 전용 참조를 MainUICanvas 원본에 Apply하지 않음
- [ ] Scene 전용 참조는 Prefab 에셋에 Apply하지 않고 Scene override로 저장

Scene 배치 세부 기준:

- `TransportInventoryPopup`: 활성 MainUICanvas 아래, 기본 비활성
- 개발용 지급 버튼: `InfoPanel` 바로 다음 sibling, 좌하단 `(20,20)`, 크기 `(260,56)`, 모든 Popup/알림보다 아래 렌더 순서
- `TradeFailureLossPopup`: 활성 MainUICanvas 아래, 기본 비활성, 일반 Popup/활동 로그보다 뒤이고 `NoticeUI` 바로 앞 sibling
- 모든 Scene `SettlementUiDataAdapter.failureLossPopup`: 동일한 `TradeFailureLossPopup`의 `ReusableMessagePopup` 참조
- Popup Button Persistent `OnClick`: 별도 Listener를 넣지 않음
- `ReusableMessagePopup`: 배치된 instance의 내용과 활성 상태만 변경

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
- [ ] 코드/Prefab 집중 테스트 통과 후, Scene 조립이 끝난 상태에서 Explicit `InGameScene_SettlementAdaptersReferencePlacedPopupInstance`도 별도 실행하여 통과
- [ ] 실패 S8 → S9 Claim → Town 위 손실 Popup → 확인 → 다음 실패 Pending 또는 Town 순서 확인
- [ ] Claim 저장 실패 시 장착/소유 인벤토리와 Pending이 모두 복구됨

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
| Debug 지급 | Wagon 2종 각 1개와 Horse 2마리 지급. `MainUICanvas/InfoPanel` 다음 sibling, 좌하단 `(20,20)`이며 모든 팝업/알림보다 아래 레이어 | [ ] |
| Rename | 별도 아이콘 버튼으로 팝업 열림 | [ ] |
| Treadmill | Display Name 클릭으로 해당 Caravan 표시 | [ ] |
| Journey Icon | Selling 느낌표, Settling/Completed 체크 | [ ] |
| Horse Animation | Traveling에서만 0~3 프레임 반복 | [ ] |
| 실패 정산 손실 | 실패 Claim 저장 성공 시 해당 Caravan의 마차·동물·화물·식량 전량 제거, 예비 자산 유지 | [ ] |
| 실패 정산 롤백 | Claim 저장 실패 시 운송 구성과 소유 인벤토리 모두 복구 | [ ] |
| 실패 손실 Popup | S9 종료 후 실패 결과에만 표시하며 런타임 생성 없이 Scene prefab instance 사용 | [ ] |
| 순차 정산 | Popup 확인 전 다음 Pending을 보류하고, 확인 후 다음 Caravan S8 또는 Town 유지 | [ ] |
| 실패 플래그 소비 | 손실 처리 후 새 마차를 장착·저장해도 재삭제되지 않음 | [ ] |

## 6. 조립 원칙

- UI 버튼, 아이콘, 팝업 루트는 Prefab에 명시적으로 배치한다.
- Scene에만 필요한 Popup과 개발용 지급 버튼은 원본 MainUICanvas Prefab을 수정하지 않고 `InGame.unity`의 활성 MainUICanvas 인스턴스 아래에 Prefab 인스턴스로 배치한다.
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
- `TradeFailureLossPopupWiringTests`
- PlayMode에서 목장 진입부터 지급, 선택, 출발, Selling/Settling 표시까지의 수동 시나리오
