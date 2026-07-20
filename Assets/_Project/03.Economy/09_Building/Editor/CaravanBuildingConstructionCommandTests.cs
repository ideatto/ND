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
            Execute_ConsumesCargoAndRaisesBuildingLevel();
            Execute_RollsBackCargoAndBuildingWhenSaveFails();
            Execute_RejectsConstructionOutsideBaseTown();
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
                SaveResult result = CaravanBuildingConstructionCommand.Execute(
                    saveData,
                    new MemorySaveService(false),
                    new[] { material },
                    CreateDefinition(),
                    "BaseCamp",
                    out _);

                Check(!result.Succeeded, "Failed save must fail the command.");
                CheckEqual(5, saveData.caravan.cargo[0].quantity, "Rolled-back cargo quantity");
                CheckEqual(0, saveData.player.villageBuildings.Count, "Rolled-back building count");
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
