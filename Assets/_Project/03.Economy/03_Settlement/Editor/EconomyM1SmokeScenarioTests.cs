using System;

namespace ND.Economy.Editor
{
    public static class EconomyM1SmokeScenarioTests
    {
        public static void RunAll()
        {
            PriceCalculator_ReturnsExpectedM1Prices();
            PriceCalculator_OrdersModifiersWithoutMutatingInput();
            PriceCalculator_ClampsFinalUnitPricesToMinimumOne();
            EconomyCaravanCalculator_CalculatesFoodShortageMultiplier();
            LjhEconomyM1InputAdapter_ConvertsSupportedSandboxPriceModifiers();
            LjhEconomyM1InputAdapter_UsesSandboxDataForM1LoopInput();
            SettlementCalculator_ReturnsExpectedM1Settlement();
            SettlementCalculator_KeepsRequiredEntriesWhenSoldItemsAreNull();
            SettlementCalculator_ClampsNegativeInputsToZeroEntries();
            GrowthCalculator_ReturnsExpectedM1RuntimeStats();
            GrowthCalculator_DoesNotIncreaseSpeedInM1();
            GrowthPurchaseCalculator_SpendsDevelopmentCurrency();
            GrowthPurchaseCalculator_FailsWhenCurrencyIsNotEnough();
            CurrencyWallet_AppliesSettlementAndGrowthPurchase();
            EconomyM1LoopCalculator_ExecutesPriceSettlementCurrencyGrowthAndRuntimeStats();
            EconomyM1LoopCalculator_ReturnsPriceFailureErrorCode();
            EconomyM1LoopCalculator_ReturnsGrowthPurchaseFailureErrorCode();
            EconomyM1LoopCalculator_UsesSavedPlayerGrowthLevelForGrowthPurchase();
            EconomyM1LoopCalculator_RepeatsThreeTimesWithoutDuplicateGrowthOrNegativeCurrency();
            EconomyM1FlowService_AppliesSuccessfulResultToSaveData();
            EconomyM1SettlementViewAdapter_ProvidesDisplayOnlyResult();
            EconomyM1TestContentAssets_ExecuteM1Flow();
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

        private static void PriceCalculator_OrdersModifiersWithoutMutatingInput()
        {
            PriceCalculationInput input = new PriceCalculationInput
            {
                TradeItemId = "apple",
                FromTownId = "town_start",
                ToTownId = "town_trade_01",
                RouteId = "route_01",
                Quantity = 1,
                BaseBuyPrice = 100,
                BaseSellPrice = 100,
                Modifiers =
                {
                    new PriceModifierInput
                    {
                        ModifierType = PriceModifierType.PlayerGrowth,
                        SourceId = "growth_price_01",
                        Target = PriceModifierTarget.BuyPrice,
                        Operation = PriceModifierOperation.Percent,
                        Value = -0.1f
                    },
                    new PriceModifierInput
                    {
                        ModifierType = PriceModifierType.Town,
                        SourceId = "town_trade_01",
                        Target = PriceModifierTarget.Both,
                        Operation = PriceModifierOperation.Add,
                        Value = 20f
                    }
                }
            };

            PriceCalculationResult result = PriceCalculator.Calculate(input);

            Check(result.IsValid, "Price modifier result should be valid: " + result.ErrorCode);
            CheckEqual(108L, result.UnitBuyPrice, "Modifier UnitBuyPrice");
            CheckEqual(120L, result.UnitSellPrice, "Modifier UnitSellPrice");
            CheckEqual(PriceModifierType.Town, result.Modifiers[0].ModifierType, "First applied modifier");
            CheckEqual(PriceModifierType.PlayerGrowth, result.Modifiers[1].ModifierType, "Second applied modifier");
            CheckEqual(PriceModifierType.PlayerGrowth, input.Modifiers[0].ModifierType, "Input first modifier should keep original order");
            CheckEqual(PriceModifierType.Town, input.Modifiers[1].ModifierType, "Input second modifier should keep original order");
        }

        private static void PriceCalculator_ClampsFinalUnitPricesToMinimumOne()
        {
            PriceCalculationResult result = PriceCalculator.Calculate(new PriceCalculationInput
            {
                TradeItemId = "apple",
                FromTownId = "town_start",
                ToTownId = "town_trade_01",
                RouteId = "route_01",
                Quantity = 3,
                BaseBuyPrice = 100,
                BaseSellPrice = 100,
                Modifiers =
                {
                    new PriceModifierInput
                    {
                        ModifierType = PriceModifierType.Debug,
                        SourceId = "debug_negative_price",
                        Target = PriceModifierTarget.Both,
                        Operation = PriceModifierOperation.Add,
                        Value = -1000f
                    }
                }
            });

            Check(result.IsValid, "Price clamp result should be valid: " + result.ErrorCode);
            CheckEqual(1L, result.UnitBuyPrice, "Clamped UnitBuyPrice");
            CheckEqual(1L, result.UnitSellPrice, "Clamped UnitSellPrice");
            CheckEqual(3L, result.TotalBuyPrice, "Clamped TotalBuyPrice");
            CheckEqual(3L, result.TotalSellPrice, "Clamped TotalSellPrice");
        }

        private static void EconomyCaravanCalculator_CalculatesFoodShortageMultiplier()
        {
            CheckEqual(1f,
                CaravanCalculator.GetFoodEfficiency(100f, 100f),
                "Enough food speed multiplier");
            CheckEqual(0.75f,
                CaravanCalculator.GetFoodEfficiency(50f, 100f),
                "Half food speed multiplier");
            CheckEqual(CaravanCalculator.FoodSpeedMultiplierMin,
                CaravanCalculator.GetFoodEfficiency(0f, 100f),
                "No food speed multiplier");
        }

        private static void LjhEconomyM1InputAdapter_ConvertsSupportedSandboxPriceModifiers()
        {
            global::ModifierInput[] sourceModifiers =
            {
                new global::ModifierInput
                {
                    modifierType = global::ModifierType.Season,
                    sourceId = "season_summer",
                    displayName = "Summer",
                    modifierBundles = new[]
                    {
                        new global::ModifierBundle
                        {
                            modifierTarget = global::Target.BuyPrice,
                            modifierOperation = global::Operation.Percent,
                            value = 0.1f
                        }
                    }
                },
                new global::ModifierInput
                {
                    modifierType = global::ModifierType.ActiveEvent,
                    sourceId = "event_market_day",
                    displayName = "Market Day",
                    modifierBundles = new[]
                    {
                        new global::ModifierBundle
                        {
                            modifierTarget = global::Target.SellPrice,
                            modifierOperation = global::Operation.Add,
                            value = 20f
                        },
                        new global::ModifierBundle
                        {
                            modifierTarget = global::Target.BaseMoveSpeed,
                            modifierOperation = global::Operation.Add,
                            value = 3f
                        }
                    }
                },
                new global::ModifierInput
                {
                    modifierType = global::ModifierType.LowerSupply,
                    modifierBundles = new global::ModifierBundle[0]
                },
                new global::ModifierInput
                {
                    modifierType = global::ModifierType.Disaster,
                    modifierBundles = new[]
                    {
                        new global::ModifierBundle
                        {
                            modifierTarget = global::Target.BuyPrice,
                            modifierOperation = global::Operation.Subtract,
                            value = 10f
                        }
                    }
                }
            };

            System.Collections.Generic.List<PriceModifierInput> modifiers =
                LjhEconomyM1InputAdapter.ToPriceModifierInputs(sourceModifiers);

            CheckEqual(2, modifiers.Count, "Supported Sandbox modifier count");
            CheckEqual(PriceModifierType.Season, modifiers[0].ModifierType, "Season modifier type");
            CheckEqual(PriceModifierTarget.BuyPrice, modifiers[0].Target, "Season modifier target");
            CheckEqual(PriceModifierOperation.Percent, modifiers[0].Operation, "Season modifier operation");
            CheckEqual(PriceModifierType.RouteEvent, modifiers[1].ModifierType, "Event modifier type");
            CheckEqual(PriceModifierTarget.SellPrice, modifiers[1].Target, "Event modifier target");
            CheckEqual(PriceModifierOperation.Add, modifiers[1].Operation, "Event modifier operation");

            PriceCalculationResult result = PriceCalculator.Calculate(new PriceCalculationInput
            {
                TradeItemId = "apple",
                FromTownId = "town_start",
                ToTownId = "town_trade_01",
                RouteId = "route_01",
                Quantity = 1,
                BaseBuyPrice = 100,
                BaseSellPrice = 100,
                Modifiers = modifiers
            });

            Check(result.IsValid, "Converted Sandbox modifiers should calculate: " + result.ErrorCode);
            CheckEqual(110L, result.UnitBuyPrice, "Converted modifier UnitBuyPrice");
            CheckEqual(120L, result.UnitSellPrice, "Converted modifier UnitSellPrice");
        }

        private static void LjhEconomyM1InputAdapter_UsesSandboxDataForM1LoopInput()
        {
            global::TradeItemData item = UnityEngine.ScriptableObject.CreateInstance<global::TradeItemData>();
            global::TownData fromTown = UnityEngine.ScriptableObject.CreateInstance<global::TownData>();
            global::TownData toTown = UnityEngine.ScriptableObject.CreateInstance<global::TownData>();
            global::RouteData route = UnityEngine.ScriptableObject.CreateInstance<global::RouteData>();

            try
            {
                SetPrivateField(item, "itemId", "apple");
                SetPrivateField(item, "baseBuyPrice", 100L);
                SetPrivateField(item, "baseSellPrice", 100L);
                SetPrivateField(item, "modifiers", new[]
                {
                    new global::ModifierInput
                    {
                        modifierType = global::ModifierType.Season,
                        sourceId = "season_summer",
                        displayName = "Summer",
                        modifierBundles = new[]
                        {
                            new global::ModifierBundle
                            {
                                modifierTarget = global::Target.BuyPrice,
                                modifierOperation = global::Operation.Percent,
                                value = 0.1f
                            }
                        }
                    }
                });

                SetPrivateField(fromTown, "townId", "town_start");
                SetPrivateField(toTown, "townId", "town_trade_01");
                SetPrivateField(route, "routeId", "route_01");
                SetPrivateField(route, "fromTown", fromTown);
                SetPrivateField(route, "toTown", toTown);
                SetPrivateField(route, "baseFoodCost", 50L);
                SetPrivateField(route, "baseMercenaryCost", 0L);

                global::SaveData saveData = new global::SaveData();
                saveData.player.tradingCurrency = 1000L;
                PriceCalculationInput input = LjhEconomyM1InputAdapter.ToPriceCalculationInput(
                    item,
                    route,
                    saveData,
                    1);
                PriceCalculationResult result = PriceCalculator.Calculate(input);

                CheckEqual(1, input.Modifiers.Count, "TradeItem modifier input count");
                Check(result.IsValid, "TradeItem modifier price input should be valid: " + result.ErrorCode);
                CheckEqual(110L, result.UnitBuyPrice, "TradeItem modifier UnitBuyPrice");
                CheckEqual(100L, result.UnitSellPrice, "TradeItem modifier UnitSellPrice");

                EconomyM1LoopResult loopResult = EconomyM1LoopCalculator.Execute(
                    LjhEconomyM1InputAdapter.ToEconomyM1LoopInput(
                        saveData,
                        item,
                        route,
                        1,
                        "sandbox_adapter_test",
                        1L,
                        false,
                        string.Empty,
                        0,
                        0));

                Check(loopResult.Success, "Sandbox data M1 loop should succeed: " + loopResult.ErrorCode);
                CheckEqual(110L, loopResult.PriceResult.TotalBuyPrice, "Sandbox loop TotalBuyPrice");
                CheckEqual(100L, loopResult.PriceResult.TotalSellPrice, "Sandbox loop TotalSellPrice");
                CheckEqual(-60L, loopResult.Settlement.NetProfit, "Sandbox loop NetProfit");
                CheckEqual(940L, loopResult.FinalCurrencyState.TradeMoney, "Sandbox loop final TradeMoney");
                CheckEqual(1L, loopResult.FinalCurrencyState.DevelopmentCurrency, "Sandbox loop final DevelopmentCurrency");

                LjhEconomyM1InputAdapter.ApplyFinalCurrencyState(saveData, loopResult.FinalCurrencyState);
                CheckEqual(940L, saveData.player.tradingCurrency, "Applied SaveData tradingCurrency");
                CheckEqual(1L, saveData.player.developmentCurrency, "Applied SaveData developmentCurrency");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(item);
                UnityEngine.Object.DestroyImmediate(fromTown);
                UnityEngine.Object.DestroyImmediate(toTown);
                UnityEngine.Object.DestroyImmediate(route);
            }
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
            Check(HasEntry(result, SettlementEntryType.ItemPurchaseCost, 500L, false, "apple"), "Settlement should include apple purchase cost entry.");
            Check(HasEntry(result, SettlementEntryType.ItemSaleRevenue, 700L, true, "apple"), "Settlement should include apple sale revenue entry.");
            Check(HasEntry(result, SettlementEntryType.FoodCost, 50L, false, "food"), "Settlement should include food cost entry.");
            Check(HasEntry(result, SettlementEntryType.MercenaryCost, 0L, false, "mercenary"), "Settlement should include zero mercenary cost entry.");
            Check(HasEntry(result, SettlementEntryType.DevelopmentCurrencyReward, 1L, true, "developmentCurrency"), "Settlement should include development currency reward entry.");
        }

        private static void SettlementCalculator_KeepsRequiredEntriesWhenSoldItemsAreNull()
        {
            SettlementBreakdown result = SettlementCalculator.Calculate(new SettlementInput
            {
                TradeId = "m1_null_item_trade",
                TradeMoneyBefore = 1000,
                SoldItems = { null },
                FoodCost = 0,
                MercenaryCost = 0,
                DevelopmentCurrencyReward = 0
            });

            CheckEqual(5, result.Entries.Count, "Null item Entries.Count");
            Check(HasEntry(result, SettlementEntryType.ItemPurchaseCost, 0L, false, "system"), "Null item settlement should include zero purchase entry.");
            Check(HasEntry(result, SettlementEntryType.ItemSaleRevenue, 0L, true, "system"), "Null item settlement should include zero sale entry.");
            Check(HasEntry(result, SettlementEntryType.FoodCost, 0L, false, "food"), "Null item settlement should include zero food entry.");
            Check(HasEntry(result, SettlementEntryType.MercenaryCost, 0L, false, "mercenary"), "Null item settlement should include zero mercenary entry.");
            Check(HasEntry(result, SettlementEntryType.DevelopmentCurrencyReward, 0L, true, "developmentCurrency"), "Null item settlement should include zero development currency reward entry.");
        }

        private static void SettlementCalculator_ClampsNegativeInputsToZeroEntries()
        {
            SettlementBreakdown result = SettlementCalculator.Calculate(new SettlementInput
            {
                TradeId = "m1_negative_settlement_trade",
                TradeMoneyBefore = 1000,
                SoldItems =
                {
                    new SoldItemInput
                    {
                        TradeItemId = "apple",
                        Quantity = 5,
                        TotalBuyPrice = -500,
                        TotalSellPrice = -700
                    }
                },
                FoodCost = -50,
                MercenaryCost = -10,
                CartRepairCost = -5,
                LostItemValue = -4,
                EventProfit = -3,
                EventLoss = -2,
                LoanRepayment = -1,
                DevelopmentCurrencyReward = -1
            });

            CheckEqual(0L, result.TotalRevenue, "Negative TotalRevenue");
            CheckEqual(0L, result.TotalExpense, "Negative TotalExpense");
            CheckEqual(0L, result.NetProfit, "Negative NetProfit");
            CheckEqual(1000L, result.TradeMoneyAfter, "Negative TradeMoneyAfter");
            CheckEqual(0L, result.DevelopmentCurrencyReward, "Negative DevelopmentCurrencyReward");
            Check(HasEntry(result, SettlementEntryType.ItemPurchaseCost, 0L, false, "apple"), "Negative settlement should clamp purchase entry.");
            Check(HasEntry(result, SettlementEntryType.ItemSaleRevenue, 0L, true, "apple"), "Negative settlement should clamp sale entry.");
            Check(HasEntry(result, SettlementEntryType.FoodCost, 0L, false, "food"), "Negative settlement should clamp food entry.");
            Check(HasEntry(result, SettlementEntryType.MercenaryCost, 0L, false, "mercenary"), "Negative settlement should clamp mercenary entry.");
            Check(HasEntry(result, SettlementEntryType.DevelopmentCurrencyReward, 0L, true, "developmentCurrency"), "Negative settlement should clamp development currency reward entry.");
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

        private static void GrowthCalculator_DoesNotIncreaseSpeedInM1()
        {
            CoreRuntimeStatModifier result = GrowthCalculator.CalculateM1RuntimeStats(1, 1);

            CheckEqual(30, result.MaxLoadBonus, "Combined MaxLoadBonus");
            CheckEqual(1f, result.SpeedMultiplier, "M1 SpeedMultiplier should stay at design placeholder value");
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
            CheckEqual(6, result.Settlement.Entries.Count, "Loop Entries.Count");
            Check(HasEntry(result.Settlement, SettlementEntryType.ItemPurchaseCost, 500L, false, "apple"), "Loop should include apple purchase cost entry.");
            Check(HasEntry(result.Settlement, SettlementEntryType.ItemSaleRevenue, 700L, true, "apple"), "Loop should include apple sale revenue entry.");
            Check(HasEntry(result.Settlement, SettlementEntryType.GrowthPurchaseCost, 1L, false, "growth_load_01"), "Loop should include growth purchase cost entry.");
            CheckEqual(1150L, result.FinalCurrencyState.TradeMoney, "Loop final TradeMoney");
            CheckEqual(0L, result.FinalCurrencyState.DevelopmentCurrency, "Loop final DevelopmentCurrency");
            CheckEqual(10, result.RuntimeStats.MaxLoadBonus, "Loop MaxLoadBonus");
        }

        private static void EconomyM1LoopCalculator_ReturnsPriceFailureErrorCode()
        {
            EconomyM1LoopResult result = EconomyM1LoopCalculator.Execute(new EconomyM1LoopInput
            {
                PriceInput = new PriceCalculationInput
                {
                    TradeItemId = "apple",
                    FromTownId = "town_start",
                    ToTownId = "town_trade_01",
                    RouteId = "route_01",
                    Quantity = 0,
                    BaseBuyPrice = 100,
                    BaseSellPrice = 140
                },
                CurrencyState = new CurrencyState
                {
                    TradeMoney = 1000,
                    DevelopmentCurrency = 0
                },
                TradeId = "m1_invalid_price_trade",
                FoodCost = 50,
                DevelopmentCurrencyReward = 1
            });

            Check(!result.Success, "M1 loop should fail when price input quantity is invalid.");
            CheckEqual("PriceCalculationFailed:" + PriceCalculator.ErrorInvalidQuantity, result.ErrorCode, "Loop invalid quantity error");
            Check(result.PriceResult != null, "Loop failure should include price result.");
            Check(!result.PriceResult.IsValid, "Loop failure price result should be invalid.");
            CheckEqual(PriceCalculator.ErrorInvalidQuantity, result.PriceResult.ErrorCode, "PriceResult invalid quantity error");
            Check(result.Settlement == null, "Loop price failure should not calculate settlement.");
            CheckEqual(1000L, result.FinalCurrencyState.TradeMoney, "Loop price failure final trade money snapshot");
            CheckEqual(0L, result.FinalCurrencyState.DevelopmentCurrency, "Loop price failure final development currency snapshot");
        }

        private static void EconomyM1LoopCalculator_ReturnsGrowthPurchaseFailureErrorCode()
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
                TradeId = "m1_growth_not_enough_currency_trade",
                FoodCost = 50,
                DevelopmentCurrencyReward = 0,
                PurchaseGrowth = true,
                GrowthPurchaseInput = new GrowthPurchaseInput
                {
                    GrowthId = "growth_load_01",
                    CurrentLevel = 0,
                    MaxLevel = 1,
                    CostDevelopmentCurrency = 1
                }
            });

