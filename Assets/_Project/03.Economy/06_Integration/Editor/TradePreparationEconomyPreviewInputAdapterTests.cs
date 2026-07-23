using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class TradePreparationEconomyPreviewInputAdapterTests
    {
        [Test]
        public void TryExecute_MapsExistingPreparationDataThroughCalculator()
        {
            var view = new global::TradePrepareViewData
            {
                currentTradingCurrency = 1000L,
                selectedRouteId = "route-a",
                selectedWagonId = "wagon-a",
                overloadLimit = 30f,
                maxLoad = 40f,
                requiredDraftAnimalFoodQuantity = 5,
                requiredMercenaryPower = 10,
                routes = new[]
                {
                    new global::RouteViewData { routeId = "route-a", distance = 10f, estimatedTime = 5f, riskLevel = 0.2f }
                },
                wagons = new[]
                {
                    new global::WagonViewData { wagonId = "wagon-a", baseMoveSpeed = 2f, minRequireAnimals = 1 }
                },
                draftAnimals = new[]
                {
                    new global::DraftAnimalViewData { draftAnimalId = "horse", selectedAmount = 2 }
                },
                loadedItems = new[]
                {
                    new global::CargoItemViewData
                    {
                        itemId = "apple", quantity = 2, unitWeight = 10f,
                        purchaseUnitPrice = 100L, estimatedSellUnitPrice = 150L
                    },
                    new global::CargoItemViewData
                    {
                        itemId = "stover", category = global::TradeItemCategory.DraftAnimalsFood,
                        quantity = 3, unitWeight = 1f, purchaseUnitPrice = 10L
                    }
                },
                mercenaries = new[]
                {
                    new global::MercenaryViewData
                    {
                        mercenaryId = "guard", isSelected = true, baseBuyPrice = 40L, combatCapability = 5
                    }
                }
            };

            TradePreparationEconomyPreviewResult result;
            bool executed = TradePreparationEconomyPreviewFlow.TryExecute(view, out result);

            Assert.That(executed, Is.True);
            Assert.That(result.CanStart, Is.True);
            Assert.That(result.CargoPurchaseCost, Is.EqualTo(200L));
            Assert.That(result.FoodCost, Is.EqualTo(30L));
            Assert.That(result.MercenaryCost, Is.EqualTo(40L));
            Assert.That(view.totalPurchaseCost, Is.EqualTo(230L));
            Assert.That(view.totalPreparationCost, Is.EqualTo(270L));
            Assert.That(view.estimatedSellRevenue, Is.EqualTo(300L));
        }

        [Test]
        public void TryExecute_AllowsWalkingConfigurationWithoutDraftAnimal()
        {
            var view = new global::TradePrepareViewData
            {
                currentTradingCurrency = 100L,
                selectedRouteId = "route-a",
                selectedWagonId = "walk",
                overloadLimit = 10f,
                maxLoad = 10f,
                routes = new[]
                {
                    new global::RouteViewData { routeId = "route-a", distance = 1f, estimatedTime = 1f }
                },
                wagons = new[]
                {
                    new global::WagonViewData { wagonId = "walk", baseMoveSpeed = 1f, minRequireAnimals = 0 }
                },
                draftAnimals = new global::DraftAnimalViewData[0],
                loadedItems = new global::CargoItemViewData[0],
                mercenaries = new global::MercenaryViewData[0]
            };

            TradePreparationEconomyPreviewResult result;
            bool executed = TradePreparationEconomyPreviewFlow.TryExecute(view, out result);

            Assert.That(executed, Is.True);
            Assert.That(result.CanStart, Is.True);
        }

        [Test]
        public void TryCreate_RejectsMissingSelectedRouteWithoutMutation()
        {
            var view = new global::TradePrepareViewData
            {
                totalPreparationCost = 99L,
                selectedRouteId = "missing",
                routes = new global::RouteViewData[0],
                loadedItems = new global::CargoItemViewData[0],
                mercenaries = new global::MercenaryViewData[0]
            };

            TradePreparationEconomyPreviewResult result;
            bool executed = TradePreparationEconomyPreviewFlow.TryExecute(view, out result);

            Assert.That(executed, Is.False);
            Assert.That(result, Is.Null);
            Assert.That(view.totalPreparationCost, Is.EqualTo(99L));
        }
    }
}
