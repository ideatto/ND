# M1 Trade Prepare UI Flow

## Purpose

This document defines the M1 trade preparation UI flow.

The trade preparation UI should work as a step-based draft flow.
All selections before departure are temporary draft selections.
The draft can be changed until the player confirms `Start Trade`.

```text
Before Start Trade = Draft State
After Start Trade = Committed State
```

M1 should keep the flow simple, but the data shape should leave room for future extensions such as waypoint towns, reserved selling, multiple route segments, and multi-town trading.

---

## Flow Summary

```text
Play Screen
-> Trade Button
-> Trade Prepare Open

1. Destination / Route Step
2. Transport Step
3. Cargo Step
4. Mercenary Step
5. Confirm Departure Step

-> Start Trade
-> Trade Progress UI
-> Settlement Calculation
-> Success / Failure Receipt
-> Next Action
```

---

## 1. Destination / Route Step

### Goal

Select the destination and route for the trade.

### UI Shows

- Current town
- Available destination towns
- Optional waypoint towns
- Available routes
- Route display name
- From town / to town
- Distance
- Base expected travel time
- Base required food quantity
- Base required mercenary power
- Base risk level
- Route locked reason, if blocked

### Player Can

- Select destination town
- Select optional waypoint towns
- Select route
- Clear destination or route selection
- Cancel trade preparation

### Output

```text
selectedDestinationTownId
selectedWaypointTownIds
selectedRouteId
baseExpectedTravelTime
baseRequiredFoodQuantity
baseRequiredMercenaryPower
baseRiskLevel
```

### Notes

- `RouteData.DefaultElapsedTime` is shown here as the base expected travel time.
- This is not the final travel time.
- Final travel time can change after transport and animal selection.
- UI should display town and route names, not raw IDs.

---

## 2. Transport Step

### Goal

Select wagon or mount and update travel estimates.

### UI Shows

- Available wagon / mount list
- Selected transport name
- Wagon type
- Durability
- Max load
- Overload limit
- Base move speed
- Required animal count, if any
- Animal selection section, if needed
- Updated expected travel time after transport selection

### Player Can

- Select wagon or mount
- Select animals if the wagon requires animals
- Change selected transport
- Go back to Destination / Route Step

### Output

```text
selectedWagonId
selectedAnimalIds
maxLoad
overloadLimit
selectedMoveSpeed
finalExpectedTravelTimePreview
```

### Notes

- `WagonWithAnimals` requires animal selection.
- `Mount` does not require pull animals.
- For `WagonWithAnimals`, movement speed can be derived from selected animals.
- UI should not directly calculate final travel time.
- Core or a calculator should provide `finalExpectedTravelTimePreview`.

---

## 3. Cargo Step

### Goal

Load food and select trade items.

### UI Shows

- Current load
- Overload limit
- Max load
- Required food quantity
- Loaded food quantity
- Food shortage warning, if any
- Buy item list from current town market
- Sell item list from current inventory, if selling before departure is allowed
- Item display name
- Purchase price
- Sell price
- Owned amount
- Selected buy amount
- Selected sell amount
- Total purchase cost
- Expected sell revenue
- Load warning or blocking reason

### Player Can

- Load food
- Select items to buy
- Select buy quantity
- Select items to sell or unload, if supported
- Select sell quantity
- Clear selected cargo
- Go back to Transport Step

### Output

```text
loadedFoodQuantity
selectedBuyItems
selectedSellItems
currentLoad
totalPurchaseCost
expectedSellRevenue
```

### Notes

- Food shortage may be a warning instead of a blocker.
- Current load above overload limit may be a warning.
- Current load above max load should block departure.
- UI should use prepared ViewData values.
- UI should not recalculate final prices.
- For M1, buying from the current town market is the default path.
- Selling before departure is optional. Selling after arrival can be handled by settlement or a later flow.

---

## 4. Mercenary Step

### Goal

Hire mercenaries and compare selected power with route requirement.

### UI Shows

- Base required mercenary power
- Selected mercenary power
- Mercenary hire cost
- Current trade money
- Mercenary shortage warning, if selected power is too low

### Player Can

- Hire mercenaries
- Reduce selected mercenary power
- Go back to Cargo Step

### Output

```text
selectedMercenaryPower
mercenaryCost
```

### Notes

- Mercenary shortage may allow departure with a warning.
- Combat risk can increase when selected power is lower than required.
- The final warning should be shown again in Confirm Departure Step.

---

## 5. Confirm Departure Step

### Goal

Show the final trade summary and allow departure.

### UI Shows

- Current town
- Destination town
- Waypoints
- Selected route
- Selected wagon / mount
- Selected animals
- Loaded food quantity
- Selected buy items
- Selected sell items
- Selected mercenary power
- Base expected travel time
- Final expected travel time
- Required food quantity
- Required mercenary power
- Current load / overload limit / max load
- Total purchase cost
- Expected sell revenue
- Warnings
- Blocking reasons
- Start Trade button

### Player Can

- Start trade
- Go back to any previous step
- Change draft selections
- Cancel trade preparation

### Output

```text
TradePrepareDraft
TradePrepareConditionResult
finalExpectedTravelTime
```

### Start Button Rule

```text
If canStart == true:
  Start Trade button is enabled.

If canStart == false:
  Start Trade button is disabled.
  disabledReason is shown.
```

### Warning Rule

Warnings do not always block departure.

Warning examples:

```text
Food is insufficient.
Mercenary power is insufficient.
Current load is above overload limit.
```

