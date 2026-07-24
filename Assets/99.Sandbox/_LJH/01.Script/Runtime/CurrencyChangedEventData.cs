[System.Serializable]
public struct CurrencyChangedEventData
{
    // The HUD displays trade money only, so development currency is intentionally excluded from this UI event.
    public long tradingCurrency;

    public CurrencyChangedEventData(long tradingCurrency)
    {
        this.tradingCurrency = tradingCurrency;
    }
}
