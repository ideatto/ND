using System;
using System.Collections.Generic;

namespace ND.Economy
{
    /// <summary>
    /// Immutable inputs used to select contextual sell-price modifiers, including a destination
    /// market specialty-ID snapshot used for per-item origin checks.
    /// </summary>
    public readonly struct SellPriceCalculationContext
    {
        private readonly IReadOnlyList<string> destinationLocalSpecialtyItemIds;

        public SellPriceCalculationContext(
            string seasonId,
            float routeDistanceKm,
            bool isLuckyMoneyActive,
            IReadOnlyList<string> destinationLocalSpecialtyItemIds = null)
        {
            SeasonId = seasonId ?? string.Empty;
            RouteDistanceKm = routeDistanceKm;
            IsLuckyMoneyActive = isLuckyMoneyActive;
            if (destinationLocalSpecialtyItemIds == null
                || destinationLocalSpecialtyItemIds.Count == 0)
            {
                this.destinationLocalSpecialtyItemIds = Array.Empty<string>();
                return;
            }

            var snapshot = new string[destinationLocalSpecialtyItemIds.Count];
            for (int i = 0; i < snapshot.Length; i++)
            {
                snapshot[i] = destinationLocalSpecialtyItemIds[i] ?? string.Empty;
            }

            this.destinationLocalSpecialtyItemIds = Array.AsReadOnly(snapshot);
        }

        public string SeasonId { get; }
        public float RouteDistanceKm { get; }
        public bool IsLuckyMoneyActive { get; }
        public IReadOnlyList<string> DestinationLocalSpecialtyItemIds =>
            destinationLocalSpecialtyItemIds ?? Array.Empty<string>();
    }
}
