# Market · Cargo · Travel · Town 구현 정리

작성 기준: 2026-07-22 현재 작업 트리  
관련 요구사항: `0720_Progression_Requested_All_Teams.md`

이 문서는 이번 작업에서 확정된 최종 구조만 설명한다. 중간에 시도했다가 제거한 Cargo 패널 기반 Market 거래 구조는 구현 기준으로 사용하지 않는다.

## 1. 구현 변경한 기능의 목적

### 1.1 도착과 상품 거래 분리

- 이동 완료 자체가 Cargo를 자동 판매하지 않게 한다.
- 정산 claim이 성공하면 목적지 `currentTownId`를 저장하고 Town 화면으로 이동한다.
- Town에 도착한 뒤 사용자가 Market에 들어가 명시적으로 구매·판매한다.
- 거래를 마친 뒤 Town으로 돌아와 다음 무역 준비를 시작한다.

최종 화면 흐름은 다음과 같다.

```text
Preparation → Traveling → 자동 정산/claim → Town
                                         ↓
                                  Market ↔ Town
                                         ↓
                               다음 Preparation 시작
```

### 1.2 Market과 Cargo 준비 책임 분리

- Market은 현재 Town의 `MarketData`, 시장 재고, 구매·판매 draft 및 거래 commit을 담당한다.
- Cargo 준비 화면은 저장된 Cargo를 읽기 전용으로 표시하고 출발 준비만 담당한다.
- Market draft는 메모리에만 존재한다.
- 성공한 commit만 화폐, 시장 재고, `SaveData.caravan.cargo`를 함께 변경한다.
- 출발 Caravan에는 UI 임시 선택값이 아니라 저장된 Cargo 전체를 자동 적재한다.

### 1.3 원자적 저장과 실패 복구

- Market 재고 생성·시간 갱신 저장이 실패하면 이전 재고로 복구한다.
- Market 거래 저장이 실패하면 화폐, 시장 재고, Cargo를 모두 복구한다.
- 실패한 거래 draft는 유지하여 저장 복구 후 재시도할 수 있다.
- 재시도 성공 시 거래가 정확히 한 번만 반영된다.

### 1.4 화면 상태 책임 명확화

- `InGameScreenState.Market`은 `Town` 뒤에 추가한 일시적 UI 상태다.
- Market 상태는 SaveData에 저장하지 않는다.
- SaveData를 다시 읽으면 현재 위치를 기준으로 Town으로 복구한다.
- Market에서는 다음 무역 준비를 직접 시작하지 못하며 먼저 Town으로 돌아가야 한다.

## 2. 기존 코드에서 변경한 코드

### 2.1 Market 조회 및 거래

#### `Assets/Scripts/UI/MarketInventoryIntegration.cs`

- 읽기 전용 `MarketInventorySession`과 변경용 `MarketInventoryMutationSession`을 분리했다.
- `MarketTransactionLine` 기반 구매·판매 증감 거래를 추가했다.
- `CalculateMarketTransaction` 결과를 사용해 화폐·재고·Cargo를 한 번에 적용한다.
- 현재 Market 재고용 catalog와 전체 거래 가능 item catalog를 분리했다.
- 목적지 Market 재고에 없는 외부 Cargo도 판매할 수 있게 했다.
- 중량과 Wagon 슬롯을 실제 저장 Caravan 기준으로 검증한다.
- 재고 생성·갱신과 거래 commit에 저장 실패 rollback을 추가했다.
- 구형 `PersistDraft`, `ReopenCommittedAsDraft`, `CancelPreparation` 및 Cargo 전체 교체 흐름을 제거했다.
- 구버전 `marketPurchasePreparation` 값이 시간별 재고 갱신을 막지 않게 했다.

#### `Assets/Scripts/UI/Market/MarketTradePanelController.cs` — 신규

- `MarketTradePanelModel`이 화면용 구매·판매 draft를 메모리에 보관한다.
- 현재/예상 화폐, 중량, 슬롯, 시장 재고, Cargo 수량을 제공한다.
- 현재 Town의 MarketId에 맞는 `MarketData`를 catalog에서 자동 선택한다.
- `SetBuyDraft`, `SetSellDraft`, `CancelDraft`, `Commit` API를 제공한다.
- `StateChanged`, `ErrorChanged`, `TransactionCompleted` 이벤트를 제공한다.
- `LastErrorCode`를 통해 open/commit 실패 원인을 UI가 조회할 수 있다.
- 동일 Market을 다시 열 때 기존 draft를 보존하고, Town을 벗어나면 draft를 닫는다.

#### `Assets/_Project/03.Economy/01_Market/MarketTransactionCalculator.cs`

