using System;
using System.Collections.Generic;
using System.Reflection;
using ND.Framework;
using ND.Framework.CargoLoading;
using ND.UI.Market;
using NUnit.Framework;
using UnityEngine;
using FrameworkSaveData = ND.Framework.SaveData;

namespace ND.Economy.Editor.Tests
{
    /// <summary>
    /// Verifies contextual modifier selection while leaving arithmetic to PriceCalculator.
    /// </summary>
    public sealed class ContextualSellPriceTests
    {
        private TradeItemData item;
        private SellPriceModifierPolicy policy;

        [SetUp]
        public void SetUp()
        {
            item = CreateItem();
            policy = ScriptableObject.CreateInstance<SellPriceModifierPolicy>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(policy);
        }

        [Test]
        public void ExistingItemSeason_MatchingAppliesAndNonMatchingIsExcluded()
        {
            SetItemModifiers(Season(GameCalendarDate.SummerId, 0.2f));

            Assert.That(Calculate(GameCalendarDate.SummerId, 0f, false).UnitSellPrice, Is.EqualTo(240L));
            Assert.That(Calculate(GameCalendarDate.WinterId, 0f, false).UnitSellPrice, Is.EqualTo(200L));
        }

        [Test]
        public void MarketPreview_UsesSavedCurrentSeasonLikeCommit()
        {
            SetItemModifiers(Season(GameCalendarDate.SummerId, 0.2f));
            var saveData = new FrameworkSaveData();
            saveData.world.currentSeasonId = GameCalendarDate.SummerId;

            Assert.That(MarketInventoryMutationSession.TryOpen(
                saveData,
                new MemorySaveService(),
                new FixedTimeProvider(),
                "contextual-preview-market",
                new[] { item },
                1,
                1,
                1d,
                1,
                out MarketInventoryMutationSession session,
                out string error), Is.True, error);

            var model = new MarketTradePanelModel(session, 100f);

            Assert.That(model.Items, Has.Count.EqualTo(1));
            Assert.That(model.Items[0].SellUnitPrice, Is.EqualTo(240L));
        }

        [Test]
        public void MarketSession_PolicyContextAlignsPreviewAndCommit_WithNullFallback()
        {
            const string marketId = "contextual-session-market";
            string tradeId = "contextual-session-" + Guid.NewGuid().ToString("N");
            SetPolicyRules(
                new[] { CategoryRule("winter-food", TradeItemCategory.Food, GameCalendarDate.WinterId, 0.2f) },
                LuckyRule(true, 0.5f),
                new[] { DistanceRule("distance-300-600", 300f, true, 600f, 0.1f) });

            var saveData = new FrameworkSaveData();
            saveData.world.currentSeasonId = GameCalendarDate.WinterId;
            saveData.player.tradingCurrency = 1000L;
            saveData.caravan.currentDistanceKm = 350f;
            saveData.tradeProgress = new ND.Framework.TradeProgressSaveData
            {
                caravanId = saveData.selectedCaravanId,
                activeTradeId = tradeId,
                state = ND.Framework.TradeProgressState.Selling
            };
            saveData.caravan.cargo.Add(new CargoEntrySaveData
            {
                quantity = 3,
                item = new TradeItemSaveData
                {
                    itemId = "contextual-item",
                    basePrice = 200L,
                    weight = 1f,
                    maxCount = 10
                }
            });

            WeatherLuckyStore.Add(tradeId);
            try
            {
                Assert.That(MarketInventoryMutationSession.TryOpen(
                    saveData,
                    saveData.selectedCaravanId,
                    MarketTradeMode.BuyAndSell,
                    new MemorySaveService(),
                    new FixedTimeProvider(),
                    marketId,
                    new[] { item },
                    new[] { item },
                    1,
                    1,
                    1,
                    1d,
                    1,
                    policy,
                    new[] { "contextual-item" },
                    out MarketInventoryMutationSession session,
                    out string error), Is.True, error);

                var model = new MarketTradePanelModel(session, 100f);
                long expectedUnitSellPrice = ContextualSellPriceCalculator.CalculateUnitPrices(
                    item,
                    new SellPriceCalculationContext(
                        GameCalendarDate.WinterId,
                        350f,
                        true,
                        new[] { "contextual-item" }),
                    policy).UnitSellPrice;
                Assert.That(model.Items[0].SellUnitPrice, Is.EqualTo(expectedUnitSellPrice));

                ND.Framework.CargoLoading.MarketTransactionResult committed = MarketTransactionCommand.Execute(
                    session,
                    new[] { new MarketTransactionLine { ItemId = "contextual-item", SellQuantity = 3 } },
                    100f);
                Assert.That(committed.Success, Is.True, committed.ErrorCode);
                Assert.That(committed.SaleRevenue, Is.EqualTo(expectedUnitSellPrice * 3L));

                var nullPolicySave = new FrameworkSaveData();
                nullPolicySave.world.currentSeasonId = GameCalendarDate.WinterId;
                Assert.That(MarketInventoryMutationSession.TryOpen(
                    nullPolicySave,
                    new MemorySaveService(),
                    new FixedTimeProvider(),
                    marketId + "-null",
                    new[] { item },
                    1,
                    1,
                    1d,
                    2,
                    out MarketInventoryMutationSession nullPolicySession,
                    out string nullPolicyError), Is.True, nullPolicyError);
                Assert.That(
                    new MarketTradePanelModel(nullPolicySession, 100f).Items[0].SellUnitPrice,
                    Is.EqualTo(200L));
            }
            finally
            {
                WeatherLuckyStore.Consume(tradeId);
            }
        }

