# Rescue Loan Contract Alignment — 2026-07-21

Authoritative source: `0720_Progression_Requested_Framework_Integration.md`.

## Replaced policies

- Replaced missing-element/configuration-cost principal with the full `RescueLoanDefinition.MinimumTradeCost`.
- Removed the permanent once-per-playthrough loan ban and `hasUsedRescueLoan` target field. Active loans still block another issue.
- Removed settlement-claim repayment and the `repayLoan` Claim parameter.
- Added separate `RepayRescueLoan(long amount)` with partial/full repayment and minimum-trade-money protection.
- Kept `RestrictedPreparation`, but release now commits in the same immediate save as rescue-trade departure.
- Defined deterministic `CanOfferLoan` / `IsRebankrupt` reevaluation after settlement, economy changes, and load.
- Updated SaveData normalization, committed events, rollback requirements, and recovery tests.

## Updated documents

- `Donation_Investment_Loan_Save_Contract.md`
- `Framework_Command_Event_Contract.md`
- `Immediate_Save_and_Dirty_Policy.md`
- `Framework_API_Event_Inventory.md`
- `Pull_Request_Description.md`
- `SaveData_V2_Field_Contract.md`
- `Save_Recovery_Test_Matrix.md`
- `Settlement_Recovery_and_Trade_ID_Contract.md`
