using UnityEngine;

namespace ND.Economy
{
    public sealed class EconomyM1DebugRunner : MonoBehaviour
    {
        [Header("Run")]
        [SerializeField] private bool runOnStart;

        [Header("Trade")]
        [SerializeField] private string tradeId = "scene_debug_trade";
        [SerializeField] private string ItemId = "apple";
        [SerializeField] private string fromTownId = "town_start";
        [SerializeField] private string toTownId = "town_trade_01";
        [SerializeField] private string routeId = "route_01";
        [SerializeField] private int quantity = 5;
        [SerializeField] private int baseBuyPrice = 100;
        [SerializeField] private int baseSellPrice = 140;

        [Header("Currency")]
        [SerializeField] private int tradeMoney = 1000;
        [SerializeField] private int developmentCurrency;

        [Header("Settlement")]
        [SerializeField] private int foodCost = 50;
        [SerializeField] private int mercenaryCost;
        [SerializeField] private int cartRepairCost;
        [SerializeField] private int lostItemValue;
        [SerializeField] private int eventProfit;
        [SerializeField] private int eventLoss;
        [SerializeField] private int loanRepayment;
        [SerializeField] private int developmentCurrencyReward = 1;

        [Header("Growth")]
        [SerializeField] private bool purchaseGrowth = true;
        [SerializeField] private string growthId = "growth_load_01";
        [SerializeField] private int playerGrowthLevel;
        [SerializeField] private int caravanGrowthLevel;
        [SerializeField] private int growthMaxLevel = 1;
        [SerializeField] private int growthCostDevelopmentCurrency = 1;

        [Header("Last Result")]
        [SerializeField] private bool lastSuccess;
        [SerializeField] private string lastErrorCode = string.Empty;
        [SerializeField] private int lastTotalBuyPrice;
        [SerializeField] private int lastTotalSellPrice;
        [SerializeField] private int lastNetProfit;
        [SerializeField] private int lastTradeMoney;
        [SerializeField] private int lastDevelopmentCurrency;
        [SerializeField] private int lastMaxLoadBonus;
        [SerializeField] private float lastSpeedMultiplier = 1f;

        private void Start()
        {
            if (runOnStart)
            {
                Run();
            }
        }

        [ContextMenu("Run M1 Economy Debug")]
        public void Run()
        {
            EconomyM1LoopResult result = EconomyM1LoopCalculator.Execute(BuildInput());
            ApplyLastResult(result);
            LogResult(result);
        }

        private EconomyM1LoopInput BuildInput()
        {
            return new EconomyM1LoopInput
            {
                PriceInput = new PriceCalculationInput
                {
                    ItemId = ItemId,
                    FromTownId = fromTownId,
                    ToTownId = toTownId,
                    RouteId = routeId,
                    Quantity = quantity,
                    BaseBuyPrice = baseBuyPrice,
                    BaseSellPrice = baseSellPrice,
                    PlayerGrowthLevel = playerGrowthLevel,
                    CaravanGrowthLevel = caravanGrowthLevel
                },
                CurrencyState = new CurrencyState
                {
                    TradeMoney = tradeMoney,
                    DevelopmentCurrency = developmentCurrency
                },
                TradeId = tradeId,
                FoodCost = foodCost,
                MercenaryCost = mercenaryCost,
                CartRepairCost = cartRepairCost,
                LostItemValue = lostItemValue,
                EventProfit = eventProfit,
                EventLoss = eventLoss,
                LoanRepayment = loanRepayment,
                DevelopmentCurrencyReward = developmentCurrencyReward,
                PurchaseGrowth = purchaseGrowth,
                GrowthPurchaseInput = new GrowthPurchaseInput
                {
                    GrowthId = growthId,
                    CurrentLevel = playerGrowthLevel,
                    MaxLevel = growthMaxLevel,
                    CostDevelopmentCurrency = growthCostDevelopmentCurrency
                },
                PlayerGrowthLevel = playerGrowthLevel,
                CaravanGrowthLevel = caravanGrowthLevel
            };
        }

        private void ApplyLastResult(EconomyM1LoopResult result)
        {
            lastSuccess = result != null && result.Success;
            lastErrorCode = result == null ? "ResultNull" : result.ErrorCode;
            lastTotalBuyPrice = result != null && result.PriceResult != null ? result.PriceResult.TotalBuyPrice : 0;
            lastTotalSellPrice = result != null && result.PriceResult != null ? result.PriceResult.TotalSellPrice : 0;
            lastNetProfit = result != null && result.Settlement != null ? result.Settlement.NetProfit : 0;
            lastTradeMoney = result != null && result.FinalCurrencyState != null ? result.FinalCurrencyState.TradeMoney : 0;
            lastDevelopmentCurrency = result != null && result.FinalCurrencyState != null ? result.FinalCurrencyState.DevelopmentCurrency : 0;
            lastMaxLoadBonus = result != null && result.RuntimeStats != null ? result.RuntimeStats.MaxLoadBonus : 0;
            lastSpeedMultiplier = result != null && result.RuntimeStats != null ? result.RuntimeStats.SpeedMultiplier : 1f;
        }

        private void LogResult(EconomyM1LoopResult result)
        {
            if (result == null)
            {
                Debug.LogError("[Economy M1 Debug] Result is null.", this);
                return;
            }

            if (!result.Success)
            {
                Debug.LogError("[Economy M1 Debug] Failed: " + result.ErrorCode, this);
                return;
            }

            Debug.Log(
                "[Economy M1 Debug] Success\n"
                + "Buy: " + result.PriceResult.TotalBuyPrice
                + ", Sell: " + result.PriceResult.TotalSellPrice
                + ", Net: " + result.Settlement.NetProfit
                + ", TradeMoney: " + result.FinalCurrencyState.TradeMoney
                + ", DevCurrency: " + result.FinalCurrencyState.DevelopmentCurrency
                + ", MaxLoadBonus: " + result.RuntimeStats.MaxLoadBonus
                + ", SpeedMultiplier: " + result.RuntimeStats.SpeedMultiplier,
                this);
        }
    }
}
