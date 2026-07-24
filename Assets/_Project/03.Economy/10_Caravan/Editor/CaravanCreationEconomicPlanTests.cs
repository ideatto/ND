using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class CaravanCreationEconomicPlanTests
    {
        [Test]
        public void Build_ValidCreation_CapturesDurableBeforeAndAfterState()
        {
            CaravanCreationPlanBuildResult result =
                CaravanCreationEconomicPlanBuilder.Build(
                    ValidInput(),
                    "caravan-new",
                    "town-home");

            Assert.That(result.Success, Is.True);
            Assert.That(result.FailureReason,
                Is.EqualTo(CaravanCreationFailureReason.None));
            Assert.That(result.Plan.CaravanId, Is.EqualTo("caravan-new"));
            Assert.That(result.Plan.InitialTownId, Is.EqualTo("town-home"));
            Assert.That(result.Plan.SlotIndex, Is.EqualTo(1));
            Assert.That(result.Plan.UnlockedSlotCount, Is.EqualTo(2));
            Assert.That(result.Plan.CaravanCountBefore, Is.EqualTo(1));
            Assert.That(result.Plan.CaravanCountAfter, Is.EqualTo(2));
            Assert.That(result.Plan.TradingCurrencyBefore, Is.EqualTo(1000));
            Assert.That(result.Plan.CreationCost, Is.EqualTo(100));
            Assert.That(result.Plan.TradingCurrencyAfter, Is.EqualTo(900));
        }

        [Test]
        public void Build_MissingStableCaravanId_RejectsPlan()
        {
            CaravanCreationPlanBuildResult result =
                CaravanCreationEconomicPlanBuilder.Build(
                    ValidInput(),
                    string.Empty,
                    "town-home");

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(CaravanCreationFailureReason.InvalidInput));
            Assert.That(result.Plan, Is.Null);
        }

        [Test]
        public void Build_MissingInitialTownId_RejectsPlan()
        {
            CaravanCreationPlanBuildResult result =
                CaravanCreationEconomicPlanBuilder.Build(
                    ValidInput(),
                    "caravan-new",
                    " ");

            Assert.That(result.FailureReason,
                Is.EqualTo(CaravanCreationFailureReason.InvalidInput));
        }

        [Test]
        public void Build_InsufficientCurrency_PropagatesPolicyFailure()
        {
            CaravanProgressionInput input = ValidInput();
            input.TradingCurrency = 99;

            CaravanCreationPlanBuildResult result =
                CaravanCreationEconomicPlanBuilder.Build(
                    input,
                    "caravan-new",
                    "town-home");

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    CaravanCreationFailureReason.InsufficientCurrency));
        }

        [Test]
        public void Build_OccupiedRequestedSlot_PropagatesPolicyFailure()
        {
            CaravanProgressionInput input = ValidInput();
            input.OccupiedSlotIndices.Add(1);
            input.OccupiedSlotIndices.Remove(0);

            CaravanCreationPlanBuildResult result =
                CaravanCreationEconomicPlanBuilder.Build(
                    input,
                    "caravan-new",
                    "town-home");

            Assert.That(result.FailureReason,
                Is.EqualTo(CaravanCreationFailureReason.SlotOccupied));
        }

        private static CaravanProgressionInput ValidInput()
        {
            return new CaravanProgressionInput
            {
                Definition = new CaravanProgressionPolicyDefinition
                {
                    MaximumSupportedSlots = 4,
                    BaseUnlockedSlots = 1,
                    SlotsPerGrowthLevel = 1,
                    MaximumGrowthSlotBonus = 2,
                    CreationCost = 100
                },
                CaravanGrowthLevel = 1,
                CurrentCaravanCount = 1,
                RequestedSlotIndex = 1,
                OccupiedSlotIndices = { 0 },
                TradingCurrency = 1000,
                HasInitialTown = true
            };
        }
    }
}
