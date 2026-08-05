namespace ND.Economy
{
    /// <summary>
    /// Read-only bridge from an active trade ID to the persisted weather-lucky count.
    /// This adapter never consumes or mutates lucky state.
    /// </summary>
    public static class WeatherLuckyMoneyStateReader
    {
        public static bool IsActive(string tradeId)
        {
            return global::WeatherLuckyStore.GetCount(tradeId) > 0;
        }
    }
}
