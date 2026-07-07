using UnityEngine;

[System.Serializable]
public class TradeItemBunddle
{
    [SerializeField] private TradeItemData itemData;
    [SerializeField] private int amount;

    public TradeItemData ItemData => itemData;
    public int Amount => amount;
}
