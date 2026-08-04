# Caravan Setting Production Service Contract

Updated: 2026-08-04  
Status: Production application service 2A implemented; InGame Scene migration pending

## Purpose

This document is the team-wide source of truth for Caravan Setting service ownership, API usage, persistence behavior, and the remaining Scene migration. Personal work logs are implementation evidence, not the canonical contract.

## Current dependency direction

```text
Scene / UI binding
  -> existing Provider and Command interfaces
  -> currently TestCaravanSettingService in the InGame Scene
  -> target CaravanSettingRuntimeBridge
  -> FrameworkRoot.CaravanSetting
  -> CaravanSettingApplicationService
  -> responsibility services
  -> SaveData / ISharedGameDataProvider / ISaveService
```

The production application service does not depend on `TestCaravanSettingService`. Direct `FrameworkRoot.Instance` access is limited to the composition root and the runtime bridge.

## Public access and contracts

Framework composition exposes the production facade through:

```csharp
FrameworkRoot.Instance.CaravanSetting
```

`CaravanSettingApplicationService` implements the existing five UI-facing contracts:

- `ICaravanSettingViewDataProvider`
- `ICaravanSettingCommand`
- `ICaravanLoadSettingViewDataProvider`
- `ICaravanLoadSettingCommand`
- `ICaravanCargoCatalogProvider`

Scene components should depend on those interfaces or on `CaravanSettingRuntimeBridge`. They must not construct the application service or mutate `SaveData` directly.

## Persistence and transaction contract

- Dependencies are explicit: current `SaveData` provider, `ISaveService`, shared-data provider, and Unity asset resolver.
- Commands validate first, snapshot the affected runtime save aggregate, mutate it, and call `Save` once.
- A successful save commits the in-memory result.
- A failed save or exception restores the snapshot and returns a visible failure result.
- Queries do not persist or mutate data.
- Caravan identity is the explicit `caravanId`; `selectedCaravanId` is not an authoritative persistence key.

## 2A limitations

- The project has no canonical unassigned wagon or draft-animal inventory yet.
- Saved Caravan equipment has no stable wagon/draft-animal definition IDs yet.
- Therefore the production command accepts only the instances already assigned to the selected Caravan.
- Missing definition IDs remain empty. Display-name guessing or implicit asset matching is prohibited.
- `CaravanSettingRuntimeBridge` exists, but the InGame Scene still references `TestCaravanSettingService`.

These limitations are deliberate safety boundaries, not completed inventory behavior.

## 2B contract decisions

Before Scene migration, the owning teams must agree on:

- stable `wagonDefinitionId` and `draftAnimalDefinitionId` fields;
- ownership and persistence location of unassigned equipment inventory;
- duplicate assignment and lock rules across multiple Caravans;
- migration behavior for old saves;
- unresolved-ID behavior and player-visible recovery.

Migration may use an automatic match only when it is unique and deterministic. Ambiguous or missing matches must remain unresolved and visible; data must not be silently replaced.

## Scene migration gate

`TestCaravanSettingService` may be moved or removed only after all of the following:

1. The 2B identity and inventory contract is approved and implemented.
2. Migration tests cover old and unresolved save data.
3. Runtime bridge asset resolution is configured.
4. InGame Scene Provider/Command references are replaced.
5. Scene contract, Missing Script, EditMode, and actual UI PlayMode regression tests pass.
6. The Framework/API and UI owners review the serialized-reference change.

## Validation baseline

The 2A baseline was verified with Unity `6000.5.2f1`:

- related EditMode tests: 36/36 passed;
- actual UI PlayMode smoke: 1/1 passed;
- relevant total: 37/37 passed;
- C# compile errors: 0;
- Missing Script errors: none;
- Scene and Prefab changes: none.

## Related documents

- [Framework API and Event Inventory](./Framework_API_Event_Inventory.md)
- [Multi-Caravan Save Architecture](./Multi_Caravan_Save_Architecture.md)
- [Framework CoreServices Team Usage Guide](../Guide/Framework_CoreServices_Team_Usage_Guide.md)
- [2A implementation and validation log](../Personal_Documents/JJH/0804_Caravan_Setting_Production_Application_Service_Work_Log.md)
- [Responsibility extraction work log](../Personal_Documents/JJH/0803_Caravan_Setting_Service_Responsibility_Extraction_Work_Log.md)
