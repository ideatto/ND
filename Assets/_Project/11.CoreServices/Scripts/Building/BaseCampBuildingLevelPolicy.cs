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
        public const string EndingBuildingId = "EndingItem";

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

            // EndingItem is a one-time Lv.1 construction unlocked by BaseCamp Lv.5. It must not
            // inherit the ordinary targetLevel <= BaseCamp rule, which would expose it at Lv.1.
            if (string.Equals(buildingId, EndingBuildingId, StringComparison.Ordinal))
            {
                return targetLevel == 1
                    && baseCampLevel >= BaseCampProgressionPolicy.EndingBuildingUnlockLevel;
            }

            return targetLevel <= baseCampLevel;
        }

        /// <summary>
        /// Returns the BaseCamp level shown when construction is rejected. This keeps user-facing
        /// guidance consistent with the same special policy used by the command gate.
        /// </summary>
        public static int GetRequiredBaseCampLevel(string buildingId, int targetLevel)
        {
            if (string.Equals(buildingId, EndingBuildingId, StringComparison.Ordinal))
                return BaseCampProgressionPolicy.EndingBuildingUnlockLevel;

            return Math.Max(1, targetLevel);
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
