using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class BuildingUpgradeEconomicPlanTests
    {
        [Test]
        public void Build_ValidUpgrade_CreatesPlan()
        {
            BuildingUpgradeInput input = Input();

            BuildingUpgradePlanBuildResult result =
                BuildingUpgradeEconomicPlanBuilder.Build(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Plan.BuildingId, Is.EqualTo("warehouse"));
            Assert.That(result.Plan.PreviousLevel, Is.EqualTo(0));
            Assert.That(result.Plan.TargetLevel, Is.EqualTo(1));
            Assert.That(result.Plan.Materials[0].ItemId, Is.EqualTo("wood"));
            Assert.That(result.Plan.Materials[0].RequiredQuantity, Is.EqualTo(3));
            Assert.That(result.Plan.Materials[0].QuantityAfter, Is.EqualTo(2));
            Assert.That(result.Plan.Effects[0].EffectId, Is.EqualTo("storage_slots"));
            Assert.That(result.Plan.Effects[0].Value, Is.EqualTo(10));
        }

        [Test]
        public void Build_CopiesValuesFromMutableInput()
        {
            BuildingUpgradeInput input = Input();

            BuildingUpgradePlanBuildResult result =
                BuildingUpgradeEconomicPlanBuilder.Build(input);
            input.HomeInventory[0].Quantity = 99;
            input.Definition.Levels[0].Materials[0].Quantity = 88;
            input.Definition.Levels[0].Effects[0].Value = 77;

            Assert.That(result.Plan.Materials[0].QuantityBefore, Is.EqualTo(5));
            Assert.That(result.Plan.Materials[0].RequiredQuantity, Is.EqualTo(3));
            Assert.That(result.Plan.Effects[0].Value, Is.EqualTo(10));
        }

        [Test]
        public void Build_InsufficientMaterials_DoesNotCreatePlan()
        {
            BuildingUpgradeInput input = Input();
            input.HomeInventory[0].Quantity = 2;

            BuildingUpgradePlanBuildResult result =
                BuildingUpgradeEconomicPlanBuilder.Build(input);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeFailureReason.InsufficientMaterials));
            Assert.That(result.Plan, Is.Null);
        }

        private static BuildingUpgradeInput Input()
        {
            return new BuildingUpgradeInput
            {
                BuildingId = "warehouse",
                Definition = new BuildingUpgradeDefinition
                {
                    BuildingId = "warehouse",
                    MaximumLevel = 1,
                    Levels =
                    {
                        new BuildingUpgradeLevelDefinition
                        {
                            Level = 1,
                            Materials =
                            {
                                new BuildingUpgradeMaterialRequirement
                                {
                                    ItemId = "wood",
                                    Quantity = 3
                                }
                            },
                            Effects =
                            {
                                new BuildingUpgradeEffect
                                {
                                    EffectId = "storage_slots",
                                    Value = 10
                                }
                            }
                        }
                    }
                },
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
