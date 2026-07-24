using UnityEngine;

[System.Serializable]
public class RouteEventData
{
    [Header("Event_Default_Info")]
    public RouteEvent eventType;
    public string routeEventId;
    public string displayName;

    [Header("Event_Description")]
    [TextArea(2, 4)]
    public string description;

    [Header("Event_Combat_Info")]
    public int eventValue; // Combat power used by Combat events.
    public float defaultEvasionPer; // Default Combat-event evasion percentage.
    public int lossCount; // Player loss count when a Combat event fails.

    [Header("Event_Reward_Info")]
    public RewardType eventRewardType;


    // Initialize nested rewards so newly created entries are safe to read.
    // Older serialized assets may still deserialize either reference as null.
    public RouteEventCurrencyReward eventRewardCurrency = new RouteEventCurrencyReward();
    public RouteEventItemReward eventRewardItem = new RouteEventItemReward();


    /// <summary>
    /// Restores missing reward objects and clamps their ranges before use.
    /// Normalize is a project-defined method, not a Unity or C# built-in.
    /// </summary>
    public void NormalizeRewards()
    {
        // Missing nested objects can occur after a serialized schema change.
        if (eventRewardCurrency == null)
            eventRewardCurrency = new RouteEventCurrencyReward();
        if (eventRewardItem == null)
            eventRewardItem = new RouteEventItemReward();

        // Apply the validation rules owned by each reward type.
        eventRewardCurrency.Normalize();
        eventRewardItem.Normalize();
    }

    [Header("Combat Event")]
    [Tooltip("산적 전투력. 0이면 기존 Combat eventValue를 호환 값으로 사용한다.")]
    [Min(0)]
    public int banditCombatPower;

    [Tooltip("패배 시 일반 무역품 약탈 비율(0~1).")]
    [Range(0f, 1f)]
    public float cargoLootRate;

    [Tooltip("패배 시 여물 약탈 비율(0~1).")]
    [Range(0f, 1f)]
    public float fodderLootRate;

    public int BanditCombatPower
    {
        get
        {
            if (banditCombatPower > 0) return banditCombatPower;
            if (eventType != RouteEvent.Combat) return 0;
            return Mathf.Max(0, Mathf.RoundToInt(eventValue));
        }
    }

    public float CargoLootRate => Mathf.Clamp01(cargoLootRate);
    public float FodderLootRate => Mathf.Clamp01(fodderLootRate);
}

[System.Serializable]
public class RouteEventCurrencyReward
{
    [Min(0)]
    public long minAmount;
    [Min(0)]
    public long maxAmount;

    /// <summary>
    /// Ensures a non-negative range whose maximum is not below its minimum.
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
    /// Ensures a safe item ID and a valid non-negative quantity range.
    /// </summary>
    public void Normalize()
    {
        // Empty string is safer than null for ID comparisons and lookups.
        itemId = itemId != null ? itemId : string.Empty;

        // Quantity cannot be negative, and max cannot be lower than min.
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
    // Explicit values preserve existing serialized assets. Combat is already 0,
    // so inserting None before it would silently reinterpret old Combat entries.
    Combat = 0,
    Lucky = 1,
    Weather = 2,

    // A completed event check with no gameplay effect. Runtime logic should not
    // count this result toward RouteData.MaxEventCount.
    None = 3
}
