using System;

namespace ND.Economy
{
    [Serializable]
    public sealed class CurrencyState
    {
        public int TradeMoney;
        public int DevelopmentCurrency;

        public CurrencyState Clone()
        {
            return new CurrencyState
            {
                TradeMoney = TradeMoney,
                DevelopmentCurrency = DevelopmentCurrency
            };
        }
    }

    [Serializable]
    public sealed class CurrencyApplyResult
    {
        public bool Success;
        public string ErrorCode = string.Empty;
        public CurrencyState Before;
        public CurrencyState After;
    }
}
