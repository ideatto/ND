using UnityEngine;

[CreateAssetMenu(fileName = "Quest_QuestName", menuName = "Quest/QuestData")]
public class QuestData : ScriptableObject
{
    [Header("Quest Default Info")]
    // Stable content ID used to connect this definition with runtime and save data.
    [SerializeField] private string questId;

    // User-facing name displayed by quest UI.
    [SerializeField] private string questName;

    [Header("Quest Description")]
    [TextArea(2, 5)]
    // User-facing description displayed by quest UI.
    [SerializeField] private string description;

    [Header("Quest Type")]
    // Defines the content category without mixing it with the repetition policy.
    [SerializeField] private QuestType type;

    [Header("Quest Regeneration")]
    // Defines whether and how this quest becomes available again after completion.
    [SerializeField] private QuestRepeatPolicy repeatPolicy;

    // Defines the delay in seconds before a delayed-repeat quest becomes available again.
    // The actual completion and next-available UTC ticks belong to runtime SaveData.
    [SerializeField, Min(0)] private int questRegenSeconds;

    [Header("Quest Completion Requirements")]
    // Defines the town where this quest can be completed.
    [SerializeField] private string submissionTownId;

    // Defines player trading currency consumed by the completion transaction.
    [SerializeField] private long requiredTradingCurrency;

    // Defines item quantities submitted from eligible Caravans.
    [SerializeField]
    private QuestItemCostData[] requiredItems = System.Array.Empty<QuestItemCostData>();

    [Header("Quest Rewards")]
    // Contains every reward applied after successful quest completion.
    [SerializeField]
    private QuestRewardData[] rewards = System.Array.Empty<QuestRewardData>();

    #region Properties
    // Id keeps this asset compatible with other content definitions that expose a common ID.
    public string Id => questId;
    public string QuestId => questId;
    public string QuestName => questName;
    public string Description => description;
    public QuestType Type => type;
    public QuestRepeatPolicy RepeatPolicy => repeatPolicy;
    public string SubmissionTownId => submissionTownId;

    // Prevents an invalid negative currency requirement from reaching Runtime.
    public long RequiredTradingCurrency => System.Math.Max(0L, requiredTradingCurrency);

    // Returns an empty collection instead of null when no item cost is configured.
    public QuestItemCostData[] RequiredItems => requiredItems ?? System.Array.Empty<QuestItemCostData>();

    // Returns an empty collection instead of null when no reward is configured.
    public QuestRewardData[] Rewards => rewards ?? System.Array.Empty<QuestRewardData>();

    // Policies without a completion delay ignore a stale regeneration value in the Inspector.
    public int QuestRegenSeconds =>
        repeatPolicy == QuestRepeatPolicy.AfterCompletionDelay
            ? Mathf.Max(0, questRegenSeconds)
            : 0;

    #endregion
}