- 구매 비용, 판매 수익, 거래 후 화폐를 계산한다.
- 시장 재고, 보유 Cargo, 화폐, 적재 중량과 슬롯 한도를 검증한다.
- 이미 한도를 초과한 Cargo라도 판매로 초과량을 줄이는 거래는 허용한다.

#### `Assets/_Project/03.Economy/01_Market/MarketTransactionModels.cs`

- 현재/최대 Cargo 슬롯과 item별 stack 크기 필드를 추가했다.
- 슬롯 초과 실패 사유를 추가했다.

### 2.2 Town과 Market 화면 전환

#### `Assets/_Project/11.CoreServices/Scripts/SceneFlow/InGameScreenState.cs`

- 기존 enum 정수값을 유지하기 위해 마지막에 `Market`을 추가했다.

#### `Assets/Scripts/UI/Market/TownMarketScreenController.cs` — 신규

- `Town → Market`, `Market → Town` 전환만 허용한다.
- Market 패널 open이 실패하면 화면을 Town에 유지한다.
- 중복 또는 잘못된 상태 전환을 차단한다.
- Scene/Prefab을 직접 변경하지 않는 버튼용 어댑터를 제공한다.

#### `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/FrameworkTradeScreenPresenter.cs`

- Town과 Market에서는 기존 Preparation/Traveling/Settlement UI를 닫는다.
- 정산 뒤 Town에 도착했을 때 Cargo 준비 UI가 자동으로 다시 열리지 않게 했다.

### 2.3 Town에서 다음 무역 준비 시작

#### `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradePreparationEntryCommand.cs` — 신규

- 완료된 Town 저장 상태를 새로운 Preparation 상태로 원자적으로 변경한다.
- 진행 ID와 시간 값을 초기화하고 저장 성공 후에만 Preparation 화면을 요청한다.
- 저장 실패 시 기존 진행 상태와 Caravan 경과 시간을 복구한다.

#### `Assets/_Project/11.CoreServices/Scripts/Bootstrap/FrameworkRoot.cs`

- `TryBeginTradePreparationFromTown()` 진입 API를 추가했다.
- 정산 자동 claim 성공 후 Town 화면으로 이어지는 연결을 정리했다.

#### `Assets/Scripts/UI/TownTradePreparationEntryController.cs` — 신규

- Town 버튼에서 새 무역 준비를 시작한다.
- 실제 현재 화면이 Town일 때만 command를 호출한다.
- 성공한 뒤에만 `FrameworkTradeScreenPresenter`를 연다.
- Market 화면에서 직접 Preparation으로 넘어가는 것을 차단한다.

### 2.4 저장 Cargo의 출발 준비 전달

#### `Assets/99.Sandbox/_LJH/01.Script/Runtime/Builder/TradePrepareViewDataBuilder.cs`

- SaveData에 저장된 기존 Cargo를 무역 준비 view data에 포함한다.
- 현재 Market catalog에서 찾을 수 없는 Cargo도 누락하지 않는다.

#### `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareCaravanFactory.cs`

- 출발 Caravan을 `SaveData.caravan.cargo` 전체 기준으로 생성한다.
- 구형 `selectedBuyItems`가 저장 Cargo를 대체하지 못하게 했다.

#### `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareStartAdapter.cs`

- 출발 요청 전에 저장 Cargo 보존 및 적재 한도를 검증한다.
- Cargo 선택 UI가 출발의 소유자가 되지 않도록 역할을 축소했다.

#### `Assets/99.Sandbox/_LJH/01.Script/Runtime/Integration/TradePrepareUiRuntimeBinding.cs`

- Cargo 화면을 자동 적재·읽기 전용 구성으로 전달한다.
- 저장 Cargo 수량을 `ownedAmount` 기준으로 복원한다.

#### `Assets/Scripts/UI/CargoLoadingPanelController.cs`

- 구형 Market session 직접 생성·commit·cancel 코드를 제거했다.
- 저장 Cargo 표시를 위한 `useOwnedCargo` 복원 경로를 추가했다.
- 자동 적재 상태에서는 Cargo 편집을 막을 수 있게 했다.
- 중량뿐 아니라 Wagon 슬롯 한도도 다음 단계 진행 조건에 포함했다.

#### `Assets/_Project/05.UI/03_TradeSetup/YHY/Panels/TradePrepareUIManager.cs`

- 자동 Cargo 적재 구성을 Cargo 패널에 전달한다.

#### `Assets/_Project/05.UI/03_TradeSetup/YHY/Runtime/TradePrepareRuntimeContextProvider.cs`

- 현재 Town과 저장 데이터 기준의 무역 준비 context를 제공하도록 연결을 보완했다.

