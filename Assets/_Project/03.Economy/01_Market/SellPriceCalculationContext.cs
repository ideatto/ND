namespace ND.Economy
{
    /// <summary>
    /// Immutable inputs used to select contextual sell-price modifiers.
    /// </summary>
    public readonly struct SellPriceCalculationContext
    {
        public SellPriceCalculationContext(
            string seasonId,
            float routeDistanceKm,
            bool isLuckyMoneyActive)
        {
            SeasonId = seasonId ?? string.Empty;
            RouteDistanceKm = routeDistanceKm;
            IsLuckyMoneyActive = isLuckyMoneyActive;
        }

        public string SeasonId { get; }
        public float RouteDistanceKm { get; }
        public bool IsLuckyMoneyActive { get; }
    }
}
