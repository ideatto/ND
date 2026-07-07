using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TradeResultData
{
    public bool IsSuccess;
    public string TradeId;
    public string RouteId;
    public string FromTownId;
    public string ToTownId;

    public int TotalPurchaseCost;
    public int TotalSellRevenue;
    public int FoodCost;
    public int MercenaryCost;
    public int LossAmount;
    public int NetProfit;

    public int EventProfitAndLoss;

    public TradeItemBundle[] LossItem;

    public float DurabilityLoss;

    public RouteEvent FailureReason;
    public List<TradeResultMessageData> Messages = new List<TradeResultMessageData>();
}

[System.Serializable]
public class TradeResultMessageData
{
    public TradeResultMessageType Type;
    public string MessageCode;
    public string MessageText;
}

public enum TradeResultMessageType
{
    Info,
    Warning,
    Error,
    Success,
    Failure
}