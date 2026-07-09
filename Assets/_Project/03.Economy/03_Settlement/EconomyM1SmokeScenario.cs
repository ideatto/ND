using System;

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
        public CurrencyState FinalCurrencyState;
    }

    public static class EconomyM1SmokeScenario
    {
        public static EconomyM1SmokeResult Run()
        {
            EconomyM1LoopResult loopResult = EconomyM1LoopCalculator.Execute(new EconomyM1LoopInput
            {
                PriceInput = new PriceCalculationInput
                {
                    TradeItemId = "apple",
                    FromTownId = "town_start",
                    ToTownId = "town_trade_01",
                    RouteId = "route_01",
                    Quantity = 5,
                    BaseBuyPrice = 100,
                    BaseSellPrice = 140
                },
                CurrencyState = new CurrencyState
                {
                    TradeMoney = 1000,
                    DevelopmentCurrency = 0
                },
                TradeId = "m1_smoke_trade",
                FoodCost = 50,
                MercenaryCost = 0,
                DevelopmentCurrencyReward = 1,
                PurchaseGrowth = true,
                GrowthPurchaseInput = new GrowthPurchaseInput
                {
                    GrowthId = "growth_load_01",
                    CurrentLevel = 0,
                    MaxLevel = 1,
                    CostDevelopmentCurrency = 1
                },
                PlayerGrowthLevel = 0,
                CaravanGrowthLevel = 0
            });

            string errorMessage = loopResult.Success
                ? Validate(loopResult)
                : "Loop failed: " + loopResult.ErrorCode;
            if (!string.IsNullOrEmpty(errorMessage))
            {
                return Fail(errorMessage, loopResult);
            }

            return new EconomyM1SmokeResult
            {
                Success = true,
                PriceResult = loopResult.PriceResult,
                Settlement = loopResult.Settlement,
                SettlementCurrencyApply = loopResult.SettlementCurrencyApply,
                GrowthPurchase = loopResult.GrowthPurchase,
                GrowthCurrencyApply = loopResult.GrowthCurrencyApply,
                RuntimeStats = loopResult.RuntimeStats,
                FinalCurrencyState = loopResult.FinalCurrencyState
            };
        }

        private static string Validate(EconomyM1LoopResult result)
        {
            PriceCalculationResult priceResult = result.PriceResult;
            SettlementBreakdown settlement = result.Settlement;
            CurrencyApplyResult settlementCurrencyApply = result.SettlementCurrencyApply;
            GrowthPurchaseResult growthPurchase = result.GrowthPurchase;
            CurrencyApplyResult growthCurrencyApply = result.GrowthCurrencyApply;
            CoreRuntimeStatModifier runtimeStats = result.RuntimeStats;

            if (priceResult.UnitBuyPrice != 100L)
            {
                return "Expected unit buy price 100.";
            }

            if (priceResult.UnitSellPrice != 140L)
            {
                return "Expected unit sell price 140.";
            }

            if (priceResult.TotalBuyPrice != 500L)
            {
                return "Expected total buy price 500.";
            }

            if (priceResult.TotalSellPrice != 700L)
            {
                return "Expected total sell price 700.";
            }

            if (settlement.NetProfit != 150L)
            {
                return "Expected net profit 150.";
            }

            if (settlement.TradeMoneyAfter != 1150L)
            {
                return "Expected trade money after settlement 1150.";
            }

            if (settlement.DevelopmentCurrencyReward != 1L)
            {
                return "Expected development currency reward 1.";
            }

            if (!settlementCurrencyApply.Success)
            {
                return "Expected settlement currency apply success.";
            }

            if (settlementCurrencyApply.After.TradeMoney != 1150L)
            {
                return "Expected currency state trade money 1150 after settlement.";
            }

            if (settlementCurrencyApply.After.DevelopmentCurrency != 1L)
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

            if (growthPurchase.DevelopmentCurrencyAfter != 0L)
            {
                return "Expected development currency after growth purchase 0.";
            }

            if (!HasEntry(settlement, SettlementEntryType.GrowthPurchaseCost, 1L, false, "growth_load_01"))
            {
                return "Expected growth purchase cost settlement entry.";
            }

            if (!growthCurrencyApply.Success)
            {
                return "Expected growth currency apply success.";
            }

            if (growthCurrencyApply.After.TradeMoney != 1150L)
            {
                return "Expected currency state trade money to stay 1150 after growth purchase.";
            }

            if (growthCurrencyApply.After.DevelopmentCurrency != 0L)
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

            if (result.FinalCurrencyState.TradeMoney != 1150L)
            {
                return "Expected final trade money 1150.";
            }

            if (result.FinalCurrencyState.DevelopmentCurrency != 0L)
            {
                return "Expected final development currency 0.";
            }

            return string.Empty;
        }

        private static bool HasEntry(
            SettlementBreakdown settlement,
            SettlementEntryType entryType,
            long amount,
            bool isPositive,
            string sourceId)
        {
            if (settlement == null || settlement.Entries == null)
            {
                return false;
            }

            for (int i = 0; i < settlement.Entries.Count; i++)
            {
                SettlementEntry entry = settlement.Entries[i];
                if (entry != null
                    && entry.EntryType == entryType
                    && entry.Amount == amount
                    && entry.IsPositive == isPositive
                    && entry.SourceId == sourceId)
                {
                    return true;
                }
            }

            return false;
        }

        private static EconomyM1SmokeResult Fail(string errorMessage, EconomyM1LoopResult loopResult)
        {
            return new EconomyM1SmokeResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                PriceResult = loopResult != null ? loopResult.PriceResult : null,
                Settlement = loopResult != null ? loopResult.Settlement : null,
                SettlementCurrencyApply = loopResult != null ? loopResult.SettlementCurrencyApply : null,
                GrowthPurchase = loopResult != null ? loopResult.GrowthPurchase : null,
                GrowthCurrencyApply = loopResult != null ? loopResult.GrowthCurrencyApply : null,
                RuntimeStats = loopResult != null ? loopResult.RuntimeStats : null,
                FinalCurrencyState = loopResult != null ? loopResult.FinalCurrencyState : null
            };
        }
    }
}
