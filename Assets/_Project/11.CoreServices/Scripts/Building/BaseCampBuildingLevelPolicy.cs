using System;
using System.Collections.Generic;

namespace ND.Framework
{
    /// <summary>
    /// Applies the shared rule that ordinary buildings cannot advance beyond BaseCamp level.
    /// SaveData is authoritative; scene presentation objects are never used for validation.
    /// </summary>
    public static class BaseCampBuildingLevelPolicy
    {
        public const string BaseCampBuildingId = "BaseCamp";
        public const string BaseCampDisplayName = "베이스 캠프";

        public static bool CanAdvance(
            string buildingId,
            int targetLevel,
            IReadOnlyList<VillageBuildingSaveData> buildings,
            out int baseCampLevel)
        {
            baseCampLevel = 0;

            if (string.IsNullOrWhiteSpace(buildingId) || targetLevel < 1 || buildings == null)
                return false;

            if (string.Equals(buildingId, BaseCampBuildingId, StringComparison.Ordinal))
                return true;

            if (!TryGetUniqueLevel(buildings, BaseCampDisplayName, out baseCampLevel))
                return false;

            return targetLevel <= baseCampLevel;
        }

        public static bool TryGetUniqueLevel(
            IReadOnlyList<VillageBuildingSaveData> buildings,
            string displayName,
            out int level)
        {
            level = 0;
            if (buildings == null || string.IsNullOrWhiteSpace(displayName))
                return false;

            bool found = false;
            for (int i = 0; i < buildings.Count; i++)
            {
                VillageBuildingSaveData building = buildings[i];
                if (building == null || string.IsNullOrWhiteSpace(building.displayName) || building.level < 1)
                    return false;
                if (!string.Equals(building.displayName, displayName, StringComparison.Ordinal))
                    continue;
                if (found)
                    return false;

                found = true;
                level = building.level;
            }

            // An unbuilt BaseCamp is a valid level-zero state.
            return true;
        }
    }
}
