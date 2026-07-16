using UnityEngine;

[CreateAssetMenu(fileName = "Wagon_WagonName", menuName = "TradeItem/WagonData")]
public class WagonData : ScriptableObject, IIdentifiableData
{
    [Header("Default_Info")]
    [SerializeField] private string wagonId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField] private GameObject prefab;

    [Header("Wagon_Description")]
    [TextArea(3, 10)]
    [SerializeField] private string description;

    [Header("Wagon_Type")]
    [SerializeField] private WagonType wagonType; //None, WagonWithAnimals, Mount

    [Header("Wagon_Info")]
    [SerializeField] private int maxDurability;
    [SerializeField] private float overLoad;
    [SerializeField] private float maxLoad;
    [SerializeField] private float baseMoveSpeed;
    // CaravanSpeed = WagonData.baseMoveSpeed + AnimalData.baseMoveSpeed 
    //if WagonType.WagonWithAnimals -> baseMoveSpeed = 0

    [Header("Wagon_Eligible_Animal_Info")]
    [SerializeField] private DraftAnimalType[] eligibleAnimalTypes;

    [Header("Wagon_Inventory_Info")]
    [SerializeField] private int inventorySlotCount;

    [Header("Wagon_Require_Animal_Info")]
    [SerializeField] private int maxPullAnimals;
    [SerializeField] private int minRequireAnimals;

    [Header("Trade_Info")]
    [SerializeField] private TradeItemRarity rarity;
    [SerializeField] private long baseBuyPrice;

    [Header("Stack_Info")]
    [SerializeField] private bool canStack;
    [SerializeField] private int maxCount;

    [Header("Modify_Info")]
    [SerializeField] private bool affectModify; // for baseBuyPrice
    [SerializeField] private ModifierInput[] modifiers;

    [Header("Local_Trade_Info")]
    [SerializeField] private bool localSpecialty;

    #region
    public string Id => wagonId;
    public string WagonId => wagonId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public GameObject Prefab => prefab;
    public string Description => description;
    public WagonType WagonType => wagonType;
    public int MaxDurability => Mathf.Max(0, maxDurability);
    public float Overload => Mathf.Max(0f, overLoad);
    public float MaxLoad => Mathf.Max(0f, maxLoad);
    public int InventorySlotCount => Mathf.Max(1, inventorySlotCount);
    public int MaxPullAnimals => Mathf.Max(0, maxPullAnimals);
    public int MinRequireAnimals => Mathf.Max(0, minRequireAnimals);
    public float BaseMoveSpeed => Mathf.Max(0f, baseMoveSpeed);
    public DraftAnimalType[] EligibleAnimalTypes =>
        eligibleAnimalTypes != null ? (DraftAnimalType[])eligibleAnimalTypes.Clone() : new DraftAnimalType[0];
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
        ApplyWagonTypeRules(); //if wagonType changed -> inspector changed
        ValidateModifierTargets(); //only BuyPrice modifiers are allowed for WagonData
    }

    private void ClampValues()
    {
        maxDurability = Mathf.Max(0, maxDurability);
        overLoad = Mathf.Max(0f, overLoad);
        maxLoad = Mathf.Max(0f, maxLoad);

        if (overLoad > maxLoad)
            overLoad = maxLoad;

        inventorySlotCount = Mathf.Max(1, inventorySlotCount);
        baseBuyPrice = baseBuyPrice >= 0 ? baseBuyPrice : 0;
        maxCount = Mathf.Max(1, maxCount);

        maxPullAnimals = Mathf.Max(0, maxPullAnimals);
        minRequireAnimals = Mathf.Max(0, minRequireAnimals);

        if (minRequireAnimals > maxPullAnimals)
            minRequireAnimals = maxPullAnimals;
    }

    private void ApplyWagonTypeRules()
    {
        if (wagonType == WagonType.None)
        {
            maxDurability = 10;
            maxPullAnimals = 0;
            minRequireAnimals = 0;
            eligibleAnimalTypes = new DraftAnimalType[0];
            rarity = TradeItemRarity.None;
            baseBuyPrice = 0;
            canStack = false;
            maxCount = 1;
            affectModify = false;
        }

        if (wagonType == WagonType.Mount)
        {
            maxPullAnimals = 0;
            minRequireAnimals = 0;
            eligibleAnimalTypes = new DraftAnimalType[0];
        }

        if(wagonType == WagonType.WagonWithAnimals) 
        {
            baseMoveSpeed = 0;
        }
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

                bundle.modifierTarget = Target.None;
                bundle.modifierOperation = Operation.None;
                bundle.value = 0f;
            }
        }
    }
#endif
}
