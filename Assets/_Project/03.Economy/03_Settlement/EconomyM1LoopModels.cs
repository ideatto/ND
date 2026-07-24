using System;

namespace ND.Economy
{
    [Serializable]
    public sealed class EconomyM1LoopInput
    {
        /// <summary>
        /// True only for legacy/direct item-trade calculations. Travel settlement sets this
        /// to false so arriving never buys or sells cargo automatically.
        /// </summary>
        public bool CalculateItemTrade = true;
        public PriceCalculationInput PriceInput = new PriceCalculationInput();
        public CurrencyState CurrencyState = new CurrencyState();
        public string TradeId = string.Empty;
        public long FoodCost;
        public long MercenaryCost;
        public long CartRepairCost;
        public long LostItemValue;
        public long EventProfit;
        public long EventLoss;
        public long LoanRepayment;
        public long DevelopmentCurrencyReward;
        public bool PurchaseGrowth;
        public GrowthPurchaseInput GrowthPurchaseInput = new GrowthPurchaseInput();
        public int PlayerGrowthLevel;
        public int CaravanGrowthLevel;
    }

    [Serializable]
    public sealed class EconomyM1LoopResult
    {
        public bool Success;
        public string ErrorCode = string.Empty;
        public PriceCalculationResult PriceResult;
        public SettlementBreakdown Settlement;
        public CurrencyApplyResult SettlementCurrencyApply;
        public GrowthPurchaseResult GrowthPurchase;
        public CurrencyApplyResult GrowthCurrencyApply;
        public CoreRuntimeStatModifier RuntimeStats;
        public CurrencyState FinalCurrencyState;
    }
}
