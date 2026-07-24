using System;
using System.Collections.Generic;

// Building construction calculation contracts. Persistence adapters live in ND.Framework.
namespace ND.Economy
{
    public enum BuildingCostFailureReason
    {
        None,
        InvalidInput,
        InvalidDefinition,
        BuildingNotFound,
        AlreadyMaxLevel,
        LevelCostNotFound,
        InventoryCorrupted,
        InsufficientMaterials,
        Overflow
    }

    [Serializable]
    public sealed class BuildingMaterialRequirement
    {
        public string ItemId = string.Empty;
        public int Quantity;
    }

    [Serializable]
    public sealed class BuildingLevelCost
    {
        public int TargetLevel;
        public List<BuildingMaterialRequirement> Materials = new List<BuildingMaterialRequirement>();
    }

    [Serializable]
    public sealed class BuildingCostDefinition
    {
        public string DisplayName = string.Empty;
        public int MaxLevel = 1;
        public List<BuildingLevelCost> LevelCosts = new List<BuildingLevelCost>();
    }

    [Serializable]
    public sealed class InventoryItemAmount
    {
        public string ItemId = string.Empty;
        public global::TradeItemCategory Category;
        public int Quantity;
    }

    [Serializable]
    public sealed class BuildingCostInput
    {
        public string DisplayName = string.Empty;
        public int CurrentLevel;
        public int MaxLevel;
        public List<BuildingLevelCost> LevelCosts = new List<BuildingLevelCost>();
        public List<InventoryItemAmount> CaravanCargo = new List<InventoryItemAmount>();
    }

    [Serializable]
    public sealed class BuildingMaterialDelta
    {
        public string ItemId = string.Empty;
        public int RequiredQuantity;
        public int QuantityBefore;
        public int QuantityAfter;
        public int MissingQuantity;
    }

    [Serializable]
    public sealed class BuildingCostResult
    {
        public bool Success;
        public BuildingCostFailureReason FailureReason;
        public string DisplayName = string.Empty;
        public int PreviousLevel;
        public int TargetLevel;
        public List<BuildingMaterialDelta> Materials = new List<BuildingMaterialDelta>();
    }
}
