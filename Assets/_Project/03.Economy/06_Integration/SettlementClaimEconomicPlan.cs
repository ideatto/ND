using System;

namespace ND.Economy
{
    public enum SettlementClaimPlanFailureReason
    {
        None = 0,
        AdapterRejected,
        ValidationRejected,
        RepaymentRejected,
        ArithmeticOverflow
    }

    public sealed class SettlementClaimEconomicPlan
    {
        public SettlementClaimEconomicPlan(
            string caravanId,
            string tradeId,
            long confirmedRevenue,
            long confirmedCost,
            long confirmedNetProfit,
            long tradeMoneyBefore,
            long tradeMoneyAfterSettlement,
            long repaymentAmount,
            long tradeMoneyAfterPlan,
            long remainingPrincipalBefore,
            long remainingPrincipalAfter,
            bool loanActiveAfter,
            bool canOfferRescueLoan,
            bool isRebankrupt,
            long recoveryShortfall)
        {
            CaravanId = caravanId ?? string.Empty;
            TradeId = tradeId ?? string.Empty;
            ConfirmedRevenue = confirmedRevenue;
            ConfirmedCost = confirmedCost;
            ConfirmedNetProfit = confirmedNetProfit;
            TradeMoneyBefore = tradeMoneyBefore;
            TradeMoneyAfterSettlement = tradeMoneyAfterSettlement;
            RepaymentAmount = repaymentAmount;
            TradeMoneyAfterPlan = tradeMoneyAfterPlan;
            RemainingPrincipalBefore = remainingPrincipalBefore;
            RemainingPrincipalAfter = remainingPrincipalAfter;
            LoanActiveAfter = loanActiveAfter;
            CanOfferRescueLoan = canOfferRescueLoan;
            IsRebankrupt = isRebankrupt;
            RecoveryShortfall = recoveryShortfall;
        }

        public string CaravanId { get; }
        public string TradeId { get; }
        public long ConfirmedRevenue { get; }
        public long ConfirmedCost { get; }
        public long ConfirmedNetProfit { get; }
        public long TradeMoneyBefore { get; }
        public long TradeMoneyAfterSettlement { get; }
        public long RepaymentAmount { get; }
        public long TradeMoneyAfterPlan { get; }
        public long RemainingPrincipalBefore { get; }
        public long RemainingPrincipalAfter { get; }
        public bool LoanActiveAfter { get; }
        public bool CanOfferRescueLoan { get; }
        public bool IsRebankrupt { get; }
        public long RecoveryShortfall { get; }
        public bool IncludesRepayment => RepaymentAmount > 0L;
    }

    public sealed class SettlementClaimPlanBuildResult
    {
        public bool Success { get; internal set; }
        public SettlementClaimPlanFailureReason FailureReason { get; internal set; }
        public SettlementEconomicAdapterFailureReason AdapterFailure { get; internal set; }
        public SettlementEconomicValidationFailureReason ValidationFailure { get; internal set; }
        public RescueLoanFailureReason RepaymentFailure { get; internal set; }
        public SettlementClaimEconomicPlan Plan { get; internal set; }
    }

