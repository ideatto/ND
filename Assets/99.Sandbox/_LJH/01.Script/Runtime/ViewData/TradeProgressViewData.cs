using System.Collections.Generic;

[System.Serializable]
public class TradeProgressViewData
{
    public string activeTradeId;
    public string activeRouteId;
    public string fromTownName;
    public string toTownName;

    public float totalTravelTime;
    public float elapsedTravelTime;
    public float remainingTravelTime;
    public float normalizedProgress;

    public string statusTitle;
    public string statusMessage;
    public List<TradeResultMessageData> eventMessages = new List<TradeResultMessageData>();

    public bool canCancel;
    public bool isCompleted;
}
