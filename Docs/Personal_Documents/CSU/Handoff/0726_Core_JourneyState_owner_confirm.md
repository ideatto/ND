# Core Contract Confirmation — Multi-Caravan Arrival Sale

## Purpose

Confirm that the existing Core APIs remain valid for Work B without modifying the Core Journey lifecycle.

## Confirmed Direction

Arrival Sale remains in the existing product flow while the trade already has a durable pending settlement.

```text
Traveling
→ SettlementPending
→ Arrival Sale
→ settlement presentation
→ Claim
```

## Requested Confirmation

Please confirm:

1. The existing Caravan Cargo mutation path can safely target an explicitly resolved Caravan instance.
2. No `JourneyRunner` change is required for Arrival Sale.
3. No new Journey state transition is required.
4. `JourneyState.Selling` remains an unused extension point.
5. `BeginSettlement` and `CancelSettlement` do not need to be activated.
6. Targeted Claim continues using the existing Core Claim behavior through the Framework coordinator.

## No Requested Code Changes

Do not modify:

```text
JourneyRunner
JourneyState
BeginSettlement
CancelSettlement
Core settlement calculation
Core Claim economy
```

If an exact-Caravan Cargo API is unexpectedly missing, report the precise missing API and the smallest required Core addition before making changes.
