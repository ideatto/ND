/*
 * Script Purpose
 * - 시장 판매 commit 시점 계절 SellPrice modifier 선택·적용과
 *   MarketTransactionCommand 경계의 currency/cargo/stock/staging rollback을 검증한다.
 *
 * Technical Ownership
 * - Responsible Area: Economy / Market
 *
 * Related Documentation
 * - Docs/Contract/Arrival_Sale_Settlement_Claim_Policy.md
 */
using System;
using System.Collections.Generic;
using System.Reflection;
using ND.Framework;
using ND.Framework.CargoLoading;
using NUnit.Framework;
using UnityEngine;
using FrameworkSaveData = ND.Framework.SaveData;

namespace ND.Economy.Editor.Tests
{
    /// <summary>
    /// 계절 SellPrice 선택기와 실제 시장 판매 transaction 경계를 검증한다.
    /// </summary>
    public sealed class SeasonalSellPriceTests
    {
        [Test]
        public void MatchingSeason_AppliesSellModifierOnly()
        {
            PriceCalculationResult result = Calculate(GameCalendarDate.SummerId,
                Season(GameCalendarDate.SummerId, PriceModifierTarget.SellPrice, PriceModifierOperation.Percent, 0.2f));

            Assert.That(result.UnitBuyPrice, Is.EqualTo(100L));
            Assert.That(result.UnitSellPrice, Is.EqualTo(240L));
        }

