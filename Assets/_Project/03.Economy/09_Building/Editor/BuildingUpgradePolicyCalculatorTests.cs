using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class BuildingUpgradePolicyCalculatorTests
    {
        [Test]
        public void Evaluate_UsesNextLevelCostAndReturnsEffects()
        {
            BuildingUpgradeInput input = Input();

            BuildingUpgradeResult result = BuildingUpgradePolicyCalculator.Evaluate(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.BuildingId, Is.EqualTo("workshop"));
            Assert.That(result.PreviousLevel, Is.EqualTo(1));
            Assert.That(result.TargetLevel, Is.EqualTo(2));
            Assert.That(result.Materials[0].QuantityBefore, Is.EqualTo(10));
            Assert.That(result.Materials[0].QuantityAfter, Is.EqualTo(6));
            Assert.That(result.TargetEffects[0].EffectId, Is.EqualTo("craft_speed"));
            Assert.That(result.TargetEffects[0].Value, Is.EqualTo(1.25));
        }

        [Test]
        public void Evaluate_DifferentStableId_IsRejected()
        {
            BuildingUpgradeInput input = Input();
            input.BuildingId = "warehouse";

            BuildingUpgradeResult result = BuildingUpgradePolicyCalculator.Evaluate(input);

            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeFailureReason.InvalidDefinition));
        }

        [Test]
        public void Evaluate_InsufficientHomeInventory_ReportsMissingAmount()
        {
            BuildingUpgradeInput input = Input();
            input.HomeInventory[0].Quantity = 2;

            BuildingUpgradeResult result = BuildingUpgradePolicyCalculator.Evaluate(input);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeFailureReason.InsufficientMaterials));
            Assert.That(result.Materials[0].MissingQuantity, Is.EqualTo(2));
        }

        [Test]
        public void Evaluate_AlreadyAtMaximum_IsRejected()
        {
            BuildingUpgradeInput input = Input();
            input.CurrentLevel = 2;

            BuildingUpgradeResult result = BuildingUpgradePolicyCalculator.Evaluate(input);

            Assert.That(result.FailureReason,
                Is.EqualTo(BuildingUpgradeFailureReason.AlreadyMaxLevel));
        }

        [Test]
        public void Evaluate_DoesNotMutateInputInventoryOrEffects()
        {
            BuildingUpgradeInput input = Input();

            BuildingUpgradeResult result = BuildingUpgradePolicyCalculator.Evaluate(input);
            result.TargetEffects[0].Value = 99;

            Assert.That(input.HomeInventory[0].Quantity, Is.EqualTo(10));
            Assert.That(input.Definition.Levels[0].Effects[0].Value, Is.EqualTo(1.25));
        }

        private static BuildingUpgradeInput Input()
        {
            return new BuildingUpgradeInput
            {
                BuildingId = "workshop",
                CurrentLevel = 1,
                Definition = new BuildingUpgradeDefinition
                {
                    BuildingId = "workshop",
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
                                    Quantity = 4
                                }
                            },
                            Effects =
                            {
                                new BuildingUpgradeEffect
                                {
                                    EffectId = "craft_speed",
                                    Value = 1.25
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
                        Quantity = 10
                    }
                }
            };
        }
    }
}
