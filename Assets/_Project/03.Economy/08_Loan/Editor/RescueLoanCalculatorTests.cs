using System;

namespace ND.Economy.Editor
{
    public static class RescueLoanCalculatorTests
    {
        public static void RunAll()
        {
            Status_OffersLoanBelowMinimumWithoutActiveLoan();
            Status_DetectsRebankruptcyWithActiveLoan();
            Status_DoesNotRequireRecoveryAtMinimum();
            Issue_UsesFullFixedMinimumAsPrincipal();
            Issue_RejectsEligibleBoundaryAndActiveLoan();
            Repay_AllowsPartialAndFullRepayment();
            Repay_RejectsAutomaticRecoveryRiskAndInvalidAmounts();
            Repay_RejectsRestrictedMode();
        }

        private static void Status_OffersLoanBelowMinimumWithoutActiveLoan()
        {
            RescueStatusResult result = RescueLoanCalculator.EvaluateStatus(new RescueStatusInput
            {
                UsableTradeMoney = 999L,
                MinimumTradeCost = 1000L,
                HasActiveLoan = false
            });

            Check(result.IsValid, "Status should be valid.");
            Check(result.NeedsRecovery, "Recovery should be required below the minimum.");
            Check(result.CanOfferLoan, "A rescue loan should be offered without an active loan.");
            Check(!result.IsRebankrupt, "First recovery should not be rebankruptcy.");
            CheckEqual(1L, result.Shortfall, "Shortfall");
        }

        private static void Status_DetectsRebankruptcyWithActiveLoan()
        {
            RescueStatusResult result = RescueLoanCalculator.EvaluateStatus(new RescueStatusInput
            {
                UsableTradeMoney = 0L,
                MinimumTradeCost = 1000L,
                HasActiveLoan = true
            });

            Check(result.NeedsRecovery, "Recovery should be required.");
            Check(!result.CanOfferLoan, "A second loan must not be offered.");
            Check(result.IsRebankrupt, "Active-loan recovery must be rebankruptcy.");
        }

        private static void Status_DoesNotRequireRecoveryAtMinimum()
        {
            RescueStatusResult result = RescueLoanCalculator.EvaluateStatus(new RescueStatusInput
            {
                UsableTradeMoney = 1000L,
                MinimumTradeCost = 1000L
            });

            Check(result.IsValid, "Status should be valid at the boundary.");
            Check(!result.NeedsRecovery, "The exact minimum should be eligible to trade.");
            CheckEqual(0L, result.Shortfall, "Boundary Shortfall");
        }

        private static void Issue_UsesFullFixedMinimumAsPrincipal()
        {
            IssueRescueLoanResult result = RescueLoanCalculator.Issue(new IssueRescueLoanInput
            {
                LoanId = "rescue_loan",
                TradeMoneyBefore = 400L,
                MinimumTradeCost = 1000L,
                IssuedUtcTicks = 123L
            });

            Check(result.Success, "Issue should succeed.");
            CheckEqual(1000L, result.Principal, "Principal");
            CheckEqual(1400L, result.TradeMoneyAfter, "TradeMoneyAfter");
            CheckEqual(1000L, result.RemainingPrincipal, "RemainingPrincipal");
            Check(result.EnterRestrictedMode, "Issue should enter restricted mode.");
        }

        private static void Issue_RejectsEligibleBoundaryAndActiveLoan()
        {
            IssueRescueLoanResult notEligible = RescueLoanCalculator.Issue(new IssueRescueLoanInput
            {
                LoanId = "rescue_loan",
                TradeMoneyBefore = 1000L,
                MinimumTradeCost = 1000L
            });
            CheckEqual(RescueLoanFailureReason.NotEligible, notEligible.FailureReason, "Boundary failure");

            IssueRescueLoanResult duplicate = RescueLoanCalculator.Issue(new IssueRescueLoanInput
            {
                LoanId = "rescue_loan",
                TradeMoneyBefore = 0L,
                MinimumTradeCost = 1000L,
                HasActiveLoan = true
            });
            CheckEqual(RescueLoanFailureReason.ActiveLoanExists, duplicate.FailureReason, "Duplicate failure");
        }

        private static void Repay_AllowsPartialAndFullRepayment()
        {
            RepayRescueLoanResult partial = RescueLoanCalculator.Repay(new RepayRescueLoanInput
            {
                TradeMoneyBefore = 2500L,
                MinimumTradeCost = 1000L,
                OriginalPrincipal = 1000L,
                RemainingPrincipalBefore = 1000L,
                IsActive = true,
                RequestedAmount = 400L
            });

            Check(partial.Success, "Partial repayment should succeed.");
            CheckEqual(2100L, partial.TradeMoneyAfter, "Partial TradeMoneyAfter");
            CheckEqual(600L, partial.RemainingPrincipalAfter, "Partial RemainingPrincipalAfter");
            Check(partial.IsActiveAfter, "Partial repayment should keep the loan active.");

            RepayRescueLoanResult full = RescueLoanCalculator.Repay(new RepayRescueLoanInput
            {
                TradeMoneyBefore = 2000L,
                MinimumTradeCost = 1000L,
                OriginalPrincipal = 1000L,
                RemainingPrincipalBefore = 1000L,
                IsActive = true,
                RequestedAmount = 1000L
            });

            Check(full.Success, "Full repayment should succeed.");
            CheckEqual(0L, full.RemainingPrincipalAfter, "Full RemainingPrincipalAfter");
            Check(!full.IsActiveAfter, "Full repayment should close the loan.");
        }

        private static void Repay_RejectsAutomaticRecoveryRiskAndInvalidAmounts()
        {
            RepayRescueLoanResult recoveryRisk = RescueLoanCalculator.Repay(new RepayRescueLoanInput
            {
                TradeMoneyBefore = 1200L,
                MinimumTradeCost = 1000L,
                OriginalPrincipal = 1000L,
                RemainingPrincipalBefore = 1000L,
                IsActive = true,
                RequestedAmount = 300L
            });
            CheckEqual(RescueLoanFailureReason.RepaymentWouldTriggerRecovery, recoveryRisk.FailureReason, "Recovery risk");

            RepayRescueLoanResult overpay = RescueLoanCalculator.Repay(new RepayRescueLoanInput
            {
                TradeMoneyBefore = 3000L,
                MinimumTradeCost = 1000L,
                OriginalPrincipal = 1000L,
                RemainingPrincipalBefore = 500L,
                IsActive = true,
                RequestedAmount = 501L
            });
            CheckEqual(RescueLoanFailureReason.RepaymentExceedsBalance, overpay.FailureReason, "Overpayment");
        }

        private static void Repay_RejectsRestrictedMode()
        {
            RepayRescueLoanResult result = RescueLoanCalculator.Repay(new RepayRescueLoanInput
            {
                TradeMoneyBefore = 3000L,
                MinimumTradeCost = 1000L,
                OriginalPrincipal = 1000L,
                RemainingPrincipalBefore = 1000L,
                IsActive = true,
                IsRestrictedPreparation = true,
                RequestedAmount = 1000L
            });

            CheckEqual(RescueLoanFailureReason.InvalidState, result.FailureReason, "Restricted repayment");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void CheckEqual<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}.");
            }
        }
    }
}
