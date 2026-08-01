# Google Sheets Validation Contract

## 1. Execution Contract

Work B validation is read-only: every rule below has `Mutating? = No`. It reports errors and warnings but does not trim, normalize, reorder, import, delete, synchronize, or migrate. “Current evidence” identifies the current implementation or approved policy; a future rule is not represented as already implemented.

Severity: an **Error** blocks the affected validation/import handoff; a **Warning** requires review but does not by itself authorize mutation.

## 2. Structural Rules

| Rule ID | Severity | Dataset | Field | Condition | Message | Current evidence | Future owner | Mutating? |
|---|---|---|---|---|---|---|---|---|
| COMMON_SOURCE_MISSING | Error | any | source | Required file/dataset is absent. | Required source dataset is missing. | Approved export set. | Work B | No |
| COMMON_SCHEMA_VERSION_UNSUPPORTED | Error | any | schemaVersion | Missing, non-integral, or not `1`. | Unsupported schemaVersion. | v1 contract. | Work B | No |
| COMMON_UNKNOWN_FIELD | Error | any | field | Envelope or record property is not declared. | Unknown field `{field}`. | Strict JSON contract. | Work B | No |
| COMMON_REQUIRED_FIELD_MISSING | Error | any | field | Required property absent or null. | Required field `{field}` is missing. | Schema contract. | Work B | No |
| COMMON_ID_MISSING | Error | any | id | Null, empty, or whitespace-only. | Definition ID is required. | Current catalog rejects whitespace-only IDs. | Work B | No |
| COMMON_ID_OUTER_WHITESPACE | Error | any | id | `id != id.Trim()` using ordinal characters. | Definition ID has leading/trailing whitespace. | Approved preservation rule. | Work B | No |
| COMMON_ID_DUPLICATE | Error | same dataset | id | Exact ordinal duplicate. | Duplicate definition ID `{id}`. | Current catalog uses ordinal ID dictionaries. | Work B | No |
| COMMON_ENUM_INVALID | Error | any | enum field | Value is not an exact allowed enum name. | Invalid enum name `{value}`. | Current C# enum-backed SO fields. | Work B | No |
| COMMON_NUMBER_INVALID | Error | any | numeric field | Wrong JSON type, fractional integer, or outside target C# range. | Invalid numeric value. | SO/C# field types. | Work B | No |
| COMMON_NUMBER_NON_FINITE | Error | any | floating field | NaN or infinity is encountered before/while encoding. | Number must be finite. | JSON and approved contract. | Work B | No |
| COMMON_ENABLED_TYPE_INVALID | Error | any | enabled | Not a JSON boolean. | `enabled` must be boolean. | Approved metadata contract. | Work B | No |

## 3. Field and Domain Rules

| Rule ID | Severity | Dataset | Field | Condition | Message | Current evidence | Future owner | Mutating? |
|---|---|---|---|---|---|---|---|---|
| ITEM_WEIGHT_NEGATIVE | Error | items | weight | finite value < 0 | Weight cannot be negative. | `TradeItemData` clamps to 0. | Work B | No |
| ITEM_STACK_COUNT_INVALID | Error | items | maxCount | integer < 1 | maxCount must be at least 1. | SO clamps to 1. | Work B | No |
| ITEM_PRICE_NEGATIVE | Error | items | baseBuyPrice/baseSellPrice | value < 0 | Item price cannot be negative. | SO getters clamp to 0. | Work B | No |
| ITEM_PRICE_ZERO | Warning | items | baseBuyPrice/baseSellPrice | value = 0 | Confirm zero-price item policy. | Zero is technically accepted. | Content review | No |
| ITEM_CATEGORY_INVALID | Error | items | category | invalid enum name | Invalid trade-item category. | `TradeItemCategory`. | Work B | No |
| WAGON_LOAD_ORDER_INVALID | Error | wagons | baseEfficientLoad | value > maxLoad | Efficient-load threshold exceeds physical limit. | SO `OnValidate` clamps overload to maxLoad. | Work B | No |
| WAGON_ANIMAL_COUNT_INVALID | Error | wagons | minRequireAnimals/maxPullAnimals | either < 0 or min > max | Invalid minimum/maximum pull-animal counts. | SO clamps and orders values. | Work B | No |
| WAGON_SLOT_COUNT_INVALID | Error | wagons | inventorySlotCount | integer < 1 | Inventory slot count must be at least 1. | SO clamps to 1. | Work B | No |
| WAGON_DURABILITY_INVALID | Error | wagons | maxDurability | integer < 0 | Maximum durability cannot be negative. | SO clamps to 0. | Work B | No |
| WAGON_PRICE_INVALID | Error | wagons | baseBuyPrice | value < 0 | Wagon base price cannot be negative. | SO getter clamps to 0. | Work B | No |
| WAGON_TYPE_SPEED_POLICY | Warning | wagons | wagonType/baseMoveSpeed | Current type-specific SO policy is violated, including an animal-drawn wagon with nonzero base speed. | Review wagon type/speed policy. | `WagonData.ApplyWagonTypeRules`. | Content owner/Work C | No |
| ANIMAL_SPEED_INVALID | Error | draft_animals | baseMoveSpeed | non-finite or < 0 | Base move speed must be finite and non-negative. | SO clamps negatives. | Work B | No |
| ANIMAL_FOOD_INVALID | Error | draft_animals | foodConsumptionPerSecond | non-finite or < 0 | Food consumption must be finite and non-negative. | SO clamps negatives. | Work B | No |
| ANIMAL_EFFICIENT_LOAD_INVALID | Error | draft_animals | additionalEfficientLoad | non-finite or < 0 | Efficient-load increase must be finite and non-negative. | SO clamps negatives. | Work B | No |
| ANIMAL_MAX_LOAD_INVALID | Error | draft_animals | increaseMaxLoad | non-finite or < 0 | Maximum-load increase must be finite and non-negative. | Approved supported field; SO clamps negatives. | Work B | No |
| ANIMAL_TYPE_INVALID | Error | draft_animals | animalType | invalid enum name | Invalid draft-animal type. | `DraftAnimalType`. | Work B | No |

