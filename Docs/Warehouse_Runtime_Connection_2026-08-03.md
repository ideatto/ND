# Warehouse Inventory Runtime 연결 상태

- 기준일: 2026-08-03
- 범위: Player Home Inventory ↔ BaseCamp Prepare Caravan Cargo
- MainUI 연결: merge 이후 별도 작업

## 권위 데이터

- Warehouse level: `SaveData.player.villageBuildings`
- Warehouse slot: `WarehouseFunction.GetSlotCount(level)`, 현재 level당 10칸
- Caravan 목록: `SaveData.caravans`
- Caravan identity: 배열 index가 아닌 `caravanId`
- Cargo 제한: `wagon.inventorySlotCount`와 `wagon.maxLoad`
- 가격 묶음: `itemId + purchaseUnitPrice`

## UI 흐름

1. 외부 진입점이 `WarehouseInventoryPopupController.TryOpen()`을 호출한다.
2. Warehouse Lv.1 이상과 Player BaseCamp 위치를 확인한다.
3. 왼쪽 Player Inventory, 오른쪽 Caravan 선택 화면을 표시한다.
4. BaseCamp·Prepare Caravan을 선택하면 Cargo로 전환한다.
5. Caravan 미선택 상태에서는 Player slot hover만 허용하고 click은 차단한다.
6. hover는 공용 catalog 이름·기본가·설명 Tooltip을 표시한다.
7. click은 source의 가격 묶음을 조회한다.
8. 묶음 하나는 Quantity 직행, 둘 이상은 PriceGroup을 먼저 표시한다.
9. 최대 이동량은 source 수량과 목적지 slot/weight capacity 중 최소값이다.
10. Confirm은 최신 SaveData를 재검증하고 이동·저장한다.
11. 성공 후에만 UI를 refresh하며 저장 실패는 양쪽 inventory를 rollback한다.

## Modal과 입력

- PriceGroup, Quantity, Busy는 상호 배타적인 presenter 상태다.
- Modal backdrop은 현재 Modal만 닫고 Caravan 선택은 유지한다.
- 취소 시 item, 가격 묶음, 선택 수량, 최대 수량을 초기화한다.
- +99는 현재 수량에서 99 증가하고 최대 이동량에서 clamp한다.
- 0.15초 unscaled guard로 같은 pointer sequence와 연속 입력을 막는다.

## 데이터 흐름

```text
SaveData / Framework event
        ↓
WarehouseInventoryPopupController
        ↓
ViewData Builder / Presenter
        ↓
Slot / Caravan / Price row View

Confirm intent
        ↓
WarehouseTransferRequest
        ↓
WarehouseInventoryTransferService
        ↓
Save success
        ↓
HomeInventoryChanged / CaravanCargoChanged
        ↓
최신 SaveData로 UI 재구성
```

## 디버그 기능 제거

- 검증용 `WarehouseInventoryDebugFixture`는 코드와 Build UI Scene에서 제거했다.
- 제품 Scene에 임시 Warehouse, Caravan, item을 주입하는 기능은 남아 있지 않다.
- 제거 전 실제 SaveService와 controller 경로로 양방향 왕복을 검증하고 snapshot을 복구했다.

## 검증 상태

- EditMode 전송 테스트: 6/6
- H→C 및 C→H 가격 묶음 왕복: 통과
- 저장 실패 양쪽 rollback: 통과
- non-Prepare Caravan mutation 차단: 통과
- Modal 초기화, Cargo 제목, +99, hover/click 분리: PlayMode 확인

## 남은 작업

- `WarehouseTransferFailure`를 NoticeUI에 연결하고 실패 원인별 사용자 문구를 확인한다.
- 정상 게임 진입에서 SharedGameData의 아이템 이름·설명·아이콘 Tooltip을 확인한다.
- full/overweight/stale state 및 빠른 연속 입력의 실패 UI를 시각 QA한다.
- 실제 SaveData 저장·불러오기와 재접속 뒤에도 Warehouse level, 양쪽 Inventory와 `itemId + purchaseUnitPrice` 묶음이 유지되는지 확인한다.
- merge 이후 MainUI Warehouse 진입 패널을 연결한다.
  - Warehouse level 0: 패널 숨김
  - Warehouse level 1 이상: 패널 표시
  - 클릭: `WarehouseInventoryPopupController.TryOpen()` 호출
  - 열기 직전 최신 SaveData로 Warehouse/BaseCamp 조건 재검증
- MainUI 연결 후 정상 게임 Scene에서 진입, Caravan 선택, Player ↔ Cargo 전송, 저장, 재접속을 포함한 PlayMode 회귀 테스트를 수행한다.
- merge 중 외부 계약 파일이 변경되면 `Warehouse_External_Script_Change_Ledger.md`를 기준으로 누락된 계약만 재적용하고 컴파일 및 EditMode 6/6을 다시 확인한다.

파일별 복구 정보는 `Warehouse_External_Script_Change_Ledger.md`를 기준으로 한다.
