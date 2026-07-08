using UnityEngine;

[System.Serializable]
public class TradeItemViewData
{
    public string itemId;
    public string displayName;
    public Sprite icon;
    public string description;

    public long purchasePrice;
    public long sellPrice;
    public int ownedAmount;
    public int selectedAmount;

    public bool canBuy;
    public bool canSell;
    public string disabledReason;
}
