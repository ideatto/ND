using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class DualGrowthStageValidatorTests
    {
        [Test]
        public void Validate_UnchangedState_AllowsStage()
        {
            DualGrowthStageValidationResult result =
                DualGrowthStageValidator.Validate(Plan(), Snapshot());

            Assert.That(result.Success, Is.True);
            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthStageFailureReason.None));
        }

        [Test]
        public void Validate_PlayerLevelChangedAfterPreview_BlocksStage()
        {
            DualGrowthPersistenceSnapshot snapshot = Snapshot();
            snapshot.PlayerGrowthLevel = 3;

            DualGrowthStageValidationResult result =
                DualGrowthStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    DualGrowthStageFailureReason.PlayerGrowthLevelChanged));
        }

        [Test]
        public void Validate_CaravanLevelChangedAfterPreview_BlocksStage()
        {
            DualGrowthPersistenceSnapshot snapshot = Snapshot();
            snapshot.CaravanGrowthLevel = 5;

            DualGrowthStageValidationResult result =
                DualGrowthStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    DualGrowthStageFailureReason.CaravanGrowthLevelChanged));
        }

        [Test]
        public void Validate_CurrencyChangedAfterPreview_BlocksStage()
        {
            DualGrowthPersistenceSnapshot snapshot = Snapshot();
            snapshot.DevelopmentCurrency = 999;

            DualGrowthStageValidationResult result =
                DualGrowthStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    DualGrowthStageFailureReason.DevelopmentCurrencyChanged));
        }

        [Test]
        public void Validate_DifferentGrowthId_BlocksStage()
        {
            DualGrowthPersistenceSnapshot snapshot = Snapshot();
            snapshot.GrowthId = "other-growth";

            DualGrowthStageValidationResult result =
                DualGrowthStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthStageFailureReason.GrowthIdMismatch));
        }

        [Test]
        public void Validate_InconsistentAxisLevels_BlocksInvalidPlan()
        {
            var invalid = new DualGrowthEconomicPlan(
                "player-growth",
                GrowthAxis.Player,
                2,
                3,
                2,
                3,
                4,
                5,
                1000,
                200,
                800,
                new[] { new GrowthEffectPlan("trade_bonus", 0.1) });

            DualGrowthStageValidationResult result =
                DualGrowthStageValidator.Validate(invalid, Snapshot());

            Assert.That(result.FailureReason,
                Is.EqualTo(DualGrowthStageFailureReason.InvalidPlan));
        }

        private static DualGrowthEconomicPlan Plan()
        {
            return new DualGrowthEconomicPlan(
                "player-growth",
                GrowthAxis.Player,
                2,
                3,
                2,
                3,
                4,
                4,
                1000,
                200,
                800,
                new[] { new GrowthEffectPlan("trade_bonus", 0.1) });
        }

        private static DualGrowthPersistenceSnapshot Snapshot()
        {
            return new DualGrowthPersistenceSnapshot
            {
                GrowthId = "player-growth",
                PlayerGrowthLevel = 2,
                CaravanGrowthLevel = 4,
                DevelopmentCurrency = 1000
            };
        }
    }
}
