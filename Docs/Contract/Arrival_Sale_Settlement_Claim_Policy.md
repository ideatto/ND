# Arrival Cargo Sale, Settlement Presentation, and Claim Policy

## Status and canonical scope

This is the canonical current-product contract for arrival cargo sale, settlement presentation, Claim, persistence, and restore. It documents existing behavior and authorizes no implementation change. Specialized SaveData, command/event, routing, and milestone documents defer to it for this flow.

## Current product lifecycle

```text
Prepare
→ Traveling
→ JourneyRunner.Settle
→ JourneyState.Settling
→ TradeProgressState.SettlementPending
→ arrival cargo sale UI
→ optional sale confirmation
→ settlement presentation
→ Claim
→ Completed
→ Prepare
```

Core creates the travel result and Framework records a durable pending settlement. A successful or partially successful arrival waits for the player to sell some, all, or none of the remaining Cargo. Failed grades may bypass arrival cargo sale and proceed directly to settlement presentation. `PresentSettlement` is the post-sale gate. Claim applies separate travel settlement economy and completes the lifecycle.

- `SettlementPending`: Framework has a durable pending travel result.
- Arrival cargo sale: a UI sub-flow for selecting remaining Cargo.
- Settlement presentation: Framework exposes the existing result to the settlement UI.
- Claim: the travel-settlement transaction that completes and resets the trade.

## Core and Framework state contracts

`JourneyState.Selling` is an optional Core extension point for a future explicit selling-domain state. The current product does not transition into it: Core remains in `JourneyState.Settling` while Framework uses `TradeProgressState.SettlementPending`.

`JourneyRunner.BeginSettlement` and `JourneyRunner.CancelSettlement` have no product callers and are not part of the current arrival-sale path.

```text
Prepare = 0
Traveling = 1
Settling = 2
Completed = 3
Selling = 4
```

These serialized integer values must not be reordered without a SaveData version migration. Activating `JourneyState.Selling` requires prior alignment of `TradeProgressState`, SaveData, pending restore, routing, Multi-Caravan restore, asset locking, and Claim.

## Arrival cargo sale

Sale quantity selection is a runtime-only draft. Editing it does not mutate original Cargo. Closing the UI or exiting discards it; restoration is not guaranteed.

Partial selling is supported. Selling all Cargo is optional, unsold Cargo remains in the caravan, and an empty draft may continue to settlement presentation. Existing transaction behavior removes Cargo entries reduced to zero.

Current arrival sale resolves its sell unit price from `TradeItemData.BaseSellPrice` and eligible modifiers at transaction commit, not from a destination-market dynamic stock price.

### Seasonal SellPrice policy

Seasonal `TradeItem` pricing applies to `SellPrice` at market sale commit. Seasonal `BuyPrice` is not enabled by this policy. The Season ID present at the durable market sale transaction determines the applicable modifier; neither the departure season nor the Claim-time season determines the sale price.

The stable technical Season IDs are `spring`, `summer`, `autumn`, and `winter`. A matching modifier must have `ModifierType` `Season`, must target `SellPrice` or its runtime equivalent that includes `SellPrice`, and must have a `SourceId` that exactly matches the current canonical Season ID. Matching is ordinal and case-sensitive. Empty, invalid, non-canonical, or non-matching `SourceId` values do not match. `DisplayName` is presentation-only and is never a season identifier.

The market transaction supplies the commit-time season context. `SeasonalSellPriceModifierSelector` determines modifier eligibility, and `PriceCalculator` remains the arithmetic source of truth. `PriceCalculator` does not query global calendar state.

The adjusted unit price and quantity-derived total revenue are committed during the durable market sale transaction. Claim reuses that committed item-sale result and does not reprice item sales. This policy requires no SaveData version change or migration.

### Durable sale-confirm transaction

`MarketTransactionCommand.Execute`, reached through `CaravanArrivalSaleController.ConfirmSaleAndOpenSettlement`, owns the transaction over caravan Cargo, destination market stock, and player `tradingCurrency`.

1. Validate and calculate.
2. Capture the pre-command rollback snapshot.
3. Apply Cargo, market-stock, and currency mutations.
4. Save.
5. On Save failure, restore all three and publish no success event.
6. On Save success, publish `CaravanCargoChanged` and `TradingCurrencyChanged`.

