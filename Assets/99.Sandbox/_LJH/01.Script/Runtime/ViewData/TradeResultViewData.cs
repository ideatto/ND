using System.Collections.Generic;

[System.Serializable]
public class TradeResultViewData
{
    public bool isSuccess;

    public string routeName;
    public string fromTownName;
    public string toTownName;

    public long itemPurchaseCost;
    public long itemSellRevenue;
    public long foodCost;
    public long mercenaryCost;
    public long eventProfit;
    public long eventLoss;

    public long lossAmount;
    public long netProfit;

    public string failureReasonText;

    public List<TradeResultMessageData> messages = new List<TradeResultMessageData>();
}
