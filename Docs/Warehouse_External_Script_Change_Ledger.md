# Warehouse Inventory - External Script Change Ledger

- 기록일: 2026-08-03
- 작업 범위: Player Inventory <-> selected Caravan Cargo 양방향 이동
- 목적: Warehouse 소유 범위 밖의 공용 파일 변경을 추적하고, 다른 담당자의 completed 결과와 충돌할 때 덮어쓰지 않기 위함

## 병합 원칙

1. 아래 파일 담당자가 동일 영역을 completed 처리했다면 해당 hunk는 먼저 제외한다.
2. 제외한 뒤에도 각 항목의 `유지해야 할 의미 계약`이 최신 코드에 존재하는지 확인한다.
3. 계약이 없다면 기존 코드를 통째로 복원하지 않고 담당자의 최신 구조에 맞춰 의미만 재적용한다.
4. `purchaseUnitPrice`의 직렬화 이름이나 저장 위치가 바뀌면 Warehouse Resolver/Transfer와 모든 복제 경로를 함께 변경한다.
5. 별도 영속 lot ID는 이번 구현에 추가하지 않았다. 현재 가격 그룹 식별자는 파생 키 `itemId + purchaseUnitPrice`다.

## 외부/공용 스크립트 변경 목록

### 1. Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs

- 위험도: 높음 — 공용 JSON 저장 계약
- 변경: `TradeItemSaveData.purchaseUnitPrice : long` 추가
- 유지해야 할 의미 계약:
  - `basePrice`는 카탈로그 기본가, `purchaseUnitPrice`는 실제 매입 단가다.
  - 비구매 획득품과 구버전 저장 데이터의 기본값은 0이다.
- completed 충돌 시: 동등 필드가 있다면 이 hunk를 제외하고 Warehouse 코드를 그 필드에 맞춘다.

### 2. Assets/_Project/11.CoreServices/Scripts/Events/FrameworkEvents.cs

- 위험도: 중간 — 전역 이벤트 계약
- 변경: `HomeInventoryChanged`, `RaiseHomeInventoryChanged()` 추가
- 유지해야 할 의미 계약: 저장 성공 뒤에만 발생하며 데이터 payload가 아닌 재조회 신호다.
- completed 충돌 시: 동일 목적 이벤트가 있다면 이 hunk를 제외하고 Transfer Service 호출부만 변경한다.

### 3. Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataView.cs

- 위험도: 중간 — 공용 카탈로그 View
- 변경: `SharedTradeItemDefinition.Description` 추가
- 유지해야 할 의미 계약: Tooltip은 원본 SO가 아니라 `ISharedGameDataProvider`에서 설명을 읽는다.

### 4. Assets/_Project/11.CoreServices/Scripts/Data/SharedGameDataService.cs

- 위험도: 중간 — 공용 데이터 변환
- 변경: `TradeItemData.Description`을 공용 정의에 복사
- completed 충돌 시: 로컬라이징 키/별도 설명 provider가 생기면 두 Description hunk를 제외하고 ViewData Builder를 최신 계약에 맞춘다.

### 5. Assets/_Project/01.Core/04_TradeLoop/YHY/TradeDataDraft.cs

- 위험도: 높음 — 공용 runtime 화물 모델
- 변경: `imsiTradeItemData.purchaseUnitPrice` 추가
- 이유: SaveData를 runtime CaravanData로 변환했다가 다시 저장할 때 구매가가 사라지지 않게 한다.
- completed 충돌 시: runtime 모델 담당자가 별도 lot/원가 모델을 완료했다면 이 필드를 제외하고 Mapper를 해당 모델로 연결한다.

### 6. Assets/_Project/11.CoreServices/Scripts/Save/CaravanSaveDataMapper.cs

- 위험도: 높음 — SaveData/runtime 양방향 변환
- 변경: 두 방향 모두 `purchaseUnitPrice` 복사
- 유지해야 할 의미 계약: Save -> Runtime -> Save 왕복 후 구매가가 동일해야 한다.

### 7. Assets/_Project/11.CoreServices/Scripts/Building/CaravanBuildingConstructionCommand.cs

- 위험도: 중간 — 건설 명령 rollback snapshot
- 변경: `CloneItem`에서 `purchaseUnitPrice` 복사
- 이유: 건설 저장 실패 시 Cargo 복구 과정에서 가격 그룹이 0으로 바뀌는 것을 방지한다.

### 8. Assets/Scripts/UI/MarketInventoryIntegration.cs

- 위험도: 높음 — Warehouse 외부 시장 흐름
- 변경: 거래 rollback용 `CloneCargo`에서 `purchaseUnitPrice` 한 필드만 복사
- 변경하지 않은 것:
  - 매수 단가 기록 방식
  - 동일 itemId 병합 정책
  - 매도 차감 순서
- 이유: 이미 존재하는 가격 그룹을 시장 저장 실패 rollback이 손상시키지 않도록 하는 최소 데이터 무결성 수정이다.
- completed 충돌 시: 시장 담당자의 snapshot 구현이 구매가 또는 동등한 원가 정보를 보존하면 이 hunk를 제외한다.

## Warehouse 소유 신규 스크립트

- Assets/_Project/11.CoreServices/Scripts/Save/WarehouseInventoryTransfer.cs
- Assets/_Project/05.UI/04_InGame/YHY/Scripts/Warehouse/WarehouseInventoryPresentation.cs

## 의도적으로 이번 커밋에서 제외하는 범위

- 별도 영속 `lotId`/`priceGroupId` 도입
- 시장 매수 시 `purchaseUnitPrice`를 생성하는 정책 변경
- 시장 판매 FIFO/LIFO 및 가격 그룹 선택 정책
- Warehouse Prefab과 Presenter의 최종 runtime 바인딩
- Warehouse 레벨별 Capacity SO 생성

## 이번 작업과 무관한 기존 Working Tree 변경

- Assets/_Project/01.Core/07_Village/YHY/BuildingPlacementController.cs
- Assets/_Project/07.Scenes/Test/Build UI.unity
- Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset

## 병합 전 최소 확인표

- [ ] SaveData -> Runtime -> Save 왕복 후 구매 단가가 유지되는가
- [ ] 건설/시장 rollback 후 구매 단가가 유지되는가
- [ ] Warehouse 이동이 선택한 복합 키 그룹만 차감하는가
- [ ] 저장 성공 전에는 Home/Cargo 변경 이벤트가 발생하지 않는가
- [ ] 외부 파일 담당자의 completed 변경을 덮어쓰지 않았는가
- [ ] 시장 구매가 생산 정책이 연결되기 전 다중 가격 그룹을 완료로 오인하지 않았는가
