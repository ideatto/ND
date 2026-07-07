using System;

namespace ND.Economy
{
    [Serializable]
    public sealed class EconomyM1LoopInput
    {
        public PriceCalculationInput PriceInput = new PriceCalculationInput();
        public CurrencyState CurrencyState = new CurrencyState();
        public string TradeId = string.Empty;
        public int FoodCost;
        public int MercenaryCost;
        public int AccidentLoss;
        public int LoanRepayment;
        public int EventAdjustment;
        public int DevelopmentCurrencyReward;
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
