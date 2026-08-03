# Warehouse Inventory 변경·복구 장부

- 기준일: 2026-08-03
- 범위: Player Home Inventory ↔ BaseCamp Prepare Caravan Cargo
- 목적: 외부 completed/merge 충돌 시 Warehouse 변경을 백업하고 새 기준 코드에 필요한 계약만 재적용하기 위한 단일 장부
- MainUI 연결은 merge 이후 별도 작업으로 제외한다.

## 복구 원칙

1. completed 담당자가 같은 파일을 수정했다면 새 구현을 기준으로 아래 계약의 존재 여부부터 비교한다.
2. 동일 계약이 있으면 과거 hunk를 버리고, 없으면 파일 전체가 아닌 해당 필드·이벤트·검증만 새 구조에 맞게 재적용한다.
3. 가격 묶음은 별도 lot ID가 아니라 `itemId + purchaseUnitPrice` 파생 키다.
4. `Build UI.unity`에는 Warehouse 이전부터 다른 dirty 변경이 있었다. Scene 전체 revert를 금지하고 Warehouse object만 선별한다.

## 외부·공용 코드 변경

### SaveData와 runtime 왕복

- `SaveData.cs`: `TradeItemSaveData.purchaseUnitPrice : long` 추가. `basePrice`는 catalog 기본가, purchaseUnitPrice는 실제 매입 단가다.
- `TradeDataDraft.cs`: runtime 임시 item에도 매입 단가를 보존한다.
- `CaravanSaveDataMapper.cs`: SaveData ↔ runtime 변환에서 단가를 양방향 복사한다.
- `CaravanBuildingConstructionCommand.cs`: rollback clone에서도 단가를 보존한다.

재적용 기준: Save → runtime → Save와 rollback 뒤에도 서로 다른 가격 묶음이 합쳐지지 않아야 한다.

### 공용 데이터와 이벤트

- `SharedGameDataView.cs`, `SharedGameDataService.cs`: `SharedTradeItemDefinition.Description`을 provider로 전달한다.
- `FrameworkEvents.cs`: `HomeInventoryChanged`와 `RaiseHomeInventoryChanged()` 계약을 추가했다.

재적용 기준: Tooltip이 공용 provider에서 설명을 얻고, 저장 성공 후 구독 UI가 최신 SaveData를 다시 읽어야 한다.

### 시장 가격 묶음

- 시장 매수 확정 단가를 purchaseUnitPrice로 저장한다.
- 같은 `itemId + purchaseUnitPrice`만 증가시키고 다른 가격은 별도 묶음으로 남긴다.
- 부분 매도는 필요한 수량만 차감하고 남은 묶음을 임의 병합하지 않는다.
- 시장 손익/FIFO·LIFO 정책 자체는 변경하지 않았다.

completed가 시장 코드를 교체했다면 새 코드를 유지하고 “서로 다른 매입 단가 보존” 계약만 재검증한다.

## Warehouse 내부 코드 변경

### 상태·전송

- `WarehouseFunction.cs`: SaveData의 창고 최고 level을 읽고 현재 `level * 10` 슬롯을 계산한다. 별도 WarehouseCapacityData는 사용하지 않는다.
- `WarehouseInventoryTransfer.cs`: 가격 resolver, slot/weight capacity, request, 실패 코드, 원자적 이동·저장·rollback을 담당한다.
- `WarehouseInventoryTransferTests.cs`: 가격 묶음, 시장 연동, non-Prepare 거부, 저장 실패 rollback을 검증한다.

### UI

- `WarehouseInventoryPresentation.cs`: 캐시 없는 ViewData builder와 PriceGroup/Quantity/Busy presenter.
- `WarehouseInventoryPopupController.cs`: TryOpen, caravanId 선택, 최신 SaveData refresh, Tooltip, 가격·수량 선택, 전송 요청, 0.15초 guard.
  - Caravan 미선택 시 Player slot click만 차단하고 hover는 허용한다.
  - 선택 slot index로 Cargo 제목을 갱신한다.
  - +99는 현재값에서 99 증가한다.
  - Modal backdrop은 Caravan은 유지하고 item·가격·수량만 초기화한다.
- `WarehouseInventorySlotView.cs`, `WarehouseCaravanSlotView.cs`, `WarehousePriceGroupRowView.cs`: render-only 값과 callback만 보관한다.

### Prefab·Scene

