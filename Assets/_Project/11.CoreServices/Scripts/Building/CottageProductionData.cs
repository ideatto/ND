using System;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>오두막 레벨별 내부 보관 한도. 생산 품목과 주기는 공통 설정을 사용한다.</summary>
    [Serializable]
    public sealed class CottageProductionLevelSetting
    {
        [Min(1)] public int level = 1;
        [Min(0)] public int wagonCapacity = 1;
        [Min(0)] public int draftAnimalCapacity = 2;
    }

    [CreateAssetMenu(
        fileName = "CottageProductionData",
        menuName = "ND/Building/Cottage Production Data")]
    public sealed class CottageProductionData : ScriptableObject
    {
        public const string ResourceName = "CottageProductionData";

        [SerializeField] private string buildingDisplayName = "오두막";
        [SerializeField] private string wagonContentId = "Wagon_M";
        [SerializeField] private string draftAnimalContentId = "Horse";
        [SerializeField, Min(1)] private int wagonProductionIntervalSeconds = 1800;
        [SerializeField, Min(1)] private int draftAnimalProductionIntervalSeconds = 600;
        [SerializeField, Min(0)] private int initialWagonCount = 1;
        [SerializeField, Min(0)] private int initialDraftAnimalCount = 2;
        [SerializeField] private CottageProductionLevelSetting[] levels =
        {
            new CottageProductionLevelSetting { level = 1, wagonCapacity = 1, draftAnimalCapacity = 2 },
            new CottageProductionLevelSetting { level = 2, wagonCapacity = 2, draftAnimalCapacity = 4 },
            new CottageProductionLevelSetting { level = 3, wagonCapacity = 3, draftAnimalCapacity = 6 }
        };

        public string BuildingDisplayName => buildingDisplayName;
        public string WagonContentId => wagonContentId;
        public string DraftAnimalContentId => draftAnimalContentId;
        public int WagonProductionIntervalSeconds => wagonProductionIntervalSeconds;
        public int DraftAnimalProductionIntervalSeconds => draftAnimalProductionIntervalSeconds;
        public int InitialWagonCount => initialWagonCount;
        public int InitialDraftAnimalCount => initialDraftAnimalCount;

        /// <summary>SO 배열 순서에 의존하지 않고 정확히 하나의 레벨 설정만 반환한다.</summary>
        public bool TryGetLevel(int level, out CottageProductionLevelSetting setting)
        {
            setting = null;
            if (levels == null || level < 1) return false;
            for (int index = 0; index < levels.Length; index++)
            {
                CottageProductionLevelSetting candidate = levels[index];
                if (candidate != null && candidate.level == level)
                {
                    if (setting != null) return false;
                    setting = candidate;
                }
            }
            return setting != null;
        }

        /// <summary>
        /// SharedGameData 로드 전에도 검사할 수 있는 구조 규칙을 검증한다.
        /// 실제 contentId 존재 여부는 CottageProductionService.ValidateCatalog에서 확인한다.
        /// </summary>
        public bool Validate(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(buildingDisplayName)
                || string.IsNullOrWhiteSpace(wagonContentId)
                || string.IsNullOrWhiteSpace(draftAnimalContentId))
            {
                error = "COTTAGE_IDENTITY_INVALID";
                return false;
            }
            if (wagonProductionIntervalSeconds < 1 || draftAnimalProductionIntervalSeconds < 1)
            {
                error = "COTTAGE_INTERVAL_INVALID";
                return false;
            }
            if (initialWagonCount < 0 || initialDraftAnimalCount < 0)
            {
                error = "COTTAGE_INITIAL_SUPPLY_INVALID";
                return false;
            }
            if (levels == null || levels.Length != 3)
            {
                error = "COTTAGE_LEVEL_COUNT_INVALID";
                return false;
            }
            for (int level = 1; level <= 3; level++)
            {
                if (!TryGetLevel(level, out CottageProductionLevelSetting setting)
                    || setting.wagonCapacity < 0
                    || setting.draftAnimalCapacity < 0)
                {
                    error = "COTTAGE_LEVEL_SETTING_INVALID";
                    return false;
                }
                if (level > 1
                    && TryGetLevel(level - 1, out CottageProductionLevelSetting previous)
                    && (setting.wagonCapacity < previous.wagonCapacity
                        || setting.draftAnimalCapacity < previous.draftAnimalCapacity))
                {
                    error = "COTTAGE_CAPACITY_MUST_NOT_DECREASE";
                    return false;
                }
            }
            if (!TryGetLevel(1, out CottageProductionLevelSetting first)
                || initialWagonCount > first.wagonCapacity
                || initialDraftAnimalCount > first.draftAnimalCapacity)
            {
                error = "COTTAGE_INITIAL_SUPPLY_EXCEEDS_CAPACITY";
                return false;
            }
            return true;
        }
    }
}
