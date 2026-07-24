using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class BuildingUpgradeInputAdapterTests
    {
        [Test]
        public void Build_ExactStableId_MapsHomeInventory()
        {
            BuildingUpgradeStateSnapshot state = State("warehouse");

            BuildingUpgradeInputAdapterResult result =
                BuildingUpgradeInputAdapter.Build(
                    "warehouse",
                    state,
                    Definition("warehouse"));

            Assert.That(result.Success, Is.True);
            Assert.That(result.BuildingId, Is.EqualTo("warehouse"));
            Assert.That(result.Input.CurrentLevel, Is.EqualTo(1));
            Assert.That(result.Input.HomeInventory[0].ItemId, Is.EqualTo("wood"));
            Assert.That(result.Input.HomeInventory[0].Quantity, Is.EqualTo(8));
        }

        [Test]
        public void Build_DifferentStateBuilding_IsRejected()
        {
            BuildingUpgradeInputAdapterResult result =
                BuildingUpgradeInputAdapter.Build(
                    "workshop",
                    State("warehouse"),
                    Definition("workshop"));

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeInputAdapterFailureReason.BuildingIdMismatch));
            Assert.That(result.Input, Is.Null);
        }

        [Test]
        public void Build_DifferentContentBuilding_IsRejected()
        {
            BuildingUpgradeInputAdapterResult result =
                BuildingUpgradeInputAdapter.Build(
                    "warehouse",
                    State("warehouse"),
                    Definition("workshop"));

            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeInputAdapterFailureReason.InvalidContentDefinition));
        }

        [Test]
        public void Build_CopiesInventoryFromSnapshot()
        {
            BuildingUpgradeStateSnapshot state = State("warehouse");

            BuildingUpgradeInputAdapterResult result =
                BuildingUpgradeInputAdapter.Build(
                    "warehouse",
                    state,
                    Definition("warehouse"));
            state.HomeInventory[0].Quantity = 99;

            Assert.That(result.Input.HomeInventory[0].Quantity, Is.EqualTo(8));
        }

        [Test]
        public void Build_NegativeInventory_IsRejected()
        {
            BuildingUpgradeStateSnapshot state = State("warehouse");
            state.HomeInventory[0].Quantity = -1;

            BuildingUpgradeInputAdapterResult result =
                BuildingUpgradeInputAdapter.Build(
                    "warehouse",
                    state,
                    Definition("warehouse"));

            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeInputAdapterFailureReason.HomeInventoryCorrupted));
        }

        private static BuildingUpgradeStateSnapshot State(string buildingId)
        {
            return new BuildingUpgradeStateSnapshot
            {
                BuildingId = buildingId,
                CurrentLevel = 1,
                HomeInventory =
                {
                    new BuildingUpgradeInventoryEntry
                    {
                        ItemId = "wood",
                        Quantity = 8
                    }
                }
            };
        }

        private static BuildingUpgradeDefinition Definition(string buildingId)
        {
            return new BuildingUpgradeDefinition
            {
                BuildingId = buildingId,
                MaximumLevel = 2,
                Levels =
                {
                    new BuildingUpgradeLevelDefinition
                    {
                        Level = 2,
                        Materials =
                        {
                            new BuildingUpgradeMaterialRequirement
                            {
                                ItemId = "wood",
                                Quantity = 3
                            }
                        }
                    }
                }
            };
        }
    }
}
