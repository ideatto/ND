/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Settlement UI에 전달할 표시 전용 데이터를 정의한다.
 * - Core JourneyResultData와 Economy M1 금액을 UI가 직접 의존하지 않아도 되는 형태로 정리한다.
 *
 * Main Features
 * - trade ID, 결과 등급, 실패 원인, long 수익/비용/순이익, M2 계산값, claim 가능 여부, 상태 메시지를 제공한다.
 * - 실패 여부를 IsFailed로 계산해 UI 조건 분기를 단순화한다.
 *
 * Usage for Team Members
 * - SettlementUiDataAdapter가 생성하고 ISettlementView 구현체가 읽어서 표시한다.
 *
 * Important Notes
 * - null 문자열 입력은 빈 문자열로 보정된다.
 * - 속성은 생성 이후 외부에서 변경할 수 없다.
 */
namespace ND.Framework
{
    /// <summary>
    /// Settlement 화면에 표시할 정산 결과 view model이다.
    /// </summary>
    public sealed class SettlementViewData
    {
        /// <summary>
        /// Settlement 표시 데이터를 생성한다.
        /// </summary>
        public SettlementViewData(
            string tradeId,
            JourneyResultGrade grade,
            JourneyFailureReason failureReason,
            long revenue,
            long cost,
            long netProfit,
            int cargoLost,
            float durabilityLost,
            float travelSeconds,
            float foodConsumed,
            float departureLoad,
            float overloadRatio,
            bool canClaim,
            string statusMessage)
        {
            TradeId = tradeId ?? string.Empty;
            Grade = grade;
            FailureReason = failureReason;
            Revenue = revenue;
            Cost = cost;
            NetProfit = netProfit;
            CargoLost = cargoLost;
            DurabilityLost = durabilityLost;
            TravelSeconds = travelSeconds;
            FoodConsumed = foodConsumed;
            DepartureLoad = departureLoad;
            OverloadRatio = overloadRatio;
            IsFailed = grade == JourneyResultGrade.Failed;
            CanClaim = canClaim;
            StatusMessage = statusMessage ?? string.Empty;
        }

        public string TradeId { get; private set; }

        public JourneyResultGrade Grade { get; private set; }

        public JourneyFailureReason FailureReason { get; private set; }

        public long Revenue { get; private set; }

        public long Cost { get; private set; }

        public long NetProfit { get; private set; }

        public int CargoLost { get; private set; }

        public float DurabilityLost { get; private set; }

        public float TravelSeconds { get; private set; }

        public float FoodConsumed { get; private set; }

        public float DepartureLoad { get; private set; }

        public float OverloadRatio { get; private set; }

        public bool IsFailed { get; private set; }

        public bool CanClaim { get; private set; }

        public string StatusMessage { get; private set; }
    }
}
