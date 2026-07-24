using System;

namespace ND.Economy.Editor
{
    public static class BuildingCostCalculatorTests
    {
        public static void RunAll()
        {
            Calculate_UsesLevelOneCostForConstruction();
            Calculate_UsesNextLevelCostForUpgrade();
            Calculate_AggregatesDuplicateInventoryEntries();
            Calculate_OnlyCountsMaterialCategory();
            Calculate_ReturnsEveryMissingMaterial();
            Calculate_RejectsMaximumLevelAndMissingCost();
            Calculate_RejectsInvalidRequirementAndInventory();
            Calculate_DoesNotMutateInput();
        }

        private static void Calculate_UsesLevelOneCostForConstruction()
        {
            BuildingCostResult result = BuildingCostCalculator.Calculate(CreateInput(
                0,
                Requirement("wood", 3),
                Inventory("wood", 5)));

            Check(result.Success, "Construction should succeed.");
            CheckEqual(0, result.PreviousLevel, "Construction PreviousLevel");
            CheckEqual(1, result.TargetLevel, "Construction TargetLevel");
            CheckEqual(2, result.Materials[0].QuantityAfter, "Construction wood after");
        }

        private static void Calculate_UsesNextLevelCostForUpgrade()
        {
            BuildingCostInput input = CreateInput(1, Requirement("wood", 3), Inventory("wood", 20));
            input.MaxLevel = 2;
            input.LevelCosts.Add(new BuildingLevelCost
            {
                TargetLevel = 2,
                Materials = { Requirement("stone", 4) }
            });
            input.CaravanCargo.Add(Inventory("stone", 5));

            BuildingCostResult result = BuildingCostCalculator.Calculate(input);

            Check(result.Success, "Upgrade should succeed.");
            CheckEqual(2, result.TargetLevel, "Upgrade TargetLevel");
            CheckEqual("stone", result.Materials[0].ItemId, "Upgrade material");
            CheckEqual(1, result.Materials[0].QuantityAfter, "Upgrade stone after");
        }

        private static void Calculate_AggregatesDuplicateInventoryEntries()
        {
            BuildingCostInput input = CreateInput(0, Requirement("wood", 5), Inventory("wood", 2));
            input.CaravanCargo.Add(Inventory("wood", 3));

            BuildingCostResult result = BuildingCostCalculator.Calculate(input);

            Check(result.Success, "Duplicate inventory entries should aggregate.");
            CheckEqual(5, result.Materials[0].QuantityBefore, "Aggregated wood");
            CheckEqual(0, result.Materials[0].QuantityAfter, "Aggregated wood after");
        }

        private static void Calculate_ReturnsEveryMissingMaterial()
        {
            BuildingCostInput input = CreateInput(0, Requirement("wood", 5), Inventory("wood", 2));
            input.LevelCosts[0].Materials.Add(Requirement("stone", 4));
            input.CaravanCargo.Add(Inventory("stone", 1));

            BuildingCostResult result = BuildingCostCalculator.Calculate(input);

            Check(!result.Success, "Missing materials should fail.");
            CheckEqual(BuildingCostFailureReason.InsufficientMaterials, result.FailureReason, "Missing failure");
            CheckEqual(2, result.Materials.Count, "Missing material count");
            CheckEqual(3, result.Materials[0].MissingQuantity, "Missing wood");
            CheckEqual(3, result.Materials[1].MissingQuantity, "Missing stone");
        }

        private static void Calculate_OnlyCountsMaterialCategory()
        {
            BuildingCostInput input = CreateInput(0, Requirement("wood", 2), Inventory("wood", 1));
            input.CaravanCargo.Add(new InventoryItemAmount
            {
                ItemId = "wood",
                Category = global::TradeItemCategory.Valuable,
                Quantity = 10
            });

            BuildingCostResult result = BuildingCostCalculator.Calculate(input);

            Check(!result.Success, "Non-material inventory must not pay construction costs.");
            CheckEqual(BuildingCostFailureReason.InsufficientMaterials, result.FailureReason, "Category failure");
            CheckEqual(1, result.Materials[0].QuantityBefore, "Material-only quantity");
            CheckEqual(1, result.Materials[0].MissingQuantity, "Material-only missing quantity");
        }

        private static void Calculate_RejectsMaximumLevelAndMissingCost()
        {
            BuildingCostInput maxLevel = CreateInput(1, Requirement("wood", 1), Inventory("wood", 1));
            CheckEqual(
                BuildingCostFailureReason.AlreadyMaxLevel,
                BuildingCostCalculator.Calculate(maxLevel).FailureReason,
                "Max-level failure");

            BuildingCostInput missingCost = CreateInput(0, Requirement("wood", 1), Inventory("wood", 1));
            missingCost.LevelCosts.Clear();
            CheckEqual(
                BuildingCostFailureReason.LevelCostNotFound,
                BuildingCostCalculator.Calculate(missingCost).FailureReason,
                "Missing-cost failure");
        }

        private static void Calculate_RejectsInvalidRequirementAndInventory()
        {
            BuildingCostInput duplicateRequirement = CreateInput(0, Requirement("wood", 1), Inventory("wood", 2));
            duplicateRequirement.LevelCosts[0].Materials.Add(Requirement("wood", 1));
            CheckEqual(
                BuildingCostFailureReason.InvalidDefinition,
                BuildingCostCalculator.Calculate(duplicateRequirement).FailureReason,
                "Duplicate requirement failure");

            BuildingCostInput corruptInventory = CreateInput(0, Requirement("wood", 1), Inventory("wood", -1));
            CheckEqual(
                BuildingCostFailureReason.InventoryCorrupted,
                BuildingCostCalculator.Calculate(corruptInventory).FailureReason,
                "Corrupt inventory failure");
        }

        private static void Calculate_DoesNotMutateInput()
        {
            BuildingCostInput input = CreateInput(0, Requirement("wood", 2), Inventory("wood", 3));

            BuildingCostCalculator.Calculate(input);

            CheckEqual(3, input.CaravanCargo[0].Quantity, "Input cargo quantity");
            CheckEqual(2, input.LevelCosts[0].Materials[0].Quantity, "Input requirement quantity");
        }

        private static BuildingCostInput CreateInput(
            int currentLevel,
            BuildingMaterialRequirement requirement,
            InventoryItemAmount inventory)
        {
            return new BuildingCostInput
            {
                DisplayName = "Workshop",
                CurrentLevel = currentLevel,
                MaxLevel = 1,
                LevelCosts =
                {
                    new BuildingLevelCost
                    {
                        TargetLevel = 1,
                        Materials = { requirement }
                    }
                },
                CaravanCargo = { inventory }
            };
        }

        private static BuildingMaterialRequirement Requirement(string itemId, int quantity)
        {
            return new BuildingMaterialRequirement { ItemId = itemId, Quantity = quantity };
        }

        private static InventoryItemAmount Inventory(string itemId, int quantity)
        {
            return new InventoryItemAmount
            {
                ItemId = itemId,
                Category = global::TradeItemCategory.Material,
                Quantity = quantity
            };
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void CheckEqual<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}.");
            }
        }
    }
}
