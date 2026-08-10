using System.Collections.Generic;

namespace ND.Framework
{
    /// <summary>
    /// Derives BaseCamp-owned progression from authoritative building save data.
    /// Caravan occupancy never unlocks another slot.
    /// </summary>
    public static class BaseCampProgressionPolicy
    {
        public const int MaxCaravanSlotCount = 4;
        public const int EndingBuildingUnlockLevel = 5;

        public static int GetBaseCampLevel(IReadOnlyList<VillageBuildingSaveData> buildings)
        {
            return BaseCampBuildingLevelPolicy.TryGetUniqueLevel(
                buildings,
                BaseCampBuildingLevelPolicy.BaseCampDisplayName,
                out int level)
                ? level
                : 0;
        }

        public static int GetUnlockedCaravanSlotCount(
            IReadOnlyList<VillageBuildingSaveData> buildings)
        {
            return System.Math.Min(
                GetBaseCampLevel(buildings),
                MaxCaravanSlotCount);
        }

        public static bool IsCaravanSlotUnlocked(
            IReadOnlyList<VillageBuildingSaveData> buildings,
            int slotIndex)
        {
            return slotIndex >= 0
                && slotIndex < GetUnlockedCaravanSlotCount(buildings);
        }

        public static bool IsEndingBuildingUnlocked(
            IReadOnlyList<VillageBuildingSaveData> buildings)
        {
            return GetBaseCampLevel(buildings) >= EndingBuildingUnlockLevel;
        }
    }
}