        [Test]
        public void CategorySeasonalRule_AppliesForMatchingCategoryAndSeason()
        {
            SetPolicyRules(
                new[] { CategoryRule("winter-food", TradeItemCategory.Food, GameCalendarDate.WinterId, 0.2f) },
                LuckyRule(false, 0.5f),
                new DistanceSellPriceRule[0]);

            Assert.That(Calculate(GameCalendarDate.WinterId, 0f, false).UnitSellPrice, Is.EqualTo(240L));
            Assert.That(Calculate(GameCalendarDate.SummerId, 0f, false).UnitSellPrice, Is.EqualTo(200L));
        }

        [Test]
        public void LuckyMoneyRule_AppliesOnlyWhenActive()
        {
            SetPolicyRules(
                new CategorySeasonalSellPriceRule[0],
                LuckyRule(true, 0.5f),
                new DistanceSellPriceRule[0]);

            Assert.That(Calculate(string.Empty, 0f, true).UnitSellPrice, Is.EqualTo(300L));
            Assert.That(Calculate(string.Empty, 0f, false).UnitSellPrice, Is.EqualTo(200L));
        }

        [TestCase(99.999f, 200L)]
        [TestCase(100f, 210L)]
        [TestCase(299.999f, 210L)]
        [TestCase(300f, 220L)]
        [TestCase(599.999f, 220L)]
        [TestCase(600f, 230L)]
        public void DistanceRules_UseInclusiveMinimumExclusiveMaximum(float distanceKm, long expected)
        {
            SetPolicyRules(
                new CategorySeasonalSellPriceRule[0],
                LuckyRule(false, 0.5f),
                DefaultDistanceRules());

            Assert.That(Calculate(string.Empty, distanceKm, false).UnitSellPrice, Is.EqualTo(expected));
        }

        [Test]
        public void AllEffectsComposeWithoutChangingBuyPrice()
        {
            SetItemModifiers(Season(GameCalendarDate.WinterId, 0.1f));
            SetPolicyRules(
                new[] { CategoryRule("winter-food", TradeItemCategory.Food, GameCalendarDate.WinterId, 0.2f) },
                LuckyRule(true, 0.5f),
                new[] { DistanceRule("distance-600", 600f, false, 0f, 0.15f) });

            PriceCalculationResult result = Calculate(GameCalendarDate.WinterId, 600f, true);

            Assert.That(result.UnitBuyPrice, Is.EqualTo(100L));
            Assert.That(result.UnitSellPrice, Is.EqualTo(455L));
        }

