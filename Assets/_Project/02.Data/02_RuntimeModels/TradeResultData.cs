using System;
using System.Collections.Generic;

[Serializable]
public class TradeResultData
{
    public string TradeId;
    public string RouteId;
    public string FromTownId;
    public string ToTownId;

    public bool IsSuccess;
    public int StartedDay;
    public int CompletedDay;

    public int TotalBuyCost;
    public int TotalSellRevenue;
    public int Profit;

    public List<TradeResultItemData> Items = new List<TradeResultItemData>();
    public List<TradeResultMessageData> Messages = new List<TradeResultMessageData>();
}

[Serializable]
public class TradeResultItemData
{
    public string ItemId;
    public int Quantity;

    public int BuyPrice;
    public int SellPrice;
    public int Profit;
}

[Serializable]
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
    Success
}