### 2.5 이동 정산과 Town 도착

#### `Assets/_Project/03.Economy/03_Settlement/EconomyM1LoopCalculator.cs`

- 이동 완료 정산에서 Cargo 자동 판매를 제거했다.
- 이동 결과와 Economy 정산만 계산하도록 책임을 축소했다.

#### `Assets/_Project/03.Economy/03_Settlement/EconomyM1LoopModels.cs`

- 변경된 이동 정산 결과 계약에 필요한 필드를 보완했다.

#### `Assets/_Project/11.CoreServices/Scripts/TradeProgress/FrameworkEconomyM1InputBuilder.cs`

- 이동 정산 입력에서 Market 자동 판매를 전제하던 변환을 제거했다.

#### `Assets/_Project/11.CoreServices/Scripts/TradeProgress/TradeProgressCoordinator.cs`

- 정산 결과 저장과 claim을 원자적으로 처리한다.
- 목적지와 route를 검증하고 성공 시 `currentTownId`를 갱신한다.
- 도착 시 Settlement 결과 패널을 기다리지 않고 자동 claim 후 Town으로 이동한다.

### 2.6 검증 코드

- `Assets/Scripts/UI/Editor/MarketInventoryIntegrationProbe.cs`
  - Market 재고 결정성, 갱신, 구매·판매, rollback, draft, 재시도, Town/Market 전환을 검증한다.
- `Assets/_Project/03.Economy/01_Market/Editor/MarketTransactionCalculatorTests.cs`
  - 화폐·재고·Cargo·중량·슬롯 계산을 검증한다.
- `Assets/_Project/03.Economy/03_Settlement/Editor/TravelSettlementCalculatorTests.cs` — 신규
  - 이동 정산이 Cargo를 자동 판매하지 않는지 검증한다.
- `Assets/_Project/11.CoreServices/Editor/TradePrepareCargoPreservationTests.cs` — 신규
  - 저장 Cargo가 view와 출발 Caravan 변환 과정에서 보존되는지 검증한다.
- `Assets/_Project/11.CoreServices/Editor/TradePreparationEntryCommandTests.cs` — 신규
  - Town 진입, 저장 실패 rollback, Preparation 전환을 검증한다.
- `Assets/_Project/11.CoreServices/Editor/FrameworkM1LoopE2EEditorTests.cs`
  - 정산 claim, 목적지 검증, 중복 claim, 재실행 복원, 자동 도착 claim을 검증한다.
- `Assets/_Project/11.CoreServices/Scripts/Debug/MarketTravelValidationHarness.cs` — 신규
  - 완성된 Market UI 없이 Market → Cargo → Travel → Town 데이터 경로를 로그로 확인한다.

## 3. 해당 코드를 변경한 사유

### 3.1 Cargo 유실 방지

기존 구조는 Cargo 준비 UI의 선택 목록이 저장 인벤토리처럼 사용될 수 있었다. 화면을 다시 열거나 catalog가 달라지면 기존 Cargo가 누락되고, 출발 commit의 Items가 0이 될 가능성이 있었다. 저장 Cargo를 단일 기준으로 사용하도록 변경했다.

### 3.2 Cargo 전체 삭제 방지

구형 Market 구매 준비 API는 draft 취소 과정에서 Cargo 전체를 지울 수 있었다. 도착지에서 기존 화물을 보존한 채 일부만 사고팔아야 하므로 증감량 기반 transaction으로 교체했다.

### 3.3 거래 책임 중복 제거

Cargo 패널, Market session, 이동 정산이 각각 상품 거래를 수행하면 동일 물품이 이중 판매되거나 화폐·재고·Cargo가 서로 다른 시점에 저장될 수 있다. 상품 거래는 Market transaction만 수행하고 이동 정산은 Cargo를 보존하도록 분리했다.

### 3.4 부분 저장 방지

화폐만 차감되거나 Cargo만 변경되는 부분 성공은 복구하기 어렵다. 계산을 먼저 수행하고 단일 저장 경계에서 적용하며, 저장 실패 시 모든 값을 rollback하도록 변경했다.

### 3.5 도착 화면과 다음 출발 분리

정산 직후 Preparation이 다시 열리면 사용자가 목적지 Market을 이용할 수 없다. 도착 후 Town을 명시적으로 거치고, Market 이용과 다음 무역 준비를 각각 별도 버튼 흐름으로 분리했다.

### 3.6 프리팹 변경 충돌 최소화

기존 `CargoLoadingPanelController`와 무역 준비 프리팹은 여러 팀이 수정할 가능성이 높다. Market 거래를 별도 controller/model로 분리하고 Scene/Prefab reference는 아직 변경하지 않아 병합 충돌 범위를 줄였다.

