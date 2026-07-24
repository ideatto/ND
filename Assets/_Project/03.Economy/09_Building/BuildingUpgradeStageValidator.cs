using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum BuildingUpgradeStageFailureReason
    {
        None = 0,
        InvalidInput,
        BuildingIdMismatch,
        LevelChanged,
        HomeInventoryCorrupted,
        HomeInventoryChanged,
        InvalidPlan
    }

    [Serializable]
    public sealed class BuildingUpgradePersistenceSnapshot
    {
        public string BuildingId = string.Empty;
        public int CurrentLevel;
        public List<BuildingUpgradeInventoryEntry> HomeInventory =
            new List<BuildingUpgradeInventoryEntry>();
    }

    public sealed class BuildingUpgradeStageValidationResult
    {
        public bool Success { get; internal set; }
        public BuildingUpgradeStageFailureReason FailureReason { get; internal set; }
    }

    public static class BuildingUpgradeStageValidator
    {
        public static BuildingUpgradeStageValidationResult Validate(
            BuildingUpgradeEconomicPlan plan,
            BuildingUpgradePersistenceSnapshot snapshot)
        {
            if (plan == null ||
                snapshot == null ||
                string.IsNullOrWhiteSpace(plan.BuildingId) ||
                string.IsNullOrWhiteSpace(snapshot.BuildingId) ||
                snapshot.CurrentLevel < 0)
            {
                return Fail(BuildingUpgradeStageFailureReason.InvalidInput);
            }
            if (!string.Equals(
                plan.BuildingId,
                snapshot.BuildingId,
                StringComparison.Ordinal))
            {
                return Fail(
                    BuildingUpgradeStageFailureReason.BuildingIdMismatch);
            }
            if (!IsValidPlan(plan))
                return Fail(BuildingUpgradeStageFailureReason.InvalidPlan);
            if (snapshot.CurrentLevel != plan.PreviousLevel)
                return Fail(BuildingUpgradeStageFailureReason.LevelChanged);

            Dictionary<string, int> inventory;
            if (!TryAggregateInventory(snapshot.HomeInventory, out inventory))
            {
                return Fail(
                    BuildingUpgradeStageFailureReason.HomeInventoryCorrupted);
            }

            var plannedItems = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Materials.Count; i++)
            {
                BuildingUpgradeMaterialPlan material = plan.Materials[i];
                if (material == null ||
                    string.IsNullOrWhiteSpace(material.ItemId) ||
                    material.RequiredQuantity <= 0 ||
                    material.QuantityBefore < material.RequiredQuantity ||
                    material.QuantityAfter !=
                        material.QuantityBefore - material.RequiredQuantity ||
                    !plannedItems.Add(material.ItemId))
                {
                    return Fail(BuildingUpgradeStageFailureReason.InvalidPlan);
                }

                int persistedQuantity;
                inventory.TryGetValue(material.ItemId, out persistedQuantity);
                if (persistedQuantity != material.QuantityBefore)
                {
                    return Fail(
                        BuildingUpgradeStageFailureReason.HomeInventoryChanged);
                }
            }

            return new BuildingUpgradeStageValidationResult
            {
                Success = true,
                FailureReason = BuildingUpgradeStageFailureReason.None
            };
        }

        private static bool IsValidPlan(BuildingUpgradeEconomicPlan plan)
        {
            try
            {
                checked
                {
                    return plan.PreviousLevel >= 0 &&
                           plan.TargetLevel == plan.PreviousLevel + 1;
                }
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static bool TryAggregateInventory(
            List<BuildingUpgradeInventoryEntry> source,
            out Dictionary<string, int> totals)
        {
            totals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source == null)
                return false;

            try
            {
                checked
                {
                    for (int i = 0; i < source.Count; i++)
                    {
                        BuildingUpgradeInventoryEntry entry = source[i];
                        if (entry == null ||
                            string.IsNullOrWhiteSpace(entry.ItemId) ||
                            entry.Quantity < 0)
                        {
                            return false;
                        }
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

        private static BuildingUpgradeStageValidationResult Fail(
            BuildingUpgradeStageFailureReason reason)
        {
            return new BuildingUpgradeStageValidationResult
            {
                FailureReason = reason
            };
        }
    }
}
