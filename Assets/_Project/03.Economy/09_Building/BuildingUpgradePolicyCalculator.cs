using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum BuildingUpgradeFailureReason
    {
        None = 0,
        InvalidInput,
        InvalidDefinition,
        AlreadyMaxLevel,
        LevelDefinitionNotFound,
        HomeInventoryCorrupted,
        InsufficientMaterials,
        ArithmeticOverflow
    }

    [Serializable]
    public sealed class BuildingUpgradeMaterialRequirement
    {
        public string ItemId = string.Empty;
        public int Quantity;
    }

    [Serializable]
    public sealed class BuildingUpgradeEffect
    {
        public string EffectId = string.Empty;
        public double Value;
    }

    [Serializable]
    public sealed class BuildingUpgradeLevelDefinition
    {
        public int Level;
        public List<BuildingUpgradeMaterialRequirement> Materials =
            new List<BuildingUpgradeMaterialRequirement>();
        public List<BuildingUpgradeEffect> Effects =
            new List<BuildingUpgradeEffect>();
    }

    [Serializable]
    public sealed class BuildingUpgradeDefinition
    {
        public string BuildingId = string.Empty;
        public int MaximumLevel;
        public List<BuildingUpgradeLevelDefinition> Levels =
            new List<BuildingUpgradeLevelDefinition>();
    }

    [Serializable]
    public sealed class BuildingUpgradeInventoryEntry
    {
        public string ItemId = string.Empty;
        public int Quantity;
    }

    [Serializable]
    public sealed class BuildingUpgradeInput
    {
        public string BuildingId = string.Empty;
        public int CurrentLevel;
        public BuildingUpgradeDefinition Definition;

        /// <summary>
        /// Only the base-town home inventory may be supplied here.
        /// Caravan cargo must never be merged into this collection.
        /// </summary>
        public List<BuildingUpgradeInventoryEntry> HomeInventory =
            new List<BuildingUpgradeInventoryEntry>();
    }

    [Serializable]
    public sealed class BuildingUpgradeMaterialDelta
    {
        public string ItemId = string.Empty;
        public int RequiredQuantity;
        public int QuantityBefore;
        public int QuantityAfter;
        public int MissingQuantity;
    }

    [Serializable]
    public sealed class BuildingUpgradeResult
    {
        public bool Success;
        public BuildingUpgradeFailureReason FailureReason;
        public string BuildingId = string.Empty;
        public int PreviousLevel;
        public int TargetLevel;
        public List<BuildingUpgradeMaterialDelta> Materials =
            new List<BuildingUpgradeMaterialDelta>();
        public List<BuildingUpgradeEffect> TargetEffects =
            new List<BuildingUpgradeEffect>();
    }

    public static class BuildingUpgradePolicyCalculator
    {
        public static BuildingUpgradeResult Evaluate(BuildingUpgradeInput input)
        {
            var result = new BuildingUpgradeResult
            {
                BuildingId = input != null ? input.BuildingId ?? string.Empty : string.Empty,
                PreviousLevel = input != null ? input.CurrentLevel : 0,
                TargetLevel = input != null ? input.CurrentLevel : 0
            };

            if (input == null ||
                string.IsNullOrWhiteSpace(input.BuildingId) ||
                input.CurrentLevel < 0 ||
                input.Definition == null)
            {
                return Fail(result, BuildingUpgradeFailureReason.InvalidInput);
            }

            BuildingUpgradeDefinition definition = input.Definition;
            if (string.IsNullOrWhiteSpace(definition.BuildingId) ||
                !string.Equals(input.BuildingId, definition.BuildingId, StringComparison.Ordinal) ||
                definition.MaximumLevel <= 0 ||
                definition.Levels == null)
            {
                return Fail(result, BuildingUpgradeFailureReason.InvalidDefinition);
            }
            if (input.CurrentLevel >= definition.MaximumLevel)
                return Fail(result, BuildingUpgradeFailureReason.AlreadyMaxLevel);

            int targetLevel;
            try
            {
                targetLevel = checked(input.CurrentLevel + 1);
            }
            catch (OverflowException)
            {
                return Fail(result, BuildingUpgradeFailureReason.ArithmeticOverflow);
            }

            BuildingUpgradeLevelDefinition target = null;
            var seenLevels = new HashSet<int>();
            for (int i = 0; i < definition.Levels.Count; i++)
            {
                BuildingUpgradeLevelDefinition level = definition.Levels[i];
                if (level == null || level.Level <= 0 || !seenLevels.Add(level.Level))
                    return Fail(result, BuildingUpgradeFailureReason.InvalidDefinition);
                if (level.Level == targetLevel)
                    target = level;
            }
            if (target == null)
                return Fail(result, BuildingUpgradeFailureReason.LevelDefinitionNotFound);

            Dictionary<string, int> inventory;
            if (!TryAggregateHomeInventory(input.HomeInventory, out inventory))
                return Fail(result, BuildingUpgradeFailureReason.HomeInventoryCorrupted);

            Dictionary<string, int> requirements;
            if (!TryAggregateRequirements(target.Materials, out requirements))
                return Fail(result, BuildingUpgradeFailureReason.InvalidDefinition);
            if (!TryCopyEffects(target.Effects, result.TargetEffects))
                return Fail(result, BuildingUpgradeFailureReason.InvalidDefinition);

            foreach (KeyValuePair<string, int> requirement in requirements)
            {
                int quantityBefore;
                inventory.TryGetValue(requirement.Key, out quantityBefore);
                int missing = Math.Max(0, requirement.Value - quantityBefore);
                result.Materials.Add(new BuildingUpgradeMaterialDelta
                {
                    ItemId = requirement.Key,
                    RequiredQuantity = requirement.Value,
                    QuantityBefore = quantityBefore,
                    QuantityAfter = Math.Max(0, quantityBefore - requirement.Value),
                    MissingQuantity = missing
                });
            }
            result.Materials.Sort((left, right) =>
                string.CompareOrdinal(left.ItemId, right.ItemId));

            for (int i = 0; i < result.Materials.Count; i++)
            {
                if (result.Materials[i].MissingQuantity > 0)
                    return Fail(result, BuildingUpgradeFailureReason.InsufficientMaterials);
            }

            result.Success = true;
            result.FailureReason = BuildingUpgradeFailureReason.None;
            result.TargetLevel = targetLevel;
            return result;
        }

        private static bool TryAggregateHomeInventory(
            List<BuildingUpgradeInventoryEntry> entries,
            out Dictionary<string, int> totals)
        {
            totals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (entries == null)
                return false;

            try
            {
                checked
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        BuildingUpgradeInventoryEntry entry = entries[i];
                        if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId) || entry.Quantity < 0)
                            return false;
                        int current;
                        totals.TryGetValue(entry.ItemId, out current);
                        totals[entry.ItemId] = current + entry.Quantity;
                    }
                }
            }
            catch (OverflowException)
            {
                return false;
            }
            return true;
        }

        private static bool TryAggregateRequirements(
            List<BuildingUpgradeMaterialRequirement> entries,
            out Dictionary<string, int> totals)
        {
            totals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (entries == null)
                return false;

            try
            {
                checked
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        BuildingUpgradeMaterialRequirement entry = entries[i];
                        if (entry == null || string.IsNullOrWhiteSpace(entry.ItemId) || entry.Quantity <= 0)
                            return false;
                        int current;
                        totals.TryGetValue(entry.ItemId, out current);
                        totals[entry.ItemId] = current + entry.Quantity;
                    }
                }
            }
            catch (OverflowException)
            {
                return false;
            }
            return true;
        }

        private static bool TryCopyEffects(
            List<BuildingUpgradeEffect> effects,
            List<BuildingUpgradeEffect> destination)
        {
            if (effects == null)
                return false;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < effects.Count; i++)
            {
                BuildingUpgradeEffect effect = effects[i];
                if (effect == null ||
                    string.IsNullOrWhiteSpace(effect.EffectId) ||
                    double.IsNaN(effect.Value) ||
                    double.IsInfinity(effect.Value) ||
                    !ids.Add(effect.EffectId))
                {
                    return false;
                }
                destination.Add(new BuildingUpgradeEffect
                {
                    EffectId = effect.EffectId,
                    Value = effect.Value
                });
            }
            return true;
        }

        private static BuildingUpgradeResult Fail(
            BuildingUpgradeResult result,
            BuildingUpgradeFailureReason reason)
        {
            result.Success = false;
            result.FailureReason = reason;
            return result;
        }
    }
}
