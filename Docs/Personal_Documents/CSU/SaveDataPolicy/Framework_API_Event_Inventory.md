# Framework API and Event Migration Inventory

This Stage 1 inventory records the production signatures inspected on 2026-07-21. `Implemented` means the inspected contract and production use exist; `Partial` means production behavior exists but is still selected-Caravan/single-active or lacks the final ID/result contract; `Contract Only` means the target is documented but absent from inspected code; `Missing` means the required surface was not found. Ownership remains unassigned unless separately confirmed.

## Mutation methods

| Method and signature | File | State changed | Current return/save behavior | Serialized callback | Confirmed owner | Target contract | Migration status |
|---|---|---|---|---|---|---|---|
| `SaveResult ISaveService.Save(SaveData data)` | `Assets/_Project/11.CoreServices/Scripts/Save/ISaveService.cs`; `JsonSaveService.cs` | Numeric version/timestamp and save file | Returns stable `SaveResult`; file write success is `Succeeded` | None found in inspected files | 확인 필요 | Same signature | Implemented |
| Dirty query / `MarkDirty(...)` | Not found in inspected save service | Target dirty revision | No production surface found | None found | 확인 필요 | `MarkDirty(reason, entityId)` plus read-only query | Missing |
| `TryStartTrade(CaravanData caravan, float distanceKm, string tradeId, string routeId, bool saveImmediately = true)` | `TradeStartService.cs` | Selected compatibility progress/snapshot and screen state | Validation result; can save immediately; no explicit `caravanId` command | No serialized callback found | 확인 필요 | `Depart(caravanId, request)` or `TryStartTrade(caravanId, request)` | Partial |
| `FinalizeSettlement(caravanId, tradeId)` | Not found in inspected production surface | Exact trade and pending entry | Target structured result and immediate save | None found | 확인 필요 | Same signature | Contract Only |
| `bool ClaimSettlementAndReset()` | `TradeProgressCoordinator.cs`; bridge in `FrameworkRoot.cs` | Selected compatibility pending/progress, rewards, runtime Caravan | Boolean; coordinator performs save/rollback path, but target IDs are inferred from current state | `SettlementUiDataAdapter.OnClickClaimSettlement()` calls bridge; Scene/Prefab binding not inspected | 확인 필요 | `ClaimSettlementResult ClaimSettlement(caravanId, tradeId)` | Partial / replacement target |
| `ClaimSettlement(caravanId, tradeId)` | Not found in inspected production surface | Exact composite pending entry and addressed Caravan | Target result includes success, failure reason, and `SaveResult` | None found | 확인 필요 | Same signature | Contract Only |
| `SaveResult IssueRescueLoan()` | `TradeStartService.cs`; exercised by `FrameworkM1LoopE2EEditorTests.cs` | Currency and rescue-loan DTO | Returns `SaveResult`; test coverage includes duplicate issue and save-failure rollback | 확인 필요 | 확인 필요 | Same signature | Implemented |
| `SaveResult RepayRescueLoan(long amount)` | `TradeStartService.cs`; exercised by `FrameworkM1LoopE2EEditorTests.cs` | Currency and rescue-loan DTO | Returns `SaveResult`; test coverage includes partial/full repayment and save-failure rollback | 확인 필요 | 확인 필요 | Same signature | Implemented |

## Events and subscribers

