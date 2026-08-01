# Google Sheets JSON Export Contract

## 1. Files and Dataset Names

| File | `dataset` | Required |
|---|---|---|
| `items.json` | `items` | Yes |
| `wagons.json` | `wagons` | Yes |
| `draft_animals.json` | `draft_animals` | Yes |
| `manifest.json` | n/a | Yes |
| `enums.json` | `enums` | No |

Each dataset uses exactly:

```json
{
  "schemaVersion": 1,
  "dataset": "items",
  "records": []
}
```

## 2. Record Fields

Exact fields and required status are those marked `Export: Yes` in [Google Sheets Schema Contract](Google_Sheets_Schema_Contract.md). Dataset records contain no `contentVersion`, `developerNote`, timestamp, Sprite, prefab, nested modifier, or runtime/SaveData field.

Primitive encoding:

- IDs, display text, descriptions, and enums: JSON strings. Enums use exact current C# member names.
- Flags including `enabled`: JSON booleans, never strings or numbers.
- Counts and prices: integral JSON numbers. Prices must be exactly representable as C# `long` values.
- Loads, weights, speeds, and consumption: finite JSON numbers; `NaN`, positive/negative infinity, and numeric strings are invalid.
- `eligibleAnimalTypes`: JSON array of unique enum-name strings.

## 3. Required, Null, and Empty Handling

Every schema-required field must be present. JSON `null` is invalid for all v1 record fields. Required identifiers and display names cannot be empty. Description may be `""`; arrays may be `[]` where the schema allows it. Export must not trim, normalize, or manufacture values. A source ID with leading/trailing whitespace fails validation instead of being corrected.

## 4. Unknown Fields

Unknown envelope or record fields are errors by default (`COMMON_UNKNOWN_FIELD`). This prevents misspellings from being silently ignored. A schema-version change must explicitly add or retire fields.

## 5. Enabled Behavior

All spreadsheet rows are exported. `enabled` remains in each JSON record and is consumed by validation and future catalog-membership integration; it is not mapped into a ScriptableObject.

```json
{
  "id": "SomeDefinition",
  "enabled": false
}
```

Disabled records remain stable source records. Their presence does not authorize asset deletion. A row missing entirely from the source creates a drift warning for manual review.

## 6. Ordering and Identity

Export records deterministically by exact `id` using ordinal comparison. Array-valued enum references are exported in authored order unless a later field contract defines semantic ordering. Ordering is never record identity; exact ID is identity.

## 7. Manifest

`manifest.json` contains the export-level metadata, for example:

```json
{
  "schemaVersion": 1,
  "sourceRevision": "authoring-revision",
  "exportedAtUtc": "2026-08-01T00:00:00Z",
  "datasets": ["items", "wagons", "draft_animals"]
}
```

`exportedAtUtc` must be an ISO-8601 UTC timestamp. Source revision and timestamps belong only in the manifest so unchanged dataset files remain byte-stable.

## 8. Schema Version Behavior

Version `1` is the only supported version in this contract. Missing, non-integral, or unsupported versions fail validation. Readers must not guess compatibility. A future version requires an explicit contract and validator update; it must not reinterpret v1 silently.