        [Test]
        public void DestinationLocalSpecialty_ExcludesSeasonAndDistanceButKeepsLuckyMoney()
        {
            SetItemModifiers(
                Season(GameCalendarDate.WinterId, 0.1f),
                ItemModifier(ModifierType.AffectToTown, "town-effect", 0.1f));
            SetPolicyRules(
                new[] { CategoryRule("winter-food", TradeItemCategory.Food, GameCalendarDate.WinterId, 0.2f) },
                LuckyRule(true, 0.5f),
                new[] { DistanceRule("distance-600", 600f, false, 0f, 0.15f) });

            PriceCalculationResult result = Calculate(
                GameCalendarDate.WinterId,
                600f,
                true,
                new[] { "contextual-item" });

            Assert.That(result.UnitBuyPrice, Is.EqualTo(100L));
            Assert.That(result.UnitSellPrice, Is.EqualTo(330L));
        }

        [Test]
        public void DestinationLocalSpecialty_WithoutLuckyMoneyUsesBaseSellPrice()
        {
            SetItemModifiers(Season(GameCalendarDate.WinterId, 0.1f));
            SetPolicyRules(
                new[] { CategoryRule("winter-food", TradeItemCategory.Food, GameCalendarDate.WinterId, 0.2f) },
                LuckyRule(true, 0.5f),
                new[] { DistanceRule("distance-600", 600f, false, 0f, 0.15f) });

            Assert.That(
                Calculate(
                    GameCalendarDate.WinterId,
                    600f,
                    false,
                    new[] { "contextual-item" }).UnitSellPrice,
                Is.EqualTo(200L));
        }

        [TestCaseSource(nameof(NonMatchingSpecialtyLists))]
        public void NonMatchingOrEmptySpecialtyList_PreservesExistingComposition(string[] specialtyItemIds)
        {
            SetItemModifiers(Season(GameCalendarDate.WinterId, 0.1f));
            SetPolicyRules(
                new[] { CategoryRule("winter-food", TradeItemCategory.Food, GameCalendarDate.WinterId, 0.2f) },
                LuckyRule(true, 0.5f),
                new[] { DistanceRule("distance-600", 600f, false, 0f, 0.15f) });

            Assert.That(
                Calculate(GameCalendarDate.WinterId, 600f, true, specialtyItemIds).UnitSellPrice,
                Is.EqualTo(455L));
        }

        [Test]
        public void Context_CopiesDestinationLocalSpecialtyItemIds()
        {
            var source = new[] { "contextual-item" };
            var context = new SellPriceCalculationContext(string.Empty, 0f, false, source);

            source[0] = "changed-item";

            Assert.That(context.DestinationLocalSpecialtyItemIds, Is.EqualTo(new[] { "contextual-item" }));
        }

