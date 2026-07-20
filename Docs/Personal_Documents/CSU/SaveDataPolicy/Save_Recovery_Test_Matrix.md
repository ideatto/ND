# Save Recovery Test Matrix

Status in this contract branch: documentation-only; no production schema is activated, so all cases are pending automation/manual execution in implementation branches.

| Area | Scenario | Expected result |
|---|---|---|
| Multi-Caravan | Two Caravans remain in Preparation with different configurations | Both restore independently |
| Multi-Caravan | Traveling plus Preparation | States do not interfere |
| Multi-Caravan | Two Traveling trades | Full trade IDs and times remain distinct |
| Multi-Caravan | SettlementPending plus Traveling | Both restore independently |
| Multi-Caravan | Two pending settlements | Both remain queryable |
| Multi-Caravan | Claim one of two pending results | Only addressed reward/removal occurs |
| Multi-Caravan | Reopen with mixed states | Every Caravan restores independently |
| Multi-Caravan | Core defines an exclusive asset used twice | Command rejects conflict without mutation |
| Preparation | Unfinished preparation survives restart | Original inputs match |
| Preparation | Purchase preview survives restart | Preview matches |
| Preparation | Edit preview | Currency/inventory/confirmed cargo unchanged |
| Preparation | Cancel one preparation | Only addressed Caravan resets |
| Preparation | Claim settlement | Food/goods clear; fixed setup remains |
| Preparation | Query reusable setup | Result is per-Caravan |
| Settlement | Restart while pending | Exact saved result restores |
| Settlement | Restore pending | No recalculation occurs |
| Settlement | Repeat claim/recovery | Reward applies once |
| Settlement | Claim save failure | Command fails and no committed payment is exposed |
| Settlement | Claim one Caravan | Other Caravan unchanged |
| Settlement | Invalid ID pair | Rejected without mutation |
| Investment quest | Pay full currency cost once | Currency, completion, and unlock commit together |
| Investment quest | Submit goods from one or several Caravans | Only explicit usable Caravan stacks are deducted |
| Investment quest | Submit home inventory or Traveling goods | Rejected without mutation |
| Investment quest | Complete twice | No duplicate deduction or unlock |
| Loan | Existing assets satisfy fixed configuration | Issue is rejected as unnecessary |
| Loan | Only some fixed elements are missing | Only their fixed-price costs form principal |
| Loan | Issue succeeds and restarts | Restriction and permanent used state restore |
| Loan | Title or restart during restriction | Restriction remains active |
| Loan | First rescue departure save fails | Restriction remains and departure rolls back |
| Loan | First rescue departure save succeeds | Restriction clears; positive principal stays active |
| Loan | Settlement payout is non-positive | Repayment choice is hidden |
| Loan | Payout below principal and repayment selected | Full payout amount repays principal |
| Loan | Payout exceeds principal and repayment selected | Principal only is repaid |
| Loan | Repayment declined | Principal remains unchanged |
| Loan | Claim save failure | Reward, principal, pending, and preparation roll back |
| Loan | Second issue after prior use | Rejected by permanent used state |
| Loan | Traveling or claimable pending remains | Game over is deferred |
| Version | Missing optional child | Safe default object created |
| Version | Null collection | Empty list created |
| Version | Duplicate Caravan ID | Visible validation failure |
| Version | Duplicate trade ID/composite pending key | Visible validation failure |
| Version | Unknown shared ID | Reported without crash or silent deletion |
| Version | Version 1 input | Not silently accepted as target V2 |
| Version | Unsupported version | Documented behavior and visible log/result |
| Compatibility | Legacy API during additive stage | Existing API still compiles |
| Compatibility | Legacy adapter calls new API | Operation executes exactly once |
| Compatibility | Existing Unity Button references | Serialized callbacks remain valid |
| Event timing | Required save fails | New committed event is not emitted |
| Event compatibility | Legacy and new events coexist | UI handles the operation once |
| Access policy | Direct SaveData display read | Read remains available |
| Access policy | Important state is directly mutated | Site is identified for owner migration |
| Selection | Restart with valid selected Caravan | Saved selection restores |
| Selection | Selected Caravan is missing, locked, deleted, or invalid | First valid owned Caravan is selected |
| Save queue | Redundant Dirty and important requests coexist | Dirty requests merge; important requests remain queued |
| Save retry | Non-retryable validation failure | Immediate rollback; invalid operation is not repeated |
| Save retry | Retryable I/O fails twice then succeeds | Attempts occur at initial, +0.2s, and +0.5s |
| Save retry | Retryable I/O fails three times | PreCommandSnapshot rollback is reported |
| Event consumption | Repeatable event succeeds and restarts | Count increments and persists |
| Event consumption | One-time event count is already one | Second consumption is rejected |
| Loan restriction | Investment quest or unrelated economy is requested | Action is blocked |
| Loan restriction | Trade-preparation action is requested | Action is permitted when otherwise valid |
| Loan guarantee | Missing fixed rescue elements are evaluated | Only their shared-data fixed prices form principal |
| Wagon loss | Durability falls below zero | It clamps to zero and destruction runs once |
| Wagon loss | Destroyed in Preparation | Ownership and preparation references are removed |
| Wagon loss | Destroyed while Traveling | Trade fails and cargo/food losses enter settlement snapshot |
| Wagon loss | Destruction save fails | Ownership, references, cargo, food, and trade state roll back |
| Wagon repair | Positive repair is valid | Cost is floored once and durability increases |
| Wagon repair | Positive repair calculates to zero | Minimum cost is one |
| Wagon repair | Exceeds maximum or wagon is destroyed | Rejected without mutation |
| Building | Transfer selected Caravan cargo home | Both inventories save together |
| Building | Upgrade using home materials | Materials deduct and stable-ID level increments together |
| Building | Attempt direct Caravan material use | Rejected without mutation |
| Building | DisplayName-era save loads | Approved adapter or migration resolves stable ID |

## Test levels

- DTO unit tests: normalization, duplicate detection, serialization round trips, GUID preservation.
- Command tests with a failing save double: no success event, accurate mutation/rollback result, retry/idempotency.
- Integration tests: two-Caravan mixed states, restart restore, exact pending snapshot, one-of-many claim.
- Unity manual checks after implementation: Editor compilation and Console free of errors; no Scene/Prefab changes required.

## Unresolved integration fixtures

Tests need stable shared definition IDs, a controllable UTC provider, a save service that can fail before/after write, Core exclusivity validation, and Progression/System eligibility/rate/guarantee providers. Their ownership and interfaces require team confirmation.
