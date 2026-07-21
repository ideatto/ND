# Settlement Recovery and Trade ID Contract

## Identity

Departure commit generates a new `Guid.NewGuid().ToString("D")` value. SaveData stores the complete string. A pending settlement is uniquely identified by `caravanId + tradeId`; both are required in every settlement command, query, result, and event.

## Finalization and recovery

Settlement calculation consumes the confirmed departure/runtime originals and shared definitions once. Before UI notification, Framework persists a complete confirmed result snapshot sufficient to render and claim without recalculation. Restart restores that exact snapshot. The ID-based collection supports multiple pending entries.

The version 6 `pendingSettlements[]` collection is the persisted source of truth. The selected-Caravan `pendingSettlement` property and runtime cache are compatibility views only and cannot overwrite unrelated collection entries.

Normal data permits at most one unresolved pending settlement per Caravan. Exact duplicate `(caravanId, tradeId)` entries are invalid. Two different `tradeId` values for one `caravanId` are ambiguous recovery data: report a visible recovery error and block automatic Depart/Claim for that Caravan. Recovery must not silently select or delete either entry; automated repair remains a follow-up implementation decision.

## Claim contract

`ClaimSettlement(caravanId, tradeId)`:

1. Rejects missing, malformed, mismatched, already claimed, or duplicate keys without mutation.
2. Stages the exact reward application, completed trade state, matching pending removal, and preparation cleanup.
3. Sets any legacy settlement `LoanRepayment` input to zero and does not mutate rescue-loan principal.
4. Saves the staged aggregate immediately.
5. On save failure reports failure and leaves the externally visible durable state uncommitted.
6. On success publishes one completion event and clears prepared goods/food while preserving fixed setup.

Settlement finalization, pending snapshots, and Claim contain no rescue-loan repayment choice or amount. A non-positive or positive settlement payout never changes `remainingPrincipal`. Partial or full repayment is performed only through the separate `RepayRescueLoan(long amount)` command after restricted preparation has ended. Settlement reward application, trade state, pending removal, preparation cleanup, aggregate save, completion event, and UI refresh remain one staged transaction; rescue-loan repayment is a different transaction with its own currency/loan snapshot and rollback.

No player-facing settlement history is retained. A consumed-key/tombstone or equivalent idempotency marker may be kept only as needed to prevent duplicate payment and must have a bounded lifecycle policy.


## Event direction

Current `Action<string, JourneyResultData>` settlement events carry only `tradeId`. Target events carry a value payload with `caravanId`, full `tradeId`, and immutable/read-only result data. Subscribers must unsubscribe with their lifecycle and treat repeated delivery as possible notification replay, never as permission to pay again.
