# Multi-Caravan Save Architecture

## Ownership

Framework owns persistence DTOs, normalization, stable key lookup, UTC timestamps, save outcomes, and integration events. Core owns gameplay validation and calculations. Progression/System or shared definitions own limits, unlock rules, costs, rates, and minimum loan guarantees. UI issues commands and queries; it does not mutate SaveData.

## Aggregate model

`caravans[]`, `tradeProgressEntries[]`, and `pendingSettlements[]` are independent lists joined by IDs. A Caravan may be Preparation, Traveling, SettlementPending, Completed, or Failed. Multiple entries may travel or await settlement simultaneously. There is no global active Caravan in the target source of truth.

Runtime lookup views use `caravanId` and composite `(caravanId, tradeId)` keys. They are rebuilt after normalization, never serialized. Duplicate IDs are load validation errors, not last-write-wins conditions.

## State transitions

```text
Preparation --depart+save--> Traveling --finalize+save--> SettlementPending
     ^                                                       |
     |                         claim+save                     |
     `---------------- Completed/Failed + retained setup <----'
```

- Depart: validate, stage a new full GUID and immutable departure originals, persist, then expose Traveling.
- Finalize: calculate once, persist the confirmed snapshot under both IDs, then notify UI.
- Claim: locate the exact composite key, stage reward application and removal, persist once, then publish completion. A retry after success must not pay again.
- Cancel preparation: clear only the addressed Caravan and mark Dirty.

## Current repository bridge

The current runtime has one `CaravanSaveData`, one `TradeProgressSaveData`, one `PendingSettlementSaveData`, one coordinator active-Caravan reference, and settlement events keyed only by `tradeId`. These may remain temporarily, but must not be treated as the target model. Full migration belongs in follow-up branches after contract approval.

Exclusive assignment of wagons, animals, or mercenaries is not defined here. When Core supplies that policy, command validation must reject cross-Caravan conflicts; persistence must still preserve the attempted independent configurations for diagnostics or return a validation failure without mutation.