Positive `increaseMaxLoad` is valid and must not emit a legacy/deprecation warning.

## 4. Reference Rules

| Rule ID | Severity | Dataset | Field | Condition | Message | Current evidence | Future owner | Mutating? |
|---|---|---|---|---|---|---|---|---|
| WAGON_ELIGIBLE_TYPE_DUPLICATE | Error | wagons | eligibleAnimalTypes | Exact enum appears more than once. | Duplicate eligible animal type. | Array maps to SO reference policy. | Work B | No |
| WAGON_ELIGIBLE_TYPE_INVALID | Error | wagons | eligibleAnimalTypes | Element is not an exact enum name. | Invalid eligible animal type. | `DraftAnimalType`. | Work B | No |

Mixed assigned-animal-type compatibility depends on a wagon plus owned runtime assignments. It is runtime validation, not source-row structural validation.

## 5. Catalog Drift Rules

| Rule ID | Severity | Dataset | Field | Condition | Message | Current evidence | Future owner | Mutating? |
|---|---|---|---|---|---|---|---|---|
| CATALOG_ACTIVE_RECORD_MISSING | Error | any | id | Enabled production record has no eligible production SO/catalog result. | Active definition is missing from production catalog flow. | Current tooling detects ProjectData drift, but not `enabled`. | Work C/catalog sync | No |
| CATALOG_DISABLED_RECORD_REGISTERED | Warning | any | enabled/id | Disabled record remains in normal production catalog membership. | Disabled definition is still registered for production. | Future membership metadata integration. | Work C/catalog sync | No |
| CATALOG_EXISTING_ASSET_MISSING_FROM_SOURCE | Warning | any | id | Managed existing SO has no source row. | Existing asset is missing from Sheets; manual review required. | Approved no-delete policy. | Work C/catalog sync | No |
| CATALOG_TEST_ASSET_IN_PRODUCTION_SCOPE | Warning | any | id/path | Dummy, sandbox, economy-test, probe, or Editor-test asset is included in production scope. | Test asset appears in production catalog scope. | Current catalog can mix production/test assets. | Catalog sync/content owner | No |

These are target requirements. The current synchronizer scans roots and GUIDs but does not understand spreadsheet membership metadata.

## 6. SaveData Migration Rules

| Rule ID | Severity | Dataset | Field | Condition | Message | Current evidence | Future owner | Mutating? |
|---|---|---|---|---|---|---|---|---|
| SAVE_WAGON_DEFINITION_ID_UNRESOLVED | Warning | wagon save | wagonDefinitionId | Legacy snapshot matches no synchronized wagon definition. | Wagon definition ID could not be resolved. | Current wagon save has name/stats, no definition ID. | SaveData Migration Work | No |
| SAVE_WAGON_DEFINITION_ID_AMBIGUOUS | Error | wagon save | wagonDefinitionId | Legacy evidence matches multiple definitions. | Wagon definition ID match is ambiguous. | Approved no-guess policy. | SaveData Migration Work | No |
| SAVE_ANIMAL_DEFINITION_ID_UNRESOLVED | Warning | animal save | draftAnimalDefinitionId | Legacy snapshot matches no synchronized animal definition. | Draft-animal definition ID could not be resolved. | Current animal save has name/stats, no definition ID. | SaveData Migration Work | No |
| SAVE_ANIMAL_DEFINITION_ID_AMBIGUOUS | Error | animal save | draftAnimalDefinitionId | Legacy evidence matches multiple definitions. | Draft-animal definition ID match is ambiguous. | Approved no-guess policy. | SaveData Migration Work | No |
| SAVE_DEFINITION_ID_NOT_FOUND | Error | owned-instance save | definition ID | Existing nonblank ID is absent from provider. | Definition ID was not found. | Provider is lookup authority. | SaveData Migration Work | No |
| SAVE_DEFINITION_ID_TYPE_MISMATCH | Error | owned-instance save | definition ID | ID resolves only in the wrong definition dataset/type. | Definition ID type mismatch. | Typed provider lookup. | SaveData Migration Work | No |

These rules belong to the later migration Work, not Work B's pure JSON/source validator. Migration may mutate SaveData only under its separately approved implementation contract; this validation specification itself is non-mutating.

## 7. Runtime Policy and Balance Advisory

| Rule ID | Severity | Dataset | Field | Condition | Message | Current evidence | Future owner | Mutating? |
|---|---|---|---|---|---|---|---|---|
| RUNTIME_MIXED_ANIMAL_TYPES_INVALID | Error | runtime assignment | assigned animals | Assignment violates wagon eligible-type or mixed-type policy. | Assigned draft-animal combination is invalid. | `CaravanCalculator` consumes runtime composition. | Core/UI Alignment Work | No |
| BALANCE_LOAD_MARGIN_SMALL | Warning | wagons | baseEfficientLoad/maxLoad | Project-defined advisory threshold is later configured and margin falls below it. | Efficient-to-maximum load margin needs balance review. | No current hard threshold; advisory only. | Content/balance owner | No |
| BALANCE_ANIMAL_CAPACITY_REVIEW | Warning | draft_animals | both load effects | Both values are unusually high under a later approved balance threshold. | Draft-animal capacity effects need balance review. | Both effects are semantically valid. | Content/balance owner | No |

Balance thresholds are intentionally not invented in Work A; enabling these advisories requires an approved numeric policy.
