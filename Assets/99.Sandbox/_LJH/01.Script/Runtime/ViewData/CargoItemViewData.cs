using UnityEngine;

[System.Serializable]
public class CargoItemViewData
{
    public string itemId;
    public string displayName;
    public Sprite icon;
    public TradeItemCategory category;

    public int quantity;
    public float unitWeight;
    public float totalWeight;

    public long purchaseUnitPrice;
    public long estimatedSellUnitPrice;
    public long totalPurchasePrice;
}
