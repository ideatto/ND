using System;

namespace ND.Economy.Editor
{
    public static class EconomyM1SmokeScenarioTests
    {
        public static void RunAll()
        {
            PriceCalculator_ReturnsExpectedM1Prices();
            SettlementCalculator_ReturnsExpectedM1Settlement();
            GrowthCalculator_ReturnsExpectedM1RuntimeStats();
            GrowthPurchaseCalculator_SpendsDevelopmentCurrency();
            GrowthPurchaseCalculator_FailsWhenCurrencyIsNotEnough();
            CurrencyWallet_AppliesSettlementAndGrowthPurchase();
            EconomyM1LoopCalculator_ExecutesPriceSettlementCurrencyGrowthAndRuntimeStats();
            EconomyM1LoopCalculator_UsesSavedPlayerGrowthLevelForGrowthPurchase();
            EconomyM1SmokeScenario_Run_Succeeds();
        }

        private static void PriceCalculator_ReturnsExpectedM1Prices()
        {
            PriceCalculationResult result = PriceCalculator.Calculate(new PriceCalculationInput
            {
                TradeItemId = "apple",
                FromTownId = "town_start",
                ToTownId = "town_trade_01",
                RouteId = "route_01",
                Quantity = 5,
                BaseBuyPrice = 100,
                BaseSellPrice = 140
            });

            Check(result.IsValid, "Price result should be valid: " + result.ErrorCode);
            CheckEqual(100L, result.UnitBuyPrice, "UnitBuyPrice");
            CheckEqual(140L, result.UnitSellPrice, "UnitSellPrice");
            CheckEqual(500L, result.TotalBuyPrice, "TotalBuyPrice");
            CheckEqual(700L, result.TotalSellPrice, "TotalSellPrice");
            CheckEqual(200L, result.ExpectedGrossProfit, "ExpectedGrossProfit");
        }

        private static void SettlementCalculator_ReturnsExpectedM1Settlement()
        {
            SettlementBreakdown result = SettlementCalculator.Calculate(new SettlementInput
            {
                TradeId = "m1_test_trade",
                TradeMoneyBefore = 1000,
                SoldItems =
                {
                    new SoldItemInput
                    {
                        TradeItemId = "apple",
                        Quantity = 5,
                        TotalBuyPrice = 500,
                        TotalSellPrice = 700
                    }
                },
                FoodCost = 50,
                MercenaryCost = 0,
                DevelopmentCurrencyReward = 1
            });

            CheckEqual(700L, result.TotalRevenue, "TotalRevenue");
            CheckEqual(550L, result.TotalExpense, "TotalExpense");
            CheckEqual(200L, result.GrossTradeProfit, "GrossTradeProfit");
            CheckEqual(150L, result.NetProfit, "NetProfit");
            CheckEqual(1150L, result.TradeMoneyAfter, "TradeMoneyAfter");
            CheckEqual(1L, result.DevelopmentCurrencyReward, "DevelopmentCurrencyReward");
            Check(!result.IsBankrupt, "Settlement should not be bankrupt.");
            CheckEqual(5, result.Entries.Count, "Entries.Count");
        }

        private static void GrowthCalculator_ReturnsExpectedM1RuntimeStats()
        {
            CoreRuntimeStatModifier result = GrowthCalculator.CalculateM1RuntimeStats(1, 0);

            CheckEqual(10, result.MaxLoadBonus, "MaxLoadBonus");
            CheckEqual(1f, result.MaxLoadMultiplier, "MaxLoadMultiplier");
            CheckEqual(1f, result.SpeedMultiplier, "SpeedMultiplier");
            CheckEqual(1f, result.FoodEfficiencyMultiplier, "FoodEfficiencyMultiplier");
            CheckEqual(0, result.CombatPowerBonus, "CombatPowerBonus");
            CheckEqual(1f, result.CombatPowerMultiplier, "CombatPowerMultiplier");
            CheckEqual(0.5f, result.LossLimitRate, "LossLimitRate");
            CheckEqual(1f, result.RiskMultiplier, "RiskMultiplier");
            CheckEqual(0L, result.MinRecoveryTradeMoney, "MinRecoveryTradeMoney");
        }

