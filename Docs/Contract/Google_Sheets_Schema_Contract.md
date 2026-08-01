# Google Sheets Schema Contract

## 1. Workbook Tabs

| Tab | Purpose |
|---|---|
| `00_README` | Ownership, workflow, terminology, and non-goals. |
| `01_ENUMS` | Allowed current C# enum string names; informative input for validation lists. |
| `10_ITEMS` | Production trade-item definitions. |
| `11_WAGONS` | Production wagon definitions. |
| `12_DRAFT_ANIMALS` | Production draft-animal definitions. |
| `90_VALIDATION_RULES` | Human-readable mirror of stable validation rule IDs. |
| `99_CHANGE_LOG` | Authoring change notes; not dataset content. |

All data columns below specify: column, source SO member, JSON field, type, required, nullable, default, unit, classification, exported, imported into SO, catalog membership use, validation, and notes. `contentVersion` and `developerNote` are authoring-only and are not exported in v1. Blank required cells are errors; optional strings use `""`, not JSON `null`.

## 2. `10_ITEMS`

Production IDs: `Apple`, `Bread`, `Cloth`, `Fish`, `Logs`, `Stone`, `Stover`, `Wheat`.

| Column | Source SO member | JSON | Type | Req. | Null | Default | Unit | Classification | Export | SO import | Membership | Validation | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| id | `TradeItemData.ItemId` | id | string | Yes | No | none | none | SO field/reference | Yes | Yes | identity | exact, nonblank, no outer whitespace, unique | Preserve case and spelling. |
| enabled | none | enabled | boolean | Yes | No | none | none | JSON-only metadata | Yes | No | Yes | boolean | Disabled rows still export. |
| displayName | `DisplayName` | displayName | string | Yes | No | none | none | SO field/display | Yes | Yes | No | non-empty | Not an ID source. |
| description | `Description` | description | string | Yes | No | `""` | none | SO field/display | Yes | Yes | No | string | Empty allowed. |
| rarity | `Rarity` | rarity | string enum | Yes | No | none | none | SO field | Yes | Yes | No | current enum name | Exact name. |
| category | `Category` | category | string enum | Yes | No | none | none | SO field | Yes | Yes | No | current enum name | Invalid category is an error. |
| baseBuyPrice | `BaseBuyPrice` | baseBuyPrice | integer/`long` | Yes | No | none | currency units | SO field | Yes | Yes | No | >= 0 | Zero is advisory. |
| baseSellPrice | `BaseSellPrice` | baseSellPrice | integer/`long` | Yes | No | none | currency units | SO field | Yes | Yes | No | >= 0 | Zero is advisory. |
| canStack | `CanStack` | canStack | boolean | Yes | No | none | none | SO field | Yes | Yes | No | boolean | — |
| maxCount | `MaxCount` | maxCount | integer | Yes | No | none | count | SO field | Yes | Yes | No | >= 1 | Applies even when not stackable. |
| weight | `Weight` | weight | number | Yes | No | none | project load units/item | SO field | Yes | Yes | No | finite, >= 0 | Quantity/current load excluded. |
| isConsumable | `IsConsumable` | isConsumable | boolean | Yes | No | none | none | SO field | Yes | Yes | No | boolean | — |
| localSpecialty | `LocalSpecialty` | localSpecialty | boolean | Yes | No | none | none | SO field | Yes | Yes | No | boolean | — |
| contentVersion | none | none | integer | No | No | 1 | revision | authoring-only metadata | No | No | No | >= 1 if present | Change tracking only. |
| developerNote | none | none | string | No | No | `""` | none | authoring-only metadata | No | No | No | string | Never runtime content. |

Direct Sprite references, `AffectModify`, and nested modifiers are deferred from flat v1. Quantity, market unit price, and inventory state are runtime/SaveData fields and excluded.

## 3. `11_WAGONS`

Production IDs: `Walk`, `Wagon_S`, `Wagon_M`.

