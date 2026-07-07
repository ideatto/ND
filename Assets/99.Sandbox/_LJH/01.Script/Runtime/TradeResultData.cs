using UnityEngine;

[System.Serializable]
public class TradeResultData
{
    public bool isSuccess;
    public string routeID;
    public string startTownID;
    public string destinationTownID;

    public int totalPurchaseCost;
    public int totalSellRevenue;
    public int foodCost;
    public int mercenaryCost;
    public int lossAmount;
    public int netProfit;

    public int eventProfitAndLoss;

    public TradeItemBundle[] lossItem;

    public float durabilityLoss;

    public RouteEvent failureReason;
    public string[] resultMessages;
}