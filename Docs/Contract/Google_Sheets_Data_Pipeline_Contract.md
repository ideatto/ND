# Google Sheets Data Pipeline Contract

## 1. Purpose

This document fixes the authority, ownership, and handoff boundaries for production item, wagon, and draft-animal data moving from Google Sheets into the existing Unity data path. It distinguishes verified current behavior from the approved target contract; it does not claim that the target pipeline is implemented.

## 2. Scope

Sheets v1 covers only the production ProjectData definitions listed in [Google Sheets Schema Contract](Google_Sheets_Schema_Contract.md): eight items, three wagons, and two draft animals. Work A produces contracts only.

## 3. Non-Goals

- No runtime Google Sheets API dependency.
- No automatic deletion of ScriptableObject assets or regeneration of `.meta` files.
- No direct automatic push to `dev2`.
- No replacement of the existing catalog synchronizer.
- No global ID normalization or definition-ID renaming.
- No migration of quantities, durability, prices, loads, travel results, or other runtime state into Sheets.
- No automatic resolution of ambiguous or missing legacy-save matches.
- No exporter, validator, importer, catalog, SharedData, Core, UI, SaveData, or CI implementation in Work A.

## 4. Authority and Ownership Model

| Layer | Authority |
|---|---|
| Google Sheets | Human-authored content source. |
| Exported JSON | Versioned source snapshot and automation input. |
| Existing ScriptableObject definition assets | Unity-native definitions updated in place. Their scripts are owned outside the user's Framework/Integration and SaveData scope. |
| Existing SharedData catalog synchronization tooling | Catalog discovery, drift detection, and synchronization authority. |
| `SharedGameDataProvider` | Runtime definition lookup authority. |
| SaveData | Player-owned state, snapshots retained for compatibility, and target stable definition references. |

The user owns Framework/Integration and SaveData-related work. Changes to the three ScriptableObject definition scripts, Core calculation, or UI require the appropriate owner or explicit authorization. Spreadsheet-management fields must not be added to those ScriptableObject types.

## 5. Current Production Data Flow

```text
TradeItemData / WagonData / DraftAnimalData ScriptableObjects
  -> SandboxSharedGameDataCatalog
  -> SharedGameDataService
  -> SharedTradeItemDefinition / SharedWagonDefinition / SharedDraftAnimalDefinition
  -> SharedGameDataView
  -> ISharedGameDataProvider / SharedGameDataProvider
  -> runtime consumers
```

Current item SaveData retains definition IDs. Wagon and animal owned-instance SaveData retain instance identity, names, and copied statistics but lack stable definition IDs. The current Shared draft-animal definition omits `IncreaseMaxLoad`, while other Core, UI, and Save paths still carry or apply it.

## 6. Target Data Flow

```text
Google Sheets
  -> deterministic JSON export
  -> read-only validation
  -> update existing ScriptableObject assets in place
  -> existing catalog drift check/synchronization
  -> SharedGameDataProvider
  -> runtime

Legacy owned-instance SaveData
  -> definition-ID migration
  -> SharedGameDataProvider lookup
```

## 7. Existing Catalog Synchronization Authority

`SharedGameDataTypeCatalog`, `SharedGameDataWatchScanner`, `SharedGameDataWatchInventory`, `SharedGameDataCatalogDriftChecker`, and `SharedGameDataCatalogSynchronizer` remain the integration path. They scan configured roots, identify assets by GUID, validate IDs, report drift, and synchronize catalog arrays. The current synchronizer does not consume spreadsheet `enabled` metadata; adding that input without creating a competing catalog authority is deferred to Work C.

## 8. Spreadsheet and JSON Responsibilities

Sheets declares authored definitions and desired production membership. Every row, including `enabled = false`, is exported. JSON preserves exact IDs, schema version, types, and `enabled` metadata. Validation is non-mutating. Export and validation do not delete assets or mutate SaveData.

## 9. ScriptableObject Responsibilities

