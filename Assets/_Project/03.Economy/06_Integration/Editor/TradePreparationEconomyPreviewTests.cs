using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class TradePreparationEconomyPreviewTests
    {
        [Test]
        public void Calculate_ReturnsCombinedPreparationPreview()
        {
            TradePreparationEconomyPreviewInput input = ValidInput();
            TradePreparationEconomyPreviewResult result = TradePreparationEconomyPreviewCalculator.Calculate(input);

            Assert.That(result.CanStart, Is.True);
            Assert.That(result.CargoPurchaseCost, Is.EqualTo(200L));
            Assert.That(result.FoodCost, Is.EqualTo(30L));
            Assert.That(result.MercenaryCost, Is.EqualTo(40L));
            Assert.That(result.TotalPreparationCost, Is.EqualTo(270L));
            Assert.That(result.CurrencyAfterPreparation, Is.EqualTo(730L));
            Assert.That(result.ExpectedSellRevenue, Is.EqualTo(300L));
            Assert.That(result.ExpectedNetProfit, Is.EqualTo(30L));
            Assert.That(result.CurrentLoad, Is.EqualTo(25f));
            Assert.That(result.FinalRisk, Is.EqualTo(0.35f).Within(0.0001f));
        }

        [Test]
        public void Calculate_BlocksCapacityOverflowButKeepsPreviewValues()
        {
            TradePreparationEconomyPreviewInput input = ValidInput();
            input.MaxLoad = 20f;
            input.EfficientLoad = 15f;

            TradePreparationEconomyPreviewResult result = TradePreparationEconomyPreviewCalculator.Calculate(input);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.CanStart, Is.False);
            Assert.That(result.BlockReason, Is.EqualTo(TradePreparationBlockReason.LoadCapacityExceeded));
            Assert.That(result.OverloadAmount, Is.EqualTo(10f));
            Assert.That(result.SpeedMultiplier, Is.LessThan(1f));
        }

        [Test]
        public void Calculate_ClampsRiskAndDetectsMissingRequirements()
        {
            TradePreparationEconomyPreviewInput input = ValidInput();
            input.HasWagon = false;
            input.BaseRisk = 5f;

            TradePreparationEconomyPreviewResult result = TradePreparationEconomyPreviewCalculator.Calculate(input);

            Assert.That(result.BlockReason, Is.EqualTo(TradePreparationBlockReason.MissingWagon));
            Assert.That(result.FinalRisk, Is.EqualTo(1f));
        }

        [Test]
        public void Calculate_RejectsArithmeticOverflow()
        {
            TradePreparationEconomyPreviewInput input = ValidInput();
            input.Cargo[0].UnitBuyPrice = long.MaxValue;

            TradePreparationEconomyPreviewResult result = TradePreparationEconomyPreviewCalculator.Calculate(input);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.BlockReason, Is.EqualTo(TradePreparationBlockReason.ArithmeticOverflow));
        }

        [Test]
        public void Calculate_DoesNotShareStateBetweenCaravans()
        {
            TradePreparationEconomyPreviewInput first = ValidInput();
            TradePreparationEconomyPreviewInput second = ValidInput();
            second.Cargo[0].Quantity = 1;

            TradePreparationEconomyPreviewResult firstResult = TradePreparationEconomyPreviewCalculator.Calculate(first);
            TradePreparationEconomyPreviewResult secondResult = TradePreparationEconomyPreviewCalculator.Calculate(second);

            Assert.That(firstResult.CargoPurchaseCost, Is.EqualTo(200L));
            Assert.That(secondResult.CargoPurchaseCost, Is.EqualTo(100L));
        }

        private static TradePreparationEconomyPreviewInput ValidInput()
        {
            var input = new TradePreparationEconomyPreviewInput
            {
                TradingCurrency = 1000L,
                FoodQuantity = 3,
                FoodUnitWeight = 1f,
                FoodUnitPrice = 10L,
                HasWagon = true,
                DraftAnimalCount = 2,
                EfficientLoad = 30f,
                MaxLoad = 40f,
                Distance = 10f,
                BaseMoveSpeed = 2f,
                BaseRisk = 0.2f,
                DistanceRiskPerUnit = 0.01f,
                FoodShortageRisk = 0.2f,
                RequiredFoodQuantity = 5,
                GrowthRiskMultiplier = 1f
            };
            input.Cargo.Add(new TradePreparationCargoInput
            {
                ItemId = "apple",
                Quantity = 2,
                UnitWeight = 11f,
                UnitBuyPrice = 100L,
                UnitExpectedSellPrice = 150L
            });
            input.Mercenaries.Add(new TradePreparationMercenaryInput
            {
                MercenaryId = "guard",
                HireCost = 40L,
                RiskReduction = 0.03f
            });
            return input;
        }
    }
}
