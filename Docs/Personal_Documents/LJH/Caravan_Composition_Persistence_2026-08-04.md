# Caravan Composition Persistence - 2026-08-04

- Caravan Setting options use real `WagonData` and `DraftAnimalData` references from `SandboxSharedGameDataCatalog` through a UI-independent provider.
- Confirm resolves explicit `caravanId`, validates SO IDs, composition, and existing cargo capacity, then replaces only `wagon` and `animals`.
- Save failure restores both fields. Success clears the committed draft and uses the existing UI refresh path.
- Warehouse, Cargo, market, currency, travel progress, `PlayerMainManager`, and the SaveData schema were not changed.
- Existing saves remain loadable; confirmed compositions use stable catalog IDs and SO-derived values.
- No Scene or Prefab serialization change was required.

Verification: new persistence tests 4/4 passed; existing setting integration tests 4/4 passed; C# build has 0 errors.
