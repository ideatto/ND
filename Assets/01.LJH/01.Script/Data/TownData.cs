using UnityEngine;

[CreateAssetMenu(fileName = "Town_TownName", menuName = "TownData")]
public class TownData : ScriptableObject
{
    [Header("Town_Default_Info")]
    [SerializeField] private string townID;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Town_Description")]
    [TextArea(3, 10)]
    [SerializeField] private string description;

    [Header("Town_TradeItem_Info")]
    [SerializeField] private TradeItemData[] tradeItems;

    [Header("Town_TradeItem_Draft_Animal_Info")]
    [SerializeField] private TradeItemData[] draftAnimalItems;

    //[Header("Town_Caravan_Info")]
    //[SerializeField] private CaravanData[] caravans;

    //[Header("Town_Hireable_Mercenary_Info")]
    //[SerializeField] private MercenaryData[] hireableMercenaries;

    [Header("Town_localSpecialty_Info")]
    [SerializeField] private TradeItemData[] localSpecialtyItems;

    [Header("Town_Trade_Info")]
    [SerializeField] private float itemRenewalCycle;

    [Header("Town_Available_Route_Info")]
    [SerializeField] private RouteData[] availableRoutes;

    [Header("Town_Maximum_Donation_Limit")]
    [SerializeField] private bool canDonate;
    [SerializeField] private int maximumDonationLimit;

    [Header("Town_Maximum_Contribution_Info")]
    [SerializeField] private bool canContribute;
    [SerializeField] private float maximumContributionLimit;

    [Header("Town_Maximum_Development_Info")]
    [SerializeField] private bool canDevelop;
    [SerializeField] private int maximumDevelopmentLevel;

    public string TownID => townID;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public string Description => description;

    public TradeItemData[] TradeItems => tradeItems;
    public TradeItemData[] DraftAnimalItems => draftAnimalItems;
    public TradeItemData[] LocalSpecialtyItems => localSpecialtyItems;
    public float ItemRenewalCycle => itemRenewalCycle;
    public RouteData[] AvailableRoutes => availableRoutes;
    public bool CanDonate => canDonate;
    public int MaximumDonationLimit => maximumDonationLimit;
    public bool CanContribute => canContribute;
    public float MaximumContributionLimit => maximumContributionLimit;
    public bool CanDevelop => canDevelop;
    public int MaximumDevelopmentLevel => maximumDevelopmentLevel;
}