## 4. 앞으로 해야 할 목록

### 4.1 필수: Market View/Adapter 구현

현재 `MarketTradePanelController`는 데이터와 명령을 제공하지만 이를 실제 상품 행으로 표시하는 View가 없다. 기존 Cargo 프리팹의 버튼만 다시 연결해서는 구매할 수 없다.

`MarketTradePanelView` 또는 동등한 컴포넌트가 필요하다.

- `StateChanged`를 구독해 상품 행을 생성·갱신한다.
- `ErrorChanged`를 오류 텍스트에 표시한다.
- `TransactionCompleted`를 거래 결과 UI에 반영한다.
- 상품 행 버튼에서 런타임 `itemId`와 수량을 `SetBuyDraft`/`SetSellDraft`에 전달한다.
- commit 버튼은 `Model.CanCommit`에 따라 활성화한다.
- 화폐, 중량, 슬롯의 현재값·예상값·최대값을 표시한다.
- `SAVE_FAILED`에서는 패널과 draft를 유지해 재시도할 수 있게 한다.

상품 행의 최소 항목:

- 상품명, 구매가, 판매가
- 시장 재고, 보유 Cargo
- 구매 draft, 판매 draft
- 거래 후 예상 시장 재고와 Cargo
- 구매/판매 수량 증가·감소 버튼

### 4.2 필수: 프리팹과 Inspector 연결

권장 구조:

```text
TownUI
├─ TownRoot
│  ├─ OpenMarketButton
│  └─ BeginTradeButton
├─ MarketRoot
│  ├─ ItemList
│  ├─ Summary
│  ├─ ErrorText
│  ├─ CommitButton
│  ├─ CancelDraftButton
│  └─ CloseButton
└─ MarketSystem (항상 활성화)
   ├─ MarketTradePanelController
   ├─ TownMarketScreenController
   └─ MarketTradePanelView
```

Inspector 연결:

1. `MarketTradePanelController.marketCatalog`에 모든 MarketData를 등록한다.
2. 각 Town의 MarketId와 MarketData의 `marketId`가 일치하는지 확인한다.
3. `TownMarketScreenController.marketTradePanel`을 연결한다.
4. 시장 열기 버튼을 `OnClickOpenMarket()`에 연결한다.
5. 시장 닫기 버튼을 `OnClickCloseMarket()`에 연결한다.
6. 다음 무역 버튼을 `TownTradePreparationEntryController.OnClickBeginTradePreparation()`에 연결한다.
7. `TownTradePreparationEntryController.tradeScreenPresenter`를 연결한다.
8. 실제 플레이에서는 Cargo 한도 override를 끄고 저장 Wagon 한도를 사용한다.

### 4.3 필수: 플레이 모드 최종 검증

1. Town에서 현재 마을 Market이 열린다.
2. 구매·판매 draft 변경 전에는 SaveData가 바뀌지 않는다.
3. 거래 성공 시에만 화폐·시장 재고·Cargo가 함께 바뀐다.
4. 다른 마을에서 가져온 Cargo도 판매할 수 있다.
5. 화폐·재고·보유량·중량·슬롯 초과 거래가 차단된다.
6. Market을 닫으면 Town으로 돌아온다.
7. Town에서 다음 무역 준비를 시작할 수 있다.
8. Cargo 준비 화면에 저장 Cargo 전체가 읽기 전용으로 표시된다.
9. 출발 후 이동과 정산을 지나도 판매하지 않은 Cargo가 유지된다.
10. 도착 시 Settlement 패널을 기다리지 않고 자동 claim 후 Town이 표시된다.

### 4.4 병합 전 확인

- Scene/Prefab 변경은 패널 담당자 작업과 함께 별도 diff로 확인한다.
- 충돌 가능성이 높은 `CargoLoadingPanelController.cs`와 무역 준비 UI 파일은 최신 dev2와 다시 비교한다.
- 테스트용 ScriptableObject asset 변경은 소유자와 필요 여부를 확인한 뒤 포함한다.
- 구형 문서의 `MarketInventorySession.PersistDraft`, `Commit`, `CancelPreparation` 호출 설명은 현재 API와 맞지 않으므로 후속 문서에서 갱신한다.

## 현재 자동 검증 결과

- `Market inventory integration probe passed.` — v20
- `Trade preparation entry probe passed (3/3).`
- `Trade prepare cargo preservation probe passed (4/4).`
- Framework 자동 도착 claim E2E 통과 로그 확인 완료
- `git diff --check` 통과

현재 Scene/Prefab asset은 이 작업에서 수정하지 않았다.
