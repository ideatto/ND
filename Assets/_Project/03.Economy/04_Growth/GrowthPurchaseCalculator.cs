using System;

namespace ND.Economy
{
    public static class GrowthPurchaseCalculator
    {
        public static GrowthPurchaseResult Purchase(GrowthPurchaseInput input)
        {
            if (input == null || string.IsNullOrWhiteSpace(input.GrowthId))
            {
                return Fail(input, GrowthPurchaseError.InvalidGrowthId);
            }

            int maxLevel = Math.Max(1, input.MaxLevel);
            int currentLevel = Math.Max(0, input.CurrentLevel);
            int cost = input.CostDevelopmentCurrency;

            if (currentLevel >= maxLevel)
            {
                return Fail(input, GrowthPurchaseError.AlreadyMaxLevel);
            }

            if (cost <= 0)
            {
                return Fail(input, GrowthPurchaseError.InvalidCost);
            }

            if (input.DevelopmentCurrencyBefore < cost)
            {
                return Fail(input, GrowthPurchaseError.NotEnoughDevelopmentCurrency);
            }

            return new GrowthPurchaseResult
            {
                GrowthId = input.GrowthId,
                Success = true,
                Error = GrowthPurchaseError.None,
                PreviousLevel = currentLevel,
                NewLevel = currentLevel + 1,
                CostDevelopmentCurrency = cost,
                DevelopmentCurrencyAfter = input.DevelopmentCurrencyBefore - cost
            };
        }

        private static GrowthPurchaseResult Fail(GrowthPurchaseInput input, GrowthPurchaseError error)
        {
            return new GrowthPurchaseResult
            {
                GrowthId = input != null ? input.GrowthId : string.Empty,
                Success = false,
                Error = error,
                PreviousLevel = input != null ? Math.Max(0, input.CurrentLevel) : 0,
                NewLevel = input != null ? Math.Max(0, input.CurrentLevel) : 0,
                CostDevelopmentCurrency = input != null ? Math.Max(0, input.CostDevelopmentCurrency) : 0,
                DevelopmentCurrencyAfter = input != null ? Math.Max(0, input.DevelopmentCurrencyBefore) : 0
            };
        }
    }
}
