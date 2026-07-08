using UnityEngine;

[CreateAssetMenu(fileName = "Market_TownName", menuName = "MarketData")]
public class MarketData : ScriptableObject
{
    [SerializeField] private string marketId;

    [Header("Market_Info")]
    [SerializeField] private int itemMaxQuantity;
    [SerializeField] private float itemRenewalCycle;

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

    #region
    public string MarketId => marketId;
    public int ItemMaxQuantity => Mathf.Max(0, itemMaxQuantity);
    public float ItemRenewalCycle => Mathf.Max(0f, itemRenewalCycle);
    public TradeItemData[] TradeItems => tradeItems;
    public TradeItemData[] DraftAnimalItems => draftAnimalItems;
    public TradeItemData[] LocalSpecialtyItems => localSpecialtyItems;
    #endregion
}

