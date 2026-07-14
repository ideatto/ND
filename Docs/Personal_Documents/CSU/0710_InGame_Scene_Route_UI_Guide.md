UI 작업자는 **Framework 이벤트를 구독해서 화면 전환 신호를 받고**, 필요한 상세 데이터는 `FrameworkRoot.Instance`에서 읽어가면 됩니다.

핵심 진입점은 이것입니다.

```csharp
var root = FrameworkRoot.Instance;
var saveData = root.CurrentSaveData;
var coordinator = root.TradeProgressCoordinator;
```

## 1. 화면 전환은 이벤트로 받기

UI 쪽 MonoBehaviour에서 `FrameworkEvents.InGameScreenChanged`를 구독하면 됩니다.

```csharp
using UnityEngine;
using ND.Framework;

public sealed class InGameScreenPresenter : MonoBehaviour
{
    private void OnEnable()
    {
        FrameworkEvents.InGameScreenChanged += OnInGameScreenChanged;

        var root = FrameworkRoot.Instance;
        if (root != null && root.InGameScreenRouter != null)
        {
            OnInGameScreenChanged(root.InGameScreenRouter.CurrentScreenState);
        }
    }

    private void OnDisable()
    {
        FrameworkEvents.InGameScreenChanged -= OnInGameScreenChanged;
    }

    private void OnInGameScreenChanged(InGameScreenState state)
    {
        switch (state)
        {
            case InGameScreenState.Preparation:
                // 준비 화면 패널 표시
                break;

            case InGameScreenState.Traveling:
                // 이동 진행 화면 패널 표시
                break;

            case InGameScreenState.Settlement:
                // 정산 화면 패널 표시
                break;
        }
    }
}
```

`InGameSceneController`가 씬 진입 시 한 번 강제 refresh를 해주므로, 보통은 씬 진입 시 현재 화면 상태 이벤트를 받을 수 있습니다. 그래도 UI 오브젝트 활성화 순서 때문에 놓칠 수 있으니, `OnEnable`에서 `CurrentScreenState`를 한 번 직접 읽어 적용하는 방식이 안전합니다.

## 2. 준비 화면 데이터 가져오기

준비 화면에서는 저장 데이터에서 플레이어와 상단 정보를 읽으면 됩니다.

```csharp
var saveData = FrameworkRoot.Instance.CurrentSaveData;

var player = saveData.player;
var caravan = saveData.caravan;
var tradeProgress = saveData.tradeProgress;
```

예시로 사용할 수 있는 값:

- `saveData.player.tradingCurrency`
- `saveData.player.developmentCurrency`
- `saveData.caravan.foodAmount`
- `saveData.caravan.cargo`
- `saveData.caravan.wagon`
- `saveData.caravan.animals`
- `saveData.caravan.mercenaries`

## 3. 이동 화면 데이터 가져오기

이동 화면에서는 현재 진행 상태와 남은 시간/도착 예정 시간을 표시할 수 있습니다.

```csharp
var progress = FrameworkRoot.Instance.CurrentSaveData.tradeProgress;

var tradeId = progress.activeTradeId;
var routeId = progress.activeRouteId;
var startTicks = progress.tradeStartUtcTick;
var endTicks = progress.expectedTradeEndUtcTick;
```

상단의 런타임 진행률이 필요하면 coordinator에서 active caravan을 볼 수 있습니다.

```csharp
var caravan = FrameworkRoot.Instance.TradeProgressCoordinator.ActiveCaravan;
var progress01 = caravan.progress01;
```

단, UI는 값을 표시만 하고 진행 상태를 직접 바꾸지 않는 것이 좋습니다.

## 4. 정산 화면 데이터 가져오기

정산 화면 진입 신호는 두 가지로 받을 수 있습니다.

화면 전환용:

```csharp
FrameworkEvents.InGameScreenChanged += OnInGameScreenChanged;
```

정산 상세 데이터용:

```csharp
FrameworkEvents.TradeSettlementReady += OnTradeSettlementReady;
```

예시:

```csharp
private JourneyResultData currentSettlementResult;

private void OnEnable()
{
    FrameworkEvents.TradeSettlementReady += OnTradeSettlementReady;
}

private void OnDisable()
{
    FrameworkEvents.TradeSettlementReady -= OnTradeSettlementReady;
}

private void OnTradeSettlementReady(string tradeId, JourneyResultData result)
{
    currentSettlementResult = result;

    // result.grade
    // result.failureReason
    // result.cargoLost
}
```

또는 최근 정산 결과는 여기서도 읽을 수 있습니다.

```csharp
var result = FrameworkRoot.Instance.TradeProgressCoordinator.LastSettlementResult;
```

## 정리

UI 작업자가 가져가야 하는 연결 포인트는 다음 3개입니다.

- 화면 전환: `FrameworkEvents.InGameScreenChanged`
- 저장/현재 상태: `FrameworkRoot.Instance.CurrentSaveData`
- 정산 결과: `FrameworkEvents.TradeSettlementReady` 또는 `TradeProgressCoordinator.LastSettlementResult`

Framework는 “어떤 화면을 보여줄지”만 알려주고, UI는 그 상태를 받아 패널 표시와 텍스트 갱신을 담당하면 됩니다.