# 2026-07-22 Progression 요청 사항 — 전체 팀

작성일: 2026-07-22

관련 구현 문서: `0722_Progression_Market_Arrival_Sale_UI_Work_Log.md`

## 1. 공통 확정 흐름

```text
Preparation
-> Traveling
-> 성공 도착 및 SettlementPending
-> Caravan별 판매 버튼
-> 목적지 MarketData 기반 판매 전용 패널
-> 판매 확인
-> 기존 정산 패널
-> 정산 claim
-> Town
-> 무역 버튼
-> 다음 Preparation
```

공통 규칙:

- 성공 도착 시 자동 판매하거나 즉시 정산 claim하지 않는다.
- 판매하지 않은 상품은 해당 Caravan의 Cargo에 유지한다.
- 판매창은 판매만, 마을 구매창은 구매만 허용한다.
- Cargo와 현재 위치는 Caravan별로 독립적이다.
- 거래 화폐는 Player 공용이다.
- 실패한 이동은 도착 판매 없이 기존 실패 정산 화면을 사용한다.
- UI가 SaveData를 직접 변경하지 않고 공개 controller와 Framework API를 호출한다.

## 2. UI 팀 요청 사항

### 2.1 Town 무역 버튼 경로 교체

대상:

```text
Assets/_Project/08.Prefabs/UI/Maps/MainUICanvas.prefab
TradeBtn
```

현재 구형 연결:

```text
TradeBtn
-> FrameworkTradeScreenPresenter.OpenTradeScreen()
```

요청 연결:

```text
TradeBtn
-> TownTradePreparationButton
-> TownTradePreparationEntryController.TryBeginTradePreparation()
-> FrameworkRoot.TryBeginTradePreparationFromTown()
-> TradePreparationEntryCommand.TryExecute()
-> FrameworkTradeScreenPresenter.OpenTradeScreen()
```

작업 방법:

1. `Button > On Click()`에 등록된 `FrameworkTradeScreenPresenter.OpenTradeScreen` 직접 호출을 제거한다.
2. `TradeBtn` GameObject에 `TownTradePreparationButton`을 추가한다.
3. `entryController`와 `tradeScreenPresenter`를 Inspector에서 명시적으로 연결한다.
4. 정산 완료 뒤 클릭했을 때 route 선택 화면부터 새 무역 준비가 시작되는지 확인한다.

완료 기준:

- 정산 완료 후 무역 버튼을 한 번 눌러 Preparation이 열린다.
- 경로와 Caravan을 다시 선택할 수 있다.
- `Trade preparation entry blocked...` 경고가 발생하지 않는다.
- Presenter 직접 호출만으로 Town 화면이 다시 닫히는 문제가 없다.

### 2.2 Caravan 상태 UI의 판매 버튼

사용 API:

```csharp
CaravanArrivalSaleButton.Bind(caravanId);
```

요청 사항:

- 각 Caravan 상태 행에 판매 버튼을 둔다.
- 해당 Caravan이 `SettlementPending`이고 성공 결과를 가진 경우에만 활성화한다.
- 여러 Caravan이 동시에 도착할 수 있으므로 공용 자동 선택 버튼에 의존하지 않는다.
- 버튼을 누르면 해당 Caravan의 목적지 MarketData 기반 판매창이 열려야 한다.

완료 기준:

- 이동 중인 Caravan 버튼은 비활성화된다.
- 도착한 해당 Caravan 버튼만 활성화된다.
- 서로 다른 마을에 도착한 Caravan이 각각 다른 MarketData를 사용한다.

### 2.3 최종 판매 패널 제작

검증용 프리팹:

```text
Assets/_Project/08.Prefabs/UI/Market/ArrivalSaleFlow.prefab
```

검증용 프리팹은 InGame 씬의 Town HUD Canvas 아래에 배치해 기능 테스트에 사용할 수 있다. 최종 UI는 같은 controller 계약을 유지하면서 아트·레이아웃을 교체한다.

필수 표시 항목:

- 상품명
- Caravan 보유 수량
- 목적지 마을 판매가
- 판매 선택 수량
- 예상 판매 수익
- 수량 감소·증가·전부 선택
- 판매 확인
- 오류 메시지

