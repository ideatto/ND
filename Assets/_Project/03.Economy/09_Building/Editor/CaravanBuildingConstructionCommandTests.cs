using System;
using ND.Framework;
using UnityEditor;
using UnityEngine;
using FrameworkCargoEntrySaveData = ND.Framework.CargoEntrySaveData;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkTradeItemSaveData = ND.Framework.TradeItemSaveData;

namespace ND.Economy.Editor
{
    public static class CaravanBuildingConstructionCommandTests
    {
        public static void RunAll()
        {
            VillageBuildingSaveData_OldJsonDefaultsPlacementFields();
            NormalizeData_NormalizesYawAndPreservesCoordinates();
            Execute_ConsumesCargoAndRaisesBuildingLevel();
            Execute_PreservesPlacementWhenRaisingBuildingLevel();
            Execute_RollsBackCargoAndBuildingWhenSaveFails();
            Execute_RejectsConstructionOutsideBaseTown();
        }

        private static void VillageBuildingSaveData_OldJsonDefaultsPlacementFields()
        {
            VillageBuildingSaveData building = JsonUtility.FromJson<VillageBuildingSaveData>(
                "{\"displayName\":\"legacy-building\",\"level\":1}");

            Check(!building.hasPlacement, "Old JSON must not report durable placement.");
            CheckEqual(0, building.gridCellX, "Old JSON grid X");
            CheckEqual(0, building.gridCellZ, "Old JSON grid Z");
            CheckEqual(0, building.yawStep, "Old JSON yaw step");
        }

        private static void NormalizeData_NormalizesYawAndPreservesCoordinates()
        {
            int[] sourceYawSteps = { -1, 0, 1, 4, 5 };
            int[] expectedYawSteps = { 3, 0, 1, 0, 1 };
            var saveData = new FrameworkSaveData();
            saveData.player.villageBuildings.Clear();

            for (int index = 0; index < sourceYawSteps.Length; index++)
            {
                saveData.player.villageBuildings.Add(new VillageBuildingSaveData
                {
                    displayName = "test-building-" + index,
                    level = 1,
                    hasPlacement = index % 2 == 0,
                    gridCellX = int.MinValue + index,
                    gridCellZ = int.MaxValue - index,
                    yawStep = sourceYawSteps[index]
                });
            }

            JsonSaveService.NormalizeData(saveData);
            for (int index = 0; index < expectedYawSteps.Length; index++)
            {
                VillageBuildingSaveData building = saveData.player.villageBuildings[index];
                CheckEqual(expectedYawSteps[index], building.yawStep, "Normalized yaw step");
                CheckEqual(int.MinValue + index, building.gridCellX, "Preserved grid X");
                CheckEqual(int.MaxValue - index, building.gridCellZ, "Preserved grid Z");
            }

            JsonSaveService.NormalizeData(saveData);
            for (int index = 0; index < expectedYawSteps.Length; index++)
            {
                CheckEqual(
                    expectedYawSteps[index],
                    saveData.player.villageBuildings[index].yawStep,
                    "Idempotent yaw step");
            }
        }

