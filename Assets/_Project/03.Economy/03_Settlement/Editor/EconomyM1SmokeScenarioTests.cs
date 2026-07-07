using NUnit.Framework;

namespace ND.Economy.Editor
{
    public sealed class EconomyM1SmokeScenarioTests
    {
        [Test]
        public void PriceCalculator_ReturnsExpectedM1Prices()
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

            Assert.That(result.IsValid, Is.True, result.ErrorCode);
            Assert.That(result.UnitBuyPrice, Is.EqualTo(100));
            Assert.That(result.UnitSellPrice, Is.EqualTo(140));
            Assert.That(result.TotalBuyPrice, Is.EqualTo(500));
            Assert.That(result.TotalSellPrice, Is.EqualTo(700));
            Assert.That(result.ExpectedGrossProfit, Is.EqualTo(200));
        }

        [Test]
        public void SettlementCalculator_ReturnsExpectedM1Settlement()
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

            Assert.That(result.TotalRevenue, Is.EqualTo(700));
            Assert.That(result.TotalExpense, Is.EqualTo(550));
            Assert.That(result.GrossTradeProfit, Is.EqualTo(200));
            Assert.That(result.NetProfit, Is.EqualTo(150));
            Assert.That(result.TradeMoneyAfter, Is.EqualTo(1150));
            Assert.That(result.DevelopmentCurrencyReward, Is.EqualTo(1));
            Assert.That(result.IsBankrupt, Is.False);
            Assert.That(result.Entries, Has.Count.EqualTo(5));
        }

        [Test]
        public void GrowthCalculator_ReturnsExpectedM1RuntimeStats()
        {
            CoreRuntimeStatModifier result = GrowthCalculator.CalculateM1RuntimeStats(1, 0);

            Assert.That(result.MaxLoadBonus, Is.EqualTo(10));
            Assert.That(result.MaxLoadMultiplier, Is.EqualTo(1f));
            Assert.That(result.SpeedMultiplier, Is.EqualTo(1f));
            Assert.That(result.FoodEfficiencyMultiplier, Is.EqualTo(1f));
            Assert.That(result.CombatPowerBonus, Is.EqualTo(0));
            Assert.That(result.CombatPowerMultiplier, Is.EqualTo(1f));
            Assert.That(result.LossLimitRate, Is.EqualTo(0.5f));
            Assert.That(result.RiskMultiplier, Is.EqualTo(1f));
            Assert.That(result.MinRecoveryTradeMoney, Is.EqualTo(0));
        }

        [Test]
        public void GrowthPurchaseCalculator_SpendsDevelopmentCurrency()
        {
            GrowthPurchaseResult result = GrowthPurchaseCalculator.Purchase(new GrowthPurchaseInput
            {
                GrowthId = "growth_load_01",
                CurrentLevel = 0,
                MaxLevel = 1,
                DevelopmentCurrencyBefore = 1,
                CostDevelopmentCurrency = 1
            });

            Assert.That(result.Success, Is.True);
            Assert.That(result.Error, Is.EqualTo(GrowthPurchaseError.None));
            Assert.That(result.PreviousLevel, Is.EqualTo(0));
            Assert.That(result.NewLevel, Is.EqualTo(1));
            Assert.That(result.CostDevelopmentCurrency, Is.EqualTo(1));
            Assert.That(result.DevelopmentCurrencyAfter, Is.EqualTo(0));
        }

        [Test]
        public void GrowthPurchaseCalculator_FailsWhenCurrencyIsNotEnough()
        {
            GrowthPurchaseResult result = GrowthPurchaseCalculator.Purchase(new GrowthPurchaseInput
            {
                GrowthId = "growth_load_01",
                CurrentLevel = 0,
                MaxLevel = 1,
                DevelopmentCurrencyBefore = 0,
                CostDevelopmentCurrency = 1
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.EqualTo(GrowthPurchaseError.NotEnoughDevelopmentCurrency));
            Assert.That(result.NewLevel, Is.EqualTo(0));
            Assert.That(result.DevelopmentCurrencyAfter, Is.EqualTo(0));
        }

        [Test]
        public void CurrencyWallet_AppliesSettlementAndGrowthPurchase()
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

            Assert.That(settlementApply.Success, Is.True, settlementApply.ErrorCode);
            Assert.That(state.TradeMoney, Is.EqualTo(1150));
            Assert.That(state.DevelopmentCurrency, Is.EqualTo(1));

            GrowthPurchaseResult growthPurchase = GrowthPurchaseCalculator.Purchase(new GrowthPurchaseInput
            {
                GrowthId = "growth_load_01",
                DevelopmentCurrencyBefore = state.DevelopmentCurrency,
                CostDevelopmentCurrency = 1
            });

            CurrencyApplyResult growthApply = CurrencyWallet.ApplyGrowthPurchase(state, growthPurchase);

            Assert.That(growthApply.Success, Is.True, growthApply.ErrorCode);
            Assert.That(state.TradeMoney, Is.EqualTo(1150));
            Assert.That(state.DevelopmentCurrency, Is.EqualTo(0));
        }

        [Test]
        public void EconomyM1LoopCalculator_ExecutesPriceSettlementCurrencyGrowthAndRuntimeStats()
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

            Assert.That(result.Success, Is.True, result.ErrorCode);
            Assert.That(result.PriceResult.TotalBuyPrice, Is.EqualTo(500));
            Assert.That(result.PriceResult.TotalSellPrice, Is.EqualTo(700));
            Assert.That(result.Settlement.NetProfit, Is.EqualTo(150));
            Assert.That(result.SettlementCurrencyApply.After.TradeMoney, Is.EqualTo(1150));
            Assert.That(result.GrowthPurchase.Success, Is.True);
            Assert.That(result.GrowthPurchase.NewLevel, Is.EqualTo(1));
            Assert.That(result.GrowthCurrencyApply.After.DevelopmentCurrency, Is.EqualTo(0));
            Assert.That(result.FinalCurrencyState.TradeMoney, Is.EqualTo(1150));
            Assert.That(result.FinalCurrencyState.DevelopmentCurrency, Is.EqualTo(0));
            Assert.That(result.RuntimeStats.MaxLoadBonus, Is.EqualTo(10));
        }

        [Test]
        public void EconomyM1SmokeScenario_Run_Succeeds()
        {
            EconomyM1SmokeResult result = EconomyM1SmokeScenario.Run();

            Assert.That(result.Success, Is.True, result.ErrorMessage);
            Assert.That(result.PriceResult.TotalBuyPrice, Is.EqualTo(500));
            Assert.That(result.PriceResult.TotalSellPrice, Is.EqualTo(700));
            Assert.That(result.Settlement.NetProfit, Is.EqualTo(150));
            Assert.That(result.Settlement.TradeMoneyAfter, Is.EqualTo(1150));
            Assert.That(result.SettlementCurrencyApply.Success, Is.True);
            Assert.That(result.SettlementCurrencyApply.After.TradeMoney, Is.EqualTo(1150));
            Assert.That(result.SettlementCurrencyApply.After.DevelopmentCurrency, Is.EqualTo(1));
            Assert.That(result.GrowthPurchase.Success, Is.True);
            Assert.That(result.GrowthPurchase.NewLevel, Is.EqualTo(1));
            Assert.That(result.GrowthPurchase.DevelopmentCurrencyAfter, Is.EqualTo(0));
            Assert.That(result.GrowthCurrencyApply.Success, Is.True);
            Assert.That(result.GrowthCurrencyApply.After.TradeMoney, Is.EqualTo(1150));
            Assert.That(result.GrowthCurrencyApply.After.DevelopmentCurrency, Is.EqualTo(0));
            Assert.That(result.RuntimeStats.MaxLoadBonus, Is.EqualTo(10));
        }
    }
}
