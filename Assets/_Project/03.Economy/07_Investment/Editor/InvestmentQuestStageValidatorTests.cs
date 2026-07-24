using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class InvestmentQuestStageValidatorTests
    {
        [Test]
        public void Validate_UnchangedState_AllowsStage()
        {
            InvestmentQuestStageValidationResult result =
                InvestmentQuestStageValidator.Validate(Plan(), Snapshot());

            Assert.That(result.Success, Is.True);
            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestStageFailureReason.None));
        }

        [Test]
        public void Validate_CompletedAfterPreview_BlocksDuplicateStage()
        {
            InvestmentQuestPersistenceSnapshot snapshot = Snapshot();
            snapshot.CompletedQuestIds.Add("invest-stage");

            InvestmentQuestStageValidationResult result =
                InvestmentQuestStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestStageFailureReason.AlreadyCompleted));
        }

        [Test]
        public void Validate_CurrencyChangedAfterPreview_BlocksStage()
        {
            InvestmentQuestPersistenceSnapshot snapshot = Snapshot();
            snapshot.TradingCurrency = 499;

            InvestmentQuestStageValidationResult result =
                InvestmentQuestStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    InvestmentQuestStageFailureReason.TradingCurrencyChanged));
        }

        [Test]
        public void Validate_CaravanInventoryChangedAfterPreview_BlocksStage()
        {
            InvestmentQuestPersistenceSnapshot snapshot = Snapshot();
            snapshot.CaravanInventory[0].Quantity = 6;

            InvestmentQuestStageValidationResult result =
                InvestmentQuestStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestStageFailureReason.InventoryChanged));
        }

        [Test]
        public void Validate_UnlockAlreadyAppliedWithoutCompletion_BlocksCorruptStage()
        {
            InvestmentQuestPersistenceSnapshot snapshot = Snapshot();
            snapshot.UnlockedRouteIds.Add("route-stage");

            InvestmentQuestStageValidationResult result =
                InvestmentQuestStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    InvestmentQuestStageFailureReason.UnlockAlreadyApplied));
        }

        [Test]
        public void Validate_DifferentCaravan_BlocksStage()
        {
            InvestmentQuestPersistenceSnapshot snapshot = Snapshot();
            snapshot.CaravanId = "other-caravan";

            InvestmentQuestStageValidationResult result =
                InvestmentQuestStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    InvestmentQuestStageFailureReason.CaravanIdMismatch));
        }

        private static InvestmentQuestEconomicPlan Plan()
        {
            return new InvestmentQuestEconomicPlan(
                "invest-stage",
                "caravan-stage",
                500,
                100,
                400,
                new[] { new InvestmentItemPlan("iron", 2, 5, 3) },
                new[] { "town-stage" },
                new[] { "route-stage" });
        }

        private static InvestmentQuestPersistenceSnapshot Snapshot()
        {
            return new InvestmentQuestPersistenceSnapshot
            {
                CaravanId = "caravan-stage",
                TradingCurrency = 500,
                CaravanInventory =
                {
                    new InvestmentInventoryEntry
                    {
                        ItemId = "iron",
                        Quantity = 5
                    }
                }
            };
        }
    }
}
