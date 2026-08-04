# Cargo Sell UI 상태 및 후속 작업

- 기준일: 2026-08-03
- 범위: 선택된 Caravan Cargo → 현재 지역 상점 판매 대기 목록
- 현재 단계: 외형 Prefab 및 Build UI 검토 Scene 조립
- 런타임 시장 데이터 연결: 보류

## 구현·조립 상태

- `CargoSellPopup.prefab`을 생성했다.
- Build UI Scene의 `BuildingPopupPreviewCanvas/CargoSellPopup`에 Prefab 인스턴스로 조립했다.
- Root RectTransform은 전체 화면 Stretch, offset 0, scale 1이다.
- 다른 Popup을 가리지 않도록 Scene 초기 상태는 비활성화했다.
- Cargo 영역과 판매 대기 목록은 각각 ScrollRect를 전제로 구성했다.
- 판매 대상 Caravan은 외부 Caravan 슬롯에서 이미 선택되어 들어오므로 Popup 내부 Caravan 선택 UI는 두지 않는다.
- 판매 대기 목록은 아이콘, 품목명, 수량, 판매 단가, 판매 금액과 총 판매 금액을 표시하는 형태다.

## 확정 상호작용

- Cargo 슬롯 hover: 아이템 설명 Tooltip
- Cargo 슬롯 click: 구매가 묶음 선택 → 수량 선택 → 판매 대기 목록 등록
- 판매 대기 행 좌클릭: 해당 구매가 묶음과 수량 변경
- 판매 대기 행 우클릭: 해당 품목 전체 등록 해제
- 실제 Cargo 차감과 저장은 판매 확정 시점에만 수행
- 목록 비우기는 판매 대기 상태만 초기화하며 Cargo SaveData는 변경하지 않음

## 아직 연결하지 않은 기능

- 선택된 `caravanId`와 Popup 연결
- 실제 `caravan.cargo` ViewData 생성
- 현재 지역 상점 이름과 가격 데이터 연결
- 가격 묶음별 판매 대기 상태 및 수량 변경
- 판매 확정 transaction, 수익 반영, 저장 실패 rollback
- 판매 성공 뒤 Cargo·통화 UI 갱신
- Cargo full/empty, stale Caravan, 중복 입력과 저장 실패 Notice
- 구매 UI와 판매 UI의 공통 슬롯·Tooltip·가격/수량 Modal 재사용 범위 확정

## Caravan 담당 작업 이후 확인

- TradeCycle과 Warehouse, Cargo Sell UI가 동일한 `caravanId`의 `caravan.cargo`를 조회해야 한다.
- Wagon의 `inventorySlotCount`만큼 Cargo 슬롯을 표시하며 나머지는 비활성화한다.
- 사용자 지정 Caravan 이름은 제목 표시에만 사용하고 조회·명령 키는 `caravanId`를 유지한다.
- `itemId + purchaseUnitPrice` 묶음이 판매 대기 등록과 취소 과정에서 합쳐지거나 유실되지 않아야 한다.

## 다른 작업자 소유로 보류한 스크립트

아래 파일은 이번 Cargo Sell UI 외형·Scene 조립 작업에서 수정하지 않았다.

- `MarketData.cs`
- `MarketTradePanelController.cs`
- `MarketInventoryIntegration.cs`
- `SaveData.cs`
- `JsonSaveService.cs`
- `MarketInventoryIntegrationProbe.cs`
- `TradeItemData.cs`
- `SharedGameDataView.cs`
- `SharedGameDataService.cs`
- `MarketTravelValidationHarness.cs`

