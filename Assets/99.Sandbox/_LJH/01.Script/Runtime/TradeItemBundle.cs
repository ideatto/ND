using UnityEngine;

[System.Serializable]
public class TradeItemBundle
{
    [SerializeField] private string itemId;
    [SerializeField] private int purchasePrice;
    [SerializeField] private int amount;

    public string ItemId => itemId;
    public int PurchasePrice => purchasePrice;
    public int Amount => amount;
}
