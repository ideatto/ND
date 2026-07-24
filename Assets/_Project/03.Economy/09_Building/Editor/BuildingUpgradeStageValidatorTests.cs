using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class BuildingUpgradeStageValidatorTests
    {
        [Test]
        public void Validate_UnchangedState_AllowsStage()
        {
            BuildingUpgradeStageValidationResult result =
                BuildingUpgradeStageValidator.Validate(Plan(), Snapshot());

            Assert.That(result.Success, Is.True);
            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeStageFailureReason.None));
        }

        [Test]
        public void Validate_LevelChangedAfterPreview_BlocksStage()
        {
            BuildingUpgradePersistenceSnapshot snapshot = Snapshot();
            snapshot.CurrentLevel = 2;

            BuildingUpgradeStageValidationResult result =
                BuildingUpgradeStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeStageFailureReason.LevelChanged));
        }

        [Test]
        public void Validate_HomeInventoryChangedAfterPreview_BlocksStage()
        {
            BuildingUpgradePersistenceSnapshot snapshot = Snapshot();
            snapshot.HomeInventory[0].Quantity = 4;

            BuildingUpgradeStageValidationResult result =
                BuildingUpgradeStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    BuildingUpgradeStageFailureReason.HomeInventoryChanged));
        }

        [Test]
        public void Validate_DifferentBuilding_BlocksStage()
        {
            BuildingUpgradePersistenceSnapshot snapshot = Snapshot();
            snapshot.BuildingId = "workshop";

            BuildingUpgradeStageValidationResult result =
                BuildingUpgradeStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    BuildingUpgradeStageFailureReason.BuildingIdMismatch));
        }

        [Test]
        public void Validate_CorruptedHomeInventory_BlocksStage()
        {
            BuildingUpgradePersistenceSnapshot snapshot = Snapshot();
            snapshot.HomeInventory[0].Quantity = -1;

            BuildingUpgradeStageValidationResult result =
                BuildingUpgradeStageValidator.Validate(Plan(), snapshot);

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    BuildingUpgradeStageFailureReason.HomeInventoryCorrupted));
        }

        private static BuildingUpgradeEconomicPlan Plan()
        {
            return new BuildingUpgradeEconomicPlan(
                "warehouse",
                1,
                2,
                new[]
                {
                    new BuildingUpgradeMaterialPlan("wood", 3, 5, 2)
                },
                new[]
                {
                    new BuildingUpgradeEffectPlan("storage_slots", 10)
                });
        }

        private static BuildingUpgradePersistenceSnapshot Snapshot()
        {
            return new BuildingUpgradePersistenceSnapshot
            {
                BuildingId = "warehouse",
                CurrentLevel = 1,
                HomeInventory =
                {
                    new BuildingUpgradeInventoryEntry
                    {
                        ItemId = "wood",
                        Quantity = 5
                    }
                }
            };
        }
    }
}