            Check(!result.Success, "M1 loop should fail when growth purchase currency is not enough.");
            CheckEqual("GrowthPurchaseFailed:" + GrowthPurchaseError.NotEnoughDevelopmentCurrency, result.ErrorCode, "Loop growth currency error");
            Check(result.Settlement != null, "Loop growth failure should include settlement result.");
            CheckEqual(150L, result.Settlement.NetProfit, "Loop growth failure settlement net profit");
            Check(result.SettlementCurrencyApply.Success, "Loop growth failure should include settlement currency apply.");
            CheckEqual(1150L, result.FinalCurrencyState.TradeMoney, "Loop growth failure final trade money snapshot");
            CheckEqual(0L, result.FinalCurrencyState.DevelopmentCurrency, "Loop growth failure final development currency snapshot");
            Check(result.GrowthPurchase != null, "Loop growth failure should include growth purchase result.");
            CheckEqual(GrowthPurchaseError.NotEnoughDevelopmentCurrency, result.GrowthPurchase.Error, "Loop growth purchase result error");
            Check(result.GrowthCurrencyApply == null, "Loop growth purchase failure should not apply growth currency.");
            Check(result.RuntimeStats == null, "Loop growth purchase failure should not calculate runtime stats.");
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

        private static void EconomyM1LoopCalculator_RepeatsThreeTimesWithoutDuplicateGrowthOrNegativeCurrency()
        {
            CurrencyState currency = new CurrencyState
            {
                TradeMoney = 1000,
                DevelopmentCurrency = 0
            };

            int playerGrowthLevel = 0;
            EconomyM1LoopResult lastResult = null;

            for (int i = 0; i < 3; i++)
            {
                bool purchaseGrowth = playerGrowthLevel < 1;

                lastResult = EconomyM1LoopCalculator.Execute(new EconomyM1LoopInput
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
                    CurrencyState = currency,
                    TradeId = "m1_repeat_trade_" + i,
                    FoodCost = 50,
                    MercenaryCost = 0,
                    DevelopmentCurrencyReward = 1,
                    PurchaseGrowth = purchaseGrowth,
                    PlayerGrowthLevel = playerGrowthLevel,
                    GrowthPurchaseInput = new GrowthPurchaseInput
                    {
                        GrowthId = "growth_load_01",
                        MaxLevel = 1,
                        CostDevelopmentCurrency = 1
                    }
                });

                Check(lastResult.Success, "Repeat loop " + i + " should succeed: " + lastResult.ErrorCode);
                Check(lastResult.FinalCurrencyState.TradeMoney >= 0L, "Repeat loop trade money should not be negative.");
                Check(lastResult.FinalCurrencyState.DevelopmentCurrency >= 0L, "Repeat loop development currency should not be negative.");

                if (purchaseGrowth)
                {
                    Check(lastResult.GrowthPurchase.Success, "Repeat loop growth purchase should succeed once.");
                    playerGrowthLevel = lastResult.GrowthPurchase.NewLevel;
                }

                currency = lastResult.FinalCurrencyState.Clone();
            }

