# InGame Scene State Routing

## Purpose

Connect the trade state managed by Core and Framework to the actual InGame UI.

Currently, when a trade is completed, the following flow occurs:

```text
JourneyRunner.Settle
→ TradeProgressState.SettlementPending
→ TradeSettlementReady event is raised
```

However, an additional connection is still required to receive this event and update the actual screen.

## Features to Implement

- Display the preparation screen.
- Display the travel progress screen.
- Display the settlement screen.
- Prevent duplicate requests for the same screen transition.
- Determine the initial screen from the current saved state.
- Restore the correct screen when re-entering the InGame scene.

## Recommended Screen State

```csharp
public enum InGameScreenState
{
    Preparation,
    Traveling,
    Settlement
}
```

## State Mapping

| Saved or Runtime State | Screen to Display |
|---|---|
| `Prepare` | Trade preparation screen |
| `Ready` | Trade preparation screen |
| `Traveling` | Trade progress screen |
| `Arrived` | Settlement screen or settlement preparation handling |
| `SettlementPending` | Settlement screen |
| `Completed` | Trade preparation screen |
| `Failed` | Failed-trade settlement screen |

## Recommended Flow

```text
Enter the InGame scene
→ Check the current TradeProgressState
→ Determine the appropriate screen state
→ Display the corresponding screen
```

Real-time state changes should be handled as follows:

```text
Trade departure succeeds
→ Show the Traveling screen

TradeSettlementReady is raised
→ Show the Settlement screen

Settlement is claimed and the trade state is reset
→ Show the Preparation screen
```

## Responsibility Boundary

Framework should determine which screen must be displayed, rather than directly managing individual UI objects.

```text
Framework
→ Determines which screen should be displayed

UI
→ Activates panels, updates text, and connects buttons
```

For example, Framework may expose only a screen-state event such as:

```csharp
public static event Action<InGameScreenState> InGameScreenChanged;
```

The UI layer can subscribe to this event and apply the corresponding panel and presentation changes.

## Completion Criteria

- Starting a trade changes the screen to the travel progress screen.
- Arriving at the destination changes the screen to the settlement screen.
- Completing settlement returns the screen to the trade preparation screen.
- Re-entering the InGame scene opens the screen that matches the current saved state.
- Duplicate events do not create or apply the same screen more than once.
