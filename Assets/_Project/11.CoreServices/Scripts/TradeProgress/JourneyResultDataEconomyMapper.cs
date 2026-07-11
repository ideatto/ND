/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Economy M1 SettlementBreakdown을 Core JourneyResultData 금액 필드에 반영한다.
 *
 * Main Features
 * - revenue, cost, netProfit을 SettlementBreakdown에서 복사한다.
 *
 * Important Notes
 * - Economy 계산 실패 시 JourneyResultData 금액 필드는 변경하지 않는다.
 */
using ND.Economy;

namespace ND.Framework
{
    /// <summary>
    /// Economy 정산 결과를 Core JourneyResultData에 매핑하는 helper이다.
    /// </summary>
    public static class JourneyResultDataEconomyMapper
    {
        /// <summary>
        /// Economy M1 loop 결과의 settlement breakdown을 JourneyResultData에 반영한다.
        /// </summary>
        /// <returns>금액 필드 반영에 성공하면 true.</returns>
        public static bool TryApplySettlement(JourneyResultData journeyResult, EconomyM1LoopResult economyResult)
        {
            if (journeyResult == null || economyResult == null || !economyResult.Success || economyResult.Settlement == null)
            {
                return false;
            }

            var settlement = economyResult.Settlement;
            journeyResult.revenue = settlement.TotalRevenue;
            journeyResult.cost = settlement.TotalExpense;
            journeyResult.netProfit = settlement.NetProfit;
            return true;
        }
    }
}
