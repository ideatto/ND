# Immediate Save and Dirty Policy

## Proposed API direction

The current `ISaveService.Save(SaveData)` returns `void`, so callers cannot distinguish durable success. The smallest safe future contract is additive:

```csharp
SaveResult TrySave(SaveData data, SaveReason reason);
void MarkDirty(SaveDirtyReason reason, string entityId = null);
bool IsDirty { get; }
```

`SaveResult` contains `Succeeded`, a stable `SaveFailureReason`, and diagnostic text safe for logs. Existing `Save` remains only as a temporary compatibility wrapper until consumers migrate. A successful save clears only the dirty revision included in that attempt; changes made during an in-flight save remain Dirty.

This branch does not implement a timer, queue, merging, retry, or transaction engine.

## Save failure retry policy

Retry classification is part of the future result-based save contract. Temporary file locks, temporary I/O failures, and replace failures caused by transient filesystem conditions may be retryable. Invalid `SaveData`, validation failures, unsupported-data serialization failures, invalid IDs, and snapshot validation failures are non-retryable.

Retryable failures retry up to an approved configurable limit and roll back if all attempts fail. Non-retryable failures do not repeat the same invalid operation and roll back immediately. The retry interval and retry limit value remain unresolved; this document does not invent defaults or add runtime behavior.

## Sequential queue and merge policy

The future queue processes requests sequentially. Redundant Dirty autosaves may merge, but important requests remain queued and are never discarded merely because a Dirty request exists. An important request containing the complete latest state may make an earlier Dirty request obsolete. Requests are not merged when distinct callers require separate success/failure results.

## Immediate-save operations

Trade departure, settlement confirmation/finalization, settlement claim, growth purchase, wagon repair, building purchase/upgrade, donation, investment conversion/completion, loan issue, and loan repayment require durable save success before the operation reports success.

Because the current flow can mutate runtime state before calling a void save, changing only the return type is insufficient. Each command must adopt validation plus snapshot/rollback, or transaction-style staging and commit. No rollback or atomicity guarantee exists until implemented and tested.

## Dirty operations

Non-critical confirmed changes mark Dirty: per-Caravan destination/route/setup/cargo/food preparation, purchase preview changes, and other explicitly approved non-critical confirmed state. Cancel preparation also marks Dirty. Display-only UI state, formatted strings, selected tabs, open popups, hover/selection, and button interactability never mark Dirty.

`MarkDirty` records intent; it does not imply disk persistence. Queries are side-effect free. Immediate save may also mark a revision internally, but success must durably include it before publishing success events.
