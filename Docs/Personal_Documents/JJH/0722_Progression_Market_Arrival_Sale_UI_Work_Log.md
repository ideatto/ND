# 2026-07-22 Market · Caravan · 도착 판매 작업 최신 내역

작성일: 2026-07-22

기준: 현재 작업 트리 및 플레이 테스트 결과

요청 사항 문서: `0722_Progression_Requested_All_Teams.md`

이 문서는 2026-07-22 기준 구현 결과, 기능 목적, 수정 파일, 타 팀 소유 가능 파일과 후속 UI 업무를 정리한다.

## 1. 오늘 확정된 플레이 흐름

```text
경로 선택
-> Caravan 선택
-> 마차와 견인 동물 선택
-> 현재 마을에서 상품 구매·적재
-> 용병 선택 및 출발
-> Traveling
-> 성공 도착 시 SettlementPending
-> 해당 Caravan의 판매 버튼 활성화
-> 목적지 MarketData 기반 판매 전용 패널
-> 판매 확인
-> 기존 정산 패널
-> 정산 claim
-> Town
-> 무역 버튼으로 다음 Preparation 시작
```

- 성공 도착은 즉시 자동 claim하지 않는다.
- 판매하지 않은 화물은 해당 Caravan의 `cargo`에 보존한다.
- 도착 판매 패널은 판매만 허용한다.
- 실패한 이동은 목적지 Market 판매 단계 없이 기존 실패 정산 화면을 사용한다.
- 정산 claim 성공 후 Caravan은 `Completed`, pending settlement는 제거되고 현재 마을이 갱신된다.
- 각 Caravan의 Cargo와 위치는 독립적이며 거래 화폐만 Player 공용이다.

## 2. 오늘 구현·수정한 주요 기능

### 2.1 Caravan 도착 판매

- `CaravanArrivalSaleController`가 `SettlementPending`인 Caravan과 저장된 정산 결과를 검증한다.
- 이동 route의 목적지 Town을 조회하고 Town의 MarketId와 일치하는 `MarketData`를 선택한다.
- `MarketTradePanelController.OpenForArrivalSale()`로 판매 전용 session을 연다.
- 판매 draft가 있으면 거래를 원자적으로 저장하고, 없으면 판매를 건너뛸 수 있다.
- 판매 확인 뒤 `SettlementUiBridge.PresentSettlement(caravanId, tradeId)`를 호출해 기존 정산 UI를 연다.

### 2.2 판매 버튼 활성화

- `CaravanArrivalSaleButton.Bind(caravanId)`로 상단별 버튼을 연결할 수 있다.
- `TradeSettlementReady` 수신 시 도착 Caravan의 버튼 상태를 갱신한다.
- 테스트용 공용 버튼은 판매 대기 Caravan이 하나일 때 ID를 자동 선택한다.
- 여러 Caravan이 동시에 판매 대기라면 각 상단 UI가 명시적으로 `Bind(caravanId)`해야 한다.
- 열기 실패 시 `[Arrival Sale UI] Open failed...` 로그로 오류 코드를 표시한다.

### 2.3 판매 UI와 테스트 프리팹

- 보유 Cargo, 판매가, 판매 수량, 예상 수익을 표시하는 `ArrivalSalePanelView`를 추가했다.
- 수량 감소, 증가, 전부 판매와 판매 확인 기능을 제공한다.
- `ArrivalSaleFlow.prefab`에 판매 controller, market controller, view와 테스트 버튼을 묶었다.
- InGame 씬의 실제 Town HUD Canvas 아래에 배치해 전체 이동·판매·정산 흐름을 테스트할 수 있다.

프리팹 경로:

```text
Assets/_Project/08.Prefabs/UI/Market/ArrivalSaleFlow.prefab
```

재생 중 생성된 구형 Clone에는 코드·프리팹 변경이 반영되지 않으므로 Play Mode 종료 후 최신 프리팹을 다시 배치한다.

### 2.4 정산 UI 표시 시점

