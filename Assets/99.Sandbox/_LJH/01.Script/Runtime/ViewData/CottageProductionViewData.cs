using System;
using ND.Framework;
using UnityEngine;

public enum CottageProductViewState
{
    Uninitialized,
    Producing,
    Full,
    Stopped
}

public sealed class CottageProductViewData
{
    public string DisplayName = string.Empty;
    public Sprite Icon;
    public int StoredCount;
    public int Capacity;
    public int RemainingSeconds;
    public CottageProductViewState State;
    public bool CanReceive;
}

public sealed class CottageProductionViewData
{
    public string BuildingDisplayName = string.Empty;
    public int CottageLevel;
    public CottageProductViewData Wagon = new CottageProductViewData();
    public CottageProductViewData DraftAnimal = new CottageProductViewData();
    public bool CanReceiveAll;
    public bool IsAllStorageFull;
}

public static class CottageProductionViewDataBuilder
{
    /// <summary>
    /// SaveData와 SharedGameData를 화면 전용 불변 스냅샷으로 변환한다.
    /// View는 이 결과만 표시하며 생산 시간이나 저장 데이터를 직접 변경하지 않는다.
    /// </summary>
    public static bool TryBuild(
        FrameworkRoot root,
        out CottageProductionViewData viewData)
    {
        viewData = null;
        if (root?.CurrentSaveData?.player?.cottageProduction == null
            || root.CottageProduction?.Config == null
            || root.GameTime == null
            || root.SharedGameData == null
            || !root.SharedGameData.IsLoaded)
            return false;

        CottageProductionData config = root.CottageProduction.Config;
        int level = ResolveLevel(root.CurrentSaveData, config.BuildingDisplayName);
        if (level < 1 || !config.TryGetLevel(level, out CottageProductionLevelSetting setting))
            return false;

        CottageProductionSaveData state =
            root.CurrentSaveData.player.cottageProduction;
        root.SharedGameData.TryGetWagon(
            state.storedWagonContentId?.Length > 0
                ? state.storedWagonContentId
                : config.WagonContentId,
            out SharedWagonDefinition wagon);
        root.SharedGameData.TryGetDraftAnimal(
            state.storedDraftAnimalContentId?.Length > 0
                ? state.storedDraftAnimalContentId
                : config.DraftAnimalContentId,
            out SharedDraftAnimalDefinition animal);

        DateTime now = root.GameTime.CurrentUtc;
        var result = new CottageProductionViewData
        {
            BuildingDisplayName = config.BuildingDisplayName,
            CottageLevel = level
        };
        result.Wagon = BuildProduct(
            wagon?.DisplayName ?? config.WagonContentId,
            wagon?.Icon,
            state.storedWagonCount,
            setting.wagonCapacity,
            state.initialized,
            state.nextWagonProductionUtcTicks,
            now);
        result.DraftAnimal = BuildProduct(
            animal?.DisplayName ?? config.DraftAnimalContentId,
            animal?.Icon,
            state.storedDraftAnimalCount,
            setting.draftAnimalCapacity,
            state.initialized,
            state.nextDraftAnimalProductionUtcTicks,
            now);
        result.CanReceiveAll = result.Wagon.CanReceive || result.DraftAnimal.CanReceive;
        result.IsAllStorageFull =
            result.Wagon.State == CottageProductViewState.Full
            && result.DraftAnimal.State == CottageProductViewState.Full;
        viewData = result;
        return true;
    }

    private static CottageProductViewData BuildProduct(
        string displayName,
        Sprite icon,
        int count,
        int capacity,
        bool initialized,
        long nextTicks,
        DateTime now)
    {
        var item = new CottageProductViewData
        {
            DisplayName = displayName,
            Icon = icon,
            StoredCount = Math.Max(0, count),
            Capacity = Math.Max(0, capacity),
            CanReceive = count > 0
        };
        if (!initialized)
        {
            item.State = CottageProductViewState.Uninitialized;
            return item;
        }
        if (capacity <= 0)
        {
            item.State = CottageProductViewState.Stopped;
            return item;
        }
        if (count >= capacity)
        {
            item.State = CottageProductViewState.Full;
            return item;
        }
        item.State = CottageProductViewState.Producing;
        item.RemainingSeconds = nextTicks > now.Ticks
            ? Mathf.CeilToInt((float)TimeSpan.FromTicks(nextTicks - now.Ticks).TotalSeconds)
            : 0;
        return item;
    }

    private static int ResolveLevel(ND.Framework.SaveData saveData, string displayName)
    {
        if (saveData?.player?.villageBuildings == null) return 0;
        int level = 0;
        foreach (ND.Framework.VillageBuildingSaveData building in saveData.player.villageBuildings)
        {
            if (building != null && string.Equals(
                    building.displayName, displayName, StringComparison.Ordinal))
                level = Math.Max(level, building.level);
        }
        return level;
    }
}
