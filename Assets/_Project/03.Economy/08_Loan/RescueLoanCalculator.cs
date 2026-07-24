using System;

namespace ND.Economy
{
    /// <summary>
    /// 구조 대출의 자격, 발급, 명시적 상환을 계산하는 순수 계산기다.
    /// SaveData 변경과 저장은 Framework command가 담당한다.
    /// </summary>
    public static class RescueLoanCalculator
    {
        public static RescueStatusResult EvaluateStatus(RescueStatusInput input)
        {
            if (input == null || input.UsableTradeMoney < 0)
            {
                return InvalidStatus(RescueLoanFailureReason.InvalidInput);
            }

            if (input.MinimumTradeCost <= 0)
            {
                return InvalidStatus(RescueLoanFailureReason.InvalidDefinition);
            }

            bool needsRecovery = input.UsableTradeMoney < input.MinimumTradeCost;

            return new RescueStatusResult
            {
                IsValid = true,
                FailureReason = RescueLoanFailureReason.None,
                NeedsRecovery = needsRecovery,
                CanOfferLoan = needsRecovery && !input.HasActiveLoan,
                IsRebankrupt = needsRecovery && input.HasActiveLoan,
                Shortfall = needsRecovery ? input.MinimumTradeCost - input.UsableTradeMoney : 0L
            };
        }

        public static IssueRescueLoanResult Issue(IssueRescueLoanInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.LoanId)
                || input.TradeMoneyBefore < 0 || input.IssuedUtcTicks < 0)
            {
                return FailIssue(input, RescueLoanFailureReason.InvalidInput);
            }

            if (input.MinimumTradeCost <= 0)
            {
                return FailIssue(input, RescueLoanFailureReason.InvalidDefinition);
            }

            if (input.HasActiveLoan)
            {
                return FailIssue(input, RescueLoanFailureReason.ActiveLoanExists);
            }

            if (input.TradeMoneyBefore >= input.MinimumTradeCost)
            {
                return FailIssue(input, RescueLoanFailureReason.NotEligible);
            }

            if (input.TradeMoneyBefore > long.MaxValue - input.MinimumTradeCost)
            {
                return FailIssue(input, RescueLoanFailureReason.Overflow);
            }

            long principal = input.MinimumTradeCost;

            return new IssueRescueLoanResult
            {
                Success = true,
                FailureReason = RescueLoanFailureReason.None,
                LoanId = input.LoanId,
                Principal = principal,
                TradeMoneyBefore = input.TradeMoneyBefore,
                TradeMoneyAfter = input.TradeMoneyBefore + principal,
                RemainingPrincipal = principal,
                IssuedUtcTicks = input.IssuedUtcTicks,
                EnterRestrictedMode = true
            };
        }

        public static RepayRescueLoanResult Repay(RepayRescueLoanInput input)
        {
            if (input == null || input.TradeMoneyBefore < 0 || input.OriginalPrincipal < 0
                || input.RemainingPrincipalBefore < 0)
            {
                return FailRepay(input, RescueLoanFailureReason.InvalidInput);
            }

            if (input.MinimumTradeCost <= 0)
            {
                return FailRepay(input, RescueLoanFailureReason.InvalidDefinition);
            }

            if (!input.IsActive || input.RemainingPrincipalBefore <= 0)
            {
                return FailRepay(input, RescueLoanFailureReason.NoActiveLoan);
            }

            if (input.RemainingPrincipalBefore > input.OriginalPrincipal)
            {
                return FailRepay(input, RescueLoanFailureReason.InvalidState);
            }

            if (input.RequestedAmount <= 0)
            {
                return FailRepay(input, RescueLoanFailureReason.InvalidRepaymentAmount);
            }

            if (input.RequestedAmount > input.RemainingPrincipalBefore)
            {
                return FailRepay(input, RescueLoanFailureReason.RepaymentExceedsBalance);
            }

            if (input.RequestedAmount > input.TradeMoneyBefore)
            {
                return FailRepay(input, RescueLoanFailureReason.InsufficientCurrency);
            }

            if (input.IsRestrictedPreparation)
            {
                return FailRepay(input, RescueLoanFailureReason.InvalidState);
            }

            long tradeMoneyAfter = input.TradeMoneyBefore - input.RequestedAmount;
            if (tradeMoneyAfter < input.MinimumTradeCost)
            {
                return FailRepay(input, RescueLoanFailureReason.RepaymentWouldTriggerRecovery);
            }

            long remainingPrincipalAfter = input.RemainingPrincipalBefore - input.RequestedAmount;
            bool isActiveAfter = remainingPrincipalAfter > 0;

            return new RepayRescueLoanResult
            {
                Success = true,
                FailureReason = RescueLoanFailureReason.None,
                RequestedAmount = input.RequestedAmount,
                RepaidAmount = input.RequestedAmount,
                TradeMoneyBefore = input.TradeMoneyBefore,
                TradeMoneyAfter = tradeMoneyAfter,
                RemainingPrincipalBefore = input.RemainingPrincipalBefore,
                RemainingPrincipalAfter = remainingPrincipalAfter,
                IsActiveAfter = isActiveAfter,
                IsRestrictedPreparationAfter = isActiveAfter && input.IsRestrictedPreparation
            };
        }

        private static RescueStatusResult InvalidStatus(RescueLoanFailureReason failureReason)
        {
            return new RescueStatusResult
            {
                IsValid = false,
                FailureReason = failureReason
            };
        }

        private static IssueRescueLoanResult FailIssue(
            IssueRescueLoanInput input,
            RescueLoanFailureReason failureReason)
        {
            return new IssueRescueLoanResult
            {
                Success = false,
                FailureReason = failureReason,
                LoanId = input != null ? input.LoanId : string.Empty,
                TradeMoneyBefore = input != null ? Math.Max(0L, input.TradeMoneyBefore) : 0L,
                TradeMoneyAfter = input != null ? Math.Max(0L, input.TradeMoneyBefore) : 0L,
                IssuedUtcTicks = input != null ? Math.Max(0L, input.IssuedUtcTicks) : 0L
            };
        }

        private static RepayRescueLoanResult FailRepay(
            RepayRescueLoanInput input,
            RescueLoanFailureReason failureReason)
        {
            long tradeMoney = input != null ? Math.Max(0L, input.TradeMoneyBefore) : 0L;
            long remainingPrincipal = input != null ? Math.Max(0L, input.RemainingPrincipalBefore) : 0L;

            return new RepayRescueLoanResult
            {
                Success = false,
                FailureReason = failureReason,
                RequestedAmount = input != null ? Math.Max(0L, input.RequestedAmount) : 0L,
                TradeMoneyBefore = tradeMoney,
                TradeMoneyAfter = tradeMoney,
                RemainingPrincipalBefore = remainingPrincipal,
                RemainingPrincipalAfter = remainingPrincipal,
                IsActiveAfter = input != null && input.IsActive,
                IsRestrictedPreparationAfter = input != null && input.IsRestrictedPreparation
            };
        }
    }
}
