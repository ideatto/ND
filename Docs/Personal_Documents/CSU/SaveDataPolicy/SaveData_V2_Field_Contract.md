# SaveData V2 Field Contract

## Status and scope

This document defines the second-build target contract. It does not activate that schema. The repository currently uses `SaveData.CurrentVersion = 5` and one `caravan`, `tradeProgress`, and `pendingSettlement`. Lowering that value to 2 would invalidate current saves; the team must resolve the product label versus the existing numeric sequence before implementation.

The target uses Unity-serializable lists, never dictionaries. Runtime indexes are rebuilt after load. Shared definitions are referenced by stable IDs; ScriptableObject values and calculated ViewData are not copied into saves.

## Proposed hierarchy

```text
SaveData
|- version, metadata(lastSavedUtcTicks)
|- player(currencies, growth levels, home inventory, selectedCaravanId)
|- caravans[]
|  |- caravanId
|  |- fixedSetup(wagonId, currentDurability, animals[], mercenaries[])
|  |- preparation(destinationTownId, routeId, cargo[], food, purchasePreview)
|  `- currentTradeId (optional reference)
|- tradeProgressEntries[](caravanId, tradeId, routeId, state, start/end UTC ticks)
|- pendingSettlements[](caravanId, tradeId, confirmed result snapshot)
|- buildings[], world, townDonations[], investments[], rescueLoan
|- unlocks and tutorial
`- compatibility (temporary V1/current-schema fields only during an approved migration)
```

No maximum Caravan count belongs in SaveData.

`selectedCaravanId` persists the last selected Caravan under player/application state, not inside an individual Caravan. On load, select it only when the ID exists and is owned. If it is missing, locked, deleted, or invalid, select the first valid owned Caravan. Selection fallback must not activate Save Version 6 or imply a schema cutover in this documentation branch.

## Field rules

| Area | Persist confirmed original state | Recalculate / do not persist |
|---|---|---|
| Player | currencies, growth levels, building levels | formatted currency, button state |
| Caravan | `caravanId`, definition IDs, counts, durability, cargo quantities, food | load, load thresholds, speed, consumption, final modified stats |
| Preparation | destination/route IDs, prepared cargo/food, fixed selections, preview DTO | popup/tab/selection presentation state |
| Trade | full GUID `tradeId`, route ID, state, UTC start/end | display progress strings; progress may be derived from timestamps |
| Settlement | confirmed result inputs/outputs needed to pay exactly once | recalculated settlement result |
| World | season/disaster IDs and confirmed mutable state | definition values and modifier calculations |

Every departed trade keeps the same full GUID through Traveling, SettlementPending, and Claim. Product flow must generate it at commit; shortened IDs are logging only.

## Preparation and preview

Each Caravan owns one preparation object. Editing it marks SaveData Dirty. Explicit cancel replaces only that Caravan's preparation with defaults. Successful claim clears prepared food and trade goods but retains wagon, animals, mercenaries, and other fixed setup for per-Caravan reuse.

`PurchasePreviewSaveData` is an explicit persisted exception for `townId`, `marketId`, preview cargo item IDs/quantities, and preview food. Preview commands never mutate currency, inventory, or confirmed cargo.

## Compatibility

The current single-value fields remain authoritative until an approved cutover. During migration they may be read into one generated/assigned Caravan entry, but dual writes must have one documented source of truth. Version 1 data must not be silently accepted as target V2. Reset or a one-time converter requires a separate approved decision.
