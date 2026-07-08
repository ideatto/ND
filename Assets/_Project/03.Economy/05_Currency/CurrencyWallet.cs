using System;

namespace ND.Economy
{
    public static class CurrencyWallet
    {
        public const string ErrorNone = "";
        public const string ErrorInvalidState = "INVALID_CURRENCY_STATE";
        public const string ErrorInvalidSettlement = "INVALID_SETTLEMENT";
        public const string ErrorInvalidGrowthPurchase = "INVALID_GROWTH_PURCHASE";

        public static CurrencyApplyResult ApplySettlement(CurrencyState state, SettlementBreakdown settlement)
        {
            if (state == null)
            {
                return Fail(ErrorInvalidState, null);
            }

            if (settlement == null)
            {
                return Fail(ErrorInvalidSettlement, state);
            }

            CurrencyState before = state.Clone();
            CurrencyState after = state.Clone();

            after.TradeMoney = Math.Max(0L, settlement.TradeMoneyAfter);
            after.DevelopmentCurrency = Math.Max(0L, after.DevelopmentCurrency + settlement.DevelopmentCurrencyReward);

            Copy(after, state);

            return new CurrencyApplyResult
            {
                Success = true,
                ErrorCode = ErrorNone,
                Before = before,
                After = after.Clone()
            };
        }

        public static CurrencyApplyResult ApplyGrowthPurchase(CurrencyState state, GrowthPurchaseResult growthPurchase)
        {
            if (state == null)
            {
                return Fail(ErrorInvalidState, null);
            }

            if (growthPurchase == null || !growthPurchase.Success)
            {
                return Fail(ErrorInvalidGrowthPurchase, state);
            }

            CurrencyState before = state.Clone();
            CurrencyState after = state.Clone();

            after.DevelopmentCurrency = Math.Max(0L, growthPurchase.DevelopmentCurrencyAfter);

            Copy(after, state);

            return new CurrencyApplyResult
            {
                Success = true,
                ErrorCode = ErrorNone,
                Before = before,
                After = after.Clone()
            };
        }

        private static CurrencyApplyResult Fail(string errorCode, CurrencyState state)
        {
            CurrencyState snapshot = state != null ? state.Clone() : null;

            return new CurrencyApplyResult
            {
                Success = false,
                ErrorCode = errorCode,
                Before = snapshot,
                After = snapshot != null ? snapshot.Clone() : null
            };
        }

        private static void Copy(CurrencyState from, CurrencyState to)
        {
            to.TradeMoney = from.TradeMoney;
            to.DevelopmentCurrency = from.DevelopmentCurrency;
        }
    }
}