- 성공 도착 시 기존 무역 화면을 닫고 판매 대기 상태를 유지한다.
- 판매 완료가 명시적으로 정산 표시를 요청했을 때만 Presenter가 정산 화면을 다시 연다.
- 판매 확인 후 무역 버튼을 한 번 눌러야 정산창이 보이던 잘못된 순서를 제거했다.
- `SettlementPaymentFlowController`가 결제 애니메이션 완료와 Framework claim 호출을 한 번만 연결한다.

### 2.5 Caravan별 현재 마을

- runtime `CaravanData`와 저장 DTO `CaravanSaveData`에 `currentTownId`를 추가했다.
- mapper가 runtime과 SaveData 사이에서 위치를 보존한다.
- 구버전 저장 데이터는 Player의 현재 마을을 Caravan 위치의 migration 기본값으로 사용한다.
- 구매 패널은 선택한 Caravan의 현재 위치를 기준으로 해당 Town의 MarketData를 선택할 수 있다.

### 2.6 Market 거래와 여물 재고

- `MarketTransactionCalculator`는 구매·판매 비용, Cargo, 시장 재고와 슬롯·중량을 검증한다.
- 여물은 `ItemId = Stover`를 대소문자 구분 없이 식별한다.
- 여물 시장 재고는 조회, 신규 생성, 구매 전후, 판매 전후 모두 999로 유지한다.
- 여물을 구매하면 화폐와 Caravan 보유량은 정상 변경되지만 시장 수량은 감소하지 않는다.
- 여물을 판매해도 시장 수량은 999를 유지한다.

### 2.7 테스트 지원 기능

- `SelectedCaravanTransportSeeder`로 선택 Caravan에 테스트용 마차와 말을 넣을 수 있다.
- 마차 선택 뒤 견인 동물의 `canSelect`와 보유량을 즉시 갱신한다.
- `BaseCampInventoryTransferService`는 Base Camp에서 Caravan Cargo와 공용 창고 사이의 수동 이동 경계를 제공한다. 실제 패널은 타 팀 작업 범위다.

## 3. 오늘 생성한 `.cs`

| 파일 | 기능과 생성 사유 |
|---|---|
| `Assets/Scripts/UI/Market/ArrivalSalePanelView.cs` | 도착 판매 목록, 수량 조절, 예상 수익, 판매 확인을 표시하는 최소 실행 View |
| `Assets/Scripts/UI/Market/CaravanArrivalSaleButton.cs` | 상단별 판매 대기 상태와 버튼 활성화 및 판매창 호출 연결 |
| `Assets/Scripts/UI/Market/CaravanArrivalSaleController.cs` | Caravan·route·목적지 Market 검증, 판매 session, 정산 표시 연결 |
| `Assets/Scripts/UI/Market/CaravanTownPurchaseController.cs` | 판매·정산 이후 Caravan 위치 기준 구매 전용 Market session 진입점 |
| `Assets/Scripts/UI/SettlementPaymentFlowController.cs` | 정산 결제 완료 이벤트와 Framework claim의 중복 연결 방지 |
| `Assets/_Project/11.CoreServices/Editor/ArrivalSaleFlowPrefabBuilder.cs` | 테스트용 `ArrivalSaleFlow.prefab` 자동 생성 및 MarketData 연결 |
| `Assets/_Project/11.CoreServices/Editor/SelectedCaravanTransportSeeder.cs` | 말·마차 부족으로 플레이 검증이 막힐 때 사용하는 Editor 전용 seeder |
| `Assets/_Project/11.CoreServices/Scripts/Save/BaseCampInventoryTransferService.cs` | Caravan Cargo와 Base Camp 공용 창고 간 수동 입출고 저장 경계 |

## 4. 우리 기능 중심으로 수정한 기존 `.cs`

