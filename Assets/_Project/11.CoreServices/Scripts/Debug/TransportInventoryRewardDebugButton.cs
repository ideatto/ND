using System;
using UnityEngine;
using UnityEngine.UI;

public enum TransportRewardType
{
    Wagon,
    DraftAnimal
}

[Serializable]
public sealed class TransportRewardEntry
{
    public TransportRewardType type;
    public string contentId = string.Empty;
    [Min(1)] public int quantity = 1;
}

/// <summary>Development-only button that grants Inspector-configured owned transports.</summary>
[RequireComponent(typeof(Button))]
public sealed class TransportInventoryRewardDebugButton : MonoBehaviour
{
    [SerializeField] private TransportRewardEntry[] rewards = Array.Empty<TransportRewardEntry>();
    private Button button;

    private void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        button = GetComponent<Button>();
        button.onClick.AddListener(GrantRewards);
#else
        gameObject.SetActive(false);
#endif
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        button?.onClick.RemoveListener(GrantRewards);
#endif
    }

    public void GrantRewards()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        TryGrantRewards(PlayerMainManager.Instance);
#endif
    }

    internal bool TryGrantRewards(PlayerMainManager player)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (player == null)
        {
            Debug.LogWarning("[Transport Reward Debug] PlayerMainManager is unavailable.", this);
            return false;
        }

        if (!TryValidateRewards(player, out TransportInventoryValidationFailure failure))
        {
            Debug.LogWarning($"[Transport Reward Debug] Grant rejected: {failure}.", this);
            return false;
        }

        foreach (TransportRewardEntry reward in rewards)
        {
            string contentId = reward.contentId.Trim();
            for (int index = 0; index < reward.quantity; index++)
            {
                bool created = reward.type == TransportRewardType.Wagon
                    ? player.TryCreateWagon(contentId, out _, out failure)
                    : player.TryCreateDraftAnimal(contentId, out _, out failure);
                if (!created)
                {
                    Debug.LogError($"[Transport Reward Debug] Prevalidated grant failed: {failure}.", this);
                    return false;
                }
            }
        }

        Debug.Log("[Transport Reward Debug] Configured rewards were granted.", this);
        return true;
#else
        return false;
#endif
    }

    private bool TryValidateRewards(PlayerMainManager player, out TransportInventoryValidationFailure failure)
    {
        int wagonCount = 0;
        int animalCount = 0;
        if (rewards == null || rewards.Length == 0)
        {
            failure = TransportInventoryValidationFailure.InvalidIdentity;
            return false;
        }

        foreach (TransportRewardEntry reward in rewards)
        {
            if (reward == null || reward.quantity <= 0 || string.IsNullOrWhiteSpace(reward.contentId))
            {
                failure = TransportInventoryValidationFailure.InvalidIdentity;
                return false;
            }
            if (reward.type == TransportRewardType.Wagon)
            {
                if (reward.quantity > int.MaxValue - wagonCount)
                {
                    failure = TransportInventoryValidationFailure.CapacityExceeded;
                    return false;
                }
                wagonCount += reward.quantity;
                if (!player.CanCreateWagon(reward.contentId, wagonCount, out failure)) return false;
            }
            else
            {
                if (reward.quantity > int.MaxValue - animalCount)
                {
                    failure = TransportInventoryValidationFailure.CapacityExceeded;
                    return false;
                }
                animalCount += reward.quantity;
                if (!player.CanCreateDraftAnimal(reward.contentId, animalCount, out failure)) return false;
            }
        }

        failure = TransportInventoryValidationFailure.None;
        return true;
    }
}
