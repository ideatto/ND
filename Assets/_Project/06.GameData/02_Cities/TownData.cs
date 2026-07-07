using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TownData", menuName = "GameData/Town")]
public class TownData : ScriptableObject
{
    [Header("Identity")]
    public string TownId;
    public string DisplayName;
    [TextArea]
    public string Description;
    public string IconId;

    [Header("Trade")]
    public List<TradeItemData> AvailableItems = new List<TradeItemData>();
}
