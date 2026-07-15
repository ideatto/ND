# Trade Prepare Runtime Context 연결 인계

## 목적

`TradePrepareViewDataBuilder`와 `TradePrepareFlowController`를 제출 UI에서 사용할 수 있도록 공식 SaveData와 콘텐츠 에셋을 공급한다. 기존 패널과 Builder는 수정하지 않는다.

## 산출물

- `TradePrepareRuntimeContextProvider.cs`
- `TradePrepareRuntimeContext.prefab`
- `TradePrepareRuntimeContextPrefabGenerator.cs`

## 데이터 연결

Prefab Inspector에 다음 공식 에셋만 연결한다.

- Towns
- Routes
- Trade Items
- Wagons
- Draft Animals
- Mercenaries

`Assets/99.Sandbox` 에셋이 연결되면 Provider가 Editor 오류를 출력한다. 제출 데이터는 `_Project/02.Data` 등 팀이 확정한 공식 에셋만 사용한다.

## UI 연결

1. 통합 씬 또는 통합 UI Prefab에 `TradePrepareRuntimeContext.prefab`을 배치한다.
2. 각 패널은 `ViewDataChanged`를 구독하고 필요한 값을 표시한다.
3. 사용자 입력은 Provider의 `SelectDestination`, `SelectRoute`, `SelectWagon`, `SetAnimalQuantity`, `SetBuyItemQuantity`, `SelectMercenary`, `DeselectMercenary`로 전달한다.
4. 출발 버튼은 `TryStartTrade(tradeId)`를 호출한다.
5. 출발 실패 시 반환된 `TradePrepareStartResult.errorMessage` 또는 `prepareCondition.disabledReason`을 표시한다.

## 정산 비용 보존 주의

상품 구매나 용병 비용이 있으면 `ITradePrepareCommitSink`의 제출용 구현체가 필요하다. 현재 `InMemoryTradePrepareCommitSink`는 Temporary 코드이므로 제출에 사용하지 않는다. 제출용 CommitSink가 연결되지 않으면 StartAdapter가 출발을 차단하도록 유지한다.

## 1차 빌드 제한

- UI에서 가격·적재량·전투력을 재계산하지 않는다.
- 계절·재난·성장 Modifier가 들어 있는 Sandbox 더미 상품을 사용하지 않는다.
- 용병 UI는 틀을 포함할 수 있으나 전투·약탈 실기능은 연결하지 않는다.
- 기존 Scene/Prefab 연결은 담당 팀이 SceneOwner 협의 후 수행한다.
