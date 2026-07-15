[System.Serializable]
public struct CurrencyChangedEventData
{
    public long tradingCurrency;
    public long developmentCurrency;

    public CurrencyChangedEventData(
        long tradingCurrency,
        long developmentCurrency)
    {
        this.tradingCurrency = tradingCurrency;
        this.developmentCurrency = developmentCurrency;
    }
}