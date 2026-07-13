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
    [SerializeField] private float combatCapability;

    [Header("Mercenary_Unlocked_Default")]
    [SerializeField] private bool unlockedByDefault;


    #region
    public string Id => mercenaryId;
    public string MercenaryId => mercenaryId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public string Description => description;
    public TradeItemRarity Rarity => rarity;
    public long BaseBuyPrice => baseBuyPrice;
    public float CombatCapability => combatCapability;
    public bool UnlockedByDefault => unlockedByDefault;
    #endregion
}
