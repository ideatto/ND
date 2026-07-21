/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - SettlementPending 상태에서 앱 종료·재실행 후에도 동일한 정산 결과를 복원하기 위한 영속 DTO이다.
 * - JourneyResultData의 확정 필드와 caravanId·tradeId·claimed·resultVersion을 SaveData에 함께 보관한다.
 *
 * Main Features
 * - 정산 결과 존재 여부(hasResult)와 수령 여부(claimed)를 기록한다.
 * - Core 결과 등급·손실·이동 지표와 Economy 표시 금액을 직렬화한다.
 * - resultVersion으로 지원하지 않는 결과 스키마를 복구 단계에서 차단한다.
 *
 * Usage for Team Members
 * - TradeProgressCoordinator가 Settle 직후 PendingSettlementSaveDataMapper로 기록한다.
 * - 로드 후 RestorePendingSettlement가 이 DTO를 검증해 runtime cache를 재구성한다.
 *
 * Important Notes
 * - Unity JsonUtility 직렬화를 위해 public field 중심이다.
 * - Economy 내부 타입(EconomyM1LoopResult)은 저장하지 않는다.
 * - Related Documentation: Docs/Personal_Documents/CSU/0712_m3-pending-settlement-persist.md
 */
using System;

namespace ND.Framework
{
    /// <summary>
    /// 대기 정산 결과를 영속화하는 저장 DTO이다.
    /// </summary>
    [Serializable]
    public sealed class PendingSettlementSaveData
    {
        /// <summary>이 정산 결과를 소유한 caravan ID이다.</summary>
        public string caravanId = string.Empty;

        /// <summary>
        /// 현재 코드가 지원하는 pending settlement 결과 schema version이다.
        /// </summary>
        public const int CurrentResultVersion = 1;

        /// <summary>
        /// 유효한 정산 결과가 저장되어 있는지 여부이다.
        /// </summary>
        public bool hasResult;

        /// <summary>
        /// 정산 대상 무역 ID이다. activeTradeId와 일치해야 복구·수령이 허용된다.
        /// </summary>
        public string tradeId = string.Empty;

        /// <summary>
        /// 정산 대상 route ID이다.
        /// </summary>
        public string routeId = string.Empty;

        /// <summary>
        /// pending settlement 결과 schema version이다. CurrentResultVersion과 다르면 복구를 차단한다.
        /// </summary>
        public int resultVersion = CurrentResultVersion;

        /// <summary>
        /// 정산 결과 등급이다.
        /// </summary>
        public JourneyResultGrade grade = JourneyResultGrade.Success;

        /// <summary>
        /// 실패 사유이다. Failed가 아니면 None이다.
        /// </summary>
        public JourneyFailureReason failureReason = JourneyFailureReason.None;

        /// <summary>
        /// 잃은 무역품 수량이다.
        /// </summary>
        public int cargoLost;

        /// <summary>
        /// 마차 내구도 손실량이다.
        /// </summary>
        public float durabilityLost;

        /// <summary>
        /// 실제 이동 시간(초)이다.
        /// </summary>
        public float travelSeconds;

        /// <summary>
        /// 총 식량 소모량이다.
        /// </summary>
        public float foodConsumed;

        /// <summary>
        /// 출발 시 적재량이다.
        /// </summary>
        public float departureLoad;

        /// <summary>
        /// 최종 적정 적재량이다.
        /// </summary>
        public float finalEfficientLoad;

        /// <summary>
        /// 과적 비율이다. 적정 이하면 0이다.
        /// </summary>
        public float overloadRatio;

        /// <summary>
        /// 판매 수익이다. 단위: abstract trade money.
        /// </summary>
        public long revenue;

        /// <summary>
        /// 유지비 등 비용이다. 단위: abstract trade money.
        /// </summary>
        public long cost;

        /// <summary>
        /// 순이익이다. 단위: abstract trade money.
        /// </summary>
        public long netProfit;

        /// <summary>
        /// 정산 수령이 완료되었는지 여부이다. true이면 복구와 재수령을 차단한다.
        /// </summary>
        public bool claimed;
    }
}
