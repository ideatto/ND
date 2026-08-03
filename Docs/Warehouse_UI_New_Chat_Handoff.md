# Warehouse Inventory 인수인계

## 환경과 범위

- 프로젝트: `C:\Users\ADMIN\ND`
- Scene 정책: Test/InGame Build UI Scene은 이 PR에서 제외하며 Warehouse Prefab과 코드만 유지한다.
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
- 관련 EditMode 10/10

## 다음 작업

1. Caravan 설정 결과가 `CaravanSaveData.wagon`에 저장되면 실제 슬롯 수와 최대 적재량을 확인한다.
2. TradeCycle 적재·구매와 Warehouse가 같은 `caravanId`의 `caravan.cargo`를 공유하는지 확인한다.
3. 구매가 묶음이 적재·이동·저장·재접속 뒤에도 유지되는지 확인한다.
4. Cargo full, overweight, stale Caravan과 Wagon 교체 후 용량 초과 상태를 시각 QA한다.
5. 사용자 지정 Caravan 이름을 표시하되 최종 조회·명령은 계속 `caravanId`를 사용한다.
6. 정상 게임 Scene에서 SharedGameData Tooltip과 Player ↔ Cargo 왕복·저장·재접속 회귀 테스트를 수행한다.
7. merge/completed로 외부 파일이 바뀌면 변경 장부의 계약만 재적용하고 compile error 0과 관련 EditMode 10/10을 다시 확인한다.
## 충돌 처리

- completed 전 변경·복구 장부의 파일 목록을 백업한다.
- 새 completed 코드를 기준으로 외부 계약의 존재를 확인한다.
- 과거 파일 전체를 덮지 말고 빠진 계약만 재적용한다.
- Build UI Scene 변경을 과거 브랜치에서 재적용하지 않고, merge 이후 최신 게임 Scene에 Warehouse Prefab을 연결한다.
- 재적용 뒤 compile error 0과 전송 테스트 6/6을 확인한다.