        [Test]
        public void NonMatchingSeason_DoesNotChangeSellPrice()
        {
            PriceCalculationResult result = Calculate(GameCalendarDate.SummerId,
                Season(GameCalendarDate.WinterId, PriceModifierTarget.SellPrice, PriceModifierOperation.Percent, 0.5f));

            Assert.That(result.UnitSellPrice, Is.EqualTo(200L));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        [TestCase("Summer")]
        [TestCase("SUMMER")]
        [TestCase("season_summer")]
        [TestCase("fall")]
        [TestCase("unknown")]
        [TestCase("여름")]
        public void InvalidSeasonSourceId_IsExcludedWithoutThrowing(string sourceId)
        {
            PriceCalculationResult result = null;
            Assert.DoesNotThrow(() => result = Calculate(GameCalendarDate.SummerId,
                Season(sourceId, PriceModifierTarget.SellPrice, PriceModifierOperation.Percent, 0.2f)));
            Assert.That(result.UnitSellPrice, Is.EqualTo(200L));
        }

        [Test]
        public void DisplayName_DoesNotParticipateInSeasonMatching()
        {
            PriceModifierInput modifier = Season(
                GameCalendarDate.WinterId,
                PriceModifierTarget.SellPrice,
                PriceModifierOperation.Percent,
                0.2f);
            modifier.DisplayNameKey = GameCalendarDate.SummerId;

            Assert.That(Calculate(GameCalendarDate.SummerId, modifier).UnitSellPrice, Is.EqualTo(200L));
        }

        [Test]
        public void BuyPriceOnlySeasonModifier_DoesNotAffectSellPrice()
        {
            PriceCalculationResult result = Calculate(GameCalendarDate.SummerId,
                Season(GameCalendarDate.SummerId, PriceModifierTarget.BuyPrice, PriceModifierOperation.Percent, 0.2f));

            Assert.That(result.UnitSellPrice, Is.EqualTo(200L));
        }

        [Test]
        public void NonSeasonModifier_PassesThroughUnchanged()
        {
            var modifier = new PriceModifierInput
            {
                ModifierType = PriceModifierType.Town,
                Target = PriceModifierTarget.SellPrice,
                Operation = PriceModifierOperation.Add,
                Value = 10f
            };

            Assert.That(Calculate(GameCalendarDate.SummerId, modifier).UnitSellPrice, Is.EqualTo(210L));
        }

        [Test]
        public void MultipleAndMixedSeasonModifiers_PreserveCalculatorOrderingForMatches()
        {
            PriceCalculationResult result = Calculate(
                GameCalendarDate.SummerId,
                Season(GameCalendarDate.SummerId, PriceModifierTarget.SellPrice, PriceModifierOperation.Percent, 0.2f),
                Season(GameCalendarDate.WinterId, PriceModifierTarget.SellPrice, PriceModifierOperation.Percent, 0.5f),
                Season(GameCalendarDate.SummerId, PriceModifierTarget.SellPrice, PriceModifierOperation.Add, 10f));

            Assert.That(result.UnitSellPrice, Is.EqualTo(250L));
        }

        [Test]
        public void MarketCommit_NonMatchingSeason_KeepsBaseSellRevenue()
        {
            TradeItemData item = CreateTradeItem();
            try
            {
                var saveData = new FrameworkSaveData();
                saveData.world.currentSeasonId = GameCalendarDate.WinterId;
                saveData.player.tradingCurrency = 1000L;
                saveData.caravan.cargo.Add(new CargoEntrySaveData
                {
                    quantity = 3,
                    item = new TradeItemSaveData
                    {
                        itemId = "seasonal-item",
                        basePrice = 200L,
                        weight = 1f,
                        maxCount = 10
                    }
                });

                Assert.That(MarketInventoryMutationSession.TryOpen(
                    saveData, new MemorySaveService(), new FixedTimeProvider(), "season-nonmatch-market",
                    new[] { item }, 1, 1, 1d, 1, out MarketInventoryMutationSession session,
                    out string error), Is.True, error);

                ND.Framework.CargoLoading.MarketTransactionResult result = MarketTransactionCommand.Execute(
                    session,
                    new[] { new MarketTransactionLine { ItemId = "seasonal-item", SellQuantity = 3 } },
                    100f);

                Assert.That(result.Success, Is.True, result.ErrorCode);
                Assert.That(result.SaleRevenue, Is.EqualTo(600L));
                Assert.That(saveData.player.tradingCurrency, Is.EqualTo(1600L));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void MarketCommit_UsesOneSeasonalSellPriceForRevenueAndMutations()
        {
            TradeItemData item = CreateTradeItem();
            try
            {
                var saveData = new FrameworkSaveData();
                saveData.world.currentSeasonId = GameCalendarDate.SummerId;
                saveData.player.tradingCurrency = 1000L;
                saveData.caravan.cargo.Add(new CargoEntrySaveData
                {
                    quantity = 3,
                    item = new TradeItemSaveData
                    {
                        itemId = "seasonal-item",
                        basePrice = 200L,
                        weight = 1f,
                        maxCount = 10
                    }
                });
                var saveService = new MemorySaveService();

                Assert.That(MarketInventoryMutationSession.TryOpen(
                    saveData, saveService, new FixedTimeProvider(), "season-test-market",
                    new[] { item }, 1, 1, 1d, 1, out MarketInventoryMutationSession session,
                    out string error), Is.True, error);

                int stockBefore = GetStockQuantity(saveData, "season-test-market", "seasonal-item");
                long stagedRevenue = 0L;
                ND.Framework.CargoLoading.MarketTransactionResult result = MarketTransactionCommand.Execute(
                    session,
                    new[] { new MarketTransactionLine { ItemId = "seasonal-item", SellQuantity = 3 } },
                    100f,
                    stageBeforeSave: staged =>
                    {
                        stagedRevenue = staged.SaleRevenue;
                        return true;
                    });

                Assert.That(result.Success, Is.True, result.ErrorCode);
                Assert.That(result.Items[0].SaleRevenue, Is.EqualTo(720L));
                Assert.That(result.SaleRevenue, Is.EqualTo(720L));
                Assert.That(stagedRevenue, Is.EqualTo(720L));
                Assert.That(saveData.player.tradingCurrency, Is.EqualTo(1720L));
                Assert.That(saveData.caravan.cargo, Is.Empty);
                Assert.That(
                    GetStockQuantity(saveData, "season-test-market", "seasonal-item") - stockBefore,
                    Is.EqualTo(3));
                Assert.That(saveService.SavedData, Is.SameAs(saveData));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void MarketCommit_SaveFailure_RollsBackCurrencyCargoStockAndStaging()
        {
            TradeItemData item = CreateTradeItem();
            try
            {
                var saveData = new FrameworkSaveData();
                saveData.world.currentSeasonId = GameCalendarDate.SummerId;
                saveData.player.tradingCurrency = 1000L;
                saveData.caravan.cargo.Add(new CargoEntrySaveData
                {
                    quantity = 3,
                    item = new TradeItemSaveData
                    {
                        itemId = "seasonal-item",
                        basePrice = 200L,
                        weight = 1f,
                        maxCount = 10
                    }
                });
                var saveService = new FailingSaveService();

                Assert.That(MarketInventoryMutationSession.TryOpen(
                    saveData, new MemorySaveService(), new FixedTimeProvider(), "season-fail-market",
                    new[] { item }, 1, 1, 1d, 1, out _, out string openError), Is.True, openError);

                // Re-open with failing save after inventory exists so open itself does not fail.
                Assert.That(MarketInventoryMutationSession.TryOpen(
                    saveData, saveService, new FixedTimeProvider(), "season-fail-market",
                    new[] { item }, 1, 1, 1d, 1, out MarketInventoryMutationSession session,
                    out string error), Is.True, error);

                long currencyBefore = saveData.player.tradingCurrency;
                int cargoQtyBefore = saveData.caravan.cargo[0].quantity;
                int stockBefore = GetStockQuantity(saveData, "season-fail-market", "seasonal-item");
                long arrivalBefore = 50L;
                var soldBefore = new List<SettlementItemSaveData>
                {
                    new SettlementItemSaveData
                    {
                        itemId = "keep",
                        quantity = 1,
                        unitPrice = 1L,
                        totalAmount = 1L
                    }
                };
                long arrivalRuntime = arrivalBefore;
                List<SettlementItemSaveData> soldRuntime = soldBefore;
                bool cargoEvent = false;
                bool currencyEvent = false;
                void OnCargo(string _) => cargoEvent = true;
                void OnCurrency(long _) => currencyEvent = true;
                FrameworkEvents.CaravanCargoChanged += OnCargo;
                FrameworkEvents.TradingCurrencyChanged += OnCurrency;

                ND.Framework.CargoLoading.MarketTransactionResult result = MarketTransactionCommand.Execute(
                    session,
                    new[] { new MarketTransactionLine { ItemId = "seasonal-item", SellQuantity = 3 } },
                    100f,
                    stageBeforeSave: staged =>
                    {
                        arrivalRuntime = staged.SaleRevenue;
                        soldRuntime = new List<SettlementItemSaveData>
                        {
                            new SettlementItemSaveData
                            {
                                itemId = staged.Items[0].ItemId,
                                quantity = staged.Items[0].SellQuantity,
                                unitPrice = staged.Items[0].SaleRevenue / staged.Items[0].SellQuantity,
                                totalAmount = staged.Items[0].SaleRevenue
                            }
                        };
                        return true;
                    },
                    rollbackStagedData: () =>
                    {
                        arrivalRuntime = arrivalBefore;
                        soldRuntime = soldBefore;
                    });

                FrameworkEvents.CaravanCargoChanged -= OnCargo;
                FrameworkEvents.TradingCurrencyChanged -= OnCurrency;

                Assert.That(result.Success, Is.False);
                Assert.That(result.ErrorCode, Is.EqualTo(MarketInventoryMutationSession.ErrorSaveFailed));
                Assert.That(saveData.player.tradingCurrency, Is.EqualTo(currencyBefore));
                Assert.That(saveData.caravan.cargo[0].quantity, Is.EqualTo(cargoQtyBefore));
                Assert.That(
                    GetStockQuantity(saveData, "season-fail-market", "seasonal-item"),
                    Is.EqualTo(stockBefore));
                Assert.That(arrivalRuntime, Is.EqualTo(arrivalBefore));
                Assert.That(soldRuntime, Is.SameAs(soldBefore));
                Assert.That(cargoEvent, Is.False);
                Assert.That(currencyEvent, Is.False);
                Assert.That(saveService.SaveCalls, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void MarketCommit_TwoSellLines_ReuseCapturedSeasonIdForEveryItem()
        {
            TradeItemData itemA = CreateTradeItem("seasonal-a");
            TradeItemData itemB = CreateTradeItem("seasonal-b");
            try
            {
                var saveData = new FrameworkSaveData();
                saveData.world.currentSeasonId = GameCalendarDate.SummerId;
                saveData.player.tradingCurrency = 1000L;
                saveData.caravan.cargo.Add(new CargoEntrySaveData
                {
                    quantity = 1,
                    item = new TradeItemSaveData
                    {
                        itemId = "seasonal-a",
                        basePrice = 200L,
                        weight = 1f,
                        maxCount = 10
                    }
                });
                saveData.caravan.cargo.Add(new CargoEntrySaveData
                {
                    quantity = 1,
                    item = new TradeItemSaveData
                    {
                        itemId = "seasonal-b",
                        basePrice = 200L,
                        weight = 1f,
                        maxCount = 10
                    }
                });

                Assert.That(MarketInventoryMutationSession.TryOpen(
                    saveData, new MemorySaveService(), new FixedTimeProvider(), "season-multi-market",
                    new[] { itemA, itemB }, 1, 1, 1d, 1, out MarketInventoryMutationSession session,
                    out string error), Is.True, error);

                // Production captures saveData.world.currentSeasonId once into transactionSeasonId
                // before pricing any line, then reuses that local for every item. This fixture proves
                // both sell lines share the same commit-time summer price; mid-loop calendar mutation
                // is covered by that single-capture source contract.
                ND.Framework.CargoLoading.MarketTransactionResult result = MarketTransactionCommand.Execute(
                    session,
                    new[]
                    {
                        new MarketTransactionLine { ItemId = "seasonal-a", SellQuantity = 1 },
                        new MarketTransactionLine { ItemId = "seasonal-b", SellQuantity = 1 }
                    },
                    100f);

                Assert.That(result.Success, Is.True, result.ErrorCode);
                Assert.That(result.SaleRevenue, Is.EqualTo(480L));
                Assert.That(result.Items[0].SaleRevenue, Is.EqualTo(240L));
                Assert.That(result.Items[1].SaleRevenue, Is.EqualTo(240L));
                Assert.That(saveData.player.tradingCurrency, Is.EqualTo(1480L));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(itemA);
                UnityEngine.Object.DestroyImmediate(itemB);
            }
        }

        private static PriceCalculationResult Calculate(string seasonId, params PriceModifierInput[] modifiers)
        {
            List<PriceModifierInput> selected = SeasonalSellPriceModifierSelector.SelectForSellPrice(
                modifiers,
                seasonId);
            return PriceCalculator.CalculateUnitPrices(100L, 200L, selected);
        }

        private static PriceModifierInput Season(
            string sourceId,
            PriceModifierTarget target,
            PriceModifierOperation operation,
            float value)
        {
            return new PriceModifierInput
            {
                ModifierType = PriceModifierType.Season,
                SourceId = sourceId,
                Target = target,
                Operation = operation,
                Value = value
            };
        }

        private static TradeItemData CreateTradeItem()
        {
            return CreateTradeItem("seasonal-item");
        }

        private static TradeItemData CreateTradeItem(string itemId)
        {
            TradeItemData item = ScriptableObject.CreateInstance<TradeItemData>();
            SetField(item, "itemId", itemId);
            SetField(item, "baseBuyPrice", 100L);
            SetField(item, "baseSellPrice", 200L);
            SetField(item, "weight", 1f);
            SetField(item, "maxCount", 10);
            SetField(item, "affectModify", true);
            SetField(item, "modifiers", new[]
            {
                new ModifierInput
                {
                    modifierType = ModifierType.Season,
                    sourceId = GameCalendarDate.SummerId,
                    modifierBundles = new[]
                    {
                        new ModifierBundle
                        {
                            modifierTarget = Target.SellPrice,
                            modifierOperation = Operation.Percent,
                            value = 0.2f
                        }
                    }
                }
            });
            return item;
        }

        private static int GetStockQuantity(FrameworkSaveData saveData, string marketId, string itemId)
        {
            MarketInventorySaveData inventory = saveData.world.marketInventories.Find(
                candidate => candidate != null && candidate.marketId == marketId);
            if (inventory?.stocks == null)
                return 0;

            for (int i = 0; i < inventory.stocks.Count; i++)
            {
                MarketStockSaveData stock = inventory.stocks[i];
                if (stock != null && string.Equals(stock.itemId, itemId, StringComparison.Ordinal))
                    return stock.quantity;
            }

            return 0;
        }

        private static void SetField(object target, string name, object value)
        {
            typeof(TradeItemData).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private sealed class FixedTimeProvider : IGameTimeProvider
        {
            public DateTime CurrentUtc => DateTime.UnixEpoch;
        }

        private sealed class MemorySaveService : ISaveService
        {
            public FrameworkSaveData SavedData { get; private set; }
            public bool HasSaveData() => SavedData != null;
            public FrameworkSaveData CreateNewGameData() => new FrameworkSaveData();
            public FrameworkSaveData Load() => SavedData;
            public SaveResult Save(FrameworkSaveData data)
            {
                SavedData = data;
                return SaveResult.Success();
            }
            public void ResetSaveData() => SavedData = null;
        }

        private sealed class FailingSaveService : ISaveService
        {
            public int SaveCalls { get; private set; }
            public bool HasSaveData() => false;
            public FrameworkSaveData CreateNewGameData() => new FrameworkSaveData();
            public FrameworkSaveData Load() => null;
            public SaveResult Save(FrameworkSaveData data)
            {
                SaveCalls++;
                return SaveResult.Failure(SaveFailureReason.WriteFailed, "forced seasonal sale save failure");
            }
            public void ResetSaveData()
            {
            }
        }
    }
}
