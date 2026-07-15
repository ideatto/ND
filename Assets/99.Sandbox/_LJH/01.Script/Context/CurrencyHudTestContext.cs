using UnityEngine;

public sealed class CurrencyHudTestContext : MonoBehaviour
{
    [SerializeField]
    private CurrencyChangedEventChannel currencyChangedChannel;

    [SerializeField]
    private long testTradingCurrency = 1_334_455L;

    [SerializeField]
    private long testDevelopmentCurrency = 123_456_789L;

    [ContextMenu("Raise Test Currency")]
    public void RaiseTestCurrency()
    {
        currencyChangedChannel?.Raise(
            new CurrencyChangedEventData(
                testTradingCurrency,
                testDevelopmentCurrency));
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
            new CurrencyChangedEventData(
                saveData.player.tradingCurrency,
                saveData.player.developmentCurrency));
    }
}
