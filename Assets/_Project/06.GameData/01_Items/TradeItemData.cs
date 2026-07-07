using UnityEngine;

[CreateAssetMenu(fileName = "TradeItemData", menuName = "GameData/Trade Item")]
public class TradeItemData : ScriptableObject
{
    [Header("Identity")]
    public string ItemId;
    public string DisplayName;
    [TextArea]
    public string Description;
    public string IconId;

    [Header("Trade")]
    public int BaseBuyPrice;
    public int BaseSellPrice;
    public int Weight;
}
