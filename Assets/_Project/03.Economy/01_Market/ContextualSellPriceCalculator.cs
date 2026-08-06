using System.Collections.Generic;

namespace ND.Economy
{
    /// <summary>
    /// Calculates item unit prices through the existing calculator after contextual selection.
    /// </summary>
    public static class ContextualSellPriceCalculator
    {
        public static PriceCalculationResult CalculateUnitPrices(
            global::TradeItemData item,
            SellPriceCalculationContext context,
            SellPriceModifierPolicy policy)
        {
            if (item == null)
                return new PriceCalculationResult();

            IReadOnlyList<PriceModifierInput> resolved =
                ContextualSellPriceModifierResolver.Resolve(item, context, policy);
            return PriceCalculator.CalculateUnitPrices(
                item.BaseBuyPrice,
                item.BaseSellPrice,
                new List<PriceModifierInput>(resolved));
        }
    }
}