            CheckEqual(1450L, currency.TradeMoney, "Repeat final TradeMoney");
            CheckEqual(2L, currency.DevelopmentCurrency, "Repeat final DevelopmentCurrency");
            CheckEqual(1, playerGrowthLevel, "Repeat final PlayerGrowthLevel");
            CheckEqual(10, lastResult.RuntimeStats.MaxLoadBonus, "Repeat final MaxLoadBonus");
        }

        private static void EconomyM1FlowService_AppliesSuccessfulResultToSaveData()
        {
            global::TradeItemData item = UnityEngine.ScriptableObject.CreateInstance<global::TradeItemData>();
            global::TownData fromTown = UnityEngine.ScriptableObject.CreateInstance<global::TownData>();
            global::TownData toTown = UnityEngine.ScriptableObject.CreateInstance<global::TownData>();
            global::RouteData route = UnityEngine.ScriptableObject.CreateInstance<global::RouteData>();

            try
            {
                SetPrivateField(item, "itemId", "apple");
                SetPrivateField(item, "baseBuyPrice", 100L);
                SetPrivateField(item, "baseSellPrice", 140L);
                SetPrivateField(fromTown, "townId", "town_start");
                SetPrivateField(toTown, "townId", "town_trade_01");
                SetPrivateField(route, "routeId", "route_01");
                SetPrivateField(route, "fromTown", fromTown);
                SetPrivateField(route, "toTown", toTown);
                SetPrivateField(route, "baseFoodCost", 50L);

                global::SaveData saveData = new global::SaveData();
                saveData.player.tradingCurrency = 1000L;

                EconomyM1LoopResult result = EconomyM1FlowService.ExecuteTradeAndApply(
                    saveData,
                    item,
                    route,
                    1,
                    "flow_service_test",
                    1L,
                    false,
                    string.Empty,
                    0,
                    0);

                Check(result.Success, "M1 flow service should succeed: " + result.ErrorCode);
                CheckEqual(990L, saveData.player.tradingCurrency, "Flow service saved TradeMoney");
                CheckEqual(1L, saveData.player.developmentCurrency, "Flow service saved DevelopmentCurrency");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(item);
                UnityEngine.Object.DestroyImmediate(fromTown);
                UnityEngine.Object.DestroyImmediate(toTown);
                UnityEngine.Object.DestroyImmediate(route);
            }
        }

