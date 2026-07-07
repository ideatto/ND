using UnityEngine;

[System.Serializable]
public class TradeItemBundle
{
    [SerializeField] private string itemId;
    [SerializeField] private int purchasePrice;
    [SerializeField] private int sellPrice;
    [SerializeField] private int quantity;

    public string ItemId => itemId;
    public int PurchasePrice => purchasePrice;
    public int SellPrice => sellPrice;
    public int Quantity => quantity;
}
