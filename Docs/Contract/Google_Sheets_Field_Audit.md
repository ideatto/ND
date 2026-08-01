# Google Sheets Field Audit

## 1. Classification Key

- **Authoritative**: static content authored in Sheets/SO.
- **Reference**: stable identifier linking layers.
- **Display**: user-facing static content.
- **JSON-only metadata**: exported automation metadata not stored on SO.
- **Authoring-only metadata**: workbook-only notes/versioning.
- **Derived**: calculated from definitions/state.
- **RuntimeState**: current session/player state.
- **SaveSnapshot**: copied value retained in SaveData.
- **CurrentMismatch**: current layers disagree with the approved contract.
- **Deferred**: approved but implemented later.

## 2. ScriptableObject Definitions

| Type | Fields / behavior | Classification | Audit result |
|---|---|---|---|
| `TradeItemData` | ID | Authoritative, Reference | `itemId`/`ItemId`; exact existing ID is preserved. |
| | display name, description | Authoritative, Display | Included in Sheets v1. |
| | rarity, category, buy/sell prices, stack fields, weight, consumable, local specialty | Authoritative | Included in Sheets v1. Negative numeric values are clamped by current accessors/validation, but source validation must reject them. |
| | icon | Unity-side | Excluded from v1. |
| | modifier graph | Deferred | Nested structure is excluded from flat v1. |
| `WagonData` | ID, display, description, type, durability, overload threshold, max load, speed, slots, pull-animal limits/types, rarity, price, stack | Authoritative | Included with `overLoad` mapped to `baseEfficientLoad`. |
| | icon, prefab | Unity-side | Preserved in place and excluded from v1. |
| | repair price/multiplier | External policy | Owned by `WagonRepairContentPolicy`, not `WagonData`. |
| `DraftAnimalData` | ID, display, description, type, feed/second, speed, efficient-load increase, maximum-load increase, rarity, price, stack | Authoritative | Both capacity effects are supported and independent. |
| | `increaseMaxLoad` | Authoritative, CurrentMismatch | Present in SO and Save/UI/Core paths but omitted from current SharedData. Approved target supports it. |
| | icon, prefab | Unity-side | Preserved in place and excluded from v1. |

The user does not own these three SO scripts. `enabled`, `contentVersion`, and `developerNote` must not be added to them. `enabled` is JSON-only metadata; the other two are authoring-only metadata.

## 3. SharedData Definitions and Service

| Type | Current fields / behavior | Classification | Audit result |
|---|---|---|---|
| `SharedTradeItemDefinition` | ID, display, rarity/category, prices, stack, weight, consumable, local specialty (and current integration-specific modifier data) | Reference, Authoritative view | Produced from `TradeItemData` by `SharedGameDataService`. Runtime read model, not Sheets authority. |
| `SharedWagonDefinition` | ID, display, type, durability, efficient/max load, speed, slots, animal limits/types, price/stack | Reference, Authoritative view | Carries both wagon load thresholds. |
| `SharedDraftAnimalDefinition` | ID, display, type, food/second, speed, additional efficient load, price/stack | Reference, CurrentMismatch | Omits `IncreaseMaxLoad`; later SharedData Alignment Work adds it and removes/aligns ignore warnings. |
| `SharedGameDataService` | Catalog load, SO-to-DTO mapping, ID/reference validation, drift checks | Integration authority | Current production definition conversion path. It must not be replaced by the spreadsheet pipeline. |
| `SharedGameDataProvider` / `ISharedGameDataProvider` | Typed exact-ID lookup and read views | Reference | Runtime definition lookup authority. Current dictionary comparison is ordinal/case-sensitive; no global trim/normalization. |

## 4. SaveData

