using System.Collections.Generic;

[System.Serializable]
public class TradeResultViewData
{
    public bool isSuccess;

    public string routeName;
    public string fromTownName;
    public string toTownName;

    public int totalPurchaseCost;
    public int totalSellRevenue;
    public int foodCost;
    public int mercenaryCost;
    public int eventProfitAndLoss;

    public int lossAmount;
    public int netProfit;

    public string failureReasonText;

    public List<TradeResultMessageData> messages = new List<TradeResultMessageData>();
}
