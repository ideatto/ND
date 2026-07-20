# Framework API and Event Migration Inventory

This is a documentation-only Stage 1 template. Record evidence before behavior changes; do not infer an owner.

## Mutation methods

| Method and signature | File | State changed | Current return/save behavior | Serialized callback | Confirmed owner | Target contract | Migration status |
|---|---|---|---|---|---|---|---|
| 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | Not started |

## Events and subscribers

| Event and payload | Declaration | Publication point and timing | Subscriber | Duplicate risk | Canonical target event | Confirmed owner | Migration status |
|---|---|---|---|---|---|---|---|
| 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | Not started |

## Direct SaveData mutations

| Mutation site | Field/state | Reason | Required command/service | Confirmed owner | Migration status |
|---|---|---|---|---|---|
| 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | Not started |

## Unity Button and serialized method references

| Scene/Prefab | Component | Target object/method | Owning team member | Compatibility adapter needed | Verification status |
|---|---|---|---|---|---|
| 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | 확인 필요 | Not checked |

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

- Concrete rescue-configuration IDs, quantities, and fixed shared-data prices
- Exact game-over evaluation hook after settlement/claim, relevant asset loss, and load recovery
- Loan-flow UI presentation and command naming consistent with approved restricted-mode behavior
- Product Version 2 label versus the repository's monotonic numeric save version
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
