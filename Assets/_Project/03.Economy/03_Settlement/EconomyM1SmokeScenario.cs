using System;
using System.Collections.Generic;

namespace ND.Economy
{
    [Serializable]
    public sealed class EconomyM1SmokeResult
    {
        public bool Success;
        public string ErrorMessage = string.Empty;
        public PriceCalculationResult PriceResult;
        public SettlementBreakdown Settlement;
        public CurrencyApplyResult SettlementCurrencyApply;
        public GrowthPurchaseResult GrowthPurchase;
        public CurrencyApplyResult GrowthCurrencyApply;
        public CoreRuntimeStatModifier RuntimeStats;
    }

    public static class EconomyM1SmokeScenario
    {
        public static EconomyM1SmokeResult Run()
        {
            PriceCalculationResult priceResult = PriceCalculator.Calculate(new PriceCalculationInput
            {
                ItemId = "apple",
                FromTownId = "town_start",
                ToTownId = "town_trade_01",
                RouteId = "route_01",
                Quantity = 5,
                BaseBuyPrice = 100,
                BaseSellPrice = 140
            });

            if (!priceResult.IsValid)
            {
                return Fail("Price calculation failed: " + priceResult.ErrorCode, priceResult, null, null, null, null, null);
            }

            CurrencyState currencyState = new CurrencyState
            {
                TradeMoney = 1000,
                DevelopmentCurrency = 0
            };

            SettlementBreakdown settlement = SettlementCalculator.Calculate(new SettlementInput
            {
                TradeId = "m1_smoke_trade",
                TradeMoneyBefore = currencyState.TradeMoney,
                SoldItems = new List<SoldItemInput>
                {
                    new SoldItemInput
                    {
                        ItemId = "apple",
                        Quantity = 5,
                        TotalBuyPrice = priceResult.TotalBuyPrice,
                        TotalSellPrice = priceResult.TotalSellPrice
                    }
                },
                FoodCost = 50,
                MercenaryCost = 0,
                DevelopmentCurrencyReward = 1
            });

            CurrencyApplyResult settlementCurrencyApply = CurrencyWallet.ApplySettlement(currencyState, settlement);
            if (!settlementCurrencyApply.Success)
            {
                return Fail(
                    "Settlement currency apply failed: " + settlementCurrencyApply.ErrorCode,
                    priceResult,
                    settlement,
                    settlementCurrencyApply,
                    null,
                    null,
                    null);
            }

            GrowthPurchaseResult growthPurchase = GrowthPurchaseCalculator.Purchase(new GrowthPurchaseInput
            {
                GrowthId = "growth_load_01",
                CurrentLevel = 0,
                MaxLevel = 1,
                DevelopmentCurrencyBefore = currencyState.DevelopmentCurrency,
                CostDevelopmentCurrency = 1
            });

            if (!growthPurchase.Success)
            {
                return Fail(
                    "Growth purchase failed: " + growthPurchase.Error,
                    priceResult,
                    settlement,
                    settlementCurrencyApply,
                    growthPurchase,
                    null,
                    null);
            }

            CurrencyApplyResult growthCurrencyApply = CurrencyWallet.ApplyGrowthPurchase(currencyState, growthPurchase);
            if (!growthCurrencyApply.Success)
            {
                return Fail(
                    "Growth currency apply failed: " + growthCurrencyApply.ErrorCode,
                    priceResult,
                    settlement,
                    settlementCurrencyApply,
                    growthPurchase,
                    growthCurrencyApply,
                    null);
            }

            CoreRuntimeStatModifier runtimeStats = GrowthCalculator.CalculateM1RuntimeStats(growthPurchase.NewLevel, 0);

            string errorMessage = Validate(priceResult, settlement, settlementCurrencyApply, growthPurchase, growthCurrencyApply, runtimeStats);
            if (!string.IsNullOrEmpty(errorMessage))
            {
                return Fail(errorMessage, priceResult, settlement, settlementCurrencyApply, growthPurchase, growthCurrencyApply, runtimeStats);
            }

            return new EconomyM1SmokeResult
            {
                Success = true,
                PriceResult = priceResult,
                Settlement = settlement,
                SettlementCurrencyApply = settlementCurrencyApply,
                GrowthPurchase = growthPurchase,
                GrowthCurrencyApply = growthCurrencyApply,
                RuntimeStats = runtimeStats
            };
        }

