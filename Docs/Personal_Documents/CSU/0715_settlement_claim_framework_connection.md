# 정산·Claim Framework 연결 인계

## 변경

- `TradeSettlementPanelController`가 `ND.Framework.ISettlementView`를 구현한다.
- Framework `SettlementViewData`의 수익·비용·순이익·실패 원인·Claim 가능 상태를 표시한다.
- `PaymentPanelController`는 Framework가 전달한 Claim 가능 상태를 사용한다.
- 기존 Economy `Show(...)` API는 유지한다.

## 통합 방법

1. 정산 통합 Prefab에 `SettlementUiDataAdapter`를 추가한다.
2. `settlementViewBehaviour`에 `TradeSettlementPanelController`를 연결한다.
3. `TradeSettlementPanelController.onPaymentCompleted`를 `SettlementUiDataAdapter.OnClickClaimSettlement()`에 연결한다.
4. 실제 Claim, 중복 입력 방지, 저장 갱신, Preparation 복귀는 Framework Adapter/Bridge/Coordinator가 수행한다.

## 제한

- UI는 전달된 금액을 재계산하지 않는다.
- 실패 정산도 Framework의 `CanClaim`이 true이면 Claim할 수 있다.
- 계절·재난·성장 보정 항목을 UI에서 추가하지 않는다.
