using UnityEngine;

[System.Serializable]
public class RouteEventData
{
    public RouteEvent eventType;
    public string routeEventId;
    public string displayName;
    [TextArea(2, 4)]
    public string description;

    public int eventValue; // combat power
    public float defaultEvasionPer; // combat event default evasion percent
    public int lossCount; // if combat event failed. player

    public RewardType eventRewardType;
    
    // new는 보상 객체가 기본적으로 null이 되지 않게 한다.
    // 객체의 필드는 C# 기본값 또는 필드 선언부에 지정한 초기값으로 생성된다.
    public RouteEventCurrencyReward eventRewardCurrency = new RouteEventCurrencyReward();
    public RouteEventItemReward eventRewardItem = new RouteEventItemReward();

    /// <summary>
    /// 누락되거나 잘못 입력된 보상 설정을 실제 계산에 사용할 수 있는 범위로 정리한다.
    /// Normalize는 C# 또는 Unity 내장 기능이 아니라 이 파일에 직접 정의한 보정 메서드다.
    /// </summary>
    public void NormalizeRewards()
    {
        // 이전 버전 에셋이나 누락된 직렬화 데이터에서는 보상 객체가 null일 수 있다.
        // 빈 객체를 생성하여 이후 필드 접근 시 NullReferenceException이 발생하지 않게 한다.
        if (eventRewardCurrency == null)
            eventRewardCurrency = new RouteEventCurrencyReward();
        if (eventRewardItem == null)
            eventRewardItem = new RouteEventItemReward();

        // 각 보상 타입에 정의된 범위 보정 규칙을 실행한다.
        eventRewardCurrency.Normalize();
        eventRewardItem.Normalize();
    }
}

[System.Serializable]
public class RouteEventCurrencyReward
{
    [Min(0)]
    public long minAmount;
    [Min(0)]
    public long maxAmount;

    /// <summary>
    /// 통화 보상 범위를 음수가 아니며 최대값이 최소값 이상이 되도록 보정한다.
    /// </summary>
    public void Normalize()
    {
        minAmount = System.Math.Max(0L, minAmount);
        maxAmount = System.Math.Max(minAmount, maxAmount);
    }
}

[System.Serializable]
public class RouteEventItemReward
{
    public string itemId;
    [Min(0)]
    public int minQuantity = 1;
    [Min(0)]
    public int maxQuantity = 1;

    /// <summary>
    /// 아이템 ID와 수량 범위를 실제 보상 계산에 사용할 수 있는 형태로 보정한다.
    /// </summary>
    public void Normalize()
    {
        // ID가 누락된 경우 null 대신 안전하게 비교할 수 있는 빈 문자열을 사용한다.
        itemId = itemId != null ? itemId : string.Empty;

        // 수량은 음수가 될 수 없고 최대 수량은 최소 수량보다 작을 수 없다.
        minQuantity = Mathf.Max(0, minQuantity);
        maxQuantity = Mathf.Max(minQuantity, maxQuantity);
    }
}

public enum RewardType
{
    None,
    TradingCurrency,
    TradeItem,
    Both
}

public enum RouteEvent
{
    Combat,
    Lucky,
    Weather
}
