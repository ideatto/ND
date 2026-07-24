using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class DualGrowthEconomicPlanTests
    {
        [Test]
        public void Build_PlayerGrowth_CreatesCompletePlan()
        {
            DualGrowthInput input = Input(GrowthAxis.Player);

            DualGrowthPlanBuildResult result =
                DualGrowthEconomicPlanBuilder.Build(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Plan.GrowthId, Is.EqualTo("player_capacity"));
            Assert.That(result.Plan.Axis, Is.EqualTo(GrowthAxis.Player));
            Assert.That(result.Plan.PlayerGrowthLevelBefore, Is.EqualTo(1));
            Assert.That(result.Plan.PlayerGrowthLevelAfter, Is.EqualTo(2));
            Assert.That(result.Plan.CaravanGrowthLevelBefore, Is.EqualTo(3));
            Assert.That(result.Plan.CaravanGrowthLevelAfter, Is.EqualTo(3));
            Assert.That(result.Plan.DevelopmentCurrencyCost, Is.EqualTo(20));
            Assert.That(result.Plan.DevelopmentCurrencyAfter, Is.EqualTo(80));
        }

        [Test]
        public void Build_CaravanGrowth_CreatesCompletePlan()
        {
            DualGrowthInput input = Input(GrowthAxis.Caravan);

            DualGrowthPlanBuildResult result =
                DualGrowthEconomicPlanBuilder.Build(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Plan.Axis, Is.EqualTo(GrowthAxis.Caravan));
            Assert.That(result.Plan.PlayerGrowthLevelAfter, Is.EqualTo(1));
            Assert.That(result.Plan.CaravanGrowthLevelAfter, Is.EqualTo(4));
        }

        [Test]
        public void Build_CopiesMutableDefinitionValues()
        {
            DualGrowthInput input = Input(GrowthAxis.Player);

            DualGrowthPlanBuildResult result =
                DualGrowthEconomicPlanBuilder.Build(input);
            input.Definition.Levels[0].DevelopmentCurrencyCost = 99;
            input.Definition.Levels[0].Effects[0].Value = 77;

            Assert.That(result.Plan.DevelopmentCurrencyCost, Is.EqualTo(20));
            Assert.That(result.Plan.TargetEffects[0].Value, Is.EqualTo(10));
        }

        [Test]
        public void Build_InsufficientCurrency_DoesNotCreatePlan()
        {
            DualGrowthInput input = Input(GrowthAxis.Player);
            input.DevelopmentCurrency = 19;

            DualGrowthPlanBuildResult result =
                DualGrowthEconomicPlanBuilder.Build(input);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthFailureReason.InsufficientDevelopmentCurrency));
            Assert.That(result.Plan, Is.Null);
        }

        private static DualGrowthInput Input(GrowthAxis axis)
        {
            int currentLevel = axis == GrowthAxis.Player ? 1 : 3;
            string growthId = axis == GrowthAxis.Player
                ? "player_capacity"
                : "caravan_load";
            return new DualGrowthInput
            {
                RequestedGrowthId = growthId,
                RequestedAxis = axis,
                PlayerGrowthLevel = 1,
                CaravanGrowthLevel = 3,
                DevelopmentCurrency = 100,
                Definition = new GrowthAxisDefinition
                {
                    GrowthId = growthId,
                    Axis = axis,
                    MaximumLevel = currentLevel + 1,
                    Levels =
                    {
                        new GrowthLevelDefinition
                        {
                            Level = currentLevel + 1,
                            DevelopmentCurrencyCost = 20,
                            Effects =
                            {
                                new GrowthLevelEffect
                                {
                                    EffectId = growthId,
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
