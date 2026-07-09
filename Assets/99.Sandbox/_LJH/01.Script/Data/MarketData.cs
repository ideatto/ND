using UnityEngine;

[CreateAssetMenu(fileName = "Market_TownName", menuName = "Town/MarketData")]
public class MarketData : ScriptableObject, IIdentifiableData
{
    [SerializeField] private string marketId;

    [Header("Market_Info")]
    [SerializeField] private int itemMaxQuantity;
    [SerializeField] private float itemRenewalCycle;

    [Header("Town_TradeItem_Info")]
    [SerializeField] private TradeItemData[] tradeItems;

    [Header("Town_TradeItem_Draft_Animal_Info")]
    [SerializeField] private TradeItemData[] draftAnimalItems;

    [Header("Town_TradeItem_Caravan_Info")]
    [SerializeField] private WagonData[] wagonItems;

    [Header("Town_localSpecialty_Info")]
    [SerializeField] private TradeItemData[] localSpecialtyItems;

    #region Public Properties
    public string Id => marketId;
    public string MarketId => marketId;
    public int ItemMaxQuantity => Mathf.Max(0, itemMaxQuantity);
    public float ItemRenewalCycle => Mathf.Max(0f, itemRenewalCycle);
    public TradeItemData[] TradeItems => tradeItems != null ? (TradeItemData[])tradeItems.Clone() : new TradeItemData[0];
    public TradeItemData[] DraftAnimalItems => draftAnimalItems != null ? (TradeItemData[])draftAnimalItems.Clone() : new TradeItemData[0];
    public WagonData[] WagonItems => wagonItems != null ? (WagonData[])wagonItems.Clone() : new WagonData[0];
    public TradeItemData[] LocalSpecialtyItems => localSpecialtyItems != null ? (TradeItemData[])localSpecialtyItems.Clone() : new TradeItemData[0];
    #endregion
}

