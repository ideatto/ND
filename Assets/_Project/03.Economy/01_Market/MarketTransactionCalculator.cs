using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public static class MarketTransactionCalculator
    {
        public const string StoverItemId = "Stover";
        public const int FixedStoverMarketStock = 999;

        public static MarketTransactionResult CalculateMarketTransaction(MarketTransactionInput input)
        {
            if (input == null)
            {
                return Fail(MarketTransactionFailureReason.InvalidInput, null, 0L, 0f);
            }

            var result = new MarketTransactionResult
            {
                TradingCurrencyBefore = input.TradingCurrencyBefore,
                TradingCurrencyAfter = input.TradingCurrencyBefore,
                CargoWeightBefore = input.CurrentCargoWeight,
                CargoWeightAfter = input.CurrentCargoWeight,
                CargoSlotsBefore = input.CurrentCargoSlots,
                CargoSlotsAfter = input.CurrentCargoSlots
            };

            if (input.TradingCurrencyBefore < 0L
                || float.IsNaN(input.CurrentCargoWeight)
                || float.IsInfinity(input.CurrentCargoWeight)
                || input.CurrentCargoWeight < 0f
                || float.IsNaN(input.MaximumCargoWeight)
                || input.MaximumCargoWeight < 0f
                || input.CurrentCargoSlots < 0
                || input.MaximumCargoSlots < 0
                || input.Items == null
                || input.Items.Count == 0)
            {
                result.FailureReason = MarketTransactionFailureReason.InvalidInput;
                return result;
            }

            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                double weightAfter = input.CurrentCargoWeight;
                int slotsAfter = input.CurrentCargoSlots;
                foreach (MarketTransactionItemInput item in input.Items)
                {
                    MarketTransactionFailureReason validation = ValidateItem(item, itemIds);
                    if (validation != MarketTransactionFailureReason.None)
                    {
                        result.FailureReason = validation;
                        result.FailedItemId = item?.ItemId ?? string.Empty;
                        return result;
                    }

                    if (item.SellQuantity > item.CargoQuantityBefore)
                    {
                        result.FailureReason = MarketTransactionFailureReason.InsufficientCargo;
                        result.FailedItemId = item.ItemId;
                        return result;
                    }

                    int marketStockBefore = GetEffectiveMarketStock(
                        item.ItemId,
                        item.MarketStockBefore);

                    if (item.BuyQuantity > marketStockBefore)
                    {
                        result.FailureReason = MarketTransactionFailureReason.InsufficientStock;
                        result.FailedItemId = item.ItemId;
                        return result;
                    }

                    long purchaseCost = checked(item.BuyUnitPrice * item.BuyQuantity);
                    long saleRevenue = checked(item.SellUnitPrice * item.SellQuantity);
                    int cargoAfter = checked(item.CargoQuantityBefore + item.BuyQuantity - item.SellQuantity);
                    int stockAfter = IsStover(item.ItemId)
                        ? FixedStoverMarketStock
                        : checked(marketStockBefore - item.BuyQuantity + item.SellQuantity);
                    float weightDelta = item.UnitWeight * (item.BuyQuantity - item.SellQuantity);
                    int slotsBeforeForItem = GetRequiredSlots(item.CargoQuantityBefore, item.MaxStackQuantity);
                    int slotsAfterForItem = GetRequiredSlots(cargoAfter, item.MaxStackQuantity);

                    result.TotalPurchaseCost = checked(result.TotalPurchaseCost + purchaseCost);
                    result.TotalSaleRevenue = checked(result.TotalSaleRevenue + saleRevenue);
                    weightAfter += weightDelta;
                    slotsAfter = checked(slotsAfter - slotsBeforeForItem + slotsAfterForItem);
                    result.Items.Add(new MarketTransactionItemResult
                    {
                        ItemId = item.ItemId,
                        BuyQuantity = item.BuyQuantity,
                        SellQuantity = item.SellQuantity,
                        CargoQuantityBefore = item.CargoQuantityBefore,
                        CargoQuantityAfter = cargoAfter,
                        MarketStockBefore = marketStockBefore,
                        MarketStockAfter = stockAfter,
                        PurchaseCost = purchaseCost,
                        SaleRevenue = saleRevenue,
                        CargoWeightDelta = weightDelta
                    });
                }

                result.NetTradeMoneyChange = checked(result.TotalSaleRevenue - result.TotalPurchaseCost);
                result.TradingCurrencyAfter = checked(result.TradingCurrencyBefore + result.NetTradeMoneyChange);
                if (result.TradingCurrencyAfter < 0L)
                {
                    result.TradingCurrencyAfter = result.TradingCurrencyBefore;
                    result.FailureReason = MarketTransactionFailureReason.InsufficientCurrency;
                    return result;
                }

                result.CargoWeightAfter = weightAfter >= float.MaxValue
                    ? float.PositiveInfinity
                    : Math.Max(0f, (float)weightAfter);
                if (float.IsInfinity(result.CargoWeightAfter)
                    || (!float.IsPositiveInfinity(input.MaximumCargoWeight)
                        && result.CargoWeightAfter > input.MaximumCargoWeight + 0.0001f
                        && result.CargoWeightAfter > result.CargoWeightBefore + 0.0001f))
                {
                    result.CargoWeightAfter = result.CargoWeightBefore;
                    result.FailureReason = MarketTransactionFailureReason.CargoWeightExceeded;
                    return result;
                }

                result.CargoSlotsAfter = slotsAfter;
                if (result.CargoSlotsAfter > input.MaximumCargoSlots
                    && result.CargoSlotsAfter > result.CargoSlotsBefore)
                {
                    result.CargoSlotsAfter = result.CargoSlotsBefore;
                    result.FailureReason = MarketTransactionFailureReason.CargoSlotExceeded;
                    return result;
                }

                result.Success = true;
                result.FailureReason = MarketTransactionFailureReason.None;
                return result;
            }
            catch (OverflowException)
            {
                result.TradingCurrencyAfter = result.TradingCurrencyBefore;
                result.CargoWeightAfter = result.CargoWeightBefore;
                result.CargoSlotsAfter = result.CargoSlotsBefore;
                result.FailureReason = MarketTransactionFailureReason.Overflow;
                return result;
            }
        }

        private static MarketTransactionFailureReason ValidateItem(
            MarketTransactionItemInput item,
            ISet<string> itemIds)
        {
            if (item == null
                || string.IsNullOrWhiteSpace(item.ItemId)
                || item.CargoQuantityBefore < 0
                || item.MarketStockBefore < 0
                || item.BuyQuantity < 0
                || item.SellQuantity < 0
                || item.BuyUnitPrice < 0L
                || item.SellUnitPrice < 0L
                || float.IsNaN(item.UnitWeight)
                || float.IsInfinity(item.UnitWeight)
                || item.UnitWeight < 0f
                || item.MaxStackQuantity <= 0
                || (item.BuyQuantity == 0 && item.SellQuantity == 0)
                || (item.BuyQuantity > 0 && item.SellQuantity > 0))
            {
                return MarketTransactionFailureReason.InvalidInput;
            }

            return itemIds.Add(item.ItemId)
                ? MarketTransactionFailureReason.None
                : MarketTransactionFailureReason.DuplicateItem;
        }

        private static int GetRequiredSlots(int quantity, int maxStackQuantity)
        {
            if (quantity <= 0) return 0;
            return checked((quantity - 1) / maxStackQuantity + 1);
        }

        private static bool IsStover(string itemId)
        {
            return string.Equals(itemId, StoverItemId, StringComparison.OrdinalIgnoreCase);
        }

        public static int GetEffectiveMarketStock(string itemId, int storedStock)
        {
            return IsStover(itemId)
                ? FixedStoverMarketStock
                : Math.Max(0, storedStock);
        }

        private static MarketTransactionResult Fail(
            MarketTransactionFailureReason reason,
            string itemId,
            long currency,
            float weight)
        {
            return new MarketTransactionResult
            {
                Success = false,
                FailureReason = reason,
                FailedItemId = itemId ?? string.Empty,
                TradingCurrencyBefore = currency,
                TradingCurrencyAfter = currency,
                CargoWeightBefore = weight,
                CargoWeightAfter = weight
            };
        }
    }
}
