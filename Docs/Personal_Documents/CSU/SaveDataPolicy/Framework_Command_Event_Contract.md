# Framework Command and Event Contract

## Status

- Command/Event policy: **Approved**
- Immediate repository-wide replacement: **Approved Direction - Staged Migration Required**

No production API, event, Unity Button callback, Scene, or Prefab is changed by this documentation branch.

## Rules

Direct SaveData access is allowed for display and simple inspection. Important state changes are requested through a command or service method. In the target contract, events notify other systems only after the state change and required save succeed. Queries return snapshots/read-only views and never mark Dirty. Event payload collections and reference types must not expose mutable SaveData instances.

These are target responsibilities. Existing direct mutations, `void` methods, result types, event names/timing, method signatures, and serialized callbacks remain compatible until inventoried and migrated.

## Proposed command surface

- Preparation: `UpdatePreparation(caravanId, patch)`, `UpdatePurchasePreview(caravanId, preview)`, `CancelPreparation(caravanId)`; successful changes mark Dirty.
- Trade: `Depart(caravanId, request)`, generating the trade GUID inside the commit boundary; immediate save.
- Settlement: `FinalizeSettlement(caravanId, tradeId, snapshot)` and `ClaimSettlement(caravanId, tradeId)`; immediate save.
- Progression: growth, repair, building, donation, investment, loan issue, and manual repayment commands; immediate save.
- Save: `TrySave(data, reason)`, `MarkDirty(reason, entityId)`, and dirty-state query.

Results distinguish validation failure, not found, conflict/duplicate, unsupported version, serialization, file I/O, and unknown failures. A false/failed result states whether runtime state changed; until transaction staging exists, callers must not assume rollback.

## Proposed queries

- `GetCaravans()` and `GetCaravan(caravanId)`
- `GetPreparation(caravanId)` and `GetReusableConfiguration(caravanId)`
- `GetTrade(caravanId, tradeId)`
- `GetPendingSettlements()` and `GetPendingSettlement(caravanId, tradeId)`
- town donation, investment, rescue-loan, and unlock snapshot queries

## Proposed committed events

Payloads include stable IDs and commit/save revision where available: preparation changed/cancelled, trade departed/state changed, settlement ready/claimed, donation changed/consumed, investment progressed/completed, content unlocked, loan issued/repaid/closed, save succeeded/failed, and Dirty state changed.

Events may repeat across subscription/recovery boundaries. Consumers deduplicate by IDs/revision and never apply rewards based only on receiving an event. Save failure events contain reason metadata but no mutable SaveData.

## Compatibility-first migration

### Stage 1 - Documentation and inventory

List current mutation methods, events, direct SaveData mutation sites, and Unity Button or serialized method references. Assign each call site to its confirmed owner without changing behavior. Use `Framework_API_Event_Inventory.md` as the working template.

### Stage 2 - Additive contracts

Add result types, command DTOs, and service methods without deleting existing APIs or renaming events. Conceptual examples such as `TradeDepartureResult RequestTradeDeparture(TradeDepartureCommand command)` and `SaveResult Save(SaveData data)` do not authorize implementation in this branch.

### Stage 3 - Compatibility adapters

Where safe, legacy APIs delegate to one new internal source of truth, preserve existing return behavior, and execute the operation once. Mark an API temporary or obsolete only after the new API compiles. Unity serialized callbacks remain intact.

### Stage 4 - Owner-by-owner migration

- Framework: Save API, queue, snapshot/rollback, and event publication timing.
- Core: departure, claim, settlement confirmation, and Caravan asset mutations.
- UI: Button calls, input blocking, result handling, and rollback refresh.
- Progression: growth, repair, donation, investment, and loan.
- Content/Tools: debug harnesses, failure presets, and event-count test data.

Each owner inventories and migrates their own scripts; ownership is confirmed rather than inferred from folders or Git history.

### Stage 5 - Event timing transition

After the result-based Save API works: validate command, stage mutation, save, confirm `SaveResult`, commit runtime state, then publish the completion event. No event is described as committed while the production `void Save()` path remains active.

### Stage 6 - Legacy removal

Remove legacy contracts only after all repository call sites migrate, serialized Scene/Prefab callbacks are checked, all owner branches merge, no external subscriber uses the old event, regression tests pass, and the team approves removal.

## Event compatibility

When old and new events coexist, emit both from one internal publication point without repeating the mutation. Document the canonical event, prevent UI from subscribing to both for the same handling path, and remove the legacy event only after every subscriber migrates.

Immediate replacement risks compile errors, missing Button callbacks, broken subscriptions, duplicate processing, stale UI, and branch-level API incompatibility.

## Suggested follow-up branches

- `feature/framework/result-save-api`
- `feature/framework/save-queue-retry`
- `feature/framework/command-adapters`
- `feature/framework/committed-events`
- Owner branches for Core, UI, Progression, and Content/Tools migrations after inventory approval