연결 대상:

- `CaravanArrivalSaleController`
- `MarketTradePanelController`
- `ArrivalSalePanelView` 또는 동일 계약의 최종 View

완료 기준:

- 구매 기능이 노출되지 않는다.
- 일부만 판매할 수 있다.
- 미판매 Cargo가 보존된다.
- 판매 확인 직후 기존 정산 패널이 자동으로 열린다.
- 정산창을 열기 위해 무역 버튼을 추가로 누를 필요가 없다.

### 2.4 마을 구매 전용 패널

사용 진입점:

```text
CaravanTownPurchaseController.OpenForCaravan(caravanId)
```

요청 사항:

- 선택한 Caravan의 `currentTownId`를 기준으로 Town과 MarketData를 선택한다.
- 판매·정산이 완료된 Caravan만 구매 대상으로 사용한다.
- 구매 기능만 제공하고 판매 기능은 노출하지 않는다.
- 구매를 마친 뒤 Town으로 돌아가 기존 무역 버튼으로 다음 출발 준비를 시작한다.

완료 기준:

- Caravan 위치와 다른 마을 Market이 열리지 않는다.
- 구매 후 해당 Caravan Cargo에 상품이 저장된다.
- 다른 Caravan Cargo에는 영향을 주지 않는다.

### 2.5 Base Camp 수동 입출고 패널

Framework 측 공개 경계:

```text
BaseCampInventoryTransferService
```

요청 사항:

- Base Camp에 있는 Caravan의 Cargo와 Player 공용 `homeInventory` 사이를 수동 이동한다.
- 실제 패널과 상호작용은 UI 팀이 제작한다.
- 이동 중이거나 다른 마을에 있는 Caravan의 입출고는 차단한다.

완료 기준:

- 입고한 상품은 Player 공용 창고에 저장된다.
- 출고한 상품은 선택 Caravan Cargo에만 들어간다.
- 저장 실패 시 양쪽 인벤토리가 원래 상태로 복구된다.

## 3. LJH UI Integration 팀 확인 요청

대상 파일:

- `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/FrameworkTradeScreenPresenter.cs`
- `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareUiRuntimeBinding.cs`

확인 내용:

### `FrameworkTradeScreenPresenter.cs`

- 성공 도착 시 기존 무역·정산 화면을 닫고 판매 대기 상태를 유지한다.
- 판매 완료 뒤 `IsSettlementPresentationRequested`가 true인 경우에만 정산 UI를 다시 연다.
- 실패 이동은 기존 정산 UI 흐름을 유지한다.
- Town에서 다음 무역 시작은 Presenter 직접 호출이 아니라 `TownTradePreparationButton` 경로를 사용한다.

### `TradePrepareUiRuntimeBinding.cs`

- Wagon 선택 뒤 `AnimalInventoryPanel.RefreshAnimalAvailability()`를 호출한다.
- view data 갱신 중 동물 패널이 열려 있으면 보유 수량과 `canSelect`를 다시 반영한다.

완료 기준:

- LJH 최신 변경과 병합해도 도착 판매 대기 분기가 유지된다.
- 말이 SaveData에 있는데 Wagon에 넣을 수 없는 stale UI 문제가 재발하지 않는다.

## 4. YHY Core Gameplay · UI & Data 팀 확인 요청

대상 파일:

- `Assets/_Project/01.Core/05_Caravan/YHY/CaravanData.cs`
- `Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/AnimalInventoryPanel.cs`
- `Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/TradePrepareUIManager.cs`

확인 내용:

### `CaravanData.cs`

- runtime Caravan에 `currentTownId`가 추가됐다.
- 이동 중에는 출발 마을을 유지하고 정산 claim 성공 후 목적지로 갱신한다.
- YHY 데이터 구조 변경과 필드명이 충돌하지 않는지 확인한다.

### `AnimalInventoryPanel.cs`

- Wagon 선택 후 동물 선택 가능 상태를 갱신하는 `RefreshAnimalAvailability()`가 추가됐다.
- 기존 슬롯 선택·해제 및 보유량 표현과 충돌하지 않는지 확인한다.

