using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class TradePreparationEconomyPreviewViewAdapterTests
    {
        [Test]
        public void TryApply_MapsPreviewWithoutReplacingUnrelatedUiState()
        {
            var condition = new global::TradePrepareConditionResult { canStart = true };
            var target = new global::TradePrepareViewData
            {
                currentTradingCurrency = 1000L,
                selectedRouteId = "route-a",
                maxLoad = 50f,
                startCondition = condition
            };
            var preview = new TradePreparationEconomyPreviewResult
            {
                IsValid = true,
                CanStart = true,
                CargoPurchaseCost = 200L,
                FoodCost = 30L,
                MercenaryCost = 40L,
                TotalPreparationCost = 270L,
                CurrencyAfterPreparation = 730L,
                ExpectedSellRevenue = 300L,
                ExpectedNetProfit = 30L,
                CurrentLoad = 25f,
                ExpectedTravelSeconds = 5f
            };

            bool applied = TradePreparationEconomyPreviewViewAdapter.TryApply(target, preview, 10f);

            Assert.That(applied, Is.True);
            Assert.That(target.totalPurchaseCost, Is.EqualTo(230L));
            Assert.That(target.draftAnimalFoodCost, Is.EqualTo(30L));
            Assert.That(target.mercenaryCost, Is.EqualTo(40L));
            Assert.That(target.totalPreparationCost, Is.EqualTo(270L));
            Assert.That(target.estimatedCurrencyAfterPurchase, Is.EqualTo(770L));
            Assert.That(target.estimatedCurrencyAfterHire, Is.EqualTo(730L));
            Assert.That(target.estimatedSellRevenue, Is.EqualTo(300L));
            Assert.That(target.estimatedNetProfit, Is.EqualTo(30L));
            Assert.That(target.currentLoad, Is.EqualTo(25f));
            Assert.That(target.finalExpectedTravelTime, Is.EqualTo(5f));
            Assert.That(target.selectedMoveSpeed, Is.EqualTo(2f));
            Assert.That(target.selectedRouteId, Is.EqualTo("route-a"));
            Assert.That(target.maxLoad, Is.EqualTo(50f));
            Assert.That(target.startCondition, Is.SameAs(condition));
        }

        [Test]
        public void TryApply_MapsValidBlockedPreviewForWarningDisplay()
        {
            var target = new global::TradePrepareViewData { currentTradingCurrency = 100L };
            var preview = new TradePreparationEconomyPreviewResult
            {
                IsValid = true,
                CanStart = false,
                BlockReason = TradePreparationBlockReason.InsufficientCurrency,
                CargoPurchaseCost = 120L,
                TotalPreparationCost = 120L,
                CurrencyAfterPreparation = -20L
            };

            bool applied = TradePreparationEconomyPreviewViewAdapter.TryApply(target, preview);

            Assert.That(applied, Is.True);
            Assert.That(target.canPurchaseCargo, Is.False);
            Assert.That(target.canHireSelectedMercenaries, Is.False);
            Assert.That(target.estimatedCurrencyAfterPurchase, Is.Zero);
            Assert.That(target.estimatedCurrencyAfterHire, Is.Zero);
        }

        [Test]
        public void TryApply_RejectsInvalidPreviewWithoutMutation()
        {
            var target = new global::TradePrepareViewData { totalPreparationCost = 99L };

            bool applied = TradePreparationEconomyPreviewViewAdapter.TryApply(
                target,
                new TradePreparationEconomyPreviewResult { IsValid = false });

            Assert.That(applied, Is.False);
            Assert.That(target.totalPreparationCost, Is.EqualTo(99L));
        }
    }
}