        private static string Validate(
            PriceCalculationResult priceResult,
            SettlementBreakdown settlement,
            CurrencyApplyResult settlementCurrencyApply,
            GrowthPurchaseResult growthPurchase,
            CurrencyApplyResult growthCurrencyApply,
            CoreRuntimeStatModifier runtimeStats)
        {
            if (priceResult.UnitBuyPrice != 100)
            {
                return "Expected unit buy price 100.";
            }

            if (priceResult.UnitSellPrice != 140)
            {
                return "Expected unit sell price 140.";
            }

            if (priceResult.TotalBuyPrice != 500)
            {
                return "Expected total buy price 500.";
            }

            if (priceResult.TotalSellPrice != 700)
            {
                return "Expected total sell price 700.";
            }

            if (settlement.NetProfit != 150)
            {
                return "Expected net profit 150.";
            }

            if (settlement.TradeMoneyAfter != 1150)
            {
                return "Expected trade money after settlement 1150.";
            }

            if (settlement.DevelopmentCurrencyReward != 1)
            {
                return "Expected development currency reward 1.";
            }

            if (!settlementCurrencyApply.Success)
            {
                return "Expected settlement currency apply success.";
            }

            if (settlementCurrencyApply.After.TradeMoney != 1150)
            {
                return "Expected currency state trade money 1150 after settlement.";
            }

            if (settlementCurrencyApply.After.DevelopmentCurrency != 1)
            {
                return "Expected currency state development currency 1 after settlement.";
            }

            if (!growthPurchase.Success)
            {
                return "Expected growth purchase success.";
            }

            if (growthPurchase.NewLevel != 1)
            {
                return "Expected growth level 1.";
            }

            if (growthPurchase.DevelopmentCurrencyAfter != 0)
            {
                return "Expected development currency after growth purchase 0.";
            }

            if (!growthCurrencyApply.Success)
            {
                return "Expected growth currency apply success.";
            }

            if (growthCurrencyApply.After.TradeMoney != 1150)
            {
                return "Expected currency state trade money to stay 1150 after growth purchase.";
            }

            if (growthCurrencyApply.After.DevelopmentCurrency != 0)
            {
                return "Expected currency state development currency 0 after growth purchase.";
            }

            if (runtimeStats.MaxLoadBonus != 10)
            {
                return "Expected max load bonus 10.";
            }

            if (runtimeStats.SpeedMultiplier != 1f)
            {
                return "Expected speed multiplier 1.0.";
            }

            if (Math.Abs(runtimeStats.LossLimitRate - 0.5f) > 0.0001f)
            {
                return "Expected loss limit rate 0.5.";
            }

            return string.Empty;
        }

        private static EconomyM1SmokeResult Fail(
            string errorMessage,
            PriceCalculationResult priceResult,
            SettlementBreakdown settlement,
            CurrencyApplyResult settlementCurrencyApply,
            GrowthPurchaseResult growthPurchase,
            CurrencyApplyResult growthCurrencyApply,
            CoreRuntimeStatModifier runtimeStats)
        {
            return new EconomyM1SmokeResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                PriceResult = priceResult,
                Settlement = settlement,
                SettlementCurrencyApply = settlementCurrencyApply,
                GrowthPurchase = growthPurchase,
                GrowthCurrencyApply = growthCurrencyApply,
                RuntimeStats = runtimeStats
            };
        }
    }
}
