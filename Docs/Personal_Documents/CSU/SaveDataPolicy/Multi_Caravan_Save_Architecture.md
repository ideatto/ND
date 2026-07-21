# Multi-Caravan Save Architecture

## Ownership

Framework owns persistence DTOs, normalization, stable key lookup, UTC timestamps, save outcomes, and integration events. Core owns gameplay validation and calculations. Progression/System or shared definitions own limits, unlock rules, costs, rates, and minimum loan guarantees. UI issues commands and queries; it does not mutate SaveData.

## Aggregate model

In numeric schema version 6, `caravans[]`, `tradeProgressEntries[]`, and `pendingSettlements[]` are the serialized source-of-truth lists joined by IDs. A Caravan may be Preparation, Traveling, SettlementPending, Completed, or Failed. The storage shape permits multiple entries to travel or await settlement simultaneously; the current coordinator and commands do not yet implement that multi-active behavior. `selectedCaravanId` is UI/application focus, not a global command target.

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

The repository has completed the version 6 collection cutover but retains non-serialized `caravan`, `tradeProgress`, and `pendingSettlement` compatibility properties. Each resolves only the entry addressed by `selectedCaravanId`. The current coordinator still has one active-Caravan runtime reference, `TryStartTrade(...)` has no explicit `caravanId`, Claim is `ClaimSettlementAndReset()`, and settlement events are keyed only by `tradeId`. These APIs are partial compatibility surfaces, not the collection source of truth or proof of multi-active execution support.

New state-changing contracts require explicit identity:

- `Depart(caravanId, request)` or a compatibly named `TryStartTrade(caravanId, request)`
- `FinalizeSettlement(caravanId, tradeId)`
- `ClaimSettlement(caravanId, tradeId)`
- `TradeSettlementReady(caravanId, tradeId, result)`

The selected Caravan must never be used to infer these targets. A command concerning one Caravan must not be blocked merely because another Caravan is Traveling or SettlementPending. The addressed Caravan must reject departure while it is Traveling or has an unresolved pending settlement.

Normal data permits at most one active Traveling progress and one unresolved pending settlement per Caravan. An exact duplicate `(caravanId, tradeId)` is corrupt/duplicate persisted data. Multiple pending entries with the same `caravanId` but different `tradeId` are ambiguous recovery data: record a visible recovery error and block automatic Depart/Claim for that Caravan. Do not choose or delete an entry automatically; executable recovery behavior is follow-up work.

Commands validate that a player-owned asset is usable and not locked by another Traveling or SettlementPending flow. Investment-quest submissions require an explicit `caravanId` for each item stack and reject unavailable Caravan goods; home temporary inventory is excluded.

Wagon durability loss clamps at zero. At zero, the wagon is destroyed: remove the owned-wagon record and Caravan `wagonId`, clear preparation and reusable-configuration references, lose all cargo and food loaded on that wagon, and mark an in-flight trade `Failed`. Reference cleanup, losses, trade failure, and settlement snapshot are one staged operation. The snapshot records destruction, wagon ID, lost goods and food, failure reason, `caravanId`, and full `tradeId`.

At the village, selected Caravan cargo may be transferred to home temporary inventory through one validated command. Building upgrades consume only material items already transferred to home temporary inventory; Caravan cargo cannot be consumed directly. Upgrade definitions and per-level costs live in shared data or a ScriptableObject, while SaveData stores stable `buildingId` and level only.

Health consumables are real item IDs. They are purchased in a Caravan village, travel as Caravan cargo, and may be transferred to home temporary inventory only after return. Building upgrades consume eligible items only from home temporary inventory.

Wagon repair uses trading currency. For positive repaired durability, `rawCost = repairedDurability * repairCostPerDurability * wagonRarityMultiplier` and `finalCost = floor(rawCost)` once after all multipliers. A positive repair whose calculated cost is zero costs at least one. Repair cannot exceed maximum durability and cannot target a destroyed wagon. Cost tables and rarity multipliers come from shared data or the owning feature's data definitions.