        [TestCase(false, 455L)]
        [TestCase(true, 300L)]
        public void ItemMissingFromDestinationTradeItems_RemainsSellable(
            bool isDestinationLocalSpecialty,
            long expectedUnitSellPrice)
        {
            string tradeId = "missing-trade-item-" + Guid.NewGuid().ToString("N");
            TradeItemData stockItem = CreateItem();
            SetField(stockItem, "itemId", "stock-only-item");
            SetItemModifiers(Season(GameCalendarDate.WinterId, 0.1f));
            SetPolicyRules(
                new[] { CategoryRule("winter-food", TradeItemCategory.Food, GameCalendarDate.WinterId, 0.2f) },
                LuckyRule(true, 0.5f),
                new[] { DistanceRule("distance-600", 600f, false, 0f, 0.15f) });

            var saveData = new FrameworkSaveData();
            saveData.world.currentSeasonId = GameCalendarDate.WinterId;
            saveData.caravan.currentDistanceKm = 600f;
            saveData.tradeProgress = new ND.Framework.TradeProgressSaveData
            {
                caravanId = saveData.selectedCaravanId,
                activeTradeId = tradeId,
                state = ND.Framework.TradeProgressState.Selling
            };
            saveData.caravan.cargo.Add(new CargoEntrySaveData
            {
                quantity = 1,
                item = new TradeItemSaveData
                {
                    itemId = "contextual-item",
                    basePrice = 200L,
                    weight = 1f,
                    maxCount = 10
                }
            });

            WeatherLuckyStore.Add(tradeId);
            try
            {
                Assert.That(MarketInventoryMutationSession.TryOpen(
                    saveData,
                    saveData.selectedCaravanId,
                    MarketTradeMode.BuyAndSell,
                    new MemorySaveService(),
                    new FixedTimeProvider(),
                    "missing-trade-item-market",
                    new[] { stockItem },
                    new[] { stockItem, item },
                    1,
                    1,
                    1,
                    1d,
                    1,
                    policy,
                    isDestinationLocalSpecialty
                        ? new[] { "contextual-item" }
                        : Array.Empty<string>(),
                    out MarketInventoryMutationSession session,
                    out string error), Is.True, error);

                Assert.That(
                    session.ResolvePreviewUnitPrices(item).UnitSellPrice,
                    Is.EqualTo(expectedUnitSellPrice));
                ND.Framework.CargoLoading.MarketTransactionResult committed =
                    MarketTransactionCommand.Execute(
                    session,
                    new[] { new MarketTransactionLine { ItemId = "contextual-item", SellQuantity = 1 } },
                    100f);
                Assert.That(committed.Success, Is.True, committed.ErrorCode);
                Assert.That(committed.SaleRevenue, Is.EqualTo(expectedUnitSellPrice));
            }
            finally
            {
                WeatherLuckyStore.Consume(tradeId);
                UnityEngine.Object.DestroyImmediate(stockItem);
            }
        }

        [Test]
        public void DuplicateSourceIdentity_IsAppliedOnce()
        {
            SetPolicyRules(
                new[]
                {
                    CategoryRule("duplicate", TradeItemCategory.Food, GameCalendarDate.WinterId, 0.2f),
                    CategoryRule("duplicate", TradeItemCategory.Food, GameCalendarDate.WinterId, 0.2f)
                },
                LuckyRule(false, 0.5f),
                new DistanceSellPriceRule[0]);

            Assert.That(Calculate(GameCalendarDate.WinterId, 0f, false).UnitSellPrice, Is.EqualTo(240L));
        }

        [Test]
        public void OverlappingDistanceRules_SelectOrdinalFirstRuleOnly()
        {
            SetPolicyRules(
                new CategorySeasonalSellPriceRule[0],
                LuckyRule(false, 0.5f),
                new[]
                {
                    DistanceRule("z-rule", 100f, true, 400f, 0.5f),
                    DistanceRule("a-rule", 100f, true, 400f, 0.1f)
                });

            Assert.That(Calculate(string.Empty, 200f, false).UnitSellPrice, Is.EqualTo(220L));
        }

        private static IEnumerable<string[]> NonMatchingSpecialtyLists()
        {
            yield return null;
            yield return Array.Empty<string>();
            yield return new[] { "other-item" };
        }

        private PriceCalculationResult Calculate(
            string seasonId,
            float distanceKm,
            bool lucky,
            IReadOnlyList<string> destinationLocalSpecialtyItemIds = null)
        {
            return ContextualSellPriceCalculator.CalculateUnitPrices(
                item,
                new SellPriceCalculationContext(
                    seasonId,
                    distanceKm,
                    lucky,
                    destinationLocalSpecialtyItemIds),
                policy);
        }

        private static TradeItemData CreateItem()
        {
            TradeItemData created = ScriptableObject.CreateInstance<TradeItemData>();
            SetField(created, "itemId", "contextual-item");
            SetField(created, "category", TradeItemCategory.Food);
            SetField(created, "baseBuyPrice", 100L);
            SetField(created, "baseSellPrice", 200L);
            SetField(created, "maxCount", 10);
            SetField(created, "affectModify", true);
            return created;
        }

