using UnityEngine;

/// <summary>
/// Defines the content category of a quest independently from its repetition policy.
/// </summary>
public enum QuestType
{
    Normal,
    Investment,
    Unique,
    Event
}

/// <summary>
/// Defines whether and how a completed quest becomes available again.
/// </summary>
public enum QuestRepeatPolicy
{
    // The quest does not become available again after completion.
    None,

    // The quest becomes available after QuestData.QuestRegenSeconds has elapsed.
    AfterCompletionDelay
}

/// <summary>
/// Defines one item quantity consumed by a quest completion transaction.
/// </summary>
[System.Serializable]
public sealed class QuestItemCostData
{
    // Stable ID of the item consumed on completion.
    [SerializeField] private string itemId;

    // Total quantity required across eligible Caravans in the submission town.
    [SerializeField, Min(1)] private int quantity = 1;

    public string ItemId => itemId;
    public int Quantity => Mathf.Max(1, quantity);
}

/// <summary>
/// Defines one reward entry authored in QuestData.
/// Runtime remains responsible for applying and saving the reward transaction.
/// </summary>
[System.Serializable]
public sealed class QuestRewardData
{
    // Determines how Runtime interprets and applies this reward entry.
    [SerializeField] private QuestRewardType rewardType;

    // TradingCurrency: currency amount.
    // TradeItem: item quantity.
    // Buff: real-time duration in minutes.
    // Unlock rewards ignore this value.
    [SerializeField] private long value;

    // TradeItem: itemId.
    // UnlockTown: townId.
    // UnlockRoute: routeId.
    // Buff: buffId.
    // TradingCurrency does not use this field.
    [SerializeField] private string rewardId;

    public QuestRewardType RewardType => rewardType;

    // Prevents invalid negative reward values from reaching Runtime.
    public long Value => System.Math.Max(0L, value);

    public string RewardId => rewardId;
}

/// <summary>
/// Identifies the operation Runtime performs for a quest reward entry.
/// </summary>
public enum QuestRewardType
{
    TradingCurrency,
    TradeItem,
    UnlockTown,
    UnlockRoute,
    Buff
}
