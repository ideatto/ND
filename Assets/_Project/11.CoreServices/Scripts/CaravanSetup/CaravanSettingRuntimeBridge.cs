using System;
using System.Collections.Generic;
using ND.Framework;
using UnityEngine;

/// <summary>
/// Scene-facing adapter for the Framework-owned Caravan setting application service.
/// It owns only Unity content-object mapping; SaveData and commands remain in Framework.
/// </summary>
[DisallowMultipleComponent]
public sealed class CaravanSettingRuntimeBridge : MonoBehaviour,
    ICaravanSettingViewDataProvider,
    ICaravanSettingCommand,
    ICaravanLoadSettingViewDataProvider,
    ICaravanLoadSettingCommand,
    ICaravanCargoCatalogProvider,
    ITradePrepareCaravanOptionProvider
{
    [SerializeField] private TradeItemData[] tradeItemAssets = Array.Empty<TradeItemData>();

    private readonly Dictionary<string, TradeItemData> tradeItemsById =
        new Dictionary<string, TradeItemData>(StringComparer.Ordinal);

    private CaravanSettingApplicationService Service => FrameworkRoot.Instance?.CaravanSetting;

    private void Awake()
    {
        RebuildAssetIndex();
        Service?.ConfigureTradeItemAssetResolver(ResolveTradeItem);
    }

    public CaravanSettingViewData GetSetting(string caravanId) => Service?.GetSetting(caravanId);

    public CaravanSettingCommandResult Execute(CaravanSettingDraft draft) => Service?.Execute(draft)
        ?? CaravanSettingCommandResult.Failure(
            CaravanSettingFailureCodes.ServiceUnavailable,
            "The Caravan setting service is unavailable.");

    public CaravanLoadSettingViewData GetLoadSetting(string caravanId) => Service?.GetLoadSetting(caravanId);

    CaravanLoadSettingCommandResult ICaravanLoadSettingCommand.Execute(CaravanLoadSettingDraft draft)
        => Service != null
            ? ((ICaravanLoadSettingCommand)Service).Execute(draft)
            : CaravanLoadSettingCommandResult.Failure(
                CaravanLoadSettingFailureCodes.ServiceUnavailable,
                "The Caravan cargo service is unavailable.");

    public CaravanCargoCatalogData GetCargoCatalog(string caravanId) => Service?.GetCargoCatalog(caravanId);

    public TradePrepareCaravanOptionViewData[] GetOptions() => Service?.GetOptions()
        ?? Array.Empty<TradePrepareCaravanOptionViewData>();

    private TradeItemData ResolveTradeItem(string itemId)
    {
        tradeItemsById.TryGetValue(Normalize(itemId), out TradeItemData item);
        return item;
    }

    private void RebuildAssetIndex()
    {
        tradeItemsById.Clear();
        foreach (TradeItemData item in tradeItemAssets ?? Array.Empty<TradeItemData>())
        {
            string id = Normalize(item != null ? item.ItemId : null);
            if (!string.IsNullOrEmpty(id)) tradeItemsById[id] = item;
        }
    }

    private static string Normalize(string value) => value?.Trim() ?? string.Empty;
}
