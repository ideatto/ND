# Framework Command and Event Contract

## Status

- Command/Event policy: **Approved**
- Immediate repository-wide replacement: **Approved Direction - Staged Migration Required**

No production API, event, Unity Button callback, Scene, or Prefab is changed by this documentation branch.

- Current production numeric version: `6`
- Approved next numeric version: `7`
- v6 test saves use a visible reset boundary, not automatic `displayName → buildingId` migration.

## Rules

Direct SaveData access is allowed for display and simple inspection. Important state changes are requested through a command or service method. In the target contract, events notify other systems only after the state change and required save succeed. Queries return snapshots/read-only views and never mark Dirty. Event payload collections and reference types must not expose mutable SaveData instances.

These are target responsibilities. Existing direct mutations, `void` methods, result types, event names/timing, method signatures, and serialized callbacks remain compatible until inventoried and migrated.

## Proposed command surface

- Preparation: `UpdatePreparation(caravanId, patch)`, `UpdatePurchasePreview(caravanId, preview)`, `CancelPreparation(caravanId)`; successful changes mark Dirty.
- Trade: `Depart(caravanId, request)`, generating the trade GUID inside the commit boundary; immediate save. If the existing name is retained during migration, its equivalent signature is `TryStartTrade(caravanId, request)`.
- Settlement: `FinalizeSettlement(caravanId, tradeId)` and `ClaimSettlement(caravanId, tradeId)`; immediate save. Settlement claim does not repay rescue loans.
- Progression: growth, repair, `UpgradeBuilding(buildingId)`, one-time InvestmentQuest completion, rescue-loan issue, `RepayRescueLoan(amount)`, restricted-mode exit as part of the departure save boundary, wagon destruction, and Caravan-to-home cargo transfer; immediate save. Donation and cumulative investment are not target commands. Building consumes home inventory, not Caravan cargo. Investment goods inputs identify each source `caravanId` and exclude home inventory.
- Save: `Save(data)`, `MarkDirty(reason, entityId)`, and dirty-state query. `SaveResult Save(...)` is the single target API.

Results distinguish validation failure, not found, conflict/duplicate, unsupported version, serialization, file I/O, and unknown failures. A false/failed result states whether runtime state changed; until transaction staging exists, callers must not assume rollback.

Every state-changing multi-Caravan command takes its target IDs explicitly. `selectedCaravanId` may control which Caravan the UI displays, but commands must not read it to infer a mutation target. `ClaimSettlement` looks up the exact `(caravanId, tradeId)` pending entry, removes and resets only that Caravan on success, and returns a structured result containing at least `Succeeded`, a stable failure reason, and `SaveResult`. It does not issue or repay a rescue loan.

For the first rescue-loan integration stage, `IssueRescueLoan()` and `RepayRescueLoan(long amount)` return `SaveResult` directly. UI obtains player-facing domain reasons from a read-only `RescueLoanCalculator` validation query; the command repeats validation and does not save on domain rejection. A later `ProgressionCommandResult<T>` requires separate approval.

## Proposed queries

- `GetCaravans()` and `GetCaravan(caravanId)`
- `GetPreparation(caravanId)` and `GetReusableConfiguration(caravanId)`
- `GetTrade(caravanId, tradeId)`
- `GetPendingSettlements()` and `GetPendingSettlement(caravanId, tradeId)`
- investment-quest completion, rescue-loan, and unlock snapshot queries

## Proposed committed events

Committed payloads include stable IDs and commit/save revision where available: trade departed/state changed, settlement ready, `SettlementClaimed(caravanId, tradeId)`, investment quest completed, content unlocked, loan issued/repaid/closed, rescue restricted mode entered/exited, rebankruptcy detected, wagon destroyed/repaired, building upgraded, and save succeeded/failed. `SettlementClaimed` is emitted once per claimed entry only after reward application, pending removal, Save success, and runtime commit.

`PreparationChanged(caravanId, dirtyRevision)` and Dirty-state changes are non-committed notifications. They report an in-memory change and may refresh UI, but do not prove persistence. `SaveSucceeded(savedRevision)` separately reports which revision became durable; `SaveFailed(attemptedRevision, failureReason)` reports failure without committing the attempted change.

Events may repeat across subscription/recovery boundaries. Consumers deduplicate by IDs/revision and never apply rewards based only on receiving an event. Save failure events contain reason metadata but no mutable SaveData.

The canonical settlement notification is `TradeSettlementReady(caravanId, tradeId, result)`. It reports an already-created state transition after Save succeeds and runtime commit completes; receiving it is never authority to calculate, claim, or pay a settlement. UI compares the payload `caravanId` with its current selection only to decide visibility. Economy/Progression consumers use the full `tradeId` to prevent duplicate application and must not depend on `selectedCaravanId`.

Production currently exposes `TradeSettlementReady(caravanId, tradeId, result)` and `SettlementUiBridge` subscribes to it, but finalization can publish before checking SaveResult, so the surface is `Partial`. The bridge's ID-reducing compatibility event and boolean Claim method remain migration targets. Compatibility APIs delegate to the canonical operation once and are removed only after call-site and serialized-reference verification.

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
- Progression: growth, wagon repair/destruction, building, one-time investment quest, and rescue loan.
- Content/Tools: debug harnesses, failure presets, and event-count test data.

Each owner inventories and migrates their own scripts; ownership is confirmed rather than inferred from folders or Git history.

### Stage 5 - Event timing transition

For each migrated caller: validate command, stage mutation, save, confirm `SaveResult`, commit runtime state, then publish the committed completion event. The Save API/result type is implemented, but a caller is not described as committed until its result inspection and rollback/commit ordering are implemented and verified.

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
