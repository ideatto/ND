using UnityEngine;

[System.Serializable]
public class TradeItemViewData
{
    public string itemId;
    public string displayName;
    public Sprite icon;
    public string description;

    public TradeItemRarity rarity;
    public TradeItemCategory category;

    public long purchasePrice;
    public long sellPrice;

    public int ownedAmount;
    public int selectedBuyAmount;
    public int selectedSellAmount;

    // Content-authored quantity ceiling from TradeItemData, not live per-town market stock.
    // This value is a UI/validation limit only and must not be persisted as committed inventory.
    public int contentQuantityLimit;
    public bool hasAuthoritativeStock;

    public float unitWeight;
    public float selectedWeight;

    public bool canBuy;
    public bool canSell;

    public string buyDisabledReason;
    public string sellDisabledReason;
}
