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
    public long eventReward;
    public long minReward;
    public long maxReward;

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

public enum RewardType
{
    None,
    TradingCurrency,
    DevelopmentCurrency
}

public enum RouteEvent
{
    Combat,
    Lucky,
    Weather
}
