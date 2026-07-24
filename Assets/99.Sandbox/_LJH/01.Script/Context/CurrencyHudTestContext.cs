using UnityEngine;

public sealed class CurrencyHudTestContext : MonoBehaviour
{
    [SerializeField]
    private CurrencyChangedEventChannel currencyChangedChannel;

    [SerializeField]
    private long testTradingCurrency = 1_334_455L;

    [ContextMenu("Raise Test Currency")]
    public void RaiseTestCurrency()
    {
        // The HUD event contract intentionally tests trade money without development currency.
        currencyChangedChannel?.Raise(
            new CurrencyChangedEventData(testTradingCurrency));
    }

    [ContextMenu("Raise Current SaveData Currency")]
    public void RaiseCurrentSaveDataCurrency()
    {
        ND.Framework.SaveData saveData =
            ND.Framework.FrameworkRoot.Instance
                ?.CurrentSaveData;

        if (saveData?.player == null)
        {
            Debug.LogWarning(
                "Framework SaveData is unavailable.");
            return;
        }

        currencyChangedChannel?.Raise(
            new CurrencyChangedEventData(saveData.player.tradingCurrency));
    }
}