    public static class SettlementClaimEconomicPlanBuilder
    {
        public static SettlementClaimPlanBuildResult Build(
            global::ND.Framework.SaveData saveData,
            string caravanId,
            string tradeId,
            long minimumRecoveryMoney,
            bool selectLoanRepayment)
        {
            SettlementEconomicValidationResult validation;
            SettlementEconomicAdapterFailureReason adapterFailure;
            if (!SettlementEconomicFrameworkAdapter.TryEvaluate(
                saveData, caravanId, tradeId, minimumRecoveryMoney, selectLoanRepayment,
                out validation, out adapterFailure))
            {
                return Fail(SettlementClaimPlanFailureReason.AdapterRejected, adapterFailure);
            }
            if (validation == null || !validation.IsValid || validation.Breakdown == null)
            {
                return Fail(
                    SettlementClaimPlanFailureReason.ValidationRejected,
                    SettlementEconomicAdapterFailureReason.None,
                    validation != null ? validation.FailureReason : SettlementEconomicValidationFailureReason.InvalidInput);
            }

            long tradeMoneyBefore = Math.Max(0L, saveData.player.tradingCurrency);
            long tradeMoneyAfterSettlement = Math.Max(0L, validation.Breakdown.TradeMoneyAfter);
            long remainingBefore = saveData.rescueLoan != null && saveData.rescueLoan.isActive
                ? saveData.rescueLoan.remainingPrincipal
                : 0L;
            long repaymentAmount = validation.SelectedRepaymentRequestAmount;
            long tradeMoneyAfterPlan = tradeMoneyAfterSettlement;
            long remainingAfter = remainingBefore;
            bool loanActiveAfter = saveData.rescueLoan != null && saveData.rescueLoan.isActive;

            if (repaymentAmount > 0L)
            {
                global::ND.Framework.RescueLoanSaveData loan = saveData.rescueLoan;
                RepayRescueLoanResult repayment = RescueLoanCalculator.Repay(new RepayRescueLoanInput
                {
                    TradeMoneyBefore = tradeMoneyAfterSettlement,
                    MinimumTradeCost = minimumRecoveryMoney,
                    OriginalPrincipal = loan != null ? loan.originalPrincipal : 0L,
                    RemainingPrincipalBefore = remainingBefore,
                    IsActive = loan != null && loan.isActive,
                    IsRestrictedPreparation = loan != null && loan.isRestrictedPreparation,
                    RequestedAmount = repaymentAmount
                });
                if (!repayment.Success)
                {
                    return Fail(
                        SettlementClaimPlanFailureReason.RepaymentRejected,
                        SettlementEconomicAdapterFailureReason.None,
                        SettlementEconomicValidationFailureReason.None,
                        repayment.FailureReason);
                }

                tradeMoneyAfterPlan = repayment.TradeMoneyAfter;
                remainingAfter = repayment.RemainingPrincipalAfter;
                loanActiveAfter = repayment.IsActiveAfter;
            }

            try
            {
                checked
                {
                    long expectedAfterSettlement = tradeMoneyBefore + validation.Breakdown.NetProfit;
                    if (Math.Max(0L, expectedAfterSettlement) != tradeMoneyAfterSettlement)
                        return Fail(SettlementClaimPlanFailureReason.ArithmeticOverflow);
                }
            }
            catch (OverflowException)
            {
                return Fail(SettlementClaimPlanFailureReason.ArithmeticOverflow);
            }

            return new SettlementClaimPlanBuildResult
            {
                Success = true,
                FailureReason = SettlementClaimPlanFailureReason.None,
                Plan = new SettlementClaimEconomicPlan(
                    caravanId,
                    tradeId,
                    validation.Breakdown.TotalRevenue,
                    validation.Breakdown.TotalExpense,
                    validation.Breakdown.NetProfit,
                    tradeMoneyBefore,
                    tradeMoneyAfterSettlement,
                    repaymentAmount,
                    tradeMoneyAfterPlan,
                    remainingBefore,
                    remainingAfter,
                    loanActiveAfter,
                    validation.CanOfferRescueLoan,
                    validation.IsRebankrupt,
                    validation.RecoveryShortfall)
            };
        }

        private static SettlementClaimPlanBuildResult Fail(
            SettlementClaimPlanFailureReason reason,
            SettlementEconomicAdapterFailureReason adapterFailure = SettlementEconomicAdapterFailureReason.None,
            SettlementEconomicValidationFailureReason validationFailure = SettlementEconomicValidationFailureReason.None,
            RescueLoanFailureReason repaymentFailure = RescueLoanFailureReason.None)
        {
            return new SettlementClaimPlanBuildResult
            {
                Success = false,
                FailureReason = reason,
                AdapterFailure = adapterFailure,
                ValidationFailure = validationFailure,
                RepaymentFailure = repaymentFailure
            };
        }
    }
}
