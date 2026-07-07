using UnityEngine;

[CreateAssetMenu(fileName = "TradeItem_ItemName", menuName = "TradeItem")]
public class TradeItemData : ScriptableObject
{
    [Header("Item_Default_Info")]
    [SerializeField] private string itemID;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Item_Description")]
    [TextArea(3, 10)]
    [SerializeField] private string description;

    [Header("Item_Trade_Info")]
    [SerializeField] private TradeItemRarity rarity;
    [SerializeField] private TradeItemCategory category;
    [SerializeField] private int defaultPrice;
    [SerializeField] private float weight;

    [Header("Item_Consumable_Info")]
    [SerializeField] private bool isConsumable;

    [Header("Item_Stack_Info")]
    [SerializeField] private bool canStack;
    [SerializeField] private int maxCount;

    [Header("Seasonal_Trade_Info")]
    [SerializeField] private bool seasonInfluenced;
    [SerializeField] private SeasonInfluenceData[] seasonInfluenceData;

    [Header("Disaster_Trade_Info")]
    [SerializeField] private bool disasterInfluenced;
    [SerializeField] private DisasterInfluenceData[] disasterInfluenceData;

    [Header("Local_Trade_Info")]
    [SerializeField] private bool localSpecialty;

    #region
    public string ItemID => itemID;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public string Description => description;
    public TradeItemRarity Rarity => rarity;
    public TradeItemCategory Category => category;
    public int DefaultPrice => defaultPrice >= 0 ? defaultPrice : 0;
    public float Weight => weight >= 0 ? weight : 0;
    public bool IsConsumable => isConsumable;
    public bool CanStack => canStack;
    public int MaxCount => maxCount > 1 ? maxCount : 2;
    public bool SeasonInfluenced => seasonInfluenced;
    public bool DisasterInfluenced => disasterInfluenced;
    public bool LocalSpecialty => localSpecialty;
    #endregion
}