        private static void Execute_ConsumesCargoAndRaisesBuildingLevel()
        {
            TradeItemData material = CreateItem("wood", global::TradeItemCategory.Material);
            try
            {
                FrameworkSaveData saveData = CreateSaveData(5, "BaseCamp");
                SaveResult result = CaravanBuildingConstructionCommand.Execute(
                    saveData,
                    new MemorySaveService(true),
                    new[] { material },
                    CreateDefinition(),
                    "BaseCamp",
                    out BuildingCostResult costResult);

                Check(result.Succeeded, "Construction save should succeed.");
                Check(costResult != null && costResult.Success, "Cost calculation should succeed.");
                CheckEqual(2, saveData.caravan.cargo[0].quantity, "Cargo quantity after construction");
                CheckEqual(1, saveData.player.villageBuildings.Count, "Building count");
                CheckEqual(1, saveData.player.villageBuildings[0].level, "Building level");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static void Execute_RollsBackCargoAndBuildingWhenSaveFails()
        {
            TradeItemData material = CreateItem("wood", global::TradeItemCategory.Material);
            try
            {
                FrameworkSaveData saveData = CreateSaveData(5, "BaseCamp");
                AddPlacedWorkshop(saveData);
                SaveResult result = CaravanBuildingConstructionCommand.Execute(
                    saveData,
                    new MemorySaveService(false),
                    new[] { material },
                    CreateUpgradeDefinition(),
                    "BaseCamp",
                    out _);

                Check(!result.Succeeded, "Failed save must fail the command.");
                CheckEqual(5, saveData.caravan.cargo[0].quantity, "Rolled-back cargo quantity");
                CheckEqual(1, saveData.player.villageBuildings.Count, "Rolled-back building count");
                CheckPlacement(saveData.player.villageBuildings[0], 1, "Rolled-back building");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static void Execute_PreservesPlacementWhenRaisingBuildingLevel()
        {
            TradeItemData material = CreateItem("wood", global::TradeItemCategory.Material);
            try
            {
                FrameworkSaveData saveData = CreateSaveData(5, "BaseCamp");
                AddPlacedWorkshop(saveData);
                SaveResult result = CaravanBuildingConstructionCommand.Execute(
                    saveData,
                    new MemorySaveService(true),
                    new[] { material },
                    CreateUpgradeDefinition(),
                    "BaseCamp",
                    out _);

                Check(result.Succeeded, "Building upgrade save should succeed.");
                CheckPlacement(saveData.player.villageBuildings[0], 2, "Upgraded building");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static void Execute_RejectsConstructionOutsideBaseTown()
        {
            TradeItemData material = CreateItem("wood", global::TradeItemCategory.Material);
            try
            {
                FrameworkSaveData saveData = CreateSaveData(5, "TradeTown");
                SaveResult result = CaravanBuildingConstructionCommand.Execute(
                    saveData,
                    new MemorySaveService(true),
                    new[] { material },
                    CreateDefinition(),
                    "BaseCamp",
                    out _);

                Check(!result.Succeeded, "Construction outside base town must fail.");
                CheckEqual(5, saveData.caravan.cargo[0].quantity, "Unchanged cargo outside base town");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
            }
        }

        private static FrameworkSaveData CreateSaveData(int quantity, string townId)
        {
            var saveData = new FrameworkSaveData();
            saveData.player.currentTownId = townId;
            saveData.caravan.cargo.Add(new FrameworkCargoEntrySaveData
            {
                item = new FrameworkTradeItemSaveData { itemId = "wood" },
                quantity = quantity
            });
            return saveData;
        }

        private static BuildingCostDefinition CreateDefinition()
        {
            return new BuildingCostDefinition
            {
                DisplayName = "Workshop",
                MaxLevel = 1,
                LevelCosts =
                {
                    new BuildingLevelCost
                    {
                        TargetLevel = 1,
                        Materials = { new BuildingMaterialRequirement { ItemId = "wood", Quantity = 3 } }
                    }
                }
            };
        }

        private static BuildingCostDefinition CreateUpgradeDefinition()
        {
            BuildingCostDefinition definition = CreateDefinition();
            definition.MaxLevel = 2;
            definition.LevelCosts.Add(new BuildingLevelCost
            {
                TargetLevel = 2,
                Materials = { new BuildingMaterialRequirement { ItemId = "wood", Quantity = 3 } }
            });
            return definition;
        }

        private static void AddPlacedWorkshop(FrameworkSaveData saveData)
        {
            saveData.player.villageBuildings.Add(new VillageBuildingSaveData
            {
                displayName = "Workshop",
                level = 1,
                hasPlacement = true,
                gridCellX = -3,
                gridCellZ = 7,
                yawStep = 3
            });
        }

        private static void CheckPlacement(
            VillageBuildingSaveData building,
            int expectedLevel,
            string name)
        {
            CheckEqual("Workshop", building.displayName, name + " display name");
            CheckEqual(expectedLevel, building.level, name + " level");
            Check(building.hasPlacement, name + " placement flag");
            CheckEqual(-3, building.gridCellX, name + " grid X");
            CheckEqual(7, building.gridCellZ, name + " grid Z");
            CheckEqual(3, building.yawStep, name + " yaw step");
        }

        private static TradeItemData CreateItem(string itemId, global::TradeItemCategory category)
        {
            TradeItemData item = ScriptableObject.CreateInstance<TradeItemData>();
            var serialized = new SerializedObject(item);
            serialized.FindProperty("itemId").stringValue = itemId;
            serialized.FindProperty("category").enumValueIndex = (int)category;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void CheckEqual<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual))
            {
                throw new InvalidOperationException($"{name}: expected {expected}, actual {actual}.");
            }
        }

        private sealed class MemorySaveService : ISaveService
        {
            private readonly bool succeeds;
            public MemorySaveService(bool succeeds) { this.succeeds = succeeds; }
            public bool HasSaveData() => false;
            public FrameworkSaveData CreateNewGameData() => new FrameworkSaveData();
            public FrameworkSaveData Load() => new FrameworkSaveData();
            public SaveResult Save(FrameworkSaveData data) => succeeds
                ? SaveResult.Success()
                : SaveResult.Failure(SaveFailureReason.WriteFailed, "Test failure.");
            public void ResetSaveData() { }
        }
    }
}