| 파일 | 변경 내용 | 사유 |
|---|---|---|
| `Assets/Scripts/UI/Market/MarketTradePanelController.cs` | 판매 전용·구매 전용 모드, Caravan 지정, 저장 Caravan 한도와 위치 기반 Market 연결 | 도착 판매와 마을 구매를 동일 거래 모델에서 안전하게 분리하기 위해 |
| `Assets/Scripts/UI/MarketInventoryIntegration.cs` | Caravan별 조회·변경 session, Cargo 보존, 거래 rollback, 여물 999 조회 | 시장·Cargo·화폐를 한 저장 경계에서 변경하고 상단 인벤토리를 분리하기 위해 |
| `Assets/_Project/03.Economy/01_Market/MarketTransactionCalculator.cs` | 여물 고정 재고와 거래 전후 재고 계산 | 여물을 모든 마을에서 항상 999개 구매 가능하게 하기 위해 |
| `Assets/_Project/03.Economy/01_Market/Editor/MarketTransactionCalculatorTests.cs` | 여물 구매·판매 후 재고 999 검증 | 고정 재고 회귀 방지 |
| `Assets/_Project/11.CoreServices/Editor/FrameworkM1LoopE2EEditorTests.cs` | 도착 판매 대기, 명시적 정산 표시, claim 흐름 검증 | 자동 claim 제거 뒤 전체 상태 전환 보장 |
| `Assets/_Project/11.CoreServices/Editor/TradePrepareCargoPreservationTests.cs` | 최신 factory 계약과 Cargo 보존 검증 | 출발 과정에서 저장 Cargo가 사라지는 회귀 방지 |

## 5. 다른 팀 소유일 수 있는 수정 `.cs`

아래 파일은 경로 또는 파일 주석상 LJH, YHY, Framework & Integration 소유 가능성이 높다. 병합 전에 담당 팀이 변경 의도와 최신 구현을 확인해야 한다.

| 소유 가능 팀 | 파일 | 수정 내용 | 수정이 필요했던 사유 |
|---|---|---|---|
| LJH UI Integration | `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/FrameworkTradeScreenPresenter.cs` | 성공 도착 시 정산 UI를 닫고 판매 대기, 판매 완료의 명시적 요청에서만 정산 UI 재개 | 도착 즉시 정산 패널이 열리거나 무역 버튼을 눌러야 뒤늦게 열리는 문제 해결 |
| LJH UI Integration | `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareUiRuntimeBinding.cs` | Wagon 선택 및 view data 변경 뒤 동물 선택 가능 수량 재계산 | 테스트용 말이 있어도 Wagon 슬롯에 넣을 수 없던 stale `canSelect` 문제 해결 |
| YHY UI & Data | `Assets/_Project/01.Core/05_Caravan/YHY/CaravanData.cs` | runtime `currentTownId` 추가 | 다중 Caravan의 현재 마을을 독립적으로 보존하기 위해 |
| YHY Core Gameplay UI | `Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/AnimalInventoryPanel.cs` | `RefreshAnimalAvailability()` 추가 | Wagon 선택 뒤 동물 보유량과 적합성 UI를 다시 계산하기 위해 |
| YHY Core Gameplay UI | `Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/TradePrepareUIManager.cs` | 정산 Payment 완료 연결을 `SettlementPaymentFlowController`로 위임 | UI가 Framework claim을 중복 호출하지 않도록 하기 위해 |
| Framework & Integration | `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs` | runtime 자동 claim 비활성화, `PresentSettlement()`, 표시 요청 상태 추가 | 도착 판매가 끝난 후에만 정산 패널을 열기 위해 |
| Framework & Integration | `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs` | 성공 도착은 판매 대기, 실패 도착만 즉시 Settlement 화면 요청 | 성공과 실패의 도착 UX를 분리하기 위해 |
| Framework & Integration | `Assets/_Project/11.CoreServices/Scripts/Save/SaveData.cs` | `CaravanSaveData.currentTownId` 추가 | 다중 상단 위치 저장 |
| Framework & Integration | `Assets/_Project/11.CoreServices/Scripts/Save/CaravanSaveDataMapper.cs` | 위치 runtime/save 복사 | 위치 필드 유실 방지 |
| Framework & Integration | `Assets/_Project/11.CoreServices/Scripts/Save/JsonSaveService.cs` | 구버전 위치 migration | 기존 save가 빈 위치로 구매 패널 진입에 실패하는 문제 방지 |

