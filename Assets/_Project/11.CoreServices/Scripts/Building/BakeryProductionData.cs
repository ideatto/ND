using System;
using UnityEngine;

namespace ND.Framework
{
    [Serializable]
    public sealed class BakeryProductionLevelSetting
    {
        [Min(1)] public int level = 1;
        [Min(1)] public int storageCapacity = 30;
    }

    [CreateAssetMenu(fileName = "BakeryProductionData", menuName = "ND/Building/Bakery Production Data")]
    public sealed class BakeryProductionData : ScriptableObject
    {
        public const string ResourceName = "BakeryProductionData";

        [SerializeField] private string buildingDisplayName = "빵집";
        [SerializeField] private string breadContentId = "Bread";
        [SerializeField, Min(1)] private int productionIntervalSeconds = 60;
        [SerializeField] private BakeryProductionLevelSetting[] levels =
        {
            new BakeryProductionLevelSetting { level = 1, storageCapacity = 5 },
            new BakeryProductionLevelSetting { level = 2, storageCapacity = 10 },
            new BakeryProductionLevelSetting { level = 3, storageCapacity = 20 },
            new BakeryProductionLevelSetting { level = 4, storageCapacity = 30 },
            new BakeryProductionLevelSetting { level = 5, storageCapacity = 40 }
        };

        public string BuildingDisplayName => buildingDisplayName;
        public string BreadContentId => breadContentId;
        public int ProductionIntervalSeconds => productionIntervalSeconds;

        public bool TryGetLevel(int level, out BakeryProductionLevelSetting setting)
        {
            setting = null;
            if (levels == null || level < 1) return false;
            foreach (BakeryProductionLevelSetting candidate in levels)
            {
                if (candidate == null || candidate.level != level) continue;
                if (setting != null) return false;
                setting = candidate;
            }
            return setting != null;
        }

        public bool Validate(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(buildingDisplayName) || string.IsNullOrWhiteSpace(breadContentId))
                return Fail("BAKERY_IDENTITY_INVALID", out error);
            if (productionIntervalSeconds < 1 || levels == null || levels.Length != 5)
                return Fail("BAKERY_SETTING_INVALID", out error);
            int previous = 0;
            for (int level = 1; level <= 5; level++)
            {
                if (!TryGetLevel(level, out BakeryProductionLevelSetting value)
                    || value.storageCapacity < 1 || value.storageCapacity < previous)
                    return Fail("BAKERY_LEVEL_SETTING_INVALID", out error);
                previous = value.storageCapacity;
            }
            return true;
        }

        private static bool Fail(string value, out string error) { error = value; return false; }
    }
}
