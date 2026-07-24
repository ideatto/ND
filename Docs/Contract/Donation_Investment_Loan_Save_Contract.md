# Donation, Investment, and Loan Save Contract

## Removed donation model

The previous town-donation balance, lifetime total, decay, timestamps, and donation-consumption model is removed from the target contract. `TownDonationSaveData` must be removed from the target schema after production usage and stored-data compatibility are checked. Any remaining production dependency is an implementation migration concern, not active target policy.

## Repeatable event consumption (non-donation systems)

Persist only `eventId` and a non-negative integer `consumptionCount`. `eventId` identifies the event type. One-time events reject processing once the count is at least one; repeatable events use the current count to select the next occurrence and increment it only through a successful operation. Runtime reconstruction must be deterministic from `eventId + consumptionCount` and shared definition data; SaveData does not store every occurrence instance.

Per-occurrence random results cannot be reconstructed from only a count unless those results were already finalized in a trade or settlement snapshot.

## One-time investment quest

Status: Superseded for InvestmentQuest details by `Investment_Quest_SaveData_Contract.md` (decision date 2026-07-21). The Rescue Loan sections below remain active.

The previous donation conversion and cumulative investment-progress model is removed. An investment quest is one-time, has no accumulated progress and no split payment. Its full cost is paid once. SaveData stores a completion entry containing `investmentQuestId`, `townId`, and `completedUtcTicks`; entry existence is completion and no `isCompleted` or `isRewardClaimed` field is stored.

Definitions in SharedGameData supply an alternative full trading-currency cost, a full item-cost set, and unlock IDs. One request selects exactly one payment mode: full currency or the full required goods set; mixed currency-plus-goods payment, partial payment, and accumulated progress are not allowed. Completion validates and deducts the selected payment, records completion, applies unlocks immediately, saves, then publishes completion. 거점 도시 인벤토리의 무역품, construction materials, ineligible items, and goods locked by Traveling or SettlementPending are excluded. Every submitted stack explicitly carries `caravanId: string`, `itemId: string`, and `amount: int`. The selected payment, completion, and unlock mutations roll back together on save failure; a completed quest cannot deduct or reward twice. There is no separate Reward Claim.

## Rescue loan

This contract follows `0720_Progression_Requested_Framework_Integration.md` and the Progression-owned `RescueLoanCalculator` as the authoritative rescue-loan behavior. Framework integrates calculator results into SaveData and durable save boundaries; it does not redefine the calculation formula.

`RescueLoanSaveData` stores `loanId: string`, `originalPrincipal: long`, `remainingPrincipal: long`, `isActive: bool`, `issuedUtcTicks: long`, and `isRestrictedPreparation: bool`. It is stored at `SaveData.rescueLoan`. It does not store a permanent prior-use flag or a loan phase in this contract. There is no interest, credit score, overdue penalty, settlement auto-repayment, or multiple simultaneous loan model.

The loan is offered only when the calculator's status evaluation permits it. The issue principal is the full fixed `RescueLoanDefinition.MinimumTradeCost`, not the current currency shortfall and not the cost of individually missing wagon, animal, mercenary, food, or cargo elements. An active loan blocks another issue. After a loan is fully repaid and becomes inactive, future eligibility is determined again by `RescueLoanCalculator.EvaluateStatus(...)`; this contract does not impose a permanent once-per-playthrough ban.

Issue stages the player's trading currency and the entire rescue-loan DTO, then performs one immediate save. Save success commits the issue, enters `RestrictedPreparation`, and permits committed issue/restriction events. Save failure restores both currency and the previous loan snapshot and emits no committed success event.

While `isRestrictedPreparation` is true, only the approved rescue-trade preparation flow, required purchases and placement, Caravan configuration, preparation cancel, Title access, settings, and game exit are allowed. Building upgrades, growth purchases, investment quests, unrelated economy, manual repayment, and spending the rescue funds outside the approved preparation flow are blocked. Title access or restart preserves the restriction.

Restriction release is staged in the same immediate-save boundary as the successful rescue-trade departure. Core departure validation alone does not release it. The departure state, full trade ID, preparation changes, and `isRestrictedPreparation = false` save once; save failure rolls all staged departure changes back and leaves the restriction active. Positive `remainingPrincipal` and `isActive` remain after departure.

Repayment uses the separate command `RepayRescueLoan(long amount)`. Partial and full repayment are supported. Requests are rejected without saving when the amount is zero, negative, greater than the remaining principal, greater than available trading currency, made without an active loan, made during restricted preparation, or would leave trading currency below `MinimumTradeCost`. Repayment stages currency and loan state, saves once, and restores both snapshots on failure. Full repayment stores zero remaining principal, `isActive = false`, and `isRestrictedPreparation = false`.

Settlement finalization and claim never repay the loan. General settlement calculation sets any legacy `LoanRepayment` input to zero, pending settlement snapshots do not store an automatic repayment amount, and Claim does not mutate loan principal. Repayment occurs only through `RepayRescueLoan(amount)`.

After settlement and other relevant economy changes, Framework requests a deterministic status evaluation using current trading currency, `MinimumTradeCost`, and active-loan state. `CanOfferLoan` may expose a rescue-loan offer. `IsRebankrupt` blocks another issue while the active loan remains and is delivered as a warning/game-over candidate to UI; Framework does not directly control the game-over screen. Rebankruptcy is derived on load unless a separate final game-over persistence contract is approved.
