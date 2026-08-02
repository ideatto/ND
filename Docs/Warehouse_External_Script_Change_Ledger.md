# Warehouse Inventory - External Script Change Ledger

- 기록일: 2026-08-03
- 범위: Player Inventory <-> selected Caravan Cargo 및 Market Cargo 가격 묶음
- 목적: Warehouse 소유 범위 밖의 공용 파일 변경을 추적하고 다른 담당자의 completed 결과를 덮어쓰지 않기 위함

## 병합 원칙

1. 아래 파일 담당자가 동일 영역을 completed 처리했다면 해당 hunk를 먼저 제외한다.
2. 제외 후에도 각 항목의 의미 계약이 최신 코드에 존재하는지 확인한다.
3. 계약이 없으면 예전 코드를 통째로 복원하지 않고 최신 구조에 맞춰 의미만 재적용한다.
4. 가격 그룹은 별도 영속 ID가 아닌 `itemId + purchaseUnitPrice` 파생 키다.

## 외부/공용 스크립트 변경 목록

### SaveData.cs

- `TradeItemSaveData.purchaseUnitPrice : long` 추가
- `basePrice`는 카탈로그 기본가, `purchaseUnitPrice`는 실제 매입 단가다.
- 비구매 획득품과 구버전 저장 데이터의 기본값은 0이다.
- 동등한 원가 필드가 다른 작업에서 추가되면 이 hunk를 제외하고 Warehouse 코드를 그 필드에 맞춘다.

### FrameworkEvents.cs

- `HomeInventoryChanged`, `RaiseHomeInventoryChanged()` 추가
- 저장 성공 뒤에만 발생하는 재조회 신호이며 인벤토리 payload를 전달하지 않는다.

### SharedGameDataView.cs / SharedGameDataService.cs

- `SharedTradeItemDefinition.Description` 추가
- `TradeItemData.Description`을 공용 정의에 복사
- Tooltip이 원본 SO에 직접 의존하지 않게 하기 위한 변경이다.

### TradeDataDraft.cs / CaravanSaveDataMapper.cs

- `imsiTradeItemData.purchaseUnitPrice` 추가
- SaveData <-> runtime 양방향 변환에서 구매가 복사
- Save -> Runtime -> Save 왕복 후 가격 묶음이 유지되어야 한다.

### CaravanBuildingConstructionCommand.cs

- 건설 실패 rollback용 `CloneItem`에서 구매가 복사
- Cargo 복구 과정에서 가격 그룹이 0원 그룹으로 바뀌는 것을 방지한다.

### MarketInventoryIntegration.cs

- 위험도: 매우 높음 — Warehouse 외부 시장 매수·매도 흐름
- 변경:
  - 매수 시 현재 `MarketStockSaveData.unitPrice`를 `purchaseUnitPrice`로 전달한다.
  - 동일 `itemId + purchaseUnitPrice` 묶음에만 매수 수량을 더한다.
  - 다른 구매가면 새 Cargo 행을 만들고 `basePrice`는 카탈로그 기본가로 유지한다.
  - 매도 시 같은 itemId의 저장된 묶음 순서대로 필요한 수량만 차감한다.
  - 완전히 소진된 묶음만 삭제하고 남은 가격 묶음은 병합하지 않는다.
  - rollback clone에서 `purchaseUnitPrice`를 보존한다.
- 변경하지 않은 것:
  - 판매 단가 및 판매 수익 계산
  - 평균원가/FIFO/LIFO 손익 정책
  - Market UI의 가격 묶음 선택 기능
  - 물리 슬롯의 itemId 합산 기준
- 저장 순서는 획득 시각 계약이 아니므로 매도 차감 규칙을 회계상 FIFO라고 부르지 않는다.
- 시장 담당자가 매수 원가/lot 모델을 completed 처리하면 이 코드를 덮어쓰지 말고, 서로 다른 매입 단가와 부분 판매 후 잔여 묶음 보존 여부만 재검증한다.

## Warehouse 소유 신규 스크립트

- Assets/_Project/11.CoreServices/Scripts/Save/WarehouseInventoryTransfer.cs
- Assets/_Project/05.UI/04_InGame/YHY/Scripts/Warehouse/WarehouseInventoryPresentation.cs
- Assets/_Project/11.CoreServices/Editor/WarehouseInventoryTransferTests.cs

## 의도적으로 제외한 범위

- 별도 영속 `lotId`/`priceGroupId`
- 시장 판매 FIFO/LIFO 손익 정책
- Market UI의 가격 묶음 선택
- Warehouse Prefab과 Presenter 최종 runtime 바인딩
- Warehouse 레벨별 Capacity SO

## 이번 작업과 무관한 Working Tree 변경

- Assets/_Project/01.Core/07_Village/YHY/BuildingPlacementController.cs
- Assets/_Project/07.Scenes/Test/Build UI.unity
- Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset
- Docs/Warehouse_UI_New_Chat_Handoff.md

## 병합 전 확인표

- [ ] 시장에서 서로 다른 단가로 매수하면 별도 가격 묶음이 생기는가
- [ ] 같은 단가 재매수는 기존 묶음에 합쳐지는가
- [ ] 부분 판매 후 미판매 가격 묶음이 유지되는가
- [ ] SaveData/runtime 왕복과 rollback 뒤 구매가가 유지되는가
- [ ] Warehouse 이동이 선택한 복합 키 그룹만 차감하는가
- [ ] 외부 파일 담당자의 completed 변경을 덮어쓰지 않았는가
