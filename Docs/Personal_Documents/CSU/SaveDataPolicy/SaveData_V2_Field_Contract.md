# SaveData V2 Field Contract

## Status and scope

`V2` is the product/document label for the second-build target contract. It is not the serialized numeric schema version. The repository currently uses `SaveData.CurrentVersion = 6`; changing that value to 2 would invalidate version compatibility.

Version 6 has completed the collection-shaped persistence cutover. The serialized source of truth is `caravans[]`, `tradeProgressEntries[]`, and `pendingSettlements[]`, plus the persisted UI selection key `selectedCaravanId`. Multi-active command processing, coordination, and UI are separate follow-up implementation work and are not implied by the storage cutover.

The target uses Unity-serializable lists, never dictionaries. Runtime indexes are rebuilt after load. Shared definitions are referenced by stable IDs; ScriptableObject values and calculated ViewData are not copied into saves.

## Proposed hierarchy

```text
SaveData
|- version, metadata(lastSavedUtcTicks)
|- player(currencies, growth levels, home inventory)
|- selectedCaravanId (last UI/application selection)
|- caravans[]
|  |- caravanId
|  |- fixedSetup(wagonId, currentDurability, animals[], mercenaries[])
|  |- preparation(destinationTownId, routeId, cargo[], food, purchasePreview)
|  `- currentTradeId (optional reference)
|- tradeProgressEntries[](caravanId, tradeId, routeId, state, start/end UTC ticks)
|- pendingSettlements[](caravanId, tradeId, confirmed result snapshot)
|- buildings[], world, investmentQuests[], rescueLoan
|- unlocks and tutorial
`- compatibility (temporary V1/current-schema fields only during an approved migration)
```

No maximum Caravan count belongs in SaveData.

`selectedCaravanId` persists the last selected Caravan at the SaveData root. On load, normalization keeps it only when the ID resolves to a stored Caravan; otherwise it selects the first valid stored Caravan. Selection is presentation/application focus, not the implicit target of a new state-changing command.

## Field rules

| Area | Persist confirmed original state | Recalculate / do not persist |
|---|---|---|
| Player | currencies, growth levels, building levels | formatted currency, button state |
| Caravan | `caravanId`, definition IDs, counts, durability, cargo quantities, food | load, load thresholds, speed, consumption, final modified stats |
| Wagon | stable `wagonId`, current durability while owned | repair costs, rarity multiplier, derived stats; destroyed wagons are removed |
| Building | stable `buildingId`, current level | display name, upgrade costs, effects |
| Investment quest | `investmentQuestId`, `townId`, completion state, completion UTC ticks | currency/item costs and unlock definitions |
| Rescue loan | `loanId`, original/remaining principal, active state, issue UTC ticks, restricted-preparation state | `MinimumTradeCost`, eligibility/rebankruptcy calculation, UI text |
| Preparation | destination/route IDs, prepared cargo/food, fixed selections, preview DTO | popup/tab/selection presentation state |
| Trade | full GUID `tradeId`, route ID, state, UTC start/end | display progress strings; progress may be derived from timestamps |
| Settlement | confirmed result inputs/outputs needed to pay exactly once | recalculated settlement result |
| World | season/disaster IDs and confirmed mutable state | definition values and modifier calculations |

Every departed trade keeps the same full GUID through Traveling, SettlementPending, and Claim. Product flow must generate it at commit; shortened IDs are logging only.

`RescueLoanSaveData` contains only `loanId`, `originalPrincipal`, `remainingPrincipal`, `isActive`, `issuedUtcTicks`, and `isRestrictedPreparation`. Missing loan data normalizes to an inactive default object. Negative principal/ticks clamp to zero; remaining principal clamps to original principal; zero remaining principal clears active state; inactive state clears restriction. A malformed active loan with missing ID or zero principal is normalized to a safe inactive state with a warning and never creates a replacement loan automatically.

The loan issue amount and eligibility are definition/calculator data, not SaveData. The issued principal is the full `RescueLoanDefinition.MinimumTradeCost`. SaveData does not store `hasUsedRescueLoan`, a loan phase, missing-configuration elements, settlement repayment choice, or a rebankruptcy flag. Rebankruptcy is recalculated deterministically after load unless a separate final game-over contract is approved.

The target schema has no donation balance, donation decay, cumulative investment progress, or investment definition-cost snapshot. Rescue-loan repayment is a separate command, but no transient repayment request or entered amount is persisted in SaveData. `VillageBuildingSaveData` uses stable `buildingId + level`; `DisplayName` is presentation data resolved from shared definitions. Existing DisplayName-based data requires an approved compatibility adapter or migration, but this documentation change does not change `SaveData.CurrentVersion`.

## Preparation and preview

Each Caravan owns one preparation object. Editing it marks SaveData Dirty. Explicit cancel replaces only that Caravan's preparation with defaults. Successful claim clears prepared food and trade goods but retains wagon, animals, mercenaries, and other fixed setup for per-Caravan reuse.

`PurchasePreviewSaveData` is an explicit persisted exception for `townId`, `marketId`, preview cargo item IDs/quantities, and preview food. Preview commands never mutate currency, inventory, or confirmed cargo.

## Version 6 compatibility boundary

The non-serialized `caravan`, `tradeProgress`, and `pendingSettlement` properties are compatibility accessors for existing single-Caravan runtime callers. They resolve only the Caravan selected by `selectedCaravanId`; they do not represent or enumerate the complete multi-Caravan state and are not an alternate persistence source.

New commands and recovery code must query the canonical collections by explicit ID. They must not infer a mutation target from `selectedCaravanId` or use the compatibility accessors to choose which Caravan departs, finalizes, or claims a settlement. Existing callers may continue through the accessors during staged migration, but collection data remains authoritative and must not be overwritten from a selected-only view.

Implemented storage scope: numeric version 6, the three canonical collections, `selectedCaravanId`, ID fields on Caravan/trade progress/pending settlement DTOs, and selected-Caravan compatibility accessors. Not yet implemented by this cutover: explicit-ID Depart/Finalize/Claim commands, a Caravan-aware settlement-ready event, simultaneous Traveling coordination, duplicate/orphan recovery policy, per-Caravan reusable preparation, or stable `buildingId` migration.
