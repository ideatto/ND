using System;
using System.Collections.Generic;

namespace ND.Framework
{
    public readonly struct TransportInventoryState
    {
        public TransportInventoryState(int level)
        {
            Level = Math.Max(0, Math.Min(5, level));
        }

        public int Level { get; }
        public int WagonSlotCount => Level * TransportInventoryFunction.WagonSlotsPerLevel;
        public int DraftAnimalSlotCount => Level * TransportInventoryFunction.DraftAnimalSlotsPerLevel;
        public bool CanOpen => Level > 0;
    }

    /// <summary>Derives Farm ownership and transport-inventory capacity from persisted building progress.</summary>
    public static class TransportInventoryFunction
    {
        public const string BuildingDisplayName = "목장";
        public const int MaximumLevel = 5;
        public const int WagonSlotsPerLevel = 10;
        public const int DraftAnimalSlotsPerLevel = 20;

        public static TransportInventoryState Evaluate(SaveData saveData)
        {
            return new TransportInventoryState(ResolveLevel(saveData?.player?.villageBuildings));
        }

        public static int ResolveLevel(IReadOnlyList<VillageBuildingSaveData> buildings)
        {
            if (buildings == null) return 0;
            int level = 0;
            for (int index = 0; index < buildings.Count; index++)
            {
                VillageBuildingSaveData building = buildings[index];
                if (building != null && string.Equals(
                        building.displayName, BuildingDisplayName, StringComparison.Ordinal))
                {
                    level = Math.Max(level, building.level);
                }
            }

            return Math.Max(0, Math.Min(MaximumLevel, level));
        }
    }
}
