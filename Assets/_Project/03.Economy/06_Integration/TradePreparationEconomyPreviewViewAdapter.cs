using System;

namespace ND.Economy
{
    /// <summary>
    /// Projects Progression-owned preview values into the existing preparation UI DTO.
    /// It never evaluates UI conditions or mutates the underlying draft/save state.
    /// </summary>
    public static class TradePreparationEconomyPreviewViewAdapter
    {
        public static bool TryApply(
            global::TradePrepareViewData target,
            TradePreparationEconomyPreviewResult preview,
            float routeDistance = 0f)
        {
            if (target == null || preview == null || !preview.IsValid)
            {
                return false;
            }

            long purchaseCost = AddClamped(preview.CargoPurchaseCost, preview.FoodCost);
            long currentCurrency = Math.Max(0L, target.currentTradingCurrency);

            target.totalPurchaseCost = purchaseCost;
            target.draftAnimalFoodCost = Math.Max(0L, preview.FoodCost);
            target.mercenaryCost = Math.Max(0L, preview.MercenaryCost);
            target.totalPreparationCost = Math.Max(0L, preview.TotalPreparationCost);
            target.estimatedCurrencyAfterPurchase = SubtractFloorZero(currentCurrency, purchaseCost);
            target.canPurchaseCargo = purchaseCost <= currentCurrency;
            target.estimatedCurrencyAfterHire = Math.Max(0L, preview.CurrencyAfterPreparation);
            target.canHireSelectedMercenaries = preview.TotalPreparationCost <= currentCurrency;
            target.estimatedSellRevenue = Math.Max(0L, preview.ExpectedSellRevenue);
            target.estimatedNetProfit = preview.ExpectedNetProfit;
            target.currentLoad = Math.Max(0f, preview.CurrentLoad);
            target.finalExpectedTravelTime = Math.Max(0f, preview.ExpectedTravelSeconds);

            if (routeDistance > 0f && preview.ExpectedTravelSeconds > 0f)
            {
                target.selectedMoveSpeed = routeDistance / preview.ExpectedTravelSeconds;
            }

            return true;
        }

        private static long AddClamped(long left, long right)
        {
            left = Math.Max(0L, left);
            right = Math.Max(0L, right);
            return left > long.MaxValue - right ? long.MaxValue : left + right;
        }

        private static long SubtractFloorZero(long value, long cost)
        {
            if (cost <= 0L) return value;
            return cost >= value ? 0L : value - cost;
        }
    }
}
