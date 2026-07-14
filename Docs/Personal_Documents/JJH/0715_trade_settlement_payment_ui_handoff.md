# Trade Settlement and Payment UI Handoff

작성일: 2026-07-15

## 통합 UI 담당자 인수인계

- 통합 UI 담당자는 **Core**로 확정한다.
- 두 프리팹은 기본 비활성 상태로 저장되어 있으며, Core 담당자가 활성화 흐름을 담당한다.
- `TradeSettlementPanelController.Show(...)`는 정산 데이터를 채우고 정산 패널을 활성화한다.
- 정산 패널의 `paymentPanel`에는 통합 프리팹 내부의 `PaymentPanelController`를 반드시 연결한다.
- `PaymentPanelController.Show(...)`는 결제 데이터를 채우고 결제 패널을 활성화한다.
- 컨트롤러는 자식 UI를 런타임에 생성하거나 위치·Scale·Sibling 순서를 강제로 변경하지 않는다.
- 프리팹 계층을 수정할 때는 컨트롤러의 숨김 직렬화 참조가 끊기지 않도록 Unity Editor에서 자식을 이동/교체하고 Missing Reference 여부를 확인한다.
- 참조가 누락되면 런타임 복구 대신 명확한 오류를 출력하고 컨트롤러를 비활성화한다.
- 프리팹 전체 재생성이 필요할 때만 `ND/UI/Generate Trade Settlement Prefabs` 메뉴를 사용한다. 이 생성 코드는 Editor 전용이며 Player 빌드에 포함되지 않는다.
- DOTween 도장/영수증 연출은 `PaymentPanelController.StampRoutine()`의 TODO 위치에 구현한다.

> 2026-07-14 담당 영역 정정: 이 문서에 남아 있는 `JourneyResultData`, `CaravanData`, `SettlementUiDataAdapter`, `ISettlementView` 직접 연동 예시는 모두 폐기되었습니다. 관련 UI 코드는 비활성화했으며, 재연동 전에는 `0715_core_services_settlement_integration_request.md`의 담당자 요청 사항을 먼저 반영해야 합니다.

작성일: 2026-07-14  
기준: `Section 9 (1).pdf`의 8. 무역 결과 정산 창, 9. 결제 창

## 구현 파일

- `Assets/Scripts/UI/TradeSettlementPanelController.cs`
- `Assets/Scripts/UI/PaymentPanelController.cs`
- `Assets/Scripts/UI/CargoLoadingPanelController.cs`

## 호출 계약

실제 무역 완료 지점에서 Economy 결과, 선택 경로, 실제 경과 시간을 정산 패널에 전달한다.

```csharp
TradeSettlementPanelController panel =
    TradeSettlementPanelController.Create(canvasTransform);

panel.Show(economyM1LoopResult, selectedRoute, elapsedSeconds);
```

Core의 이동 결과까지 함께 표시할 때는 다음 오버로드를 사용한다.

```csharp
panel.Show(
    economyM1LoopResult,
    selectedRoute,
    journeyResultData,
    caravanData);
```

이 호출은 `JourneyResultData.travelSeconds`를 실제 소요 시간으로 사용한다.

씬에 컨트롤러를 미리 배치한 경우 `Create` 없이 직렬화된 참조의 `Show`를 호출한다.

## 8. 무역 결과 정산 창

- 위에서 아래로 내려오는 활성화 연출을 사용한다.
- 비활성화 시 아래에서 위로 올라간다.
- 정산 본문을 순차적으로 타이핑한다.
- 타이핑 중 영수증 영역을 누르면 전체 본문을 즉시 표시한다.
- 성공 결과의 타이핑이 완료되면 결제 버튼을 활성화한다.
- 실패 결과는 `ErrorCode`를 실패 사유로 표시하고 결제를 허용하지 않는다.

UI는 다음 Economy 결과를 다시 계산하지 않고 표시한다.

- `PriceCalculationResult`: M1 상품 ID, 수량, 단위 구매가, 단위 판매가
- `SettlementBreakdown.Entries`: 구매/판매, 먹이, 용병, 수리, 손실, 이벤트 손익
- `SettlementBreakdown.TotalExpense`: 총 사용 금액
- `SettlementBreakdown.TotalRevenue`: 총 수익
- `SettlementBreakdown.NetProfit`: 순이익
- `SettlementBreakdown.DevelopmentCurrencyReward`: 성장 포인트

Core 결과를 함께 전달하면 다음 값을 추가 표시한다.

- `JourneyResultData.grade`, `failureReason`
- `JourneyResultData.travelSeconds`, `foodConsumed`
- `JourneyResultData.cargoLost`, `durabilityLost`
- `CaravanData.runFoodLost`, `runBattlesFought`
- `CaravanData.mercenaries.Count`
- `CaravanData.currentDurability`, `wagon.maxDurability`