        private static void GrowthPurchaseCalculator_SpendsDevelopmentCurrency()
        {
            GrowthPurchaseResult result = GrowthPurchaseCalculator.Purchase(new GrowthPurchaseInput
            {
                GrowthId = "growth_load_01",
                CurrentLevel = 0,
                MaxLevel = 1,
                DevelopmentCurrencyBefore = 1,
                CostDevelopmentCurrency = 1
            });

            Check(result.Success, "Growth purchase should succeed.");
            CheckEqual(GrowthPurchaseError.None, result.Error, "GrowthPurchaseError");
            CheckEqual(0, result.PreviousLevel, "PreviousLevel");
            CheckEqual(1, result.NewLevel, "NewLevel");
            CheckEqual(1L, result.CostDevelopmentCurrency, "CostDevelopmentCurrency");
            CheckEqual(0L, result.DevelopmentCurrencyAfter, "DevelopmentCurrencyAfter");
        }

        private static void GrowthPurchaseCalculator_FailsWhenCurrencyIsNotEnough()
        {
            GrowthPurchaseResult result = GrowthPurchaseCalculator.Purchase(new GrowthPurchaseInput
            {
                GrowthId = "growth_load_01",
                CurrentLevel = 0,
                MaxLevel = 1,
                DevelopmentCurrencyBefore = 0,
                CostDevelopmentCurrency = 1
            });

            Check(!result.Success, "Growth purchase should fail.");
            CheckEqual(GrowthPurchaseError.NotEnoughDevelopmentCurrency, result.Error, "GrowthPurchaseError");
            CheckEqual(0, result.NewLevel, "NewLevel");
            CheckEqual(0L, result.DevelopmentCurrencyAfter, "DevelopmentCurrencyAfter");
        }

        private static void CurrencyWallet_AppliesSettlementAndGrowthPurchase()
        {
            CurrencyState state = new CurrencyState
            {
                TradeMoney = 1000,
                DevelopmentCurrency = 0
            };

            SettlementBreakdown settlement = SettlementCalculator.Calculate(new SettlementInput
            {
                TradeId = "m1_test_trade",
                TradeMoneyBefore = state.TradeMoney,
                SoldItems =
                {
                    new SoldItemInput
                    {
                        TradeItemId = "apple",
                        Quantity = 5,
                        TotalBuyPrice = 500,
                        TotalSellPrice = 700
                    }
                },
                FoodCost = 50,
                DevelopmentCurrencyReward = 1
            });

            CurrencyApplyResult settlementApply = CurrencyWallet.ApplySettlement(state, settlement);

            Check(settlementApply.Success, "Settlement apply should succeed: " + settlementApply.ErrorCode);
            CheckEqual(1150L, state.TradeMoney, "TradeMoney after settlement");
            CheckEqual(1L, state.DevelopmentCurrency, "DevelopmentCurrency after settlement");

            GrowthPurchaseResult growthPurchase = GrowthPurchaseCalculator.Purchase(new GrowthPurchaseInput
            {
                GrowthId = "growth_load_01",
                DevelopmentCurrencyBefore = state.DevelopmentCurrency,
                CostDevelopmentCurrency = 1
            });

            CurrencyApplyResult growthApply = CurrencyWallet.ApplyGrowthPurchase(state, growthPurchase);

            Check(growthApply.Success, "Growth apply should succeed: " + growthApply.ErrorCode);
            CheckEqual(1150L, state.TradeMoney, "TradeMoney after growth");
            CheckEqual(0L, state.DevelopmentCurrency, "DevelopmentCurrency after growth");
        }

