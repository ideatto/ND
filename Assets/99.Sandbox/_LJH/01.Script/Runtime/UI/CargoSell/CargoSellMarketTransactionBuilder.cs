using System;
using System.Collections.Generic;
using System.Linq;
using ND.Framework.CargoLoading;
using ND.UI.Market;

namespace ND.UI.CargoSell
{
    /// <summary>Converts the popup's exact purchase groups into existing market sell lines.</summary>
    public static class CargoSellMarketTransactionBuilder
    {
        public static List<MarketTransactionLine> Build(
            IReadOnlyList<CargoSellPendingSaleRowViewData> pending)
        {
            return (pending ?? Array.Empty<CargoSellPendingSaleRowViewData>())
                .Where(item => item != null
                    && !string.IsNullOrWhiteSpace(item.ItemId)
                    && item.Quantity > 0)
                .GroupBy(item => item.ItemId.Trim(), StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new MarketTransactionLine
                {
                    ItemId = group.Key,
                    SellQuantity = group.Sum(item => Math.Max(0, item.Quantity)),
                    SalePriceGroups = group
                        .GroupBy(item => Math.Max(0L, item.PurchaseUnitPrice))
                        .OrderBy(priceGroup => priceGroup.Key)
                        .Select(priceGroup => new MarketSalePriceGroup
                        {
                            PurchaseUnitPrice = priceGroup.Key,
                            Quantity = priceGroup.Sum(item => Math.Max(0, item.Quantity))
                        })
                        .ToList()
                })
                .ToList();
        }
    }
}