| Event and payload | Declaration | Publication point and timing | Subscriber | Duplicate risk | Canonical target event | Confirmed owner | Migration status |
|---|---|---|---|---|---|---|---|
| `TradeSettlementReady(string tradeId, JourneyResultData result)` | `FrameworkEvents.cs` | `RaiseTradeSettlementReady` after current settlement persistence path; `RestorePendingSettlement` may replay | `SettlementUiBridge` in `FrameworkRoot.cs`; its `SettlementReady` is consumed by `SettlementUiDataAdapter` | Replay and legacy/canonical dual-subscription risk | `TradeSettlementReady(caravanId, tradeId, result)` | 확인 필요 | Partial / compatibility |
| `TradeSettlementReady(caravanId, tradeId, result)` | Not found | Target: after addressed pending snapshot save succeeds | No production subscriber yet | Must deduplicate by full trade ID; UI uses Caravan ID only for visibility | Same signature | 확인 필요 | Contract Only |
| `InGameScreenChanged(InGameScreenState)` | `FrameworkEvents.cs`; raised by `InGameScreenStateRouter.cs` | Router updates `CurrentScreenState` before publication | `SettlementUiDataAdapter` in inspected scope | Forced refresh may intentionally repeat | Same signature | 확인 필요 | Implemented |
| `PreparationChanged` | Not found in `11.CoreServices` C# search | Target notification only | None found | Unknown until implementation | ID-bearing committed/dirty event | 확인 필요 | Missing |
| `WagonDestroyed` | Not found in `11.CoreServices` C# search | Target notification after staged destruction save | None found | Unknown until implementation | ID-bearing committed event | 확인 필요 | Missing |

## Direct SaveData mutations

| Mutation site | Field/state | Reason | Required command/service | Confirmed owner | Migration status |
|---|---|---|---|---|---|
| `TradeStartService.TryStartTrade` | Selected compatibility `tradeProgress`, Caravan snapshot/state | Existing single-active departure path | Explicit-ID Depart command | 확인 필요 | Partial |
| `TradeProgressCoordinator` settlement creation/recovery | Selected compatibility `pendingSettlement`, `LastSettlementTradeId`, `LastSettlementResult` | Existing single-active finalization and replay | ID-based Finalize/query plus canonical collection lookup | 확인 필요 | Partial |
| `TradeProgressCoordinator.ClaimSettlementAndReset` | Selected compatibility pending/progress, player reward/location, runtime Caravan | Existing claim path | `ClaimSettlement(caravanId, tradeId)` | 확인 필요 | Partial |

## Unity Button and serialized method references

| Scene/Prefab | Component | Target object/method | Owning team member | Compatibility adapter needed | Verification status |
|---|---|---|---|---|---|
| 확인 필요 | `SettlementUiDataAdapter` | `OnClickClaimSettlement()` | 확인 필요 | Yes, while legacy bridge is retained | Code call verified; serialized Scene/Prefab reference not checked |

## Owner migration checklist

- [ ] Framework: Save API, sequential queue/merge, retry classification, snapshot/rollback, publication timing
- [ ] Core: departure, claim, settlement confirmation, Caravan asset mutations
- [ ] UI: Button references, input blocking, result handling, rollback refresh, duplicate subscription prevention
- [ ] Progression: growth, wagon repair/destruction, building upgrade, one-time investment quest, rescue loan and restricted-mode policy
- [ ] Content/Tools: debug harnesses, save-failure presets, repeatable-event count data
- [ ] Each entry has a confirmed owner and branch
- [ ] Serialized callbacks are checked in Unity Editor before legacy removal
- [ ] External subscribers and owner branches are merged before legacy removal

## Remaining implementation decisions

- Confirm the Progression-owned `RescueLoanDefinition.MinimumTradeCost` asset/ID and the integration lookup path
- Exact game-over evaluation hook after settlement/claim, relevant asset loss, and load recovery
- Loan-flow UI presentation for issue, separate partial/full repayment, restriction, and rebankruptcy snapshots
- Migration ordering for product-label `V2` documentation versus numeric schema version 6 is resolved; implementation compatibility for older numeric versions remains a separate policy concern
- Exclusive cross-Caravan asset assignment policy

## Risk register

| Risk | Control |
|---|---|
| Compile or branch incompatibility | Add contracts before removing old signatures |
| Missing Unity Button callback | Preserve callback signatures and verify serialized references |
| Duplicate mutation or UI handling | One internal source/publication point; subscriber inventory |
| Event emitted after failed save | Result-based save and failure-path tests before timing transition |
| Lost or merged important save result | Sequential processing; never merge calls needing distinct results |
| Non-deterministic event reconstruction | Persist finalized random results in trade/settlement snapshots |
| Invalid policy defaults | Keep unresolved values configurable and unimplemented |
