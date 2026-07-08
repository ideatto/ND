using UnityEngine;

[System.Serializable]
public class RouteEventData
{
    public RouteEvent eventType;
    public string routeEventId;
    public string displayName;
    [TextArea(2, 4)]
    public string description;

    public float eventValue;

    public RewardType eventRewardType;
    public float eventReward;
    public float minReward;
    public float maxReward;
}

public enum RewardType
{
    None,
    TradingCurrency,
    DevelopmentCurrency
}

public enum RouteEvent
{
    Combat,
    Lucky
}