Blocking examples:

```text
Route is not selected.
Route is locked.
Not enough trade money.
Current load is above max load.
```

---

## Start Trade Commit Rule

When the player presses `Start Trade`:

```text
1. Validate TradePrepareDraft.
2. Commit draft into trade progress state.
3. Update runtime trade state.
4. Close Trade Prepare UI.
5. Open Trade Progress UI.
```

After this point:

```text
Undo is no longer allowed.
Draft selection is no longer editable.
Trade progress state becomes the source of truth.
```

---

## Trade Progress UI

### UI Shows

- Route name
- From town
- To town
- Final expected travel time
- Remaining time
- Progress
- Risk summary
- Loaded food summary
- Durability summary
- Occurred events, if any

### Notes

- Trade progress UI should not modify the committed draft.
- Progress state should come from Core / Framework runtime state.

---

## Trade Completion Flow

```text
Trade Progress Complete
-> Settlement Calculation
-> Success / Failure Receipt
-> Confirm Receipt
-> Apply / show next action
```

Settlement calculation should happen before the receipt is shown.
The receipt displays the result of settlement.
It should not calculate settlement by itself.

---

## Success Receipt

### UI Shows

- Success state
- Route summary
- Purchased items
- Sold items
- Total purchase cost
- Total sell revenue
- Food cost
- Mercenary cost
- Event profit / loss
- Net profit
- Result messages

---

## Failure Receipt

### UI Shows

- Failure state
- Failure reason
- Route summary
- Lost item value
- Food cost
- Mercenary cost
- Event loss
- Net profit or loss
- Result messages

---

## Data Responsibility Rules

### UI Should Display

```text
RouteViewData
TradeItemViewData
TradePrepareViewData
TradePrepareConditionResult
TradeResultViewData
```

### UI Should Not

```text
Calculate final travel time directly
Recalculate final buy price
Recalculate final sell price
Recalculate net profit
Mutate SaveData directly
Commit draft before Start Trade
```

### Core / Calculator Should Provide

```text
finalExpectedTravelTime
requiredFoodQuantity
requiredMercenaryPower
currentLoad
totalPurchaseCost
expectedSellRevenue
startCondition
settlement result
trade result
```

---

## Recommended Draft Data

M1 can use a simple draft shape.
Fields for future extensions can exist but remain empty or unused.

```csharp
public class TradePrepareDraft
{
    public string currentTownId;

    public string selectedDestinationTownId;
    public string[] selectedWaypointTownIds;

    public string selectedRouteId;

    public string selectedWagonId;
    public string[] selectedAnimalIds;

    public int loadedFoodQuantity;

    public TradeItemBundle[] selectedBuyItems;
    public TradeItemBundle[] selectedSellItems;

    public int selectedMercenaryPower;
}
```

---

## Recommended View Data Additions

```csharp
public class TradePrepareViewData
{
    public float baseExpectedTravelTime;
    public float finalExpectedTravelTime;
    public float selectedMoveSpeed;

    public int requiredFoodQuantity;
    public int loadedFoodQuantity;

    public int requiredMercenaryPower;
    public int selectedMercenaryPower;

    public float currentLoad;
    public float overloadLimit;
    public float maxLoad;

    public TradePrepareConditionResult startCondition;
}
```

---

## Future Extension Points

M1 uses a single destination and a simple trade plan.
The draft and flow should remain compatible with the following future features.

### Waypoint Towns

Future flow:

```text
Start town
-> Waypoint town
-> Destination town
```

Possible data:

```csharp
public string[] selectedWaypointTownIds;
```

For M1, this can remain empty.

### Multiple Route Segments

Future route data may need more than one route ID.

```csharp
public class RouteSegmentDraft
{
    public string fromTownId;
    public string toTownId;
    public string routeId;
}
```

For M1, `selectedRouteId` is enough.
When waypoint support is added, it can be expanded to route segments.

### Reserved Selling

Reserved selling means the player plans where items will be sold before departure.

```csharp
public class ReservedSellDraft
{
    public string targetTownId;
    public string itemId;
    public int quantity;
}
```

For M1, selling can be handled simply through selected sell items or settlement.
Reserved selling can be added later if needed.

### Multi-Town Trade Plan

If the player can buy and sell at multiple towns, the draft can evolve into a stop-based plan.

```csharp
public class TradePlanDraft
{
    public string startTownId;
    public TradePlanStopDraft[] stops;
    public RouteSegmentDraft[] routeSegments;

    public string selectedWagonId;
    public string[] selectedAnimalIds;

    public int loadedFoodQuantity;
    public int selectedMercenaryPower;
}

public class TradePlanStopDraft
{
    public string townId;
    public TradeItemBundle[] plannedBuyItems;
    public TradeItemBundle[] plannedSellItems;
}
```

For M1, this full structure is not required.
The simple `TradePrepareDraft` should be enough.

---

## M1 Implementation Priority

1. Destination / route selection
2. Wagon / mount selection
3. Food and cargo selection
4. Trade prepare condition validation
5. Confirm departure step
6. Trade progress UI
7. Success / failure receipt
8. Settlement display

---

## Stable Rules

- SaveData should store IDs and values, not ScriptableObject references.
- UI edits draft data before departure.
- Core commits draft data when `Start Trade` is confirmed.
- Route step shows base expected travel time.
- Transport step updates expected travel time preview.
- Confirm step shows final expected travel time.
- Receipt displays calculated settlement results.
- Receipt does not calculate settlement by itself.