        private void SetItemModifiers(params ModifierInput[] modifiers)
        {
            SetField(item, "modifiers", modifiers);
        }

        private void SetPolicyRules(
            CategorySeasonalSellPriceRule[] categoryRules,
            LuckyMoneySellPriceRule luckyRule,
            DistanceSellPriceRule[] distanceRules)
        {
            SetField(policy, "categorySeasonalRules", new List<CategorySeasonalSellPriceRule>(categoryRules));
            SetField(policy, "luckyMoneyRule", luckyRule);
            SetField(policy, "distanceRules", new List<DistanceSellPriceRule>(distanceRules));
        }

        private static ModifierInput Season(string seasonId, float value)
        {
            return ItemModifier(ModifierType.Season, seasonId, value);
        }

        private static ModifierInput ItemModifier(ModifierType modifierType, string sourceId, float value)
        {
            return new ModifierInput
            {
                modifierType = modifierType,
                sourceId = sourceId,
                modifierBundles = new[]
                {
                    new ModifierBundle
                    {
                        modifierTarget = Target.SellPrice,
                        modifierOperation = Operation.Percent,
                        value = value
                    }
                }
            };
        }

        private static CategorySeasonalSellPriceRule CategoryRule(
            string ruleId,
            TradeItemCategory category,
            string seasonId,
            float value)
        {
            var rule = new CategorySeasonalSellPriceRule();
            SetField(rule, "ruleId", ruleId);
            SetField(rule, "enabled", true);
            SetField(rule, "category", category);
            SetField(rule, "seasonId", seasonId);
            SetField(rule, "operation", PriceModifierOperation.Percent);
            SetField(rule, "value", value);
            SetField(rule, "modifierType", PriceModifierType.Season);
            return rule;
        }

        private static LuckyMoneySellPriceRule LuckyRule(bool enabled, float value)
        {
            var rule = new LuckyMoneySellPriceRule();
            SetField(rule, "effectId", "lucky_money_default");
            SetField(rule, "enabled", enabled);
            SetField(rule, "operation", PriceModifierOperation.Percent);
            SetField(rule, "value", value);
            SetField(rule, "modifierType", PriceModifierType.RouteEvent);
            return rule;
        }

        private static DistanceSellPriceRule[] DefaultDistanceRules()
        {
            return new[]
            {
                DistanceRule("distance-0-100", 0f, true, 100f, 0f),
                DistanceRule("distance-100-300", 100f, true, 300f, 0.05f),
                DistanceRule("distance-300-600", 300f, true, 600f, 0.1f),
                DistanceRule("distance-600-plus", 600f, false, 0f, 0.15f)
            };
        }

        private static DistanceSellPriceRule DistanceRule(
            string ruleId,
            float minimum,
            bool hasMaximum,
            float maximum,
            float value)
        {
            var rule = new DistanceSellPriceRule();
            SetField(rule, "ruleId", ruleId);
            SetField(rule, "enabled", true);
            SetField(rule, "minimumDistanceKm", minimum);
            SetField(rule, "hasMaximumDistance", hasMaximum);
            SetField(rule, "maximumDistanceKm", maximum);
            SetField(rule, "operation", PriceModifierOperation.Percent);
            SetField(rule, "value", value);
            SetField(rule, "modifierType", PriceModifierType.RouteEvent);
            return rule;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private sealed class FixedTimeProvider : IGameTimeProvider
        {
            public DateTime CurrentUtc => DateTime.UnixEpoch;
        }

        private sealed class MemorySaveService : ISaveService
        {
            public bool HasSaveData() => false;
            public FrameworkSaveData CreateNewGameData() => new FrameworkSaveData();
            public FrameworkSaveData Load() => null;
            public SaveResult Save(FrameworkSaveData data) => SaveResult.Success();
            public void ResetSaveData()
            {
            }
        }
    }
}
