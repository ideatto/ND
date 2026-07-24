using System;

namespace ND.Economy
{
    public enum SettlementEconomicValidationFailureReason
    {
        None = 0,
        InvalidInput,
        InvalidAmount,
        InvalidSoldItem,
        AutomaticLoanRepaymentNotAllowed,
        ArithmeticOverflow,
        RepaymentSelectionWithoutActiveLoan
    }

    [Serializable]
    public sealed class SettlementEconomicValidationInput
    {
        public SettlementInput Settlement = new SettlementInput();
        public long MinimumRecoveryMoney;
        public bool HasActiveLoan;
        public long RemainingLoanPrincipal;
        public bool SelectLoanRepayment;
    }

    [Serializable]
    public sealed class SettlementEconomicValidationResult
    {
        public bool IsValid;
        public SettlementEconomicValidationFailureReason FailureReason;
        public SettlementBreakdown Breakdown;
        public long PositiveSettlementPayout;
        public long MaximumRepaymentFromPayout;
        public long SelectedRepaymentRequestAmount;
        public bool CanOfferRescueLoan;
        public bool IsRebankrupt;
        public long RecoveryShortfall;
    }

    public static class SettlementEconomicValidationCalculator
    {
        public static SettlementEconomicValidationResult Evaluate(SettlementEconomicValidationInput input)
        {
            var result = new SettlementEconomicValidationResult();
            SettlementEconomicValidationFailureReason validation = Validate(input);
            if (validation != SettlementEconomicValidationFailureReason.None)
            {
                result.FailureReason = validation;
                return result;
            }

            try
            {
                ValidateCheckedArithmetic(input.Settlement);
            }
            catch (OverflowException)
            {
                result.FailureReason = SettlementEconomicValidationFailureReason.ArithmeticOverflow;
                return result;
            }

            SettlementBreakdown breakdown = SettlementCalculator.Calculate(
                input.Settlement,
                input.MinimumRecoveryMoney);
            long positivePayout = Math.Max(0L, breakdown.NetProfit);
            long maximumRepayment = input.HasActiveLoan
                ? Math.Min(positivePayout, input.RemainingLoanPrincipal)
                : 0L;
            long usableMoneyAfterSettlement = Math.Max(0L, breakdown.TradeMoneyAfter);
            RescueStatusResult recovery = RescueLoanCalculator.EvaluateStatus(new RescueStatusInput
            {
                UsableTradeMoney = usableMoneyAfterSettlement,
                MinimumTradeCost = input.MinimumRecoveryMoney,
                HasActiveLoan = input.HasActiveLoan
            });

            result.IsValid = true;
            result.FailureReason = SettlementEconomicValidationFailureReason.None;
            result.Breakdown = breakdown;
            result.PositiveSettlementPayout = positivePayout;
            result.MaximumRepaymentFromPayout = maximumRepayment;
            result.SelectedRepaymentRequestAmount = input.SelectLoanRepayment ? maximumRepayment : 0L;
            result.CanOfferRescueLoan = recovery.CanOfferLoan;
            result.IsRebankrupt = recovery.IsRebankrupt;
            result.RecoveryShortfall = recovery.Shortfall;
            return result;
        }

        private static SettlementEconomicValidationFailureReason Validate(SettlementEconomicValidationInput input)
        {
            if (input == null || input.Settlement == null || input.Settlement.SoldItems == null ||
                input.MinimumRecoveryMoney <= 0L || input.RemainingLoanPrincipal < 0L)
                return SettlementEconomicValidationFailureReason.InvalidInput;
            if (input.Settlement.LoanRepayment != 0L)
                return SettlementEconomicValidationFailureReason.AutomaticLoanRepaymentNotAllowed;
            if (input.Settlement.TradeMoneyBefore < 0L || input.Settlement.FoodCost < 0L ||
                input.Settlement.MercenaryCost < 0L || input.Settlement.CartRepairCost < 0L ||
                input.Settlement.LostItemValue < 0L || input.Settlement.EventProfit < 0L ||
                input.Settlement.EventLoss < 0L || input.Settlement.DevelopmentCurrencyReward < 0L)
                return SettlementEconomicValidationFailureReason.InvalidAmount;
            if (!input.HasActiveLoan && input.RemainingLoanPrincipal != 0L)
                return SettlementEconomicValidationFailureReason.InvalidInput;
            if (input.HasActiveLoan && input.RemainingLoanPrincipal <= 0L)
                return SettlementEconomicValidationFailureReason.InvalidInput;
            if (input.SelectLoanRepayment && !input.HasActiveLoan)
                return SettlementEconomicValidationFailureReason.RepaymentSelectionWithoutActiveLoan;

            for (int index = 0; index < input.Settlement.SoldItems.Count; index++)
            {
                SoldItemInput item = input.Settlement.SoldItems[index];
                if (item == null || item.Quantity < 0 || item.TotalBuyPrice < 0L || item.TotalSellPrice < 0L)
                    return SettlementEconomicValidationFailureReason.InvalidSoldItem;
            }
            return SettlementEconomicValidationFailureReason.None;
        }

        private static void ValidateCheckedArithmetic(SettlementInput input)
        {
            long purchase = 0L;
            long sale = 0L;
            for (int index = 0; index < input.SoldItems.Count; index++)
            {
                purchase = checked(purchase + input.SoldItems[index].TotalBuyPrice);
                sale = checked(sale + input.SoldItems[index].TotalSellPrice);
            }
            long revenue = checked(sale + input.EventProfit);
            long expense = checked(purchase + input.FoodCost);
            expense = checked(expense + input.MercenaryCost);
            expense = checked(expense + input.CartRepairCost);
            expense = checked(expense + input.LostItemValue);
            expense = checked(expense + input.EventLoss);
            long net = checked(revenue - expense);
            checked { long ignored = input.TradeMoneyBefore + net; }
        }
    }
}
