using UnityEngine;

[CreateAssetMenu(fileName = "Mercenary_MercenaryName", menuName = "Mercenary/MercenaryData")]
public class MercenaryData : ScriptableObject, IIdentifiableData
{
    [Header("Mercenary_Default_Info")]
    [SerializeField] private string mercenaryId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Mercenary_Description")]
    [TextArea(3, 10)]
    [SerializeField] private string description;

    [Header("Trade_Info")]
    [SerializeField] private TradeItemRarity rarity;
    [SerializeField] private long baseBuyPrice;

    [Header("Combat_Info")]
    [SerializeField] private int combatCapability;

    [Header("Mercenary_Unlocked_Default")]
    [SerializeField] private bool unlockedByDefault;

    [Header("Modify_Info")]
    [SerializeField] private bool affectModify; // for baseBuyPrice
    [SerializeField] private ModifierInput[] modifiers;

    #region
    public string Id => mercenaryId;
    public string MercenaryId => mercenaryId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public string Description => description;
    public TradeItemRarity Rarity => rarity;
    public long BaseBuyPrice => baseBuyPrice >= 0 ? baseBuyPrice : 0;
    public int CombatCapability => Mathf.Max(0, combatCapability);
    public bool UnlockedByDefault => unlockedByDefault;
    public bool AffectModify => affectModify;
    public ModifierInput[] Modifiers =>
        modifiers != null ? (ModifierInput[])modifiers.Clone() : new ModifierInput[0];
    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        baseBuyPrice = baseBuyPrice >= 0 ? baseBuyPrice : 0;
        combatCapability = Mathf.Max(0, combatCapability);
        ValidateModifierTargets();
    }

    private void ValidateModifierTargets()
    {
        if (!affectModify || modifiers == null)
            return;

        foreach (ModifierInput modifier in modifiers)
        {
            if (modifier == null || modifier.modifierBundles == null)
                continue;

            foreach (ModifierBundle bundle in modifier.modifierBundles)
            {
                if (bundle == null ||
                    bundle.modifierTarget == Target.None ||
                    bundle.modifierTarget == Target.BuyPrice)
                {
                    continue;
                }

                bundle.modifierTarget = Target.None;
                bundle.modifierOperation = Operation.None;
                bundle.value = 0f;
            }
        }
    }
#endif
}
