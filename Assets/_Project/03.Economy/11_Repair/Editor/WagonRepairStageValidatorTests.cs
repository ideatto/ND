using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class WagonRepairStageValidatorTests
    {
        [Test]
        public void Validate_UnchangedState_AllowsStage()
        {
            WagonRepairStageValidationResult result =
                WagonRepairStageValidator.Validate(Plan(), Snapshot());

            Assert.That(result.Success, Is.True);
            Assert.That(result.FailureReason,
                Is.EqualTo(WagonRepairStageFailureReason.None));
        }

        [Test]
        public void Validate_DurabilityChangedAfterPreview_BlocksStage()
        {
            WagonRepairPersistenceSnapshot snapshot = Snapshot();
            snapshot.CurrentDurability = 55;

            WagonRepairStageValidationResult result =
                WagonRepairStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(WagonRepairStageFailureReason.DurabilityChanged));
        }

        [Test]
        public void Validate_CurrencyChangedAfterPreview_BlocksStage()
        {
            WagonRepairPersistenceSnapshot snapshot = Snapshot();
            snapshot.TradingCurrency = 99;

            WagonRepairStageValidationResult result =
                WagonRepairStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(WagonRepairStageFailureReason.CurrencyChanged));
        }

        [Test]
        public void Validate_DifferentCaravan_BlocksStage()
        {
            WagonRepairPersistenceSnapshot snapshot = Snapshot();
            snapshot.CaravanId = "other-caravan";

            WagonRepairStageValidationResult result =
                WagonRepairStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(WagonRepairStageFailureReason.CaravanIdMismatch));
        }

        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(false, false, true)]
        public void Validate_NonRepairableCurrentState_BlocksStage(
            bool noWagon,
            bool destroyed,
            bool inJourney)
        {
            WagonRepairPersistenceSnapshot snapshot = Snapshot();
            snapshot.HasWagon = !noWagon;
            snapshot.IsDestroyed = destroyed;
            snapshot.IsInJourney = inJourney;

            WagonRepairStageValidationResult result =
                WagonRepairStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(WagonRepairStageFailureReason.DurabilityChanged));
        }

        private static WagonRepairEconomicPlan Plan()
        {
            return new WagonRepairEconomicPlan(
                "caravan-repair",
                50,
                60,
                10,
                20,
                100,
                80);
        }

        private static WagonRepairPersistenceSnapshot Snapshot()
        {
            return new WagonRepairPersistenceSnapshot
            {
                CaravanId = "caravan-repair",
                HasWagon = true,
                CurrentDurability = 50,
                MaximumDurability = 100,
                TradingCurrency = 100
            };
        }
    }
}
