namespace ND.UI.RescueLoan
{
    /// <summary>구조 대출 패널이 한 번에 그리는 읽기 전용 상태 묶음이다.</summary>
    public sealed class RescueLoanPanelSnapshot
    {
        public bool IsAvailable;
        public bool CanOfferLoan;
        public bool NeedsRecovery;
        public bool IsRebankrupt;
        public bool IsActive;
        public bool IsRestrictedPreparation;
        public string LoanId = string.Empty;
        public long TradeMoney;
        public long MinimumTradeCost;
        public long Shortfall;
        public long OriginalPrincipal;
        public long RemainingPrincipal;
    }
}
