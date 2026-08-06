# Caravan Composition Persistence - 2026-08-04

- Caravan Setting options use real `WagonData` and `DraftAnimalData` references from `SandboxSharedGameDataCatalog` through a UI-independent provider.
- Confirm resolves explicit `caravanId`, validates SO IDs, composition, and existing cargo capacity, then replaces only `wagon` and `animals`.
- Save failure restores both fields. Success clears the committed draft and uses the existing UI refresh path.
- Warehouse, Cargo, market, currency, travel progress, `PlayerMainManager`, and the SaveData schema were not changed.
- Existing saves remain loadable; confirmed compositions use stable catalog IDs and SO-derived values.
- No Scene or Prefab serialization change was required.

## 2026-08-06 transport inventory follow-up

- Player-owned wagon and draft-animal inventories now provide stable `instanceId` and `contentId` identity.
- Wagon selection remains instance-based; draft animals support content-and-quantity requests resolved to concrete owned instances by the production command.
- Resolution keeps matching instances already assigned to the edited Caravan, then uses inventory order, while excluding instances assigned to other Caravans.
- Validation is mutation-free and rejects ownership/assignment identity conflicts, incompatible composition, insufficient quantity, and existing cargo that does not fit the selected wagon.
- The InGame Scene still uses the temporary setting service. UI Draft cutover and `CaravanSettingRuntimeBridge` reference replacement must occur together.

Verification: new persistence tests 4/4 passed; existing setting integration tests 4/4 passed; C# build has 0 errors.
