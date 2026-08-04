using System;
using System.Collections.Generic;
using ND.Framework;

namespace ND.Economy
{
    /// <summary>
    /// Selects converted price modifiers that are eligible for a market sale committed in one
    /// canonical season. Price arithmetic remains owned by <see cref="PriceCalculator"/>.
    /// </summary>
    public static class SeasonalSellPriceModifierSelector
    {
        public static List<PriceModifierInput> SelectForSellPrice(
            IEnumerable<PriceModifierInput> modifiers,
            string currentSeasonId)
        {
            var selected = new List<PriceModifierInput>();
            if (modifiers == null)
                return selected;

            foreach (PriceModifierInput modifier in modifiers)
            {
                if (modifier == null)
                    continue;

                bool targetsSellPrice = modifier.Target == PriceModifierTarget.SellPrice
                    || modifier.Target == PriceModifierTarget.Both;
                if (modifier.ModifierType != PriceModifierType.Season || !targetsSellPrice)
                {
                    selected.Add(modifier);
                    continue;
                }

                if (IsCanonicalSeasonId(modifier.SourceId)
                    && string.Equals(modifier.SourceId, currentSeasonId, StringComparison.Ordinal))
                {
                    selected.Add(modifier);
                }
            }

            return selected;
        }

        private static bool IsCanonicalSeasonId(string seasonId)
        {
            return string.Equals(seasonId, GameCalendarDate.SpringId, StringComparison.Ordinal)
                || string.Equals(seasonId, GameCalendarDate.SummerId, StringComparison.Ordinal)
                || string.Equals(seasonId, GameCalendarDate.AutumnId, StringComparison.Ordinal)
                || string.Equals(seasonId, GameCalendarDate.WinterId, StringComparison.Ordinal);
        }
    }
}
