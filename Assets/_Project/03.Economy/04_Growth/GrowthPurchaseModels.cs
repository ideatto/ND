using System;

namespace ND.Economy
{
    public enum GrowthPurchaseError
    {
        None,
        InvalidGrowthId,
        AlreadyMaxLevel,
        NotEnoughDevelopmentCurrency,
        InvalidCost
    }

    [Serializable]
    public sealed class GrowthPurchaseInput
    {
        public string GrowthId = string.Empty;
        public int CurrentLevel;
        public int MaxLevel = 1;
        public long DevelopmentCurrencyBefore;
        public long CostDevelopmentCurrency = 1L;
    }

    [Serializable]
    public sealed class GrowthPurchaseResult
    {
        public string GrowthId = string.Empty;
        public bool Success;
        public GrowthPurchaseError Error;
        public int PreviousLevel;
        public int NewLevel;
        public long CostDevelopmentCurrency;
        public long DevelopmentCurrencyAfter;
    }
}
