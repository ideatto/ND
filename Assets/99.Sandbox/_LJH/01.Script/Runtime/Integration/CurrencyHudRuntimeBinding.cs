using ND.Framework;
using UnityEngine;

/// <summary>
/// Bridges authoritative Framework and player currency changes into the UI-owned event channel.
/// The binding avoids frame polling and keeps CurrencyHudPresenter independent from gameplay managers.
/// </summary>
[DisallowMultipleComponent]
public sealed class CurrencyHudRuntimeBinding : MonoBehaviour
{
    [Tooltip("Receives current currency snapshots and forwards them to CurrencyHudPresenter.")]
    [SerializeField] private CurrencyChangedEventChannel currencyChangedChannel;

    private PlayerMainManager subscribedPlayerManager;

    private void OnEnable()
    {
        FrameworkEvents.LoadCompleted += HandleLoadCompleted;
        FrameworkEvents.SceneChanged += HandleSceneChanged;

        TrySubscribeToPlayerManager();
        PublishCurrentSaveData();
    }

    private void Start()
    {
        // PlayerMainManager may finish Awake after this component is enabled during scene loading.
        TrySubscribeToPlayerManager();
        PublishCurrentSaveData();
    }

    private void OnDisable()
    {
        FrameworkEvents.LoadCompleted -= HandleLoadCompleted;
        FrameworkEvents.SceneChanged -= HandleSceneChanged;
        UnsubscribeFromPlayerManager();
    }

    private void HandleLoadCompleted(ND.Framework.SaveData saveData)
    {
        TrySubscribeToPlayerManager();
        Publish(saveData);
    }

    private void HandleSceneChanged(string sceneName)
    {
        // A persistent PlayerMainManager can be created or replaced during scene transitions.
        TrySubscribeToPlayerManager();
        PublishCurrentSaveData();
    }

    private void HandleGoldChanged(long tradingCurrency)
    {
        Publish(tradingCurrency);
    }

    private void TrySubscribeToPlayerManager()
    {
        PlayerMainManager current = PlayerMainManager.Instance;
        if (subscribedPlayerManager == current)
        {
            return;
        }

        UnsubscribeFromPlayerManager();
        subscribedPlayerManager = current;

        if (subscribedPlayerManager != null)
        {
            subscribedPlayerManager.OnGoldChanged += HandleGoldChanged;
        }
    }

    private void UnsubscribeFromPlayerManager()
    {
        if (subscribedPlayerManager != null)
        {
            subscribedPlayerManager.OnGoldChanged -= HandleGoldChanged;
            subscribedPlayerManager = null;
        }
    }

    private void PublishCurrentSaveData()
    {
        Publish(FrameworkRoot.Instance?.CurrentSaveData);
    }

    private void Publish(ND.Framework.SaveData saveData)
    {
        if (saveData?.player == null)
        {
            Publish(0L);
            return;
        }

        Publish(saveData.player.tradingCurrency);
    }

    private void Publish(long tradingCurrency)
    {
        // Keep this UI event focused on the only currency rendered by CurrencyHudPresenter.
        currencyChangedChannel?.Raise(
            new CurrencyChangedEventData(tradingCurrency));
    }
}
