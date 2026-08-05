using System;
using ND.UI.CargoSell;
using UnityEngine;

/// <summary>
/// Read-only snapshot rendered by CargoSellPopup. It carries explicit trade identity and never
/// exposes mutable SaveData or TradeItemData instances to the view.
/// </summary>
[Serializable]
public sealed class CargoSellViewData
{
    public string caravanId = string.Empty;
    public string tradeId = string.Empty;
    public string caravanDisplayName = string.Empty;
    public string destinationTownName = string.Empty;
    public CargoSellCargoItemViewData[] cargoItems = Array.Empty<CargoSellCargoItemViewData>();
    public CargoSellPendingSaleRowViewData[] pendingItems = Array.Empty<CargoSellPendingSaleRowViewData>();
    public int cargoQuantity;
    public int pendingTypeCount;
    public int pendingQuantity;
    public long totalRevenue;
    public float currentLoad;
    public float maximumLoad;
    public bool canConfirm;
    public string message = string.Empty;
}

/// <summary>
/// One purchase-price group in the Caravan Cargo area. Equal item IDs with different purchase
/// prices remain separate rows, while physical slot and load rules may still aggregate by item ID.
/// </summary>
[Serializable]
public sealed class CargoSellCargoItemViewData
{
    public string itemId = string.Empty;
    public long purchaseUnitPrice;
    public string displayName = string.Empty;
    public string description = string.Empty;
    public Sprite icon;
    public int cargoQuantity;
    public int selectedSellQuantity;
    public long sellUnitPrice;
    public float unitWeight;

    public long lineRevenue
    {
        get
        {
            decimal value = (decimal)Math.Max(0L, sellUnitPrice) * Math.Max(0, selectedSellQuantity);
            return value >= long.MaxValue ? long.MaxValue : (long)value;
        }
    }
}