Sale confirmation and Claim are separate commands. Sale proceeds are credited during successful sale-confirm Save, before Claim. `JourneyResultData` excludes item-trade revenue and FrameworkEconomy item-trade calculation is disabled for this path. Claim must not pay cargo-sale revenue again.

A future Framework wrapper may add bookkeeping only after accounting for this boundary. It must not duplicate calculation, mutation, Save, rollback, or post-save events.

### Explicitly deferred pricing work

Sale-panel display alignment, Seasonal `BuyPrice`, the distance multiplier, lightning jackpot payout, and positive-profit-only settlement bonuses are outside this policy. This document does not claim that the sale panel displays the adjusted committed price or that distance and lightning pricing are Production-wired.

## Settlement presentation and Claim

`SettlementUiBridge.PresentSettlement(caravanId, tradeId)` presents the pending result after the arrival-sale gate. `TradeProgressCoordinator.ClaimSettlement(caravanId, tradeId)` is the canonical product Claim using explicit `caravanId` and full `tradeId`. Claim applies travel settlement economy, removes the matching pending result, completes the lifecycle, and resets the caravan to Prepare. Legacy selected-caravan helpers are compatibility paths only.

For `JourneyResultGrade.Failed`, Claim additionally consumes the claimed Caravan's complete equipped transport composition and remaining load. The equipped wagon and draft animals are removed from their owned inventories; wagon, animals, cargo, food, durability, and failure references are cleared on the Caravan. This mutation belongs to the same snapshot, Save, and rollback transaction as Claim. It never applies to successful or partial-success settlement.

After a failed Claim Save succeeds, UI may show the placed `TradeFailureLossPopup` acknowledgement. The next failed pending settlement is not presented until that Popup is confirmed. If no failed pending remains, Town stays active. The Popup is presentation only and must not be instantiated by runtime code.

`TradeSettlementReady` and `SettlementReady`, where present, concern result readiness or presentation; they do not authorize sale calculation or payout. No `EnterSelling`, `SalesConfirmed`, `CargoSold`, or `SaleRestored` API is part of the current contract.

## Persistence and restore

Persisted:

- remaining caravan Cargo;
- destination market stock after confirmed sale;
- player `tradingCurrency` after confirmed sale;
- `JourneyState.Settling`;
- `TradeProgressState.SettlementPending`;
- pending travel settlement result.

Not persisted:

- sale draft quantities or separate `SaleSaveData`;
- detailed confirmed-sale ledger;
- product-flow `JourneyState.Selling`;
- runtime settlement-presentation request flags.

Confirmed effects survive restart through canonical Cargo, market stock, and currency data. Unsold Cargo survives normally, and claimed settlements do not reappear. Runtime drafts, itemized ledger reconstruction, and automatic restoration of a runtime-only presentation request are not guaranteed. Restore expects Core `JourneyState.Settling`.

## Multi-Caravan and routing

Sale targets explicit `caravanId`; presentation and Claim target `caravanId + full tradeId`. `selectedCaravanId` is a UI facade, not durable identity. Multiple pending settlements may coexist, and one caravan must not overwrite another.

Current limitations remain: global `LastSettlementResult` is a compatibility risk; some restore logic uses a legacy singular pending path; and a shared arrival-sale button may be ambiguous for multiple pending caravans. Complete Multi-Caravan pending restoration is therefore not guaranteed.

Framework stays in `SettlementPending` while arrival sale is an overlay/sub-flow. `PresentSettlement` enters presentation. There is no current `InGameScreenState.Selling`.

## Ownership and future extensions

This policy does not reassign ownership: existing UI/Market owners retain sale implementation/UI; Core retains `JourneyState`; Framework & Integration retains SaveData, transaction, restore, event, and Multi-Caravan integration; Economy/Progression retains price and economy rules.

Optional `JourneyState.Selling`, persisted drafts, an itemized ledger, dynamic price providers, a per-caravan runtime registry, full all-caravan pending restore, and dedicated Selling routing are not current product contracts. Each requires separate planning, owner agreement, SaveData impact review, and regression testing.

