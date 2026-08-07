# Settlement Recovery and Trade ID Contract

## Arrival-sale recovery boundary

`Arrival_Sale_Settlement_Claim_Policy.md` is canonical. Restore expects Core `JourneyState.Settling` with Framework `TradeProgressState.SettlementPending`; confirmed Cargo, market-stock, and currency effects persist, but runtime drafts, itemized ledgers, and presentation requests do not.

Multi-Caravan support remains partial: global `LastSettlementResult`, legacy singular pending restore, and shared arrival-sale button resolution remain compatibility risks. The ID-keyed target below does not mean those legacy paths are fully removed.

## Identity

Departure commit generates a new `Guid.NewGuid().ToString("D")` value. SaveData stores the complete string. A pending settlement is uniquely identified by `caravanId + tradeId`; both are required in every settlement command, query, result, and event.

## Finalization and recovery

Settlement calculation consumes the confirmed departure/runtime originals and shared definitions once. Before UI notification, Framework persists a complete confirmed result snapshot sufficient to render and claim without recalculation. Restart restores that exact snapshot. The ID-based collection supports multiple pending entries.

The version 6 `pendingSettlements[]` collection is the current persisted source of truth and remains so in the approved version 7 target. The selected-Caravan `pendingSettlement` property and runtime cache are compatibility views only and cannot overwrite unrelated collection entries.

Normal data permits at most one unresolved pending settlement per Caravan. Exact duplicate `(caravanId, tradeId)` entries are invalid. Two different `tradeId` values for one `caravanId` are ambiguous recovery data: report a visible recovery error and block automatic Depart/Claim for that Caravan. Recovery must not silently select or delete either entry; automated repair remains a follow-up implementation decision.

## Claim contract

`ClaimSettlement(caravanId, tradeId)`:

1. Rejects missing, malformed, mismatched, already claimed, or duplicate keys without mutation.
2. Stages the exact reward application, completed trade state, matching pending removal, and preparation cleanup.
3. Sets any legacy settlement `LoanRepayment` input to zero and does not mutate rescue-loan principal.
4. Saves the staged aggregate immediately.
5. On save failure reports failure and leaves the externally visible durable state uncommitted.
6. On success publishes `SettlementClaimed(caravanId, tradeId)` once for that claimed entry and clears prepared goods/food. Success and partial success preserve fixed transport setup. A failed settlement is the explicit exception: its Claim atomically removes the Caravan's equipped wagon and draft animals from owned inventories and clears its wagon, animals, cargo, food, durability, and failure references.

Failed-settlement transport loss is staged inside the same SaveData snapshot and rollback boundary as Claim. Save failure restores the Caravan, owned transport inventories, matching pending settlement, and preparation commit. Save success may then show a non-durable acknowledgement Popup; that Popup does not define the commit boundary.

Claim applies travel settlement economy only. Cargo-sale proceeds were already credited by successful sale-confirm Save and are excluded from `JourneyResultData`, preventing duplicate payout.

Settlement finalization follows the same commit boundary: stage pending state, Save once, require `SaveResult` success, commit runtime state, then publish `TradeSettlementReady(caravanId, tradeId, result)`. Save failure rolls back the staged tick/batch and publishes no committed Event or forced screen transition.

`TradeSettlementReady` means a saved result is available to view and claim; it never means that rewards were paid. `SettlementClaimed` means reward application, matching pending removal, Save success, and runtime commit completed. Claim Save failure publishes neither a claimed Event nor a success UI transition.

Settlement finalization, pending snapshots, and Claim contain no rescue-loan repayment choice or amount. A non-positive or positive settlement payout never changes `remainingPrincipal`. Partial or full repayment is performed only through the separate `RepayRescueLoan(long amount)` command after restricted preparation has ended. Settlement reward application, trade state, pending removal, preparation cleanup, aggregate save, completion event, and UI refresh remain one staged transaction; rescue-loan repayment is a different transaction with its own currency/loan snapshot and rollback.

No player-facing settlement history is retained. A consumed-key/tombstone or equivalent idempotency marker may be kept only as needed to prevent duplicate payment and must have a bounded lifecycle policy.


## Event direction

Current production `Action<string caravanId, string tradeId, JourneyResultData>` already carries both IDs, but finalization does not yet enforce Save-success publication timing and the mutable payload remains a follow-up risk. Target events retain both IDs and use an immutable/read-only result snapshot when introduced. Subscribers must unsubscribe with their lifecycle and treat repeated delivery as possible notification replay, never as permission to pay again.

Current production numeric version is 6. The approved next numeric version is 7; v6 test saves are reset explicitly rather than automatically migrated.