        private static void EconomyM1LoopCalculator_ExecutesPriceSettlementCurrencyGrowthAndRuntimeStats()
        {
            EconomyM1LoopResult result = EconomyM1LoopCalculator.Execute(new EconomyM1LoopInput
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
                TradeId = "m1_loop_test_trade",
                FoodCost = 50,
                DevelopmentCurrencyReward = 1,
                PurchaseGrowth = true,
                GrowthPurchaseInput = new GrowthPurchaseInput
                {
                    GrowthId = "growth_load_01",
                    CurrentLevel = 0,
                    MaxLevel = 1,
                    CostDevelopmentCurrency = 1
                }
            });

            Check(result.Success, "M1 loop should succeed: " + result.ErrorCode);
            CheckEqual(500L, result.PriceResult.TotalBuyPrice, "Loop TotalBuyPrice");
            CheckEqual(700L, result.PriceResult.TotalSellPrice, "Loop TotalSellPrice");
            CheckEqual(150L, result.Settlement.NetProfit, "Loop NetProfit");
            CheckEqual(1150L, result.SettlementCurrencyApply.After.TradeMoney, "Loop settlement TradeMoney");
            Check(result.GrowthPurchase.Success, "Loop growth purchase should succeed.");
            CheckEqual(1, result.GrowthPurchase.NewLevel, "Loop NewLevel");
            CheckEqual(0L, result.GrowthCurrencyApply.After.DevelopmentCurrency, "Loop DevelopmentCurrency");
            CheckEqual(1150L, result.FinalCurrencyState.TradeMoney, "Loop final TradeMoney");
            CheckEqual(0L, result.FinalCurrencyState.DevelopmentCurrency, "Loop final DevelopmentCurrency");
            CheckEqual(10, result.RuntimeStats.MaxLoadBonus, "Loop MaxLoadBonus");
        }

        private static void EconomyM1LoopCalculator_UsesSavedPlayerGrowthLevelForGrowthPurchase()
        {
            EconomyM1LoopResult result = EconomyM1LoopCalculator.Execute(new EconomyM1LoopInput
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
                    DevelopmentCurrency = 1
                },
                TradeId = "m1_growth_level_guard_trade",
                FoodCost = 50,
                DevelopmentCurrencyReward = 1,
                PurchaseGrowth = true,
                PlayerGrowthLevel = 1,
                GrowthPurchaseInput = new GrowthPurchaseInput
                {
                    GrowthId = "growth_load_01",
                    MaxLevel = 1,
                    CostDevelopmentCurrency = 1
                }
            });

            Check(!result.Success, "M1 loop should fail when saved growth level is already max.");
            CheckEqual("GrowthPurchaseFailed:" + GrowthPurchaseError.AlreadyMaxLevel, result.ErrorCode, "Loop growth max error");
            CheckEqual(GrowthPurchaseError.AlreadyMaxLevel, result.GrowthPurchase.Error, "Loop growth purchase error");
            CheckEqual(1, result.GrowthPurchase.PreviousLevel, "Loop previous growth level");
        }

        private static void EconomyM1SmokeScenario_Run_Succeeds()
        {
            EconomyM1SmokeResult result = EconomyM1SmokeScenario.Run();

            Check(result.Success, "Smoke scenario should succeed: " + result.ErrorMessage);
            CheckEqual(500L, result.PriceResult.TotalBuyPrice, "Smoke TotalBuyPrice");
            CheckEqual(700L, result.PriceResult.TotalSellPrice, "Smoke TotalSellPrice");
            CheckEqual(150L, result.Settlement.NetProfit, "Smoke NetProfit");
            CheckEqual(1150L, result.Settlement.TradeMoneyAfter, "Smoke TradeMoneyAfter");
            Check(result.SettlementCurrencyApply.Success, "Smoke settlement currency apply should succeed.");
            Check(result.GrowthPurchase.Success, "Smoke growth purchase should succeed.");
            Check(result.GrowthCurrencyApply.Success, "Smoke growth currency apply should succeed.");
            CheckEqual(10, result.RuntimeStats.MaxLoadBonus, "Smoke MaxLoadBonus");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void CheckEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException(label + " expected " + expected + " but was " + actual + ".");
            }
        }
    }
}
