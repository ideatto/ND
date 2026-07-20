# Donation, Investment, and Loan Save Contract

## Removed donation model

The previous town-donation balance, lifetime total, decay, timestamps, and donation-consumption model is removed from the target contract. `TownDonationSaveData` must be removed from the target schema after production usage and stored-data compatibility are checked. Any remaining production dependency is an implementation migration concern, not active target policy.

## Repeatable event consumption (non-donation systems)

Persist only `eventId` and a non-negative integer `consumptionCount`. `eventId` identifies the event type. One-time events reject processing once the count is at least one; repeatable events use the current count to select the next occurrence and increment it only through a successful operation. Runtime reconstruction must be deterministic from `eventId + consumptionCount` and shared definition data; SaveData does not store every occurrence instance.

Per-occurrence random results cannot be reconstructed from only a count unless those results were already finalized in a trade or settlement snapshot.

## One-time investment quest

The previous donation conversion and cumulative investment-progress model is removed. An investment quest is one-time, has no accumulated progress and no split payment. Its full cost is paid once. SaveData stores only result state such as `investmentQuestId`, `townId`, `isCompleted`, and `completedUtcTicks`; it does not copy definition costs.

Definitions in shared data or a ScriptableObject supply trading-currency cost, item costs, and unlock IDs. Completion validates and deducts currency and submitted Caravan trade goods, records completion, applies unlocks immediately, saves, then publishes completion. Home temporary inventory, construction materials, health consumables, ineligible items, and goods locked by Traveling or SettlementPending are excluded. Duplicate submissions are summed. Currency, per-Caravan goods, completion, and unlock mutations roll back together on save failure; a completed quest cannot deduct or reward twice.

## Rescue loan

`RescueLoanSaveData` stores full `loanId`, original principal, remaining principal, active state, permanent prior-use state such as `hasUsedRescueLoan`, phase, and issued UTC timestamp. There is no interest, score, overdue penalty, or multiple product model. Only one rescue loan may ever be issued in a playthrough; active state and prior-use state are separate.

The issue amount is the fixed shared-data cost of only the missing elements in one fixed rescue-trade configuration. Usable currency, wagon, placed draft animals and mercenaries, food, and trade goods offset matching requirements. Destroyed or unavailable wagons, assets locked by another trade, health-only items, and unrelated items do not count. The amount is not a simple total-asset sum and does not use current market prices.

After issue, the player enters `RestrictedPreparation`. Only rescue-trade preparation, required purchases and placement, Caravan configuration, preparation cancel, Title access, settings, and game exit are allowed. Building upgrades, growth purchases, investment quests, unrelated economy, and spending rescue funds for another purpose are blocked. Title access or exit preserves the restriction across restart.

The restriction is released only after the first rescue trade departure is durably saved, entering `RescueTradeInProgress`; settlement completion enters `RescueTradeCompleted`. Restriction release and principal repayment are separate, so positive principal remains active after departure.

Repayment is an optional player choice inside settlement claim, not a separate command and not automatic. Only the trading-currency payout newly produced by that settlement may repay. If payout is non-positive, no choice is shown. Repayment amount is `min(settlement trading-currency payout, remaining principal)`; choosing repayment pays that full amount and declining pays none. Claim reward, principal, trade and pending state, preparation cleanup, aggregate save, completion event, and UI refresh are one staged transaction and roll back together on save failure.

After the one permitted loan has been used, game over requires all of these: current currency cannot form the minimum rescue configuration, all owned usable assets still cannot form it, no Caravan is Traveling, no claimable pending settlement exists, and another loan is prohibited. Other active trades or claimable settlements defer game over. The implementation owner must place checks after settlement/claim, relevant currency or asset loss, and load recovery.
