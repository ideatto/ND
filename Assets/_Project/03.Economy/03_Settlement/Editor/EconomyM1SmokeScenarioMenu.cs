using UnityEditor;
using UnityEngine;

namespace ND.Economy.Editor
{
    public static class EconomyM1SmokeScenarioMenu
    {
        [MenuItem("ND/Economy/Run M1 Smoke Scenario")]
        public static void Run()
        {
            EconomyM1SmokeResult result = EconomyM1SmokeScenario.Run();

            if (!result.Success)
            {
                Debug.LogError("[Economy M1 Smoke] Failed: " + result.ErrorMessage);
                return;
            }

            Debug.Log(
                "[Economy M1 Smoke] Success\n"
                + $"Buy: {result.PriceResult.TotalBuyPrice}, "
                + $"Sell: {result.PriceResult.TotalSellPrice}, "
                + $"Net: {result.Settlement.NetProfit}, "
                + $"TradeMoneyAfter: {result.Settlement.TradeMoneyAfter}, "
                + $"DevCurrency: {result.Settlement.DevelopmentCurrencyReward}, "
                + $"GrowthLevel: {result.GrowthPurchase.NewLevel}, "
                + $"DevCurrencyAfterGrowth: {result.GrowthPurchase.DevelopmentCurrencyAfter}, "
                + $"WalletTradeMoney: {result.GrowthCurrencyApply.After.TradeMoney}, "
                + $"WalletDevCurrency: {result.GrowthCurrencyApply.After.DevelopmentCurrency}, "
                + $"MaxLoadBonus: {result.RuntimeStats.MaxLoadBonus}");
        }

        [MenuItem("ND/Economy/Run All M1 Economy Checks")]
        public static void RunAllChecks()
        {
            try
            {
                EconomyM1SmokeScenarioTests.RunAll();
                Debug.Log("[Economy M1 Checks] Success");
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Economy M1 Checks] Failed: " + exception.Message);
                throw;
            }
        }
    }
}
