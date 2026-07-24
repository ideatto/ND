using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum BuildingUpgradeInputAdapterFailureReason
    {
        None = 0,
        InvalidRequest,
        InvalidState,
        BuildingIdMismatch,
        InvalidContentDefinition,
        HomeInventoryCorrupted
    }

    [Serializable]
    public sealed class BuildingUpgradeStateSnapshot
    {
        public string BuildingId = string.Empty;
        public int CurrentLevel;

        /// <summary>
        /// Framework must map only PlayerSaveData.homeInventory into this list.
        /// </summary>
        public List<BuildingUpgradeInventoryEntry> HomeInventory =
            new List<BuildingUpgradeInventoryEntry>();
    }

    public sealed class BuildingUpgradeInputAdapterResult
    {
        public BuildingUpgradeInputAdapterResult()
        {
            BuildingId = string.Empty;
        }

        public bool Success { get; internal set; }
        public BuildingUpgradeInputAdapterFailureReason FailureReason { get; internal set; }
        public string BuildingId { get; internal set; }
        public BuildingUpgradeInput Input { get; internal set; }
    }

    public static class BuildingUpgradeInputAdapter
    {
        public static BuildingUpgradeInputAdapterResult Build(
            string requestedBuildingId,
            BuildingUpgradeStateSnapshot state,
            BuildingUpgradeDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(requestedBuildingId))
                return Fail(BuildingUpgradeInputAdapterFailureReason.InvalidRequest);
            if (state == null ||
                string.IsNullOrWhiteSpace(state.BuildingId) ||
                state.CurrentLevel < 0)
            {
                return Fail(BuildingUpgradeInputAdapterFailureReason.InvalidState);
            }
            if (!string.Equals(
                requestedBuildingId,
                state.BuildingId,
                StringComparison.Ordinal))
            {
                return Fail(BuildingUpgradeInputAdapterFailureReason.BuildingIdMismatch);
            }
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.BuildingId) ||
                !string.Equals(
                    requestedBuildingId,
                    definition.BuildingId,
                    StringComparison.Ordinal))
            {
                return Fail(
                    BuildingUpgradeInputAdapterFailureReason.InvalidContentDefinition);
            }

            List<BuildingUpgradeInventoryEntry> inventory;
            if (!TryCopyHomeInventory(state.HomeInventory, out inventory))
            {
                return Fail(
                    BuildingUpgradeInputAdapterFailureReason.HomeInventoryCorrupted);
            }

            return new BuildingUpgradeInputAdapterResult
            {
                Success = true,
                FailureReason = BuildingUpgradeInputAdapterFailureReason.None,
                BuildingId = requestedBuildingId,
                Input = new BuildingUpgradeInput
                {
                    BuildingId = requestedBuildingId,
                    CurrentLevel = state.CurrentLevel,
                    Definition = definition,
                    HomeInventory = inventory
                }
            };
        }

        private static bool TryCopyHomeInventory(
            List<BuildingUpgradeInventoryEntry> source,
            out List<BuildingUpgradeInventoryEntry> copy)
        {
            copy = new List<BuildingUpgradeInventoryEntry>();
            if (source == null)
                return false;

            for (int i = 0; i < source.Count; i++)
            {
                BuildingUpgradeInventoryEntry entry = source[i];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.ItemId) ||
                    entry.Quantity < 0)
                {
                    return false;
                }
                copy.Add(new BuildingUpgradeInventoryEntry
                {
                    ItemId = entry.ItemId,
                    Quantity = entry.Quantity
                });
            }
            return true;
        }

        private static BuildingUpgradeInputAdapterResult Fail(
            BuildingUpgradeInputAdapterFailureReason reason)
        {
            return new BuildingUpgradeInputAdapterResult
            {
                FailureReason = reason
            };
        }
    }
}
