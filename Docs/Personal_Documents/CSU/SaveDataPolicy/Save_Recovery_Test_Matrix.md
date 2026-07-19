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
| Donation | Separate towns | Balances remain independent |
| Donation | Process decay | Stored last-processed UTC is the only time baseline |
| Donation | Event consumes too much | Balance never becomes negative |
| Investment | Convert donation | Available donation and progress change together |
| Investment | Complete twice | Unlock applies once |
| Donation/Investment | Restart | Balances, progress, completion, unlocks restore |
| Loan | Responsible system says ineligible | Issue rejected |
| Loan | Eligible issue | Currency reaches supplied minimum guarantee |
| Loan | Repayment | Principal only; no interest |
| Loan | Second active issue | Rejected |
| Loan | Partial repayment/restart | Remaining balance restores |
| Loan | Full repayment | Balance zero and inactive |
| Loan | Save failure on issue/repay | Command fails with no committed mutation |
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
| Save retry | Retryable I/O failure | Configured retry path is followed, then success or rollback is reported |
| Event consumption | Repeatable event succeeds and restarts | Count increments and persists |
| Event consumption | One-time event count is already one | Second consumption is rejected |
| Loan restriction | Donation or investment is requested | Action is blocked |
| Loan restriction | Trade-preparation action is requested | Action is permitted when otherwise valid |
| Loan guarantee | Player selects configuration within fixed guaranteed amount | Selection is allowed; Framework does not choose a preset |

## Test levels

- DTO unit tests: normalization, duplicate detection, serialization round trips, GUID preservation.
- Command tests with a failing save double: no success event, accurate mutation/rollback result, retry/idempotency.
- Integration tests: two-Caravan mixed states, restart restore, exact pending snapshot, one-of-many claim.
- Unity manual checks after implementation: Editor compilation and Console free of errors; no Scene/Prefab changes required.

## Unresolved integration fixtures

Tests need stable shared definition IDs, a controllable UTC provider, a save service that can fail before/after write, Core exclusivity validation, and Progression/System eligibility/rate/guarantee providers. Their ownership and interfaces require team confirmation.
