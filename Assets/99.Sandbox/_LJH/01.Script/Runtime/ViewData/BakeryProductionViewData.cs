using System;
using ND.Framework;
using UnityEngine;

/// <summary>
/// 빵집 Popup이 표시하는 읽기 전용 데이터다. UI가 SaveData를 직접 해석하거나 변경하지 않도록
/// 생산 상태, 레벨 설정, 카탈로그 아이콘을 한 번 조합해 Presenter에 전달한다.
/// </summary>
public sealed class BakeryProductionViewData
{
    public string DisplayName = "빵";
    public Sprite Icon;
    public int StoredCount;
    public int Capacity;
    public int RemainingSeconds;
    public bool IsFull;
    public bool CanReceive;
}

public static class BakeryProductionViewDataBuilder
{
    public static bool TryBuild(FrameworkRoot root, out BakeryProductionViewData data)
    {
        data = null;
        BakeryProductionData config = root?.BakeryProduction?.Config;
        ND.Framework.SaveData save = root?.CurrentSaveData;
        if (config == null || save?.player?.bakeryProduction == null
            || root.SharedGameData == null || root.GameTime == null)
            return false;

        int level = ResolveBuildingLevel(save, config.BuildingDisplayName);
        if (!config.TryGetLevel(level, out BakeryProductionLevelSetting setting)
            || !root.SharedGameData.TryGetTradeItem(config.BreadContentId,
                out SharedTradeItemDefinition item))
            return false;

        BakeryProductionSaveData state = save.player.bakeryProduction;
        long remainingTicks = Math.Max(
            0L,
            state.nextProductionUtcTicks - root.GameTime.CurrentUtc.Ticks);
        data = new BakeryProductionViewData
        {
            DisplayName = item.DisplayName,
            Icon = item.Icon,
            StoredCount = Math.Max(0, state.storedBreadCount),
            Capacity = setting.storageCapacity,
            RemainingSeconds = (int)Math.Ceiling(
                remainingTicks / (double)TimeSpan.TicksPerSecond),
            IsFull = state.storedBreadCount >= setting.storageCapacity,
            // 창고가 없거나 가득 찬 사유는 버튼 클릭 후 Service 결과를 Notice로 안내한다.
            CanReceive = state.storedBreadCount > 0
        };
        return true;
    }

    private static int ResolveBuildingLevel(ND.Framework.SaveData save, string displayName)
    {
        int level = 0;
        if (save?.player?.villageBuildings == null) return level;
        foreach (VillageBuildingSaveData building in save.player.villageBuildings)
        {
            if (building != null && string.Equals(
                    building.displayName, displayName, StringComparison.Ordinal))
                level = Math.Max(level, building.level);
        }
        return level;
    }
}
