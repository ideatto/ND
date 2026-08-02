using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Framework
{
    public readonly struct WarehouseState
    {
        public WarehouseState(int level, int slotCount)
        {
            Level = Math.Max(0, level);
            SlotCount = Math.Max(0, slotCount);
        }
        public int Level { get; }
        public int SlotCount { get; }
        public bool Exists => Level > 0;
        public bool CanOpen => Exists && SlotCount > 0;
    }

    /// <summary>
    /// 창고 진행도의 단일 조회 경계다. 레벨은 중복 저장하지 않고 SaveData만 읽는다.
    /// MainUI는 추후 Evaluate(...).CanOpen으로 버튼/패널 노출 여부를 결정하면 된다.
    /// </summary>
    public static class WarehouseFunction
    {
        
        public const string BaseTownId = "BaseCamp";
public const string BuildingDisplayName = "창고";

/// <summary>Derives Warehouse existence and capacity only from persisted building progress.</summary>
        public static WarehouseState Evaluate(SaveData saveData)
        {
            int level = ResolveLevel(saveData?.player?.villageBuildings);
            return new WarehouseState(level, GetSlotCount(level));
        }

/// <summary>
        /// 현재 정책은 창고 레벨당 10칸이다. 상위 레벨도 같은 규칙으로 확장된다.
        /// 정책이 비선형으로 바뀌는 시점에만 별도 정책 데이터로 분리한다.
        /// </summary>
        public static int GetSlotCount(int level)
        {
            return Math.Max(0, level) * 10;
        }


        /// <summary>Uses the highest duplicate level so malformed saves do not silently shrink capacity.</summary>
        public static int ResolveLevel(IReadOnlyList<VillageBuildingSaveData> buildings)
        {
            if (buildings == null) return 0;
            int level = 0;
            for (int i = 0; i < buildings.Count; i++)
            {
                VillageBuildingSaveData building = buildings[i];
                if (building != null && string.Equals(building.displayName, BuildingDisplayName, StringComparison.Ordinal))
                    level = Math.Max(level, building.level);
            }
            return Math.Max(0, level);
        }
    }
}