        private static void EconomyM1SettlementViewAdapter_ProvidesDisplayOnlyResult()
        {
            EconomyM1LoopResult result = EconomyM1LoopCalculator.Execute(new EconomyM1LoopInput
            {
                PriceInput = new PriceCalculationInput
                {
                    TradeItemId = "apple",
                    FromTownId = "town_start",
                    ToTownId = "town_trade_01",
                    RouteId = "route_01",
                    Quantity = 1,
                    BaseBuyPrice = 100,
                    BaseSellPrice = 140
                },
                CurrencyState = new CurrencyState
                {
                    TradeMoney = 1000,
                    DevelopmentCurrency = 0
                },
                TradeId = "ui_view_test",
                FoodCost = 50,
                DevelopmentCurrencyReward = 1
            });

            EconomyM1SettlementViewData viewData = EconomyM1SettlementViewAdapter.Create(result);

            Check(viewData.Success, "Settlement view data should keep success result.");
            CheckEqual(result.PriceResult, viewData.PriceResult, "View PriceResult reference");
            CheckEqual(result.Settlement, viewData.Settlement, "View Settlement reference");
            CheckEqual(result.Settlement.Entries, viewData.Settlement.Entries, "View Settlement entries reference");
            CheckEqual(-10L, viewData.Settlement.NetProfit, "View NetProfit");
        }