### `TradePrepareUIManager.cs`

- Payment 완료와 Framework claim 연결을 `SettlementPaymentFlowController`에 위임한다.
- 기존 Inspector persistent listener가 남아 중복 claim하지 않는지 확인한다.

완료 기준:

- Caravan 위치 필드가 runtime reset 또는 copy 과정에서 사라지지 않는다.
- Wagon과 동물 선택이 기존 UI 규칙대로 동작한다.
- 정산 claim이 한 번만 호출된다.

## 5. Framework & Integration 팀 확인 요청

대상 파일:

- `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs`
- `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/CaravanSaveDataMapper.cs`
- `Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs`

확인 내용:

### 도착과 정산

- runtime의 `autoClaimOnArrival`은 false다.
- 성공 도착은 결과를 저장하고 판매 대기 상태를 유지한다.
- `SettlementUiBridge.PresentSettlement(caravanId, tradeId)`가 판매 완료 후 정산 표시를 요청한다.
- 실패 결과는 판매 단계 없이 Settlement 화면을 연다.
- claim 성공 시 pending, commit, runtime cache를 정리하고 Town으로 전환한다.

### Caravan별 위치 저장

- `CaravanSaveData.currentTownId`를 저장한다.
- mapper가 runtime과 SaveData 사이에서 위치를 보존한다.
- 구버전 save는 Player의 현재 마을을 migration 기본값으로 사용한다.

완료 기준:

- 성공 도착 후 앱을 재실행해도 판매 대기 Caravan과 결과가 복구된다.
- 판매 완료 전 claim할 수 없다.
- claim 뒤 `Completed`, pending 0, commit false, 목적지 `currentTownId`가 저장된다.
- 여러 Caravan의 위치와 진행 상태가 서로 덮어쓰이지 않는다.

## 6. Economy 팀 확인 요청

대상 파일:

- `Assets/_Project/03.Economy/01_Market/MarketTransactionCalculator.cs`
- `Assets/_Project/03.Economy/01_Market/Editor/MarketTransactionCalculatorTests.cs`

확인 내용:

- `Stover` 시장 재고는 항상 999다.
- 조회, 재고 생성, 구매 결과, 판매 결과가 모두 같은 고정 재고 정책을 사용한다.
- 구매 비용과 Caravan Cargo 증가는 정상 반영한다.
- 판매 수익과 Caravan Cargo 감소는 정상 반영한다.

완료 기준:

- 여물 구매·판매 전후 시장 표시 수량이 999다.
- 여물 외 상품은 기존 시장 재고 증감 규칙을 유지한다.

## 7. 전체 팀 통합 테스트

1. 서로 다른 두 Caravan을 준비한다.
2. 각 Caravan에 마차, 말과 서로 다른 Cargo를 배치한다.
3. 서로 다른 목적지로 출발시킨다.
4. 도착한 Caravan별 판매 버튼이 독립적으로 활성화되는지 확인한다.
5. 목적지별 MarketData 가격으로 일부 상품만 판매한다.
6. 미판매 Cargo가 각 Caravan에 남는지 확인한다.
7. 판매 확인 후 정산 패널과 claim을 완료한다.
8. Town의 구매 전용 패널에서 상품을 구매한다.
9. 무역 버튼으로 새 Preparation을 시작한다.
10. Base Camp로 돌아온 Caravan Cargo를 공용 창고에 수동 입고한다.

## 8. 병합 전 공통 확인

- 담당 경로의 최신 파일과 diff를 먼저 비교한다.
- Scene·Prefab 변경은 UI 팀 변경을 우선하고 controller 공개 계약을 유지한다.
- `CargoLoadingPanelController.cs`는 충돌 가능성이 높으므로 불필요한 변경을 포함하지 않는다.
- 테스트용 `SelectedCaravanTransportSeeder`는 Editor 전용으로 유지한다.
- 검증용 `ArrivalSaleFlow.prefab`은 최종 아트 프리팹으로 교체할 수 있다.
- 각 팀 확인 뒤 전체 E2E를 다시 실행한다.
