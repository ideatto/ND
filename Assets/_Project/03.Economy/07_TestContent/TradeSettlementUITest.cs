using UnityEngine;
using EconomyInput = ND.Economy.EconomyM1LoopInput;
using EconomyResult = ND.Economy.EconomyM1LoopResult;
using EconomyPriceInput = ND.Economy.PriceCalculationInput;
using EconomyCurrencyState = ND.Economy.CurrencyState;

public sealed class TradeSettlementUITest : MonoBehaviour
{
    [SerializeField] private TradeSettlementPanelController settlementPanel;
    [SerializeField] private RouteData route;

    [ContextMenu("Show Test Settlement")]
    public void ShowTestSettlement()
    {
        EconomyResult result =
            ND.Economy.EconomyM1LoopCalculator.Execute(new EconomyInput
            {
                PriceInput = new EconomyPriceInput
                {
                    TradeItemId = "apple",
                    FromTownId = "town_start",
                    ToTownId = "town_trade_01",
                    RouteId = "route_01",
                    Quantity = 10,
                    BaseBuyPrice = 100,
                    BaseSellPrice = 140,
                    PlayerGrowthLevel = 0,
                    CaravanGrowthLevel = 0
                },
                CurrencyState = new EconomyCurrencyState
                {
                    TradeMoney = 5000,
                    DevelopmentCurrency = 0
                },
                TradeId = "ui_test",
                FoodCost = 50,
                MercenaryCost = 30,
                CartRepairCost = 20,
                LostItemValue = 40,
                EventProfit = 100,
                EventLoss = 25,
                DevelopmentCurrencyReward = 1,
                PurchaseGrowth = false,
                PlayerGrowthLevel = 0,
                CaravanGrowthLevel = 0
            });

        if (!result.Success)
        {
            Debug.LogError($"Settlement test failed: {result.ErrorCode}", this);
            return;
        }

        settlementPanel.Show(result, route, 185f);
    }
}