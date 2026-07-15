# 용병 고용 패널 Prefab 연결 인계

## 1차 빌드 범위

- 용병 패널은 제출 동선에 포함한다.
- 비용과 전투력은 `TradePrepareViewDataBuilder` 결과만 표시한다.
- UI에서 비용이나 전투력을 다시 계산하지 않는다.
- 용병 선택은 선택 사항이며, 미선택 상태에서도 확인 버튼으로 다음 단계에 갈 수 있다.
- 전투, 약탈, 위험도 상쇄 실기능은 2차 빌드 범위로 남긴다.

## 산출물

- Runtime component: `MercenarySelectionPanel.cs`
- Prefab: `Assets/_Project/08.Prefabs/UI/Trade/MercenarySelectionPanel.prefab`
- Generator: `Assets/_Project/05.UI/03_TradeSetup/YHY/Editor/MercenarySelectionPrefabGenerator.cs`

## 연결 방법

1. 준비 UI 흐름에서 아이템 적재와 요약 사이에 Prefab을 배치한다.
2. `TradePrepareFlowController.ViewDataChanged`를 구독해 `MercenarySelectionPanel.Bind(viewData)`를 호출한다.
3. `SelectionRequested(id, selected)` 처리:
   - `selected == true`: `TradePrepareFlowController.SelectMercenary(id)`
   - `selected == false`: `TradePrepareFlowController.DeselectMercenary(id)`
4. `Confirmed` 이벤트는 다음 요약 패널 전환에 연결한다.
5. `TradePrepareBuildContext.mercenaries`에 팀이 확정한 `MercenaryData[]`를 공급한다.

## 표시 필드

- `viewData.mercenaries`
- `viewData.selectedMercenaryPower`
- `viewData.requiredMercenaryPower`
- `viewData.currentTradingCurrency`
- `viewData.mercenaryCost`

## 주의

- 기존 `UI_TradePrepare.prefab`과 `TradePrepareFlowDemo.cs`는 수정하지 않았다.
- Prefab의 이벤트는 연결팀이 최종 통합 Prefab/Scene에서 연결한다.
- `TemporaryTradeSettlementService`, `TemporaryTradePrepareStartGateway`는 제출 빌드에 연결하지 않는다.
