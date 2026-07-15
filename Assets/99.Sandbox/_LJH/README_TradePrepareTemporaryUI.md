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