| Column | Source SO member | JSON | Type | Req. | Null | Default | Unit | Classification | Export | SO import | Membership | Validation | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| id | `WagonData.WagonId` | id | string | Yes | No | none | none | SO field/reference | Yes | Yes | identity | common ID rules | Exact existing ID. |
| enabled | none | enabled | boolean | Yes | No | none | none | JSON-only metadata | Yes | No | Yes | boolean | All rows export. |
| displayName | `DisplayName` | displayName | string | Yes | No | none | none | SO field/display | Yes | Yes | No | non-empty | — |
| description | `Description` | description | string | Yes | No | `""` | none | SO field/display | Yes | Yes | No | string | — |
| wagonType | `WagonType` | wagonType | string enum | Yes | No | none | none | SO field | Yes | Yes | No | current enum name | Type policy also warns. |
| maxDurability | `MaxDurability` | maxDurability | integer | Yes | No | none | durability points | SO field | Yes | Yes | No | >= 0 | Current durability excluded. |
| baseEfficientLoad | `Overload` (`overLoad`) | baseEfficientLoad | number | Yes | No | none | project load units | SO field | Yes | Yes | No | finite, >= 0, <= maxLoad | Name clarifies threshold. |
| maxLoad | `MaxLoad` | maxLoad | number | Yes | No | none | project load units | SO field | Yes | Yes | No | finite, >= 0 | Physical limit. |
| baseMoveSpeed | `BaseMoveSpeed` | baseMoveSpeed | number | Yes | No | none | project speed units | SO field | Yes | Yes | No | finite, >= 0 | Type-specific warning applies. |
| inventorySlotCount | `InventorySlotCount` | inventorySlotCount | integer | Yes | No | none | slots | SO field | Yes | Yes | No | >= 1 | — |
| maxPullAnimals | `MaxPullAnimals` | maxPullAnimals | integer | Yes | No | none | animals | SO field | Yes | Yes | No | >= 0 | — |
| minRequireAnimals | `MinRequireAnimals` | minRequireAnimals | integer | Yes | No | none | animals | SO field | Yes | Yes | No | 0..maxPullAnimals | — |
| eligibleAnimalTypes | `EligibleAnimalTypes` | eligibleAnimalTypes | string array | Yes | No | `[]` | none | SO field/reference | Yes | Yes | No | unique valid enum names | JSON array, not delimited text. |
| rarity | `Rarity` | rarity | string enum | Yes | No | none | none | SO field | Yes | Yes | No | current enum name | — |
| baseBuyPrice | `BaseBuyPrice` | baseBuyPrice | integer/`long` | Yes | No | none | currency units | SO field | Yes | Yes | No | >= 0 | — |
| canStack | `CanStack` | canStack | boolean | Yes | No | none | none | SO field | Yes | Yes | No | boolean | — |
| maxCount | `MaxCount` | maxCount | integer | Yes | No | none | count | SO field | Yes | Yes | No | >= 1 | — |
| contentVersion | none | none | integer | No | No | 1 | revision | authoring-only metadata | No | No | No | >= 1 if present | — |
| developerNote | none | none | string | No | No | `""` | none | authoring-only metadata | No | No | No | string | — |

Excluded: `currentDurability`, `instanceId`, current/effective load, repair price and rarity multiplier, icon, prefab, modifier graph, local specialty, and SaveData snapshots. Repair rules belong to `WagonRepairContentPolicy`, not `WagonData`.

## 4. `12_DRAFT_ANIMALS`

Production IDs: `Horse`, `Donkey`.

| Column | Source SO member | JSON | Type | Req. | Null | Default | Unit | Classification | Export | SO import | Membership | Validation | Notes |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| id | `DraftAnimalData.DraftAnimalId` | id | string | Yes | No | none | none | SO field/reference | Yes | Yes | identity | common ID rules | Exact existing ID. |
| enabled | none | enabled | boolean | Yes | No | none | none | JSON-only metadata | Yes | No | Yes | boolean | All rows export. |
| displayName | `DisplayName` | displayName | string | Yes | No | none | none | SO field/display | Yes | Yes | No | non-empty | — |
| description | `Description` | description | string | Yes | No | `""` | none | SO field/display | Yes | Yes | No | string | — |
| animalType | `AnimalType` | animalType | string enum | Yes | No | none | none | SO field | Yes | Yes | No | current enum name | — |
| foodConsumptionPerSecond | `FeedConsumption` (`feedConsumption`) | foodConsumptionPerSecond | number | Yes | No | none | food units/second | SO field | Yes | Yes | No | finite, >= 0 | Time base is seconds. |
| baseMoveSpeed | `BaseMoveSpeed` | baseMoveSpeed | number | Yes | No | none | project speed units | SO field | Yes | Yes | No | finite, >= 0 | — |
| additionalEfficientLoad | `IncreaseOverLoad` (`increaseOverLoad`) | additionalEfficientLoad | number | Yes | No | none | project load units | SO field | Yes | Yes | No | finite, >= 0 | Efficient threshold effect. |
| increaseMaxLoad | `IncreaseMaxLoad` (`increaseMaxLoad`) | increaseMaxLoad | number | Yes | No | none | project load units | SO field/current mismatch | Yes | Yes | No | finite, >= 0 | Supported physical-limit effect; not deprecated. |
| rarity | `Rarity` | rarity | string enum | Yes | No | none | none | SO field | Yes | Yes | No | current enum name | — |
| baseBuyPrice | `BaseBuyPrice` | baseBuyPrice | integer/`long` | Yes | No | none | currency units | SO field | Yes | Yes | No | >= 0 | — |
| canStack | `CanStack` | canStack | boolean | Yes | No | none | none | SO field | Yes | Yes | No | boolean | — |
| maxCount | `MaxCount` | maxCount | integer | Yes | No | none | count | SO field | Yes | Yes | No | >= 1 | — |
| contentVersion | none | none | integer | No | No | 1 | revision | authoring-only metadata | No | No | No | >= 1 if present | — |
| developerNote | none | none | string | No | No | `""` | none | authoring-only metadata | No | No | No | string | — |

A draft animal may affect efficient load only, maximum load only, both, or neither. Excluded: instance identity, owned count, assignment, current food, runtime speed/load results, icon, prefab, modifiers, local specialty, and SaveData snapshots.
