using TMPro;
using UnityEngine;

public class CurrencyHudPresenter : MonoBehaviour
{
    [SerializeField] private TMP_Text tradingCurrencyText;
    [SerializeField] private TMP_Text developmentCurrencyText;

    [Header("EventChannel")]
    [SerializeField] private CurrencyChangedEventChannel currencyChangedChannel;

    private long lastTradingCurrency = long.MinValue;
    private long lastDevelopmentCurrency = long.MinValue;

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
        lastDevelopmentCurrency = long.MinValue;

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
            ApplyIfChanged(0, 0);
            return;
        }

        ApplyIfChanged(saveData.player.tradingCurrency, saveData.player.developmentCurrency);
    }

    private void HandleCurrencyChanged(CurrencyChangedEventData eventData)
    {
        ApplyIfChanged(eventData.tradingCurrency, eventData.developmentCurrency);
    }

    private void ApplyIfChanged(long tradingCurrency, long developmentCurrency)
    {
        if(tradingCurrency == lastTradingCurrency && developmentCurrency == lastDevelopmentCurrency)
        {
            return;
        }

        lastTradingCurrency = tradingCurrency;
        lastDevelopmentCurrency = developmentCurrency;

        Render(tradingCurrency, developmentCurrency);
    }

    private void Render(long tradingCurrency, long developmentCurrency)
    {
        if (tradingCurrencyText != null)
        {
            tradingCurrencyText.text = CurrencyTextFormatter.Format(tradingCurrency);
        }

        if (developmentCurrencyText != null)
        {
            developmentCurrencyText.text = CurrencyTextFormatter.Format(developmentCurrency);
        }
    }

    
}
