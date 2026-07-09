namespace ND.Framework
{
    public sealed class SettlementViewData
    {
        public SettlementViewData(
            string tradeId,
            JourneyResultGrade grade,
            JourneyFailureReason failureReason,
            int revenue,
            int cost,
            int netProfit,
            bool canClaim,
            string statusMessage)
        {
            TradeId = tradeId ?? string.Empty;
            Grade = grade;
            FailureReason = failureReason;
            Revenue = revenue;
            Cost = cost;
            NetProfit = netProfit;
            IsFailed = grade == JourneyResultGrade.Failed;
            CanClaim = canClaim;
            StatusMessage = statusMessage ?? string.Empty;
        }

        public string TradeId { get; private set; }
        public JourneyResultGrade Grade { get; private set; }
        public JourneyFailureReason FailureReason { get; private set; }
        public int Revenue { get; private set; }
        public int Cost { get; private set; }
        public int NetProfit { get; private set; }
        public bool IsFailed { get; private set; }
        public bool CanClaim { get; private set; }
        public string StatusMessage { get; private set; }
    }
}