The importer updates existing definition assets in place, preserving asset paths, GUIDs, Unity-only Sprite and prefab references, and fields deferred from v1. It must not add `enabled`, `contentVersion`, or `developerNote` to the SO scripts. Missing source rows do not authorize asset or `.meta` deletion.

## 10. SharedData Responsibilities

SharedData maps synchronized SO definitions into runtime-facing definitions. The approved target adds `IncreaseMaxLoad` independently from `AdditionalEfficientLoad`. Current omission and warning behavior are an integration mismatch owned by the later SharedData Alignment Work.

## 11. SaveData Responsibilities

SaveData owns player state, not authored static definitions. Target wagon and animal instances gain stable references conceptually named `wagonDefinitionId` and `draftAnimalDefinitionId`. Migration must preserve valid IDs; otherwise it uses legacy name and copied-stat snapshots against synchronized production definitions, writes only an unambiguous match, reports unresolved or ambiguous results, and preserves legacy snapshots unless a later contract removes them. Catalog synchronization and SaveData migration are complementary, separate layers.

## 12. Active and Inactive Content Contract

`enabled` is Sheets/JSON/catalog-membership metadata, not an SO field.

- `true`: active production definition eligible for production catalog membership.
- `false`: preserved record exported to JSON, not eligible for normal production membership, and not an instruction to delete an existing SO asset.
- Row absent from Sheets: missing-source/drift warning requiring manual review; never automatic asset, `.meta`, catalog, or SaveData removal.

## 13. Stable ID Compatibility Contract

Existing IDs such as `Apple`, `Wagon_M`, `Horse`, and `Donkey` remain valid and must be preserved exactly. Lookup is case-sensitive and no global trim or normalization is applied. Import rejects null, empty, whitespace-only, outer-whitespace, and duplicate IDs within a dataset. It does not lowercase, convert to snake_case, derive IDs from display names, or run a global format migration.

## 14. Production and Test Data Separation

Sheets v1 contains production ProjectData only. Dummy, sandbox-only, economy-test, probe, and Editor-test assets are excluded. The existing catalog may contain mixed production and test assets; that is current technical debt and a validation concern, not authority to delete them.

## 15. Asset and GUID Preservation

Existing assets are updated at their current paths. The pipeline must preserve GUIDs, Unity references, and `.meta` files. Disabled or absent rows never imply deletion, relocation, recreation, or GUID regeneration.

## 16. Work Boundaries

| Work | Responsibility |
|---|---|
| Work A | Contract documents and schema decisions. |
| Work B | Read-only JSON/source validator. |
| Work C | Existing SO in-place importer and integration with existing catalog sync. |
| SaveData Migration Work | Definition-ID fields, migration, rollback/recovery policy, and compatibility. |
| SharedData Alignment Work | Add `IncreaseMaxLoad` to Shared DTO and mapping; align warnings. |
| Core/UI Alignment Work | Ensure maximum-load calculation and UI use the same supported policy. |
| Export Automation Work | Google Sheets Apps Script and deterministic JSON export. |
| CI Work | Pull-request validation. |

## 17. Deferred Implementation Requirements

- Export the envelopes and manifest defined in [JSON Export Contract](Google_Sheets_JSON_Export_Contract.md).
- Implement the non-mutating rules in [Validation Contract](Google_Sheets_Validation_Contract.md).
- Define how `enabled` filters catalog membership through the existing synchronizer.
- Add and migrate wagon/animal definition IDs without discarding legacy snapshots.
- Carry `IncreaseMaxLoad` through SharedData, Core, UI, and SaveData consistently.
- Establish any future unified ID naming convention as a separately approved migration.

## 18. Acceptance Criteria

- The authority chain and work boundaries above are retained.
- Disabled rows remain in JSON and do not map to SO fields.
- Existing SO assets are updated in place; no automatic deletion or GUID change occurs.
- The existing catalog tooling remains authoritative.
- `IncreaseMaxLoad` is supported as an independent capacity effect.
- SaveData definition-ID migration is part of the overall initiative but not Work A or Work B.
- Exact existing IDs and production-only Sheets v1 inventory are preserved.
