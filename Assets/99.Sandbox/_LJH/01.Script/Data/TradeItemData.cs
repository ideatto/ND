using UnityEngine;

[CreateAssetMenu(fileName = "TradeItem_ItemName", menuName = "TradeItem/TradeItemData")]
public class TradeItemData : ScriptableObject
{
    [Header("Default_Info")]
    [SerializeField] private string itemId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("TradeItem_Description")]
    [TextArea(3, 10)]
    [SerializeField] private string description;

    [Header("Trade_Info")]
    [SerializeField] private TradeItemRarity rarity;
    [SerializeField] private TradeItemCategory category;
    [SerializeField] private long baseBuyPrice;
    [SerializeField] private long baseSellPrice;

    [Header("TradeItem_Stack_Info")]
    [SerializeField] private bool canStack;
    [SerializeField] private int maxCount;

    [Header("Item_Weight_Info")]
    [SerializeField] private float weight;

    [Header("Item_Consumable_Info")]
    [SerializeField] private bool isConsumable;

    [Header("TradeItem_Modify_Info")]
    [SerializeField] private bool affectModify;
    [SerializeField] private ModifierInput[] modifiers;

    [Header("TradeItme_Local_Trade_Info")]
    [SerializeField] private bool localSpecialty;

    #region
    public string ItemId => itemId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public string Description => description;
    public TradeItemRarity Rarity => rarity;
    public TradeItemCategory Category => category;
    public long BaseBuyPrice => baseBuyPrice >= 0 ? baseBuyPrice : 0;
    public long BaseSellPrice => baseSellPrice >= 0 ? baseSellPrice : 0;
    public bool CanStack => canStack;
    public int MaxCount => Mathf.Max(1, maxCount);
    public float Weight => Mathf.Max(0, weight);
    public bool IsConsumable => isConsumable;
    public bool AffectModify => affectModify;
    public ModifierInput[] Modifiers => modifiers != null ? (ModifierInput[])modifiers.Clone() : new ModifierInput[0];
    public bool LocalSpecialty => localSpecialty;
    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        weight = Mathf.Max(0f, weight);
        maxCount = Mathf.Max(1, maxCount);

        ValidateModifierTargets();
        //if !affectModify -> return
        //if modifierTarget == Target.BuyPrice, but BaseBuyPrice <= 0 -> return
        //if modifierTarget == Target.SellPrice, but BaseSellPrice <= 0 -> return
    }

    private void ValidateModifierTargets()
    {
        if (!affectModify || modifiers == null)
            return;

        foreach (var modifier in modifiers)
        {
            if (modifier == null || modifier.modifierBundles == null)
                continue;

            foreach (var bundle in modifier.modifierBundles)
            {
                if (bundle == null)
                    continue;

                if (bundle.modifierTarget == Target.None)
                    continue;

                if (bundle.modifierTarget == Target.BuyPrice)
                    continue;

                if (bundle.modifierTarget == Target.SellPrice)
                    continue;

                bundle.modifierTarget = Target.None;
                bundle.modifierOperation = Operation.None;
                bundle.value = 0f;
            }
        }
    }
#endif
}
