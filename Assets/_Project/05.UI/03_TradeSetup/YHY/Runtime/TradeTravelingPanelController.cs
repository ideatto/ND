using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ND.Framework;
using FrameworkSaveData = ND.Framework.SaveData;
using FrameworkTradeProgressState = ND.Framework.TradeProgressState;

/// <summary>Displays the current Framework traveling state without changing trade state.</summary>
public sealed class TradeTravelingPanelController : MonoBehaviour
{
    [SerializeField] private TMP_Text routeText;
    [SerializeField] private TMP_Text remainingTimeText;
    [SerializeField] private TMP_Text foodText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Slider progressSlider;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.2f;
    private float nextRefresh;

    private void OnEnable() { Refresh(); }
    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    public void Refresh()
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        FrameworkSaveData save = root != null ? root.CurrentSaveData : null;
        TradeProgressViewData view = TradeProgressViewDataBuilder.Build(save, root);
        if (view == null)
        {
            Set(routeText, "출발지 → 목적지"); Set(remainingTimeText, "남은 시간 --:--");
            Set(foodText, "식량 --"); Set(statusText, "진행 중인 무역이 없습니다.");
            if (progressSlider != null) progressSlider.value = 0f;
            return;
        }
        Set(routeText, $"{view.fromTownName} → {view.toTownName}");
        Set(remainingTimeText, $"남은 시간 {Format(view.remainingTravelTime)}");
        Set(foodText, view.statusMessage);
        Set(statusText, view.statusTitle);
        if (progressSlider != null) progressSlider.value = view.normalizedProgress;
    }

    private static void Set(TMP_Text target, string value) { if (target != null) target.text = value; }
    private static string Format(float seconds)
    {
        TimeSpan t = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        return t.TotalHours >= 1d ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}" : $"{t.Minutes:00}:{t.Seconds:00}";
    }
}

public static class TradeProgressViewDataBuilder
{
    public static TradeProgressViewData Build(FrameworkSaveData save, FrameworkRoot root)
    {
        string caravanId = save?.selectedCaravanId;
        if (!SaveDataLookup.TryGetTradeProgress(save, caravanId, out var progress))
        {
            progress = save?.tradeProgress;
            caravanId = progress?.caravanId;
        }

        return Build(save, root, caravanId, progress?.activeTradeId);
    }

    public static TradeProgressViewData Build(
        FrameworkSaveData save,
        FrameworkRoot root,
        string caravanId,
        string tradeId)
    {
        if (save == null
            || string.IsNullOrWhiteSpace(caravanId)
            || string.IsNullOrWhiteSpace(tradeId)
            || !SaveDataLookup.TryGetTradeProgress(save, caravanId, out var progress)
            || progress == null
            || progress.state != FrameworkTradeProgressState.Traveling
            || !string.Equals(progress.caravanId, caravanId, StringComparison.Ordinal)
            || !string.Equals(progress.activeTradeId, tradeId, StringComparison.Ordinal))
        {
            return null;
        }

        DateTime now = root?.GameTime != null ? root.GameTime.CurrentUtc : DateTime.UtcNow;
        DateTime start = SafeUtc(progress.tradeStartUtcTick);
        DateTime end = SafeUtc(progress.expectedTradeEndUtcTick);
        float total = Mathf.Max(0f, (float)(end - start).TotalSeconds);
        float elapsed = Mathf.Clamp((float)(now - start).TotalSeconds, 0f, total);
        float normalized = total > 0f ? Mathf.Clamp01(elapsed / total) : 0f;

        string from = "출발지", to = "목적지";
        if (root?.SharedGameData != null
            && root.SharedGameData.TryGetRoute(progress.activeRouteId, out SharedRouteDefinition route))
        {
            from = TownName(root.SharedGameData, route.FromTownId);
            to = TownName(root.SharedGameData, route.ToTownId);
        }

        CaravanData caravan = SaveDataLookup.TryGetCaravan(save, caravanId, out var caravanSave)
            ? CaravanSaveDataMapper.ToRuntime(caravanSave)
            : null;
        float food = caravan != null
            ? Mathf.Max(0f, CaravanCalculator.GetRemainingFood(caravan))
            : 0f;
        bool depleted = caravan != null && caravan.runFoodDepleted;
        return new TradeProgressViewData
        {
            activeTradeId = progress.activeTradeId,
            activeRouteId = progress.activeRouteId,
            fromTownName = from,
            toTownName = to,
            totalTravelTime = total,
            elapsedTravelTime = elapsed,
            remainingTravelTime = Mathf.Max(0f, total - elapsed),
            normalizedProgress = normalized,
            statusTitle = depleted ? "먹이 부족" : "무역 진행 중",
            statusMessage = depleted ? $"먹이 부족 · 남은 먹이 {food:0.#}" : $"현재 먹이 {food:0.#}",
            canCancel = false,
            isCompleted = normalized >= 1f
        };
    }
    private static DateTime SafeUtc(long ticks) { return ticks > DateTime.MinValue.Ticks && ticks < DateTime.MaxValue.Ticks ? new DateTime(ticks, DateTimeKind.Utc) : DateTime.UtcNow; }
    private static string TownName(ISharedGameDataProvider data, string id) { return data.TryGetTown(id, out SharedTownDefinition town) && !string.IsNullOrWhiteSpace(town.DisplayName) ? town.DisplayName : id; }
}
