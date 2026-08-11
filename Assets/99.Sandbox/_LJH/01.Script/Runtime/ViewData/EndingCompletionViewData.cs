using System;

/// <summary>
/// 엔딩 건물의 저장된 건설 상태만 UI에 전달한다.
/// 표시 이름은 BuildData가 소유하며, 이 타입은 과거 저장 이름만 호환한다.
/// </summary>
public sealed class EndingCompletionViewData
{
    // 과거 빌드가 저장한 이름이다. 신규 표시와 신규 저장의 권위로 사용하지 않는다.
    public const string LegacyEndingBuildingDisplayName = "황금 침대";

    public bool IsCompleted { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;

    public static EndingCompletionViewData Build(
        ND.Framework.SaveData saveData,
        string endingBuildingDisplayName)
    {
        string currentName = endingBuildingDisplayName?.Trim() ?? string.Empty;
        bool completed = false;
        if (saveData?.player?.villageBuildings != null)
        {
            foreach (ND.Framework.VillageBuildingSaveData building in saveData.player.villageBuildings)
            {
                if (building != null
                    && IsEndingBuildingName(building.displayName, currentName)
                    && building.level >= 1)
                {
                    completed = true;
                    break;
                }
            }
        }

        return new EndingCompletionViewData
        {
            IsCompleted = completed,
            DisplayName = currentName
        };
    }

    public static bool IsEndingBuildingName(string candidate, string currentDisplayName)
    {
        return (!string.IsNullOrWhiteSpace(currentDisplayName)
                && string.Equals(candidate, currentDisplayName, StringComparison.Ordinal))
            || string.Equals(candidate, LegacyEndingBuildingDisplayName, StringComparison.Ordinal);
    }
}
