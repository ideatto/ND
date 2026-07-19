# Donation, Investment, and Loan Save Contract

## Town donation

Each `TownDonationSaveData` stores `townId`, available amount, lifetime donated amount, last processed decay UTC timestamp, and event-consumption idempotency data where needed. Amounts use a team-approved integral unit and cannot fall below zero. Framework persists timestamps; Progression/System or definitions supply decay rates and event costs.

Donation decay uses game time. Online and offline progression use the same approved real-time-to-game-time conversion policy, and decay runs only after elapsed real time has been converted. If the current timestamp is earlier than the last processed timestamp, elapsed time is zero, a warning is logged, and the stored last-processed timestamp is not moved backward. Donation is clamped at zero. The decay interval, fixed decay amount, maximum offline duration, and protected minimum balance remain unresolved and must come from approved Progression/System or shared data.

Donation, decay processing, and event consumption are commands keyed by town. Their target contract validates and stages changes, saves immediately where value is consumed or granted, and publishes completion events only after durable success. This timing is not guaranteed by the current production `void Save()` API and requires the staged migration in `Framework_Command_Event_Contract.md`.

## Repeatable event consumption

Persist only `eventId` and a non-negative integer `consumptionCount`. `eventId` identifies the event type. One-time events reject processing once the count is at least one; repeatable events use the current count to select the next occurrence and increment it only through a successful operation. Runtime reconstruction must be deterministic from `eventId + consumptionCount` and shared definition data; SaveData does not store every occurrence instance.

Per-occurrence random results cannot be reconstructed from only a count unless those results were already finalized in a trade or settlement snapshot.

## Investment

Each `InvestmentSaveData` stores `investmentId`, source `townId`, converted donation/current progress, a stable requirement/target reference, completion state, and unlocked content/state IDs. Conversion cannot exceed available town donation. Completion and unlock application are idempotent; an already completed investment never grants twice. No time-based completion is defined here.

Required values come from Progression/System or shared data, not Framework constants. Donation conversion and completion/unlock are one staged immediate-save operation.

## Rescue loan

`RescueLoanSaveData` stores full `loanId`, original principal, remaining principal, active state, and issued UTC timestamp. There is no interest, score, overdue penalty, or multiple product model. Only one active loan is allowed.

The responsible system supplies eligibility and the minimum guarantee. `IssueRescueLoan` and `RepayRescueLoan(amount)` validate, stage currency and principal changes together, and require immediate save success. Partial repayment persists. Full repayment sets remaining balance to zero and inactive. A second active issue and over/negative repayment are rejected. Automatic settlement-profit deduction is outside this contract.

After a rescue loan is issued, the target policy enters a restricted trade-preparation mode. It permits trade-related preparation and a player-selected valid configuration within available funds, while blocking donation, investment, and unrelated economic or progression actions. The mode exits only after approved departure succeeds. Cancellation, failure to form a valid configuration, Title access, settings/quit availability, and persistence across restart remain unresolved; no behavior is implied for them.

The guarantee is one configurable fixed minimum-trade cost supplied by Progression/System or shared data and compared with player-wide usable trading currency. Framework neither hardcodes it nor calculates the cheapest market combination, and it guarantees no wagon, animal, food, or cargo preset. UI must show whether the selected configuration exceeds the guaranteed amount.