출발지, 목적지와 소요 시간은 `EconomyM1LoopResult`에 없으므로 각각 `RouteData`와 실제 경과 시간을 별도 입력으로 받는다.

## 9. 결제 창

- 정산 창의 성공 결과를 그대로 받아 요약 영수증을 표시한다.
- 결제 버튼을 도장 형태로 표시한다.
- 결제 시 도장 확대/회전과 패널 흔들림 연출을 실행한다.
- 연출 완료 후 `PaymentCompleted` 이벤트를 호출하고 결제 패널을 닫는다.
- `TradeSettlementPanelController.onPaymentCompleted`를 무역 준비 데이터 초기화 함수에 연결한다.
- Cargo 데이터 초기화는 `CargoLoadingPanelController.ResetAfterTradeCompleted()`를 사용한다.

결제 화면은 정산 결과를 다시 합산하거나 CurrencyWallet을 다시 적용하지 않는다. Economy 계산과 화폐 반영이 완료된 결과를 확인하고 연출하는 화면이다.

## 팀별 정산 책임 경계

- Content는 이벤트와 내구도 계산에 필요한 SO Data의 기준값을 작성하고 관리한다.
- Core는 Content의 SO Data를 적용해 먹이 소모량, 이벤트 먹이 손실량, 상품 손실 개수, 내구도 감소량, 용병 수, 전투 횟수 등 원시 결과를 계산하고 제공한다.
- Economy / Progression은 Core가 제공한 원시 결과와 정책 단가를 사용해 먹이 비용, 용병 비용, 수리 비용, 상품 손실 금액, `EventProfit`, `EventLoss`, `CartRepairCost`를 계산하고 `SettlementBreakdown.Entries`로 제공한다.
- QA / 통합은 Core의 원시 결과와 `RouteData.BaseFoodCost`, `BaseMercenaryCost`를 Economy 입력에 연결하고, 정산 결과가 정책대로 산출되는지 통합 검증한다.
- UI는 Core의 원시 수량을 금액으로 임의 환산하지 않고, Economy / Progression이 확정한 정산 결과만 표시한다.

## 아직 필요한 데이터 계약

PDF에는 있지만 현재 `EconomyM1LoopResult`에 포함되지 않은 항목이다. UI가 임의 추정하지 않는다.

- 마차 수리 후 내구도 또는 실제 수리 전후 값
- 생존 견인동물 목록과 개별 상태
- 전투/이벤트 상세 설명 목록
- 다중 상품별 구매·판매 수량과 단가

현재 `PriceCalculationResult`는 상품 1종만 표현하므로 M1 화면에서는 해당 상품의 수량과 단가만 표시한다. 다중 상품 지원 시 상품별 결과 DTO를 추가해야 한다.

## 완료 이벤트 연결 예시

Inspector에서 정산 패널의 `On Payment Completed`에 다음 함수를 연결한다.

```text
CargoLoadingPanelController.ResetAfterTradeCompleted
```

Core 담당자는 실제 거래 흐름을 소유한 Core 컨트롤러의 무역 준비 상태 초기화 함수를 연결한다.
## 폐기된 이전 11.CoreServices 연동 기록 (2026-07-14)

> 아래 내용은 이전 검토 기록이며 **현재 실행하면 안 된다**. `TradeSettlementPanelController`의 `ISettlementView` 직접 구현과 `SettlementUiDataAdapter` 직접 연결은 폐기되었다. 재연동은 먼저 `0715_core_services_settlement_integration_request.md`의 요청 사항을 반영하고 공개 계약이 확정된 뒤 진행한다.

이전에 검토했으나 폐기된 방식:

- `TradeSettlementPanelController`에서 `ND.Framework.ISettlementView`를 직접 구현
- `SettlementUiDataAdapter.settlementViewBehaviour`에 `TradeSettlementPanelController`를 직접 지정
- 패널 또는 부모 오브젝트에 `SettlementUiDataAdapter`를 배치하거나 `settlementUiAdapter` 필드로 직접 참조
- `TradeSettlementReady -> SettlementUiBridge -> SettlementUiDataAdapter` 흐름에 UI 구현을 직접 연결
- 결제 연출 완료 시 UI에서 `SettlementUiDataAdapter.OnClickClaimSettlement()`를 직접 호출

폐기 당시 확인된 제약:

- CoreServices 입력 빌더에는 `BaseFoodCost`, `BaseMercenaryCost`, `durabilityLost` 기반 임시 수리비만 연결되어 있었다.
- `SettlementViewData` 공개 계약에는 개별 영수증 행이 없었다. 먹이·용병·수리·손실·이벤트 손익을 표시하려면 QA/통합 담당자가 `SettlementBreakdown.Entries`에 대응하는 읽기 전용 공개 계약을 먼저 제공해야 한다.
