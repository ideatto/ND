using System.Collections.Generic;

namespace ND.Economy
{
    public static class EconomyM1LoopCalculator
    {
        public static EconomyM1LoopResult Execute(EconomyM1LoopInput input)
        {
            if (input == null)
            {
                return Fail("InputNull", null, null, null, null, null, null, null);
            }

            CurrencyState workingCurrency = input.CurrencyState == null
                ? new CurrencyState()
                : input.CurrencyState.Clone();

            PriceCalculationResult priceResult = null;
            var soldItems = new List<SoldItemInput>();
            if (input.CalculateItemTrade)
            {
                priceResult = PriceCalculator.Calculate(input.PriceInput);
                if (!priceResult.IsValid)
                {
                    return Fail("PriceCalculationFailed:" + priceResult.ErrorCode, priceResult, null, null, null, null, null, workingCurrency);
                }

                soldItems.Add(new SoldItemInput
                {
                    TradeItemId = input.PriceInput.TradeItemId,
                    Quantity = input.PriceInput.Quantity,
                    TotalBuyPrice = priceResult.TotalBuyPrice,
                    TotalSellPrice = priceResult.TotalSellPrice
                });
            }

            SettlementBreakdown settlement = SettlementCalculator.Calculate(new SettlementInput
            {
                TradeId = input.TradeId,
                TradeMoneyBefore = workingCurrency.TradeMoney,
                SoldItems = soldItems,
                FoodCost = input.FoodCost,
                MercenaryCost = input.MercenaryCost,
                CartRepairCost = input.CartRepairCost,
                LostItemValue = input.LostItemValue,
                EventProfit = input.EventProfit,
                EventLoss = input.EventLoss,
                LoanRepayment = input.LoanRepayment,
                DevelopmentCurrencyReward = input.DevelopmentCurrencyReward
            });

            CurrencyApplyResult settlementCurrencyApply = CurrencyWallet.ApplySettlement(workingCurrency, settlement);
            if (!settlementCurrencyApply.Success)
            {
                return Fail(
                    "SettlementCurrencyApplyFailed:" + settlementCurrencyApply.ErrorCode,
                    priceResult,
                    settlement,
                    settlementCurrencyApply,
                    null,
                    null,
                    null,
                    workingCurrency);
            }

            GrowthPurchaseResult growthPurchase = null;
            CurrencyApplyResult growthCurrencyApply = null;
            int playerGrowthLevel = input.PlayerGrowthLevel;

            if (input.PurchaseGrowth)
            {
                GrowthPurchaseInput growthInput = BuildGrowthPurchaseInput(input, workingCurrency.DevelopmentCurrency);

                growthPurchase = GrowthPurchaseCalculator.Purchase(growthInput);
                if (!growthPurchase.Success)
                {
                    return Fail(
                        "GrowthPurchaseFailed:" + growthPurchase.Error,
                        priceResult,
                        settlement,
                        settlementCurrencyApply,
                        growthPurchase,
                        null,
                        null,
                        workingCurrency);
                }

                growthCurrencyApply = CurrencyWallet.ApplyGrowthPurchase(workingCurrency, growthPurchase);
                if (!growthCurrencyApply.Success)
                {
                    return Fail(
                        "GrowthCurrencyApplyFailed:" + growthCurrencyApply.ErrorCode,
                        priceResult,
                        settlement,
                        settlementCurrencyApply,
                        growthPurchase,
                        growthCurrencyApply,
                        null,
                        workingCurrency);
                }

                AddGrowthPurchaseEntry(settlement, growthPurchase);
                playerGrowthLevel = growthPurchase.NewLevel;
            }

            CoreRuntimeStatModifier runtimeStats = GrowthCalculator.CalculateM1RuntimeStats(playerGrowthLevel, input.CaravanGrowthLevel);

            return new EconomyM1LoopResult
            {
                Success = true,
                PriceResult = priceResult,
                Settlement = settlement,
                SettlementCurrencyApply = settlementCurrencyApply,
                GrowthPurchase = growthPurchase,
                GrowthCurrencyApply = growthCurrencyApply,
                RuntimeStats = runtimeStats,
                FinalCurrencyState = workingCurrency.Clone()
            };
        }

        private static void AddGrowthPurchaseEntry(SettlementBreakdown settlement, GrowthPurchaseResult growthPurchase)
        {
            if (settlement == null || growthPurchase == null || !growthPurchase.Success)
            {
                return;
            }

            settlement.Entries.Add(new SettlementEntry
            {
                EntryType = SettlementEntryType.GrowthPurchaseCost,
                DisplayNameKey = "settlement.growth_purchase_cost",
                Amount = growthPurchase.CostDevelopmentCurrency < 0L ? 0L : growthPurchase.CostDevelopmentCurrency,
                IsPositive = false,
                SourceId = string.IsNullOrWhiteSpace(growthPurchase.GrowthId) ? "growth" : growthPurchase.GrowthId
            });
        }

        private static GrowthPurchaseInput BuildGrowthPurchaseInput(EconomyM1LoopInput input, long developmentCurrencyBefore)
        {
            GrowthPurchaseInput source = input.GrowthPurchaseInput ?? new GrowthPurchaseInput();

            return new GrowthPurchaseInput
            {
                GrowthId = source.GrowthId,
                CurrentLevel = input.PlayerGrowthLevel,
                MaxLevel = source.MaxLevel,
                DevelopmentCurrencyBefore = developmentCurrencyBefore,
                CostDevelopmentCurrency = source.CostDevelopmentCurrency
            };
        }

        private static EconomyM1LoopResult Fail(
            string errorCode,
            PriceCalculationResult priceResult,
            SettlementBreakdown settlement,
            CurrencyApplyResult settlementCurrencyApply,
            GrowthPurchaseResult growthPurchase,
            CurrencyApplyResult growthCurrencyApply,
            CoreRuntimeStatModifier runtimeStats,
            CurrencyState finalCurrencyState)
        {
            return new EconomyM1LoopResult
            {
                Success = false,
                ErrorCode = errorCode,
                PriceResult = priceResult,
                Settlement = settlement,
                SettlementCurrencyApply = settlementCurrencyApply,
                GrowthPurchase = growthPurchase,
                GrowthCurrencyApply = growthCurrencyApply,
                RuntimeStats = runtimeStats,
                FinalCurrencyState = finalCurrencyState == null ? null : finalCurrencyState.Clone()
            };
        }
    }
}