| Type | Current fields / behavior | Classification | Audit result |
|---|---|---|---|
| `TradeItemSaveData` | `itemId` plus item name, weight, base price, max count, quantity | Reference, SaveSnapshot, RuntimeState | Item-owned state already carries a definition ID. Quantity is player state; copied values are snapshots, not Sheets fields. |
| `WagonSaveData` | `instanceId`, `wagonName`, `overLoad`, `maxLoad`, speed, durability/slot/animal limits and related copied values | Reference (instance), SaveSnapshot, RuntimeState | Stable instance identity exists; stable wagon definition ID does not. Target definition ID and migration are Deferred. |
| `AnimalSaveData` | `instanceId`, `animalName`, type, feed, speed, `increaseOverLoad`, `increaseMaxLoad`, related copied values | Reference (instance), SaveSnapshot | Stable instance identity exists; stable draft-animal definition ID does not. Snapshot proves current Save path still retains maximum-load increase. |

Target migration preserves a valid definition ID, otherwise compares legacy name and copied-stat evidence to synchronized production definitions and writes only a single unambiguous match. Unresolved/ambiguous cases produce structured results and preserve legacy snapshots. Current catalog synchronization does not perform this migration.

## 5. Runtime Calculation and State Boundary

| Area | Classification | Audit result |
|---|---|---|
| `CaravanCalculator` | Derived, RuntimeState, CurrentMismatch | Calculates caravan capacity/speed from wagon and assigned-animal inputs. Core alignment is deferred so efficient-load and physical maximum-load effects follow the same approved policy used by SharedData and UI. |
| Current durability | RuntimeState | Owned-instance state; excluded from Sheets. |
| Quantities and market unit prices | RuntimeState / SaveSnapshot | Market/inventory state; excluded from Sheets. Base definition prices remain authored fields. |
| Current/effective load and travel results | Derived / RuntimeState | Calculated or persisted gameplay state; excluded from Sheets. |

## 6. Catalog Synchronization Tooling

| Component | Current responsibility | Classification / target note |
|---|---|---|
| `SharedGameDataTypeCatalog` | Maps supported SO types to catalog arrays and ID readers. | Existing authority; reused. |
| `SharedGameDataWatchScanner` | Scans configured ProjectData/SandboxLegacy roots and captures paths/GUIDs/IDs. | Existing authority; reused. |
| `SharedGameDataWatchInventory` | Stores player-build drift inventory. | Existing authority; reused. |
| `SharedGameDataCatalogDriftChecker` | Compares watch inventory/catalog and reports drift. | Existing authority; reused. |
| `SharedGameDataCatalogSynchronizer` | Builds preview/sync plans, validates IDs, adds ProjectData assets, retains/validates catalog entries, ignores unregistered SandboxLegacy assets, refreshes watch inventory. | Existing authority; currently unaware of Sheets `enabled`; membership integration is Deferred. |

The catalog currently can include production and dummy/test assets. Sheets v1 intentionally includes production ProjectData only: items `Apple`, `Bread`, `Cloth`, `Fish`, `Logs`, `Stone`, `Stover`, `Wheat`; wagons `Walk`, `Wagon_S`, `Wagon_M`; draft animals `Horse`, `Donkey`.

## 7. Preserved Findings

1. Item SaveData carries definition IDs.
2. Wagon and animal SaveData currently lack stable definition IDs.
3. Wagon and animal statistics are copied into SaveData snapshots.
4. `IncreaseMaxLoad` has a current ignore/apply mismatch across systems.
5. The approved target supports `IncreaseMaxLoad`; it is not deprecated.
6. Icons and prefabs remain Unity-side in v1.
7. Repair cost policy is outside `WagonData`.
8. The catalog contains or may surface production and test/dummy assets together.
9. Existing catalog synchronization tooling is reused.
10. SO script ownership prevents spreadsheet-management fields from being added to those types.

## 8. Related Contracts

- [Data Pipeline Contract](Google_Sheets_Data_Pipeline_Contract.md)
- [Schema Contract](Google_Sheets_Schema_Contract.md)
- [JSON Export Contract](Google_Sheets_JSON_Export_Contract.md)
- [Validation Contract](Google_Sheets_Validation_Contract.md)
