using System;
using System.Collections.Generic;
using System.Linq;
using ND.Framework;
using ND.UI.CargoSell;
using ND.UI.Market;

/// <summary>
/// Projects persisted Cargo price groups and the active Market sell-price snapshot into immutable
/// UI values. It performs no transaction, SaveData mutation, or price calculation of its own.
/// </summary>
public static class CargoSellViewDataBuilder
{
    public static CargoSellViewData Build(
        string caravanId,
        string tradeId,
        string caravanDisplayName,
        string destinationTownName,
        IEnumerable<CargoEntrySaveData> savedCargo,
        IReadOnlyList<MarketTradeItemState> marketItems,
        IReadOnlyList<CargoSellPendingSaleRowViewData> pendingItems,
        float maximumLoad)
    {
        Dictionary<string, MarketTradeItemState> marketByItemId =
            (marketItems ?? Array.Empty<MarketTradeItemState>())
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ItemId))
            .GroupBy(item => item.ItemId.Trim(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

        CargoSellPendingSaleRowViewData[] pending =
            (pendingItems ?? Array.Empty<CargoSellPendingSaleRowViewData>())
            .Where(item => item != null
                && !string.IsNullOrWhiteSpace(item.ItemId)
                && item.Quantity > 0)
            .Select(ClonePending)
            .ToArray();

        Dictionary<CargoPriceGroupKey, int> selectedByGroup = pending
            .GroupBy(item => new CargoPriceGroupKey(item.ItemId, item.PurchaseUnitPrice))
            .ToDictionary(
                group => group.Key,
                group => group.Sum(item => Math.Max(0, item.Quantity)));

        CargoSellCargoItemViewData[] cargo =
            (savedCargo ?? Enumerable.Empty<CargoEntrySaveData>())
            .Where(entry => entry?.item != null
                && entry.quantity > 0
                && !string.IsNullOrWhiteSpace(entry.item.itemId))
            .GroupBy(entry => new CargoPriceGroupKey(
                entry.item.itemId,
                entry.item.purchaseUnitPrice))
            .OrderBy(group => group.Key.ItemId, StringComparer.Ordinal)
            .ThenBy(group => group.Key.PurchaseUnitPrice)
            .Select(group =>
            {
                CargoEntrySaveData saved = group.First();
                marketByItemId.TryGetValue(group.Key.ItemId, out MarketTradeItemState market);
                TradeItemData definition = market?.Item;
                selectedByGroup.TryGetValue(group.Key, out int selected);
                int quantity = group.Sum(entry => Math.Max(0, entry.quantity));
                return new CargoSellCargoItemViewData
                {
                    itemId = group.Key.ItemId,
                    purchaseUnitPrice = group.Key.PurchaseUnitPrice,
                    displayName = definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName)
                        ? definition.DisplayName
                        : saved.item.itemName ?? group.Key.ItemId,
                    description = definition != null ? definition.Description ?? string.Empty : string.Empty,
                    icon = definition != null ? definition.Icon : null,
                    cargoQuantity = quantity,
                    selectedSellQuantity = Math.Min(quantity, Math.Max(0, selected)),
                    sellUnitPrice = market != null ? Math.Max(0L, market.SellUnitPrice) : 0L,
                    unitWeight = definition != null
                        ? Math.Max(0f, definition.Weight)
                        : Math.Max(0f, saved.item.weight)
                };
            })
            .ToArray();

        int cargoQuantity = cargo.Sum(item => Math.Max(0, item.cargoQuantity));
        int pendingQuantity = pending.Sum(item => Math.Max(0, item.Quantity));
        decimal revenue = pending.Sum(item =>
            (decimal)Math.Max(0L, item.SellUnitPrice) * Math.Max(0, item.Quantity));
        double load = cargo.Sum(item =>
            (double)Math.Max(0f, item.unitWeight) * Math.Max(0, item.cargoQuantity));

        return new CargoSellViewData
        {
            caravanId = caravanId ?? string.Empty,
            tradeId = tradeId ?? string.Empty,
            caravanDisplayName = caravanDisplayName ?? string.Empty,
            destinationTownName = destinationTownName ?? string.Empty,
            cargoItems = cargo,
            pendingItems = pending,
            cargoQuantity = cargoQuantity,
            pendingTypeCount = pending
                .Select(item => new CargoPriceGroupKey(item.ItemId, item.PurchaseUnitPrice))
                .Distinct()
                .Count(),
            pendingQuantity = pendingQuantity,
            totalRevenue = revenue >= long.MaxValue ? long.MaxValue : (long)revenue,
            currentLoad = load >= float.MaxValue ? float.PositiveInfinity : (float)load,
            maximumLoad = Math.Max(0f, maximumLoad),
            canConfirm = !string.IsNullOrWhiteSpace(caravanId)
                && !string.IsNullOrWhiteSpace(tradeId)
        };
    }

    private static CargoSellPendingSaleRowViewData ClonePending(
        CargoSellPendingSaleRowViewData source)
    {
        return new CargoSellPendingSaleRowViewData
        {
            ItemId = source.ItemId?.Trim() ?? string.Empty,
            PurchaseUnitPrice = Math.Max(0L, source.PurchaseUnitPrice),
            DisplayName = source.DisplayName ?? string.Empty,
            Icon = source.Icon,
            Quantity = Math.Max(0, source.Quantity),
            SellUnitPrice = Math.Max(0L, source.SellUnitPrice)
        };
    }

    private readonly struct CargoPriceGroupKey : IEquatable<CargoPriceGroupKey>
    {
        public CargoPriceGroupKey(string itemId, long purchaseUnitPrice)
        {
            ItemId = itemId?.Trim() ?? string.Empty;
            PurchaseUnitPrice = Math.Max(0L, purchaseUnitPrice);
        }

        public string ItemId { get; }
        public long PurchaseUnitPrice { get; }

        public bool Equals(CargoPriceGroupKey other) =>
            string.Equals(ItemId, other.ItemId, StringComparison.Ordinal)
            && PurchaseUnitPrice == other.PurchaseUnitPrice;

        public override bool Equals(object obj) => obj is CargoPriceGroupKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ItemId != null ? StringComparer.Ordinal.GetHashCode(ItemId) : 0) * 397)
                    ^ PurchaseUnitPrice.GetHashCode();
            }
        }
    }
}
