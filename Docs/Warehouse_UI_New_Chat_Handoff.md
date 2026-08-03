# Warehouse Inventory 인수인계

## 환경과 범위

- 프로젝트: `C:\Users\ADMIN\ND`
- 테스트 Scene: `Assets/_Project/07.Scenes/Test/Build UI.unity`
- UI root: `BuildingPopupPreviewCanvas`
- MainUI 연결은 merge 이후 별도 작업이다.

## 먼저 읽을 문서

1. `Docs/Warehouse_External_Script_Change_Ledger.md`
2. `Docs/Warehouse_Runtime_Connection_2026-08-03.md`
3. `Docs/Warehouse_Inventory_UI_Implementation_Spec.md`
4. `output/pdf/WareHouse_UI_Approved_Revisions.pdf`

문서와 코드가 다르면 현재 코드와 변경·복구 장부를 우선한다. 오래된 WarehouseCapacityData 안은 사용하지 않는다.

## 현재 확정 설계

- Warehouse level은 SaveData의 창고 진행도에서 읽는다.
- 슬롯은 현재 `max(0, level) * 10` 정책이다.
- 별도 WarehouseCapacityData ScriptableObject는 없다.
- Cargo 제한은 wagon의 inventorySlotCount와 maxLoad다.
- Caravan은 slotIndex로 표시하고 caravanId로 조회·명령한다.
- 선택 가능 조건은 BaseCamp + JourneyState.Prepare다.
- slot은 itemId 총수량을 stack size에 맞게 분할한다.
- 이동은 `itemId + purchaseUnitPrice` 가격 묶음을 보존한다.
- 가격 묶음 하나는 Quantity 직행, 여러 개는 PriceGroup 선택 후 Quantity로 이동한다.
- Confirm 시 재검증하고 저장 실패 시 양쪽 inventory를 rollback한다.
- Backdrop은 최상위 Modal만 닫고 item·가격·수량을 초기화한다.
- 입력 guard는 unscaled time 0.15초다.

## 구현 파일

- `WarehouseInventoryPopupController.cs`
- `WarehouseInventoryPresentation.cs`
- `WarehouseInventorySlotView.cs`
- `WarehouseCaravanSlotView.cs`
- `WarehousePriceGroupRowView.cs`
- `WarehouseFunction.cs`
- `WarehouseInventoryTransfer.cs`
- `WarehouseInventoryTransferTests.cs`
- `Assets/_Project/08.Prefabs/UI/Warehouse/*`

## 제거된 테스트 기능

- `WarehouseInventoryDebugFixture.cs`와 Scene component를 제거했다.
- 임시 데이터 준비/Open/Restore 버튼은 더 이상 나타나지 않는다.

## 확인 완료

- Player → Cargo → Player 실제 SaveService 왕복
- 가격 묶음 보존과 저장 실패 rollback
- Prepare가 아닌 Caravan 거부
- Caravan 미선택 click 차단과 hover 유지
- Cargo 제목, 수량 방향 겹침, +99, Modal 초기화
- EditMode 6/6

## 다음 작업

1. NoticeUI에 WarehouseTransferFailure를 연결하고 실패 원인별 문구를 확인한다.
2. 정상 게임 Scene에서 SharedGameData 기반 아이템 이름·설명·아이콘 Tooltip을 확인한다.
3. Cargo full, overweight, stale Caravan, 빠른 연속 입력을 시각 QA한다.
4. 실제 SaveData 저장·불러오기와 재접속 뒤 Warehouse level, Player Inventory, Caravan Cargo 및 가격 묶음 보존을 확인한다.
5. merge 이후 MainUI Warehouse 진입 패널을 연결한다.
   - level 0은 숨기고 level 1 이상만 표시한다.
   - 클릭 시 `WarehouseInventoryPopupController.TryOpen()`을 호출한다.
   - 열기 직전 최신 SaveData로 Warehouse와 BaseCamp 조건을 다시 검증한다.
6. MainUI 연결 후 정상 게임 Scene에서 진입 → Caravan 선택 → Player ↔ Cargo 전송 → 저장 → 재접속 PlayMode 회귀 테스트를 수행한다.
7. merge/completed로 외부 파일이 바뀌었다면 변경 장부를 기준으로 누락된 계약만 재적용하고 compile error 0과 EditMode 6/6을 다시 확인한다.
## 충돌 처리

- completed 전 변경·복구 장부의 파일 목록을 백업한다.
- 새 completed 코드를 기준으로 외부 계약의 존재를 확인한다.
- 과거 파일 전체를 덮지 말고 빠진 계약만 재적용한다.
- Build UI Scene 전체 revert를 하지 않는다.
- 재적용 뒤 compile error 0과 전송 테스트 6/6을 확인한다.
