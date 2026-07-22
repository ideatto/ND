using TMPro;
using UnityEngine;

/// <summary>
/// Presents the player's trading currency in the persistent in-game HUD.
/// Development currency remains part of Framework data but is intentionally not displayed here.
/// </summary>
public class CurrencyHudPresenter : MonoBehaviour
{
    [SerializeField] private TMP_Text tradingCurrencyText;

    [Header("EventChannel")]
    [SerializeField] private CurrencyChangedEventChannel currencyChangedChannel;

    private long lastTradingCurrency = long.MinValue;

    private void OnEnable()
    {
        if(currencyChangedChannel != null)
        {
            currencyChangedChannel.Register(HandleCurrencyChanged);
        }

        RefreshImmediately();
    }

    private void OnDisable()
    {
        if(currencyChangedChannel != null)
        {
            currencyChangedChannel.Unregister(HandleCurrencyChanged);
        }
    }

    public void RefreshImmediately()
    {
        lastTradingCurrency = long.MinValue;

        RefreshIfChanged();
    }

    private void RefreshIfChanged()
    {
        ND.Framework.FrameworkRoot framework = 
            ND.Framework.FrameworkRoot.Instance;

        ND.Framework.SaveData saveData =
            framework != null ? framework.CurrentSaveData : null;  

        if(saveData?.player == null)
        {
            ApplyIfChanged(0);
            return;
        }

        ApplyIfChanged(saveData.player.tradingCurrency);
    }

    private void HandleCurrencyChanged(CurrencyChangedEventData eventData)
    {
        // The HUD-owned event contract carries only the trade money rendered by this presenter.
        ApplyIfChanged(eventData.tradingCurrency);
    }

    private void ApplyIfChanged(long tradingCurrency)
    {
        if(tradingCurrency == lastTradingCurrency)
        {
            return;
        }

        lastTradingCurrency = tradingCurrency;

        Render(tradingCurrency);
    }

    private void Render(long tradingCurrency)
    {
        if (tradingCurrencyText != null)
        {
            tradingCurrencyText.text = CurrencyTextFormatter.Format(tradingCurrency);
        }
    }
}
