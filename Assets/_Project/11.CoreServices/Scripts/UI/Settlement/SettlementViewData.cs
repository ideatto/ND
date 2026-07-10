/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Settlement UI에 전달할 표시 전용 데이터를 정의한다.
 * - Core JourneyResultData를 UI가 직접 의존하지 않아도 되는 형태로 정리한다.
 *
 * Main Features
 * - trade ID, 결과 등급, 실패 원인, 수익/비용/순이익, claim 가능 여부, 상태 메시지를 제공한다.
 * - 실패 여부를 IsFailed로 계산해 UI 조건 분기를 단순화한다.
 *
 * Usage for Team Members
 * - SettlementUiDataAdapter가 생성하고 ISettlementView 구현체가 읽어서 표시한다.
 * - UI 구현체는 이 객체를 표시 데이터로만 사용하고 저장 데이터 변경을 수행하지 않는다.
 *
 * Main Public APIs
 * - SettlementViewData(...): settlement 표시 데이터를 생성한다.
 * - TradeId, Grade, FailureReason, Revenue, Cost, NetProfit, IsFailed, CanClaim, StatusMessage: 표시용 속성.
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
        /// <param name="tradeId">정산 결과가 연결된 trade ID.</param>
        /// <param name="grade">정산 결과 등급.</param>
        /// <param name="failureReason">실패한 경우의 실패 원인.</param>
        /// <param name="revenue">총 수익.</param>
        /// <param name="cost">총 비용.</param>
        /// <param name="netProfit">순이익.</param>
        /// <param name="canClaim">사용자가 현재 claim할 수 있으면 true.</param>
        /// <param name="statusMessage">UI에 표시할 상태 메시지.</param>
        public SettlementViewData(
            string tradeId,
            JourneyResultGrade grade,
            JourneyFailureReason failureReason,
            int revenue,
            int cost,
            int netProfit,
            bool canClaim,
            string statusMessage)
        {
            TradeId = tradeId ?? string.Empty;
            Grade = grade;
            FailureReason = failureReason;
            Revenue = revenue;
            Cost = cost;
            NetProfit = netProfit;
            IsFailed = grade == JourneyResultGrade.Failed;
            CanClaim = canClaim;
            StatusMessage = statusMessage ?? string.Empty;
        }

        /// <summary>
        /// 정산 대상 trade ID이다.
        /// </summary>
        public string TradeId { get; private set; }

        /// <summary>
        /// 정산 결과 등급이다.
        /// </summary>
        public JourneyResultGrade Grade { get; private set; }

        /// <summary>
        /// 실패한 경우의 실패 원인이다.
        /// </summary>
        public JourneyFailureReason FailureReason { get; private set; }

        /// <summary>
        /// 정산 수익이다.
        /// </summary>
        public int Revenue { get; private set; }

        /// <summary>
        /// 정산 비용이다.
        /// </summary>
        public int Cost { get; private set; }

        /// <summary>
        /// 수익에서 비용을 뺀 순이익이다.
        /// </summary>
        public int NetProfit { get; private set; }

        /// <summary>
        /// 결과 등급이 실패인지 여부이다.
        /// </summary>
        public bool IsFailed { get; private set; }

        /// <summary>
        /// UI에서 claim 버튼을 활성화할 수 있는지 여부이다.
        /// </summary>
        public bool CanClaim { get; private set; }

        /// <summary>
        /// settlement 상태를 사용자에게 설명하는 메시지이다.
        /// </summary>
        public string StatusMessage { get; private set; }
    }
}
