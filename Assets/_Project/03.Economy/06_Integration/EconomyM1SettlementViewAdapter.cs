using System;

namespace ND.Economy
{
    /// <summary>
    /// UI 정산 화면이 표시만 할 수 있도록 M1 결과를 묶은 ViewData.
    /// 금액·화폐·성장 값을 다시 계산하지 않는다.
    /// </summary>
    [Serializable]
    public sealed class EconomyM1SettlementViewData
    {
        public bool Success;
        public string ErrorCode = string.Empty;
        public PriceCalculationResult PriceResult;
        public SettlementBreakdown Settlement;
        public GrowthPurchaseResult GrowthPurchase;
        public CoreRuntimeStatModifier RuntimeStats;
    }

    public static class EconomyM1SettlementViewAdapter
    {
        public static EconomyM1SettlementViewData Create(EconomyM1LoopResult result)
        {
            if (result == null)
            {
                return new EconomyM1SettlementViewData
                {
                    Success = false,
                    ErrorCode = "NULL_ECONOMY_M1_RESULT"
                };
            }

            return new EconomyM1SettlementViewData
            {
                Success = result.Success,
                ErrorCode = result.ErrorCode ?? string.Empty,
                PriceResult = result.PriceResult,
                Settlement = result.Settlement,
                GrowthPurchase = result.GrowthPurchase,
                RuntimeStats = result.RuntimeStats
            };
        }
    }
}