`CargoLoadingPanelController.cs`는 현재 Git 상태에서 수정 표시가 있지만 일반 `git diff` 기준 텍스트 차이가 확인되지 않았다. 다른 팀 변경과 충돌을 피하기 위해 실제 내용 변경 파일 목록에는 포함하지 않았으며 병합 직전 상태를 다시 확인한다.

## 6. UI 팀 후속 작업

### 6.1 `TradeBtn` 진입 경로 교체

현재 `MainUICanvas.prefab`의 무역 버튼은 구형 `FrameworkTradeScreenPresenter.OpenTradeScreen()`을 직접 호출한다. 정산 후 SaveData는 Town이므로 Presenter만 열면 다시 닫힌다.

UI 팀 교체 경로:

```text
TradeBtn
-> TownTradePreparationButton
-> TownTradePreparationEntryController.TryBeginTradePreparation()
-> FrameworkRoot.TryBeginTradePreparationFromTown()
-> TradePreparationEntryCommand.TryExecute()
-> InGameScreenState.Preparation
-> FrameworkTradeScreenPresenter.OpenTradeScreen()
```

작업 항목:

1. `TradeBtn > Button > On Click()`의 Presenter 직접 호출 제거
2. `TownTradePreparationButton` 추가
3. `entryController`, `tradeScreenPresenter` 명시 연결
4. 정산 완료 뒤 route와 Caravan을 다시 선택할 수 있는지 확인

이 프리팹 경로 교체는 UI 팀 담당으로 남겨두었으며 현재 작업에서는 적용하지 않았다.

### 6.2 판매 프리팹 InGame 테스트

1. `ArrivalSaleFlow.prefab`을 InGame 씬의 메인 Town Canvas 아래에 배치한다.
2. 성공 무역을 완료해 Caravan을 `SettlementPending`으로 만든다.
3. 해당 Caravan의 판매 버튼 활성화를 확인한다.
4. 목적지 MarketData 가격과 잔존 Cargo가 표시되는지 확인한다.
5. 일부만 판매하고 미판매 Cargo가 보존되는지 확인한다.
6. 판매 확인 뒤 기존 정산 패널이 자동으로 열리는지 확인한다.
7. 정산 claim 뒤 Town으로 복귀하는지 확인한다.
8. 교체된 무역 버튼으로 다음 Preparation이 열리는지 확인한다.

## 7. 현재 플레이 검증 결과

- 판매 버튼 정상 활성화 확인
- 도착 판매 UI 정상 표시 확인
- 판매 거래 정상 처리 확인
- 판매 확인 뒤 정산 패널 정상 표시 확인
- 정산 버튼과 claim 정상 처리 확인
- 정산 후 SaveData 확인: `Completed`, pending settlement 0, commit false, `currentTownId=RiverTown`
- 다음 무역 버튼 미동작 원인 확인: SaveData나 Framework 차단이 아니라 `TradeBtn`의 구형 Presenter 직접 호출
- 다음 무역 버튼 프리팹 연결 교체와 최종 route 선택 검증은 UI 팀 후속 작업

## 8. 병합 전 확인

- 타 팀 소유 가능 파일은 담당 팀 최신본과 diff를 비교한다.
- `FrameworkTradeScreenPresenter`의 판매 대기 분기와 UI 팀 변경이 충돌하지 않는지 확인한다.
- YHY의 `AnimalInventoryPanel`, `TradePrepareUIManager`, `CaravanData` 최신 변경을 우선 확인한다.
- Save schema 필드 추가와 migration을 Framework 담당과 확인한다.
- 테스트용 `SelectedCaravanTransportSeeder`는 Editor 폴더에만 유지한다.
- `ArrivalSaleFlow.prefab`은 검증용 최소 UI이므로 최종 아트·레이아웃 프리팹과 교체 가능하다.
- `TradeBtn` 경로 교체 후 전체 E2E를 한 번 더 실행한다.