        private static void EconomyM1TestContentAssets_ExecuteM1Flow()
        {
            const string ItemPath = "Assets/_Project/03.Economy/07_TestContent/EconomyM1_Apple.asset";
            const string RoutePath = "Assets/_Project/03.Economy/07_TestContent/EconomyM1_Route.asset";

            global::TradeItemData item = UnityEditor.AssetDatabase.LoadAssetAtPath<global::TradeItemData>(ItemPath);
            global::RouteData route = UnityEditor.AssetDatabase.LoadAssetAtPath<global::RouteData>(RoutePath);

            Check(item != null, "M1 test TradeItemData asset is missing: " + ItemPath);
            Check(route != null, "M1 test RouteData asset is missing: " + RoutePath);
            CheckEqual(100L, item.BaseBuyPrice, "M1 test asset BaseBuyPrice");
            CheckEqual(140L, item.BaseSellPrice, "M1 test asset BaseSellPrice");
            CheckEqual(50L, route.BaseFoodCost, "M1 test asset BaseFoodCost");
            CheckEqual(0L, route.BaseMercenaryCost, "M1 test asset BaseMercenaryCost");

            global::SaveData saveData = new global::SaveData();
            saveData.player.tradingCurrency = 1000L;

            EconomyM1LoopResult result = EconomyM1FlowService.ExecuteTradeAndApply(
                saveData,
                item,
                route,
                1,
                "m1_content_asset_test",
                1L,
                false,
                string.Empty,
                0,
                0);

            Check(result.Success, "M1 test content flow should succeed: " + result.ErrorCode);
            CheckEqual(100L, result.PriceResult.TotalBuyPrice, "M1 content TotalBuyPrice");
            CheckEqual(140L, result.PriceResult.TotalSellPrice, "M1 content TotalSellPrice");
            CheckEqual(-10L, result.Settlement.NetProfit, "M1 content NetProfit");
            CheckEqual(990L, saveData.player.tradingCurrency, "M1 content saved TradeMoney");
            CheckEqual(1L, saveData.player.developmentCurrency, "M1 content saved DevelopmentCurrency");
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

        private static void CheckEqual<T>(T expected, T actual, string label)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException(label + " expected " + expected + " but was " + actual + ".");
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field == null)
            {
                throw new InvalidOperationException("Missing test field: " + fieldName);
            }

            field.SetValue(target, value);
        }
    }
}
