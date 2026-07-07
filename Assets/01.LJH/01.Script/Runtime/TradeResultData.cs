using UnityEngine;

[System.Serializable]
public class TradeResultData
{
    [SerializeField] private bool isSuccess;
    [SerializeField] private string routeID;
    [SerializeField] private string startTownID;
    [SerializeField] private string destinationTownID;

    [SerializeField] private int totalPurchaseCost;
    [SerializeField] private int totalSellRevenue;
    [SerializeField] private int foodCost;
    [SerializeField] private int mercenaryCost;
    [SerializeField] private int lossAmount;
    [SerializeField] private int netProfit;

    [SerializeField] private int eventProfitAndLoss;

    [SerializeField] private TradeItemBunddle[] lossItem;

    [SerializeField] private float durabilityLoss;

    [SerializeField] private RouteEvent failureReason;
    [SerializeField] private string[] resultMessages;

    #region
    public bool IsSuccess => isSuccess;
    public string RouteID => routeID;
    public string StartTownID => startTownID;
    public string DestinationTownID => destinationTownID;
    public int TotalPurchaseCost => totalPurchaseCost;
    public int TotalSellRevenue => totalSellRevenue;
    public int FoodCost => foodCost;
    public int MercenaryCost => mercenaryCost;
    public int LossAmount => lossAmount;
    public int NetProfit => netProfit;  
    public int EventProfitAndLoss => eventProfitAndLoss;
    public TradeItemBunddle[] LossItem => lossItem;
    public float DurabilityLoss => durabilityLoss;
    public RouteEvent FailureReason => failureReason;
    public string[] ResultMessages => resultMessages;
    #endregion

    public TradeResultData(bool isSuccess, string routeID, string startTownID, string destinationTownID, 
        int totalPurchaseCost, int totalSellRevenue, int foodCost, int mercenaryCost, int lossAmount, int netProfit,
        int eventProfitAndLoss, TradeItemBunddle[] lossItem, float durabilityLoss, RouteEvent failureReason, string[] resultMessages)
    {
        this.isSuccess = isSuccess;
        this.routeID = routeID;
        this.startTownID = startTownID;
        this.destinationTownID = destinationTownID;
        this.totalPurchaseCost = totalPurchaseCost;
        this.totalSellRevenue = totalSellRevenue;
        this.foodCost = foodCost;
        this.mercenaryCost = mercenaryCost;
        this.lossAmount = lossAmount;
        this.netProfit = netProfit;
        this.eventProfitAndLoss = eventProfitAndLoss;
        this.lossItem = lossItem;
        this.durabilityLoss = durabilityLoss;
        this.failureReason = failureReason;
        this.resultMessages = resultMessages;
    }
}