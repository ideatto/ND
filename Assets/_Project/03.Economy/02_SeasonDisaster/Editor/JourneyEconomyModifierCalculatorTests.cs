using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class JourneyEconomyModifierCalculatorTests
    {
        [Test]
        public void Evaluate_ComposesDifferentSourcesOnce()
        {
            JourneyEconomyModifierInput input = Input();
            input.Modifiers.Add(Modifier(
                JourneyModifierSourceType.Season,
                "winter",
                price: 1.2,
                speed: 0.8,
                food: 1.25,
                risk: 1.1,
                loss: 1));
            input.Modifiers.Add(Modifier(
                JourneyModifierSourceType.Disaster,
                "blizzard",
                price: 1.5,
                speed: 0.5,
                food: 1.2,
                risk: 2,
                loss: 1.5));

            JourneyEconomyModifierResult result =
                JourneyEconomyModifierCalculator.Evaluate(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.AppliedModifierCount, Is.EqualTo(2));
            Assert.That(result.AdjustedUnitPrice, Is.EqualTo(180));
            Assert.That(result.AdjustedSpeed, Is.EqualTo(4).Within(0.0001));
            Assert.That(result.AdjustedFoodConsumption, Is.EqualTo(15).Within(0.0001));
            Assert.That(result.AdjustedRiskRate, Is.EqualTo(0.44).Within(0.0001));
            Assert.That(result.AdjustedLossRate, Is.EqualTo(0.15).Within(0.0001));
        }

        [Test]
        public void Evaluate_DuplicateSource_IsRejected()
        {
            JourneyEconomyModifierInput input = Input();
            input.Modifiers.Add(Modifier(
                JourneyModifierSourceType.RouteEvent,
                "ambush",
                1, 1, 1, 1.5, 2));
            input.Modifiers.Add(Modifier(
                JourneyModifierSourceType.RouteEvent,
                "ambush",
                2, 1, 1, 1, 1));

            JourneyEconomyModifierResult result =
                JourneyEconomyModifierCalculator.Evaluate(input);

            Assert.That(result.FailureReason,
                Is.EqualTo(JourneyModifierFailureReason.DuplicateModifier));
        }

        [Test]
        public void Evaluate_NegativeOrNonFiniteModifier_IsRejected()
        {
            JourneyEconomyModifierInput negative = Input();
            negative.Modifiers.Add(Modifier(
                JourneyModifierSourceType.Season,
                "invalid",
                -1, 1, 1, 1, 1));

            JourneyEconomyModifierInput infinite = Input();
            infinite.Modifiers.Add(Modifier(
                JourneyModifierSourceType.Disaster,
                "invalid",
                double.PositiveInfinity, 1, 1, 1, 1));

            Assert.That(
                JourneyEconomyModifierCalculator.Evaluate(negative).FailureReason,
                Is.EqualTo(JourneyModifierFailureReason.InvalidModifier));
            Assert.That(
                JourneyEconomyModifierCalculator.Evaluate(infinite).FailureReason,
                Is.EqualTo(JourneyModifierFailureReason.InvalidModifier));
        }

        [Test]
        public void Evaluate_ExtremeFactors_AreCapped()
        {
            JourneyEconomyModifierInput input = Input();
            input.Modifiers.Add(Modifier(
                JourneyModifierSourceType.Season,
                "extreme-a",
                1000, 1000, 1000, 1000, 1000));
            input.Modifiers.Add(Modifier(
                JourneyModifierSourceType.Disaster,
                "extreme-b",
                1000, 1000, 1000, 1000, 1000));

            JourneyEconomyModifierResult result =
                JourneyEconomyModifierCalculator.Evaluate(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.AdjustedUnitPrice, Is.EqualTo(1000));
            Assert.That(result.CombinedPriceFactor, Is.EqualTo(10));
            Assert.That(result.AdjustedRiskRate, Is.EqualTo(1));
            Assert.That(result.AdjustedLossRate, Is.EqualTo(0.5));
        }

        [Test]
        public void Evaluate_ZeroFactors_NeverProduceNegativeValues()
        {
            JourneyEconomyModifierInput input = Input();
            input.Modifiers.Add(Modifier(
                JourneyModifierSourceType.Combat,
                "perfect-defense",
                0, 0, 0, 0, 0));

            JourneyEconomyModifierResult result =
                JourneyEconomyModifierCalculator.Evaluate(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.AdjustedUnitPrice, Is.Zero);
            Assert.That(result.AdjustedSpeed, Is.Zero);
            Assert.That(result.AdjustedFoodConsumption, Is.Zero);
            Assert.That(result.AdjustedRiskRate, Is.Zero);
            Assert.That(result.AdjustedLossRate, Is.Zero);
        }

        private static JourneyEconomyModifierInput Input()
        {
            return new JourneyEconomyModifierInput
            {
                BaseUnitPrice = 100,
                BaseSpeed = 10,
                BaseFoodConsumption = 10,
                BaseRiskRate = 0.2,
                BaseLossRate = 0.1
            };
        }

        private static JourneyEconomyModifier Modifier(
            JourneyModifierSourceType sourceType,
            string sourceId,
            double price,
            double speed,
            double food,
            double risk,
            double loss)
        {
            return new JourneyEconomyModifier
            {
                SourceType = sourceType,
                SourceId = sourceId,
                PriceFactor = price,
                SpeedFactor = speed,
                FoodFactor = food,
                RiskFactor = risk,
                LossFactor = loss
            };
        }
    }
}
