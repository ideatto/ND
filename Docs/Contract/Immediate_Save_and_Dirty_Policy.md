# Immediate Save and Dirty Policy

## Approved target API direction

The target save contract is:

```csharp
SaveResult Save(SaveData data);
void MarkDirty(SaveDirtyReason reason, string entityId = null);
bool IsDirty { get; }
```

`SaveResult` contains `Succeeded`, `FailureReason: SaveFailureReason`, `Message: string`, and `FailedDataCategory: string`. `Message` is diagnostic text safe for logs. `SaveResult Save(...)` is the single approved target; a separate `TrySave()` or `void Save()` wrapper is not part of it. The API and result type are implemented, but caller-side result inspection, rollback, retry, and queue adoption remain partial or pending as identified in the inventory. A successful save clears only the dirty revision included in that attempt; changes made during an in-flight save remain Dirty.

This branch does not implement a timer, queue, merging, retry, or transaction engine.

## Save failure retry policy

Retry classification is part of the future result-based save contract. Temporary file locks, temporary I/O failures, and replace failures caused by transient filesystem conditions may be retryable. Invalid `SaveData`, validation failures, unsupported-data serialization failures, invalid IDs, and snapshot validation failures are non-retryable.

Retryable failures make at most three attempts including the initial attempt: retry after 0.2 seconds following the first failure and after 0.5 seconds following the second. `WriteFailed`, temporary file locks, transient replace failures, and explicitly classified transient filesystem failures are retryable. `InvalidData`, `ValidationFailed`, `DomainRejected`, `SerializationFailed`, and `SnapshotFailed` are non-retryable. Every final failure rolls back to the PreCommandSnapshot; `RollbackFailed` is its own final failure.

## Sequential queue and merge policy

Important commands execute sequentially and block other important-command input. Important requests are not merged and each returns its own result. Redundant Dirty autosaves may merge and run after the important save. An important request containing the complete latest state may obsolete an earlier Dirty request. The next important command cannot run until rollback finishes.

## Immediate-save operations

Trade departure, settlement confirmation/finalization, settlement claim, growth purchase, wagon repair, building purchase/upgrade, one-time investment-quest completion, rescue-loan issue, rescue-loan repayment, wagon destruction, and Caravan-to-home cargo transfer require durable save success before the operation reports success.

Because the current flow can mutate runtime state before calling a void save, changing only the return type is insufficient. Each command must adopt validation plus snapshot/rollback, or transaction-style staging and commit. No rollback or atomicity guarantee exists until implemented and tested.

Rescue-loan issue and `RepayRescueLoan(amount)` each stage player trading currency and the complete loan state in one immediate-save boundary. Rescue-trade departure stages the departure state and restricted-mode release in the same save. Settlement claim does not modify loan principal and does not include repayment selection.

## Snapshot and rollback contract

- `PreCommandSnapshot` is created immediately before each important command and restores that command after final save failure.
- `LastDurableSnapshot` updates only after durable save success and supports whole-session recovery.
- Disk backup is written before replacing the main save file and is used for corrupt-main-file or load-failure recovery.

SaveData snapshots use the same deep-copy path as persistence, followed by normalization, validation, and runtime lookup rebuild. ViewData and other derived lookup/cache objects are rebuilt rather than serialized into a snapshot. After rollback succeeds, UI rebuilds from restored state. If rollback fails, the affected input remains blocked and UI directs the player to Title recovery or restart.

## Dirty operations

Non-critical confirmed changes mark Dirty: per-Caravan destination/route/setup/cargo/food preparation, purchase preview changes, and other explicitly approved non-critical confirmed state. Cancel preparation also marks Dirty. Display-only UI state, formatted strings, selected tabs, open popups, hover/selection, and button interactability never mark Dirty.

`MarkDirty` records intent; it does not imply disk persistence. Queries are side-effect free. Immediate save may also mark a revision internally, but success must durably include it before publishing success events.

`PreparationChanged(caravanId, dirtyRevision)` is a non-committed notification that preparation state changed in memory and was marked Dirty. It may refresh UI, but it never proves disk persistence. `SaveSucceeded(savedRevision)` is the committed notification that the save containing revisions through `savedRevision` succeeded. `SaveFailed(attemptedRevision, failureReason)` reports failure without converting the attempted preparation changes into committed state. If a newer revision is created while a save is in flight, the aggregate remains Dirty after the older revision succeeds.
