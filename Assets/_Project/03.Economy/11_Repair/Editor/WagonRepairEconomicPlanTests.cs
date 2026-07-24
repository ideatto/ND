using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class WagonRepairEconomicPlanTests
    {
        [Test]
        public void Build_ValidCalculation_CreatesImmutablePlan()
        {
            WagonRepairPlanBuildResult result = WagonRepairEconomicPlanBuilder.Build(
                "caravan-2",
                new WagonRepairInput
                {
                    CurrentDurability = 70,
                    MaximumDurability = 100,
                    RequestedRepairAmount = 20,
                    RepairCostPerDurability = 4,
                    RarityMultiplier = 1.5,
                    TradingCurrency = 200
                });

            Assert.That(result.Success, Is.True);
            Assert.That(result.Plan.CaravanId, Is.EqualTo("caravan-2"));
            Assert.That(result.Plan.DurabilityBefore, Is.EqualTo(70));
            Assert.That(result.Plan.DurabilityAfter, Is.EqualTo(90));
            Assert.That(result.Plan.RepairCost, Is.EqualTo(120));
            Assert.That(result.Plan.CurrencyAfter, Is.EqualTo(80));
        }

        [Test]
        public void Build_MissingCaravanId_IsRejected()
        {
            WagonRepairPlanBuildResult result =
                WagonRepairEconomicPlanBuilder.Build(" ", new WagonRepairInput());

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(WagonRepairFailureReason.InvalidInput));
            Assert.That(result.Plan, Is.Null);
        }

        [Test]
        public void Build_FailedCalculation_DoesNotCreatePlan()
        {
            WagonRepairPlanBuildResult result = WagonRepairEconomicPlanBuilder.Build(
                "caravan-1",
                new WagonRepairInput
                {
                    CurrentDurability = 100,
                    MaximumDurability = 100,
                    RequestedRepairAmount = 1,
                    RepairCostPerDurability = 1,
                    TradingCurrency = 100
                });

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(WagonRepairFailureReason.AlreadyFull));
            Assert.That(result.Plan, Is.Null);
        }
    }
}
