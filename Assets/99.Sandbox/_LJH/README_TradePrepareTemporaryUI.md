# Temporary Trade Prepare UI

## Create and run

1. Enter a normal game flow so `FrameworkRoot.CurrentSaveData` exists.
2. In the target scene, use `GameObject > ND > Trade Prepare Temporary UI`.
3. Enter Play Mode. The IMGUI window reads the current save but never mutates it.
4. Select route, wagon, animals, cargo, food, and mercenaries.
5. `Temporary Start` stages an in-memory commit and keeps currency unchanged.
6. `Temporary Settlement Claim` applies the preview costs/revenue once to the temporary currency.

The temporary settlement is only a hand-off and timing test. Season, disaster,
route events, loss, repair, and final Economy values still belong to the production
Framework settlement.

## Production replacement points

- Replace `TradePrepareTemporaryUI` with the final UI. Render
  `TradePrepareFlowController.CurrentViewData`, subscribe to `ViewDataChanged`, and
  send user input through the controller methods.
- Replace `TemporaryTradePrepareStartGateway` with
  `FrameworkTradePrepareStartGateway`.
- Replace `InMemoryTradePrepareCommitSink` with the Framework-owned persistent
  `ITradePrepareCommitSink` implementation.
- Remove `TemporaryTradeSettlementService`; the production Economy settlement
  must consume the committed values and apply final event-adjusted results.

The Draft, Evaluator, Builder, Caravan factory, FlowController, and ViewData do not
need to be replaced when the final UI arrives.

## Deferred: wagon durability source during preparation

This issue is intentionally kept unchanged for now so the team can decide how
owned-wagon durability and walking should behave before changing the runtime
contract.

### Current behavior

`TradePrepareViewDataBuilder.GetCurrentDurability(...)` uses
`SaveData.caravan.currentDurability` when the selected `WagonData` matches the
wagon stored in the last caravan. Otherwise, it uses `WagonData.MaxDurability`.

Because `SaveData.caravan` is the current or most recently used trade caravan,
it is not a reliable replacement for a player-owned wagon inventory. A balance
change to a wagon SO can therefore be hidden by durability left in SaveData. For
example, changing `Wagon_Walk.maxDurability` from 100 to 1 can still allow a new
trade to start with the old saved durability when SaveData has not been reset.

The current distance-wear rule is `0.1 durability per kilometer`. A walk entry
with 1 durability therefore breaks before completing a 20 km route. This result
is deterministic; it is not a random success/failure roll.

### Code location

- `01.Script/Runtime/Builder/TradePrepareViewDataBuilder.cs`
- Method: `GetCurrentDurability(SaveData saveData, WagonData wagon)`
- Related restore path: Framework `CaravanSaveDataMapper.ToRuntime(...)`

### Recommended follow-up

1. Keep active-trade restoration on `CaravanSaveDataMapper`; an already departed
   trade should continue from its saved durability snapshot.
2. For a newly prepared trade, read physical-wagon durability from the future
   player-owned wagon runtime/inventory instead of `SaveData.caravan`.
3. Decide whether `WagonType.None` (walking) should ignore wagon durability and
   distance wear. If walking still uses durability, seed it from the current SO
   instead of the previous caravan SaveData.
4. Until the owned-wagon runtime exists, a minimal compatibility fix is to use
   the SO maximum for walking and clamp saved physical-wagon durability to the
   current SO maximum.

### Regression checks for the eventual change

- Changing an SO maximum must not let a new trade use durability above that
  maximum.
- Reloading during an active trade must preserve its saved current durability.
- Starting another trade with walking must not inherit the previous caravan's
  durability.
- Physical-wagon damage must remain persistent once the owned-wagon inventory
  becomes the durability source.
- A 20 km route with starting durability 1 must either fail consistently under
  the current wear rule or succeed consistently if walking is explicitly exempt.
