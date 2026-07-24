using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class DualGrowthPolicyCalculatorTests
    {
        [Test]
        public void Evaluate_PlayerGrowth_ChangesOnlyPlayerAxis()
        {
            DualGrowthInput input = Input(GrowthAxis.Player, "player_capacity");
            input.PlayerGrowthLevel = 1;
            input.CaravanGrowthLevel = 3;

            DualGrowthResult result = DualGrowthPolicyCalculator.Evaluate(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.PreviousLevel, Is.EqualTo(1));
            Assert.That(result.TargetLevel, Is.EqualTo(2));
            Assert.That(result.PlayerGrowthLevelAfter, Is.EqualTo(2));
            Assert.That(result.CaravanGrowthLevelAfter, Is.EqualTo(3));
            Assert.That(result.DevelopmentCurrencyAfter, Is.EqualTo(80));
            Assert.That(result.TargetEffects[0].EffectId, Is.EqualTo("player_capacity"));
        }

        [Test]
        public void Evaluate_CaravanGrowth_ChangesOnlyCaravanAxis()
        {
            DualGrowthInput input = Input(GrowthAxis.Caravan, "caravan_load");
            input.PlayerGrowthLevel = 2;
            input.CaravanGrowthLevel = 1;

            DualGrowthResult result = DualGrowthPolicyCalculator.Evaluate(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.PlayerGrowthLevelAfter, Is.EqualTo(2));
            Assert.That(result.CaravanGrowthLevelAfter, Is.EqualTo(2));
        }

        [Test]
        public void Evaluate_AxisMismatch_IsRejected()
        {
            DualGrowthInput input = Input(GrowthAxis.Player, "player_capacity");
            input.Definition.Axis = GrowthAxis.Caravan;

            DualGrowthResult result = DualGrowthPolicyCalculator.Evaluate(input);

            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthFailureReason.AxisMismatch));
        }

        [Test]
        public void Evaluate_StableIdMismatch_IsRejected()
        {
            DualGrowthInput input = Input(GrowthAxis.Player, "player_capacity");
            input.Definition.GrowthId = "other_growth";

            DualGrowthResult result = DualGrowthPolicyCalculator.Evaluate(input);

            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthFailureReason.GrowthIdMismatch));
        }

        [Test]
        public void Evaluate_InsufficientCurrency_PreservesBothLevels()
        {
            DualGrowthInput input = Input(GrowthAxis.Caravan, "caravan_load");
            input.PlayerGrowthLevel = 2;
            input.CaravanGrowthLevel = 1;
            input.DevelopmentCurrency = 19;

            DualGrowthResult result = DualGrowthPolicyCalculator.Evaluate(input);

            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthFailureReason.InsufficientDevelopmentCurrency));
            Assert.That(result.PlayerGrowthLevelAfter, Is.EqualTo(2));
            Assert.That(result.CaravanGrowthLevelAfter, Is.EqualTo(1));
            Assert.That(result.DevelopmentCurrencyAfter, Is.EqualTo(19));
        }

        private static DualGrowthInput Input(GrowthAxis axis, string growthId)
        {
            return new DualGrowthInput
            {
                RequestedAxis = axis,
                RequestedGrowthId = growthId,
                PlayerGrowthLevel = 1,
                CaravanGrowthLevel = 1,
                DevelopmentCurrency = 100,
                Definition = new GrowthAxisDefinition
                {
                    Axis = axis,
                    GrowthId = growthId,
                    MaximumLevel = 2,
                    Levels =
                    {
                        new GrowthLevelDefinition
                        {
                            Level = 2,
                            DevelopmentCurrencyCost = 20,
                            Effects =
                            {
                                new GrowthLevelEffect
                                {
                                    EffectId = axis == GrowthAxis.Player
                                        ? "player_capacity"
                                        : "caravan_load",
                                    Value = 10
                                }
                            }
                        }
                    }
                }
            };
        }
    }
}
