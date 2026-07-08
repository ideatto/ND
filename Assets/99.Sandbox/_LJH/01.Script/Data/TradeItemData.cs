using UnityEngine;

[CreateAssetMenu(fileName = "TradeItem_ItemName", menuName = "TradeItem")]
public class TradeItemData : ScriptableObject
{
    [Header("Item_Default_Info")]
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Item_Description")]
    [TextArea(3, 10)]
    [SerializeField] private string description;

    [Header("Item_Trade_Info")]
    [SerializeField] private TradeItemRarity rarity;
    [SerializeField] private TradeItemCategory category;
    [SerializeField] private int baseBuyPrice;
    [SerializeField] private int baseSellPrice;
    [SerializeField] private float weight;

    [Header("Item_Consumable_Info")]
    [SerializeField] private bool isConsumable;

    [Header("Item_Stack_Info")]
    [SerializeField] private bool canStack;
    [SerializeField] private int maxCount;

    [Header("Modify_ItemPrice_Info")]
    [SerializeField] private bool modified;
    [SerializeField] private PriceModifierInput[] modifiers;

    [Header("Local_Trade_Info")]
    [SerializeField] private bool localSpecialty;

    #region Public Properties
    public string ItemId => itemId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public string Description => description;
    public TradeItemRarity Rarity => rarity;
    public TradeItemCategory Category => category;
    public int BaseBuyPrice => Mathf.Max(0, baseBuyPrice);
    public int BaseSellPrice => Mathf.Max(0, baseSellPrice);
    public float Weight => Mathf.Max(0, weight);
    public bool IsConsumable => isConsumable;
    public bool CanStack => canStack;
    public int MaxCount => Mathf.Max(1, maxCount);
    public bool Modified => modified;
    public PriceModifierInput[] Modifiers => modifiers != null ? (PriceModifierInput[])modifiers.Clone() : new PriceModifierInput[0];
    public bool LocalSpecialty => localSpecialty;
    #endregion
}
