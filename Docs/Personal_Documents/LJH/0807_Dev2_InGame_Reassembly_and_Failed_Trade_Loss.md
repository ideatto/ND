# 최신 dev2 InGame UI 재조립 및 실패 무역 전손 가이드

## 1. 목적

최신 `dev2`에서 아래 Scene/Prefab을 버리거나 충돌 해결한 뒤에도 현재 기능을 같은 상태로 복구하기 위한 기준 문서다.

- `MainUICanvas.prefab`: Caravan Slot, Rename, 상태 아이콘과 말 애니메이션
- `TradePrepareUI.prefab`: Wagon 개체 선택 행 연결
- `InGame.unity`: RuntimeBridge, Transport Inventory, Treadmill, 실패 손실 Popup 연결
- 실패 Claim: 해당 Caravan의 장착 마차·동물·Cargo·Food 전손과 순차 정산

슬롯처럼 반복되는 Pool 항목을 제외하고 UI를 런타임 생성하지 않는다. Popup과 버튼은 Prefab 또는 Scene instance로 미리 배치한다.

## 2. 반드시 보존할 파일

### 코드와 테스트

- `Assets/_Project/01.Core/04_TradeLoop/YHY/JourneyRunner.cs`
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/FailedTradeTransportLoss.cs`
- `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/CaravanSaveDataMapper.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs`
- `Assets/_Project/11.CoreServices/Scripts/UI/Settlement/SettlementUiDataAdapter.cs`
- `Assets/_Project/05.UI/04_InGame/YHY/Scripts/Common/ReusableMessagePopup.cs`
- `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/FrameworkTradeScreenPresenter.cs`
- `Assets/_Project/11.CoreServices/Editor/FailedTradeTransportLossTests.cs`
- `Assets/_Project/11.CoreServices/Editor/TradeFailureLossPopupWiringTests.cs`
- 관련 `.meta`

### Prefab과 Scene

- `Assets/_Project/08.Prefabs/MainUICanvas.prefab`
- `Assets/_Project/08.Prefabs/UI/Maps/TradePrepareUI.prefab`
- `Assets/_Project/07.Scenes/04_InGame/InGame.unity`
- `Assets/_Project/08.Prefabs/UI/Trade/TradeFailureLossPopup.prefab`
- `Assets/_Project/08.Prefabs/UI/Trade/TradeFailureLossPopup.prefab.meta`

`TradeFailureLossPopup.prefab`은 현재 브랜치에서 새로 만든 필수 에셋이다. untracked 상태에서 Scene/Prefab을 discard하기 전에 반드시 추적하거나 별도로 보존한다.

## 3. 권장 재조립 순서

1. 코드와 `.meta`를 최신 dev2에 먼저 반영한다.
2. Unity 컴파일 오류가 0건이 될 때까지 기다린다.
3. `TradePrepareUI.prefab`을 조립한다.
4. `MainUICanvas.prefab`을 조립한다.
5. `InGame.unity`에서 Scene 전용 instance와 reference를 연결한다.
6. EditMode 계약 테스트를 실행한다.
7. PlayMode에서 실패 S8 → S9 → Popup → 다음 Pending 흐름을 검증한다.

## 4. TradePrepareUI.prefab

대상은 `TradePrepareUI/S3_Animal`이 사용하는 `WagonSelectPopup`이다.

이 코드/문서 브랜치는 기존 `TradePrepareUI.prefab` 조립 결과를 포함하지 않는다. 최신 dev2에서 아래 참조를 직접 연결하고 Prefab 담당자가 저장하는 것이 정상 절차다.

| 필드 | 연결 |
| --- | --- |
| `listContainer` | Wagon 그룹/개체 행이 들어갈 기존 Content |
| `buttonPrefab` | 기존 그룹 버튼 Template |
| `instanceRowPrefab` | `WagonInstanceRow.prefab`의 `WagonInstanceRowView` |
| `cancelButton` | 기존 취소 버튼 |
| `AnimalInventoryPanel.wagonPopup` | 위 `WagonSelectPopup` instance |

`instanceRowPrefab`이 비면 마차 그룹은 보여도 실제 개체를 선택할 수 없다. Popup은 기본 비활성화한다. 행은 필요한 수량만 만든 뒤 재사용한다.

`tradeScreenPresenter: null`처럼 새 serialized 필드의 기본값만 기록된 줄은 선택적으로 정리할 수 있다. 단, `instanceRowPrefab` 연결까지 버리면 안 된다.

## 5. MainUICanvas.prefab

각 Caravan Slot의 기준 구조:

```text
CaravanSlot
├─ CaravanInfo / DisplayName
├─ RenameButton
│  ├─ Icon
│  └─ Label (빈 문자열, 비활성)
├─ SettingButton
├─ CargoButton
├─ JourneyStateDisplay
│  ├─ Label
│  └─ Icon (Image + Animator)
└─ LockOverlay
```

필수 계약:

- DisplayName 클릭은 Treadmill만 호출한다.
- RenameButton 클릭은 Rename Popup만 호출한다.
- 네 Slot 모두 `renameButton`, 상태 Sprite, 상태 Icon의 Animator를 연결한다.
- Prepare/Traveling은 `HorseCycle_0`, Traveling에서만 `IsTraveling=true`로 0~3 프레임을 반복한다.
- Selling은 느낌표, Settling과 Completed는 체크 아이콘을 사용한다.
- RenameButton, Icon, Popup을 코드가 런타임 생성하지 않는다.
- Scene 전용 Transport Inventory Popup, 실패 손실 Popup, 개발용 지급 버튼은 원본 `MainUICanvas.prefab`에 Apply하지 않는다.

## 6. InGame.unity Scene 조립

### 기존 Scene 전용 연결

- `TestCaravanSettingService`: 0개
- `CaravanSettingRuntimeBridge`: 정확히 1개
- RuntimeBridge `tradeItemAssets`: Apple, Wheat, Cloth, Stover, Logs, Stone
- `TransportInventoryMainUiEntry`: 정확히 1개
- `TransportInventoryPopup.prefab`: 활성 MainUICanvas 아래 1개, 기본 비활성
- `CaravanOverviewPresenter.treadmillPanel`: Scene의 `TreadmillPanel` 연결
- 개발용 지급 버튼: 일반 Popup보다 아래 렌더 순서가 되도록 배치

### 실패 손실 Popup

1. `TradeFailureLossPopup.prefab`을 `InGame.unity`의 활성 MainUICanvas instance 아래에 한 번 배치한다.
2. 이름은 `TradeFailureLossPopup`으로 유지한다.
3. 전체 화면 Stretch를 유지하고 시작 상태는 비활성화한다.
4. 일반 Town/Trade UI보다 뒤 sibling에 두어 활성화될 때 앞에 렌더링되게 한다.
5. Scene에서 사용하는 모든 `SettlementUiDataAdapter.failureLossPopup`에 같은 Scene Popup의 `ReusableMessagePopup`을 연결한다.
6. Button의 Persistent `OnClick`에는 별도 호출을 넣지 않는다. `ReusableMessagePopup`이 listener를 등록·해제한다.
7. Scene reference를 원본 `MainUICanvas.prefab`에 Apply하지 않는다.

Popup Prefab 필수 필드:

| 필드 | 연결 |
| --- | --- |
| `messageText` | 상황 설명 TMP |
| `confirmButton` | 확인 Button |
| `confirmButtonText` | 확인 Button TMP |

## 7. 실패 무역 데이터 흐름

```text
Traveling 중 치명 실패
→ JourneyRunner.Settle
→ Failed 결과 snapshot 저장
→ Selling을 건너뛰고 S8
→ S9 Claim
→ SaveData에서 장착 wagon/animal instanceId snapshot
→ Economy/Reset
→ FailedTradeTransportLoss.Apply
→ 장착 Wagon/Animal 소유 인벤토리 제거
→ Caravan wagon/animals/cargo/food/durability/실패 참조 정리
→ matching Pending 제거
→ Save 1회
→ 성공 시 Town 전환 및 손실 Popup
→ 확인 후 다음 Failed Pending S8 또는 Town 유지
```

저장 경계:

- S8 생성 시 Cargo/Food 실제 목록은 지우지 않는다.
- 실패 S8의 `cargoLost`, `foodLost`에는 이미 잃은 양과 남은 전량을 합산한다.
- 실제 전손은 S9 Claim의 SaveData snapshot/rollback 범위에서 수행한다.
- Save 실패 시 Caravan, Cargo/Food, 소유 Wagon/Animal, Pending을 모두 복구한다.
- Save 성공 후에만 Popup을 표시한다.
- `caravanId + full tradeId`가 권위 identity다.
- 성공/부분 성공은 고정 운송 구성을 보존한다. 전손은 `Failed`에만 적용한다.

Claim 직전 실제 구성에 따른 문구:

| 구성 | 문구 요약 |
| --- | --- |
| 마차 + 동물 | 마차와 동물, 적재 물품을 모두 잃음 |
| 마차만 | 마차와 적재 물품을 모두 잃음 |
| 동물만 | 동물과 적재 물품을 모두 잃음 |
| 둘 다 없음 | 적재 물품을 모두 잃음 |

Wagon content type으로 분기하지 않는다. 새로운 Wagon 종류에도 실제 장착 여부만 사용한다.

## 8. 재실행과 순차 정산

- `RestorePendingSettlements`는 durable cache를 복구한다.
- 복구 성공 뒤 `SettlementUiBridge.ContinuePendingSettlementPresentation()`을 한 번 호출한다.
- 자동 표시는 실패 Pending만 대상으로 한다. 성공 Pending은 Selling 흐름이 소유한다.
- `Update()` polling을 사용하지 않는다.
- 실패 Claim은 `presentNextPendingSettlement=false`로 다음 표시를 보류한다.
- Popup 확인 callback이 다음 Failed Pending 표시를 진행한다.
- Popup 확인 전에 종료해도 성공한 Claim을 되돌리지 않는다. 다음 실행은 남은 Pending부터 복구한다. Popup 확인 대기는 비영속 UX다.

## 9. 테스트

- `FailedTradeTransportLossTests`: 장착 자산만 제거, 예비 자산 보존, 반복 적용, Runtime ID 누락 방어
- `TradeArrivalSellingLifecycleTests`: 실패 Selling 생략, 전손 snapshot, S9 전 Cargo/Food 유지
- `TradeFailureLossPopupWiringTests`: Prefab 참조, 기본 비활성, 모든 Adapter의 Scene Popup 연결
- `FrameworkM1LoopE2EEditorTests`: Claim Save/rollback 회귀

Scene 변경을 discard하기 전 관련 집중 테스트는 12/12 통과했다. Scene을 보존하지 않는 코드/문서 브랜치에서는 `InGameScene_SettlementAdaptersReferencePlacedPopupInstance`를 Explicit로 두며, 최신 dev2에서 Scene 조립을 마친 뒤 해당 테스트를 명시적으로 실행해야 한다.

## 10. PlayMode 최종 체크

- [ ] 마차·동물 장착 및 Cargo/Food 적재 후 실패시킨다.
- [ ] 실패가 Selling 없이 S8로 간다.
- [ ] S8 손실 수량이 실제 잔량을 포함한다.
- [ ] S9 Claim 전에는 Wagon/Animal/Cargo/Food가 존재한다.
- [ ] Claim 저장 성공 후 Town 화면 위에 Popup이 열린다.
- [ ] 문구가 Claim 직전 실제 장착 구성과 맞는다.
- [ ] Popup 확인 전 다음 실패 Pending이 열리지 않는다.
- [ ] 확인 후 다음 실패 Pending S8 또는 Town을 유지한다.
- [ ] 장착 Wagon/Animal/Cargo/Food가 제거된다.
- [ ] 예비 자산과 다른 Caravan 자산은 유지된다.
- [ ] 새 Wagon/Animal을 정상 장착·저장할 수 있다.
- [ ] 재시작 시 같은 자산이 다시 삭제되지 않는다.
- [ ] Missing Script/Reference와 중복 listener 오류가 없다.

## 11. 알려진 별도 항목

S8의 `상품 손실 0G`는 `cargoLost` 수량과 별개의 손실 금액 연결 문제다. 실제 전손이 정상이어도 금액이 0으로 보일 수 있으며 별도 수정·테스트가 필요하다.

`WorldMapRenderRootV2.prefab`의 `townData: null`, `iconWorldSize: 1.5` 기본값 기록은 이 재조립에 필요하지 않다.
