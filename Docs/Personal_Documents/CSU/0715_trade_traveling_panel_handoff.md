# Traveling 패널 연결 인계

- `TradeTravelingPanelController`는 Framework SaveData의 실제 UTC 시작·종료 시각과 Caravan 상태를 표시한다.
- 진행률, 남은 시간, 현재 식량, 식량 부족 상태를 표시한다.
- 식량은 `CaravanSaveDataMapper`와 `CaravanCalculator.GetRemainingFood()` 결과를 사용하며 UI에서 공식을 재구현하지 않는다.
- 무역 취소는 1차 제외 범위이므로 제공하지 않는다.
- 패널은 표시 전용이며 완료·정산 상태 변경은 `TradeProgressCoordinator`가 담당한다.
- Prefab: `Assets/_Project/08.Prefabs/UI/Trade/TradeTravelingPanel.prefab`
- 통합팀은 `InGameScreenState.Traveling`에서 활성화하고 다른 상태에서는 비활성화한다.
