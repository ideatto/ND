using System;

namespace ND.Economy
{
    public enum RescueLoanFailureReason
    {
        None,
        InvalidInput,
        InvalidDefinition,
        NotEligible,
        ActiveLoanExists,
        NoActiveLoan,
        InvalidRepaymentAmount,
        RepaymentExceedsBalance,
        InsufficientCurrency,
        RepaymentWouldTriggerRecovery,
        InvalidState,
        Overflow
    }

    [Serializable]
    public sealed class RescueLoanDefinition
    {
        public string LoanId = "rescue_loan";
        public long MinimumTradeCost;
    }

    [Serializable]
    public sealed class RescueStatusInput
    {
        public long UsableTradeMoney;
        public long MinimumTradeCost;
        public bool HasActiveLoan;
    }

    [Serializable]
    public sealed class RescueStatusResult
    {
        public bool IsValid;
        public RescueLoanFailureReason FailureReason;
        public bool NeedsRecovery;
        public bool CanOfferLoan;
        public bool IsRebankrupt;
        public long Shortfall;
    }

    [Serializable]
    public sealed class IssueRescueLoanInput
    {
        public string LoanId = string.Empty;
        public long TradeMoneyBefore;
        public long MinimumTradeCost;
        public bool HasActiveLoan;
        public long IssuedUtcTicks;
    }

    [Serializable]
    public sealed class IssueRescueLoanResult
    {
        public bool Success;
        public RescueLoanFailureReason FailureReason;
        public string LoanId = string.Empty;
        public long Principal;
        public long TradeMoneyBefore;
        public long TradeMoneyAfter;
        public long RemainingPrincipal;
        public long IssuedUtcTicks;
        public bool EnterRestrictedMode;
    }

    [Serializable]
    public sealed class RepayRescueLoanInput
    {
        public long TradeMoneyBefore;
        public long MinimumTradeCost;
        public long OriginalPrincipal;
        public long RemainingPrincipalBefore;
        public bool IsActive;
        public bool IsRestrictedPreparation;
        public long RequestedAmount;
    }

    [Serializable]
    public sealed class RepayRescueLoanResult
    {
        public bool Success;
        public RescueLoanFailureReason FailureReason;
        public long RequestedAmount;
        public long RepaidAmount;
        public long TradeMoneyBefore;
        public long TradeMoneyAfter;
        public long RemainingPrincipalBefore;
        public long RemainingPrincipalAfter;
        public bool IsActiveAfter;
        public bool IsRestrictedPreparationAfter;
    }
}
