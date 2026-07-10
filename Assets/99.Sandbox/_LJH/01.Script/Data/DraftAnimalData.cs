using UnityEngine;

[CreateAssetMenu(fileName = "DraftAnimal_DraftAnimalName", menuName ="TradeItem/DraftAnimalData")]
public class DraftAnimalData : ScriptableObject, IIdentifiableData
{
    [Header("Default_Info")]
    [SerializeField] private string draftAnimalId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject prefab;

    [Header("Draft_Animal_Description")]
    [TextArea(3, 10)]
    [SerializeField] private string description;

    [Header("Draft_Animal_Type")]
    [SerializeField] private DraftAnimalType animalType;

    [Header("Draft_Animal_Info")]
    [SerializeField] private float feedConsumption; // per seconds
    [SerializeField] private float baseMoveSpeed;
    [SerializeField] private float increaseOverLoad; // Increase maximum overload limit
    [SerializeField] private float increaseMaxLoad; // Increase maximum Maxload limit

    [Header("Trade_Info")]
    [SerializeField] private TradeItemRarity rarity;
    [SerializeField] private long baseBuyPrice;

    [Header("Stack_Info")]
    [SerializeField] private bool canStack;
    [SerializeField] private int maxCount;

    [Header("Modify_Info")]
    [SerializeField] private bool affectModify; // for baseBuyPrice, baseMoveSpeed
    [SerializeField] private ModifierInput[] modifiers;

    [Header("Local_Trade_Info")]
    [SerializeField] private bool localSpecialty;

    #region
    public string Id => draftAnimalId;
    public string DraftAnimalId => draftAnimalId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public GameObject Prefab => prefab;
    public string Description => description;
    public DraftAnimalType AnimalType => animalType;
    public float FeedConsumption => Mathf.Max(0f, feedConsumption);
    public float BaseMoveSpeed => Mathf.Max(0f, baseMoveSpeed);
    public float IncreaseOverLoad => Mathf.Max(0f, increaseOverLoad);
    public float IncreaseMaxLoad => Mathf.Max(0f, increaseMaxLoad);
    public TradeItemRarity Rarity => rarity;
    public long BaseBuyPrice => baseBuyPrice >= 0 ? baseBuyPrice : 0;
    public bool CanStack => canStack;
    public int MaxCount => Mathf.Max(1, maxCount);
    public bool AffectModify => affectModify;
    public ModifierInput[] Modifiers => modifiers != null ? (ModifierInput[])modifiers.Clone() : new ModifierInput[0];
    public bool LocalSpecialty => localSpecialty;
    #endregion

#if UNITY_EDITOR
    private void OnValidate()
    {
        ClampValues();
        ValidateModifierTargets();
    }

    private void ClampValues()
    {
        feedConsumption = Mathf.Max(0f, feedConsumption);
        baseMoveSpeed = Mathf.Max(0f, baseMoveSpeed);
        increaseOverLoad = Mathf.Max(0f, increaseOverLoad);
        increaseMaxLoad = Mathf.Max(0f, increaseMaxLoad);

        baseBuyPrice = baseBuyPrice >= 0 ? baseBuyPrice : 0;
        maxCount = Mathf.Max(1, maxCount);
    }

    private void ValidateModifierTargets()
    {
        //TODO require enhancing exception handling
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

                if (bundle.modifierTarget == Target.BaseMoveSpeed)
                    continue;

                bundle.modifierTarget = Target.None;
                bundle.modifierOperation = Operation.None;
                bundle.value = 0f;
            }
        }
    }
#endif
}
