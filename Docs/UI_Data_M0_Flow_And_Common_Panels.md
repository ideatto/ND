# UI & Data M0 Flow And Common Panels

## Purpose

This document summarizes the M0 screen flow wireframe and the shared selection list / information panel structure.

UI should display values received from Data, Core, Progression, and Framework. UI should not directly calculate economy results or mutate save data.

---

## Screen Flow Wireframe

```text
[Title]
  - New Game
  - Continue
  - Save availability / continue availability state

        ↓

[Trade Prepare]
  - Current town
  - Trade money
  - Development currency
  - Current load / max load
  - Current season / disaster summary

  [Town Selection]
    - Shows unlocked / locked towns
    - Shows current town
    - Selecting a town updates available routes

  [Route Selection]
    - Shows route name
    - Shows from town → to town
    - Shows distance, estimated time, food cost, mercenary cost, risk
    - Disabled route shows disabledReason

  [Trade Item Selection]
    - Shows item name, icon, purchase price, sell price
    - Shows owned amount and selected amount
    - Disabled item shows disabledReason

  [Selected Cargo Summary]
    - Selected items
    - Total purchase cost
    - Current load / max load
    - Expected sell revenue
    - Expected profit

  [Start Button]
    - Enabled only when all departure conditions are valid
    - Disabled state shows departure disabled reason

        ↓

[Trade Progress]
  - Route name
  - From town → to town
  - Progress
  - Remaining time
  - Food / durability / risk summary
  - Occurred events

        ↓

[Settlement]
  - Success / failure
  - Route summary
  - Purchase cost
  - Sell revenue
  - Food cost
  - Mercenary cost
  - Event profit / loss
  - Lost item value
  - Net profit
  - Failure reason
  - Result messages

        ↓

[Growth / Next Action]
  - Growth purchase availability
  - Currency after settlement
  - Save status
  - Next trade
```

---

## Shared Selection List Pattern

The same list pattern should be reused for town, route, item, caravan, animal, mercenary, growth, donation, investment, and loan entries where possible.

```text
[Selection List]
  - Receives a list of ViewData
  - Displays displayName and key summary values
  - Does not display internal IDs as user-facing text
  - Has selected / unselected state
  - Has enabled / disabled state
  - Disabled entries expose disabledReason
  - Selecting an entry updates the Information Panel
```

Common fields expected by list items:

- `id`: Internal selection key. Not shown as main UI text.
- `displayName`: User-facing name.
- `icon`: Optional visual.
- `canSelect`: Whether the entry can currently be selected.
- `disabledReason`: User-facing reason when selection is blocked.

Current ViewData coverage:

- `TownViewData`: town list and current town display.
- `RouteViewData`: route list and route detail.
- `TradeItemViewData`: item purchase / sell list.
- `TradeResultViewData`: settlement result display.

Recommended small addition:

```csharp
public bool canSelect;
public string disabledReason;
```

Add these to `TownViewData` when locked towns or non-selectable towns need a visible reason.

---

## Shared Information Panel Pattern

```text
[Information Panel]
  - Receives selected ViewData
  - Shows displayName, description, and important stats
  - Shows action controls when applicable
  - Shows disabledReason when the action is unavailable
  - Does not fetch save data or calculate economy results directly
```

Panel rules:

- The list owns selection state.
- The panel only displays selected data.
- Buttons use `canSelect`, `canBuy`, `canSell`, or equivalent state.
- Economy values shown in the panel should come from Progression results or prepared ViewData.
- Core-owned state such as load, route validity, progress, and failure reason should be received as ViewData or input data.

---

## Town List And Panel

Input:

- `List<TownViewData>`

List shows:

- `displayName`
- `icon`
- `isCurrentTown`
- `isUnlocked`
- `canSelect`
- `disabledReason`

Panel shows:

- Town name
- Description
- Current town state
- Unlock state
- Available route count
- Market item count

Notes:

- `townId` is used for selection and data lookup.
- `townId` should not be shown as the main label.

---

## Route List And Panel

Input:

- `List<RouteViewData>`

List shows:

- `displayName`
- `fromTownName`
- `toTownName`
- `estimatedTime`
- `riskLevel`
- `canSelect`
- `disabledReason`

Panel shows:

- Distance
- Estimated time
- Food cost
- Mercenary cost
- Risk level
- Unlock / select state

Notes:

- `routeId` is used for selection and calculation input.
- Display should use town names, not raw town IDs.

---

## Trade Item List And Panel

Input:

- `List<TradeItemViewData>`

List shows:

- `displayName`
- `icon`
- `purchasePrice`
- `sellPrice`
- `ownedAmount`
- `selectedAmount`
- `canBuy`
- `canSell`
- `disabledReason`

Panel shows:

- Item name
- Description
- Purchase price
- Sell price
- Owned amount
- Selected amount
- Total selected price

Notes:

- Final price values should come from Progression through `PriceCalculationInput` and result data.
- UI should not duplicate price formula logic.

---

## Settlement Panel

Input:

- `TradeResultViewData`

Panel shows:

- Success / failure
- Route name
- From town / to town
- Total purchase cost
- Total sell revenue
- Food cost
- Mercenary cost
- Event profit / loss
- Loss amount
- Net profit
- Failure reason text
- Result messages

Notes:

- `SettlementInput` is calculation input.
- `TradeResultData` / `TradeResultViewData` are calculation result display data.

---

## Error And Status Message Rules

Use short user-facing messages for blocked actions.

Examples:

- `Route is not unlocked yet.`
- `Not enough trade money.`
- `Max load exceeded.`
- `Food is insufficient.`
- `Required town is not selected.`

Message ownership:

- UI decides where to show the message.
- Core / Progression / Framework should provide the reason code or reason text.
- Content can later replace fixed text with localized strings.

---

## M0 Completion Notes

M0 is considered covered when:

- Dummy data can be converted into ViewData and displayed or logged.
- IDs are kept as internal keys.
- User-facing text uses display names and descriptions.
- The same list / panel pattern can be reused for town, route, and item selection.
- Calculation inputs are separated from result display data.