- `WarehouseUiPrefabBuilder.cs`: rebuild 시 PriceGroup row view와 Quantity Modal 연결을 유지한다.
- `WarehouseInventoryPopup.prefab`: controller, SelectionModalLayer, PriceGroup, Quantity, Tooltip과 runtime prefab 참조를 연결했다.
- `WarehouseInventorySlot.prefab`, `WarehouseCaravanSlot.prefab`, `WarehousePriceGroupRow.prefab`: 전용 View component 연결.
- `Build UI.unity`: Warehouse Popup preview 배치. 다른 dirty 변경과 섞여 있으므로 선별 병합한다.

## 제거한 디버그 기능

- 삭제: `WarehouseInventoryDebugFixture.cs`와 meta
- 제거: `BuildingPopupPreviewCanvas`의 fixture component
- 이유: UI·양방향 전송 검증이 끝났고 fixture가 실제 SaveData에 임시 데이터를 저장할 수 있어 제품 코드에 남기지 않는다.
- 제거 전 결과: EditMode 6/6, 실제 SaveService H→C→H 왕복, controller 경로 H→C→H 왕복, snapshot restore 모두 통과.

## 남은 기능

- NoticeUI와 WarehouseTransferFailure 연결
- 정상 게임 Scene의 SharedGameData Tooltip 확인
- full/overweight/stale state/연속 입력 시각 QA
- 실제 SaveData 저장 후 재접속·불러오기에서도 Warehouse level, Player Inventory, Caravan Cargo와 가격 묶음이 유지되는지 검증
- merge 이후 MainUI Warehouse 진입 패널을 연결한다.
  - Warehouse level 0이면 패널을 표시하지 않는다.
  - Warehouse level 1 이상이면 패널을 표시한다.
  - 버튼은 `WarehouseInventoryPopupController.TryOpen()`을 호출하며, 진입 시점의 최신 SaveData로 조건을 다시 검증한다.
- MainUI 연결 후 정상 게임 Scene에서 진입 → Caravan 선택 → 양방향 전송 → 저장 → 재접속까지 PlayMode 회귀 검증
- merge/completed 과정에서 아래 외부 파일이 바뀌었다면 과거 파일을 덮지 않고 이 장부의 계약만 새 코드에 재적용
## 이번 작업과 무관한 기존 dirty

- `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`
- `Assets/_Project/01.Core/07_Village/YHY/BuildingPlacementController.cs`
- `output/`, `tmp/`

위 항목은 Warehouse 정리 과정에서 되돌리지 않는다.

## 수정한 문서 목록

- `Docs/Warehouse_External_Script_Change_Ledger.md`: 외부·내부·Prefab·Scene·삭제와 복구 절차를 통합했다.
- `Docs/Warehouse_Runtime_Connection_2026-08-03.md`: 현재 데이터 흐름, 상태 전이, 검증, debug 제거를 반영했다.
- `Docs/Warehouse_UI_New_Chat_Handoff.md`: 폐기된 capacity asset 안을 제거하고 현재 정책과 남은 작업을 반영했다.

## completed 발생 시 절차

1. 이 문서와 위 코드/Prefab의 현재 diff를 patch 또는 복사본으로 백업한다.
2. completed가 수정한 외부 파일은 새 기준으로 돌리되 Warehouse 신규 파일은 유지한다.
3. 외부 계약이 새 코드에 이미 있는지 확인하고 빠진 계약만 재작성한다.
4. controller/presenter/view/prefab 연결을 다시 적용한다.
5. Build UI Scene은 전체 revert하지 않고 Warehouse override만 재적용한다.
6. compile error 0, 전송 테스트 6/6, 가격 묶음 왕복·rollback을 재검증한다.

## 최종 체크리스트

- [ ] 다른 매입 단가가 별도 묶음으로 남는가
- [ ] 같은 단가 이동은 해당 묶음만 증감하는가
- [ ] 물리 슬롯은 itemId 총수량과 stack size로 계산되는가
- [ ] Player/Caravan BaseCamp 및 Caravan Prepare일 때만 이동하는가
- [ ] save 실패 시 양쪽 inventory가 rollback되는가
- [ ] rebuild 뒤 Quantity Modal과 View component가 유지되는가
- [ ] Scene의 다른 담당자 변경을 덮어쓰지 않았는가
- [ ] debug fixture가 코드와 Scene에 남지 않았는가
