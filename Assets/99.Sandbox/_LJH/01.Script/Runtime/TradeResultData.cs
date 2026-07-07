using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TradeResultData
{
    public bool isSuccess;

    public string tradeId;
    public string routeId;
    public string fromTownId;
    public string toTownId;

    public int totalPurchaseCost;
    public int totalSellRevenue;
    public int foodCost;
    public int mercenaryCost;
    public int eventProfitAndLoss;

    public int lossAmount;
    public int netProfit;

    public TradeItemBundle[] lossItems;

    public float durabilityLoss;

    public FailureReason failureReason;
    public List<TradeResultMessageData> messages = new List<TradeResultMessageData>();
}

public enum FailureReason
{
    None,
    FoodShortage,
    DurabilityBroken,
    CombatDefeat,
    GiveUp,
    Unknown
}

[System.Serializable]
public class TradeResultMessageData
{
    public TradeResultMessageType type;
    public string messageCode;
    public string messageText;
}

public enum TradeResultMessageType
{
    Info,
    Warning,
    Error,
    Success,
    Failure
}