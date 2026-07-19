# Settlement Recovery and Trade ID Contract

## Identity

Departure commit generates a new `Guid.NewGuid().ToString("D")` value. SaveData stores the complete string. A pending settlement is uniquely identified by `caravanId + tradeId`; both are required in every settlement command, query, result, and event.

## Finalization and recovery

Settlement calculation consumes the confirmed departure/runtime originals and shared definitions once. Before UI notification, Framework persists a complete confirmed result snapshot sufficient to render and claim without recalculation. Restart restores that exact snapshot. The ID-based collection supports multiple pending entries.

The current single pending DTO and runtime cache may be compatibility fallbacks only. Once collection cutover occurs, they cannot overwrite collection entries and the collection is the source of truth.

## Claim contract

`ClaimSettlement(caravanId, tradeId)`:

1. Rejects missing, malformed, mismatched, already claimed, or duplicate keys without mutation.
2. Stages the exact reward application, completed trade state, and matching pending removal.
3. Saves the staged aggregate immediately.
4. On save failure reports failure and leaves the externally visible durable state uncommitted.
5. On success publishes one completion event and clears prepared goods/food while preserving fixed setup.

No player-facing settlement history is retained. A consumed-key/tombstone or equivalent idempotency marker may be kept only as needed to prevent duplicate payment and must have a bounded lifecycle policy.

## Event direction

Current `Action<string, JourneyResultData>` settlement events carry only `tradeId`. Target events carry a value payload with `caravanId`, full `tradeId`, and immutable/read-only result data. Subscribers must unsubscribe with their lifecycle and treat repeated delivery as possible notification replay, never as permission to pay again.

