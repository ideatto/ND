/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 월드맵 등 읽기 전용 소비자가 무역 진행 표시에 필요한 스냅샷을 담는다.
 * - TradeProgressCoordinator.TryGetMapProgress가 반환하는 값 객체이다.
 *
 * Main Features
 * - active trade ID, route ID, TradeProgressState를 제공한다.
 * - UTC tick 기반 Progress01과 ProgressPercent를 제공한다.
 *
 * Usage for Team Members
 * - FrameworkRoot.Instance.TradeProgressCoordinator.TryGetMapProgress(out snapshot)로 조회한다.
 * - 반환된 스냅샷은 읽기 전용으로 사용하고 저장 데이터나 무역 상태를 변경하지 않는다.
 *
 * Important Notes
 * - Progress01은 TradeProgressCoordinator의 내부 CalculateProgress와 동일한 UTC 공식을 따른다.
 * - ProgressPercent는 Progress01 * 100이다 (0~100).
 * - Related Documentation: Docs/Guide/Framework_World_Map_API_Guide.md
 */
namespace ND.Framework
{
    /// <summary>
    /// 월드맵 표시용 무역 진행 읽기 전용 스냅샷이다.
    /// </summary>
    public readonly struct TradeMapProgressSnapshot
    {
        /// <summary>
        /// 맵에 표시할 의미 있는 active trade가 있는지 여부이다.
        /// </summary>
        public bool HasActiveTrade { get; }

        /// <summary>
        /// 현재 진행 또는 정산 대기 중인 무역 ID이다.
        /// </summary>
        public string ActiveTradeId { get; }

        /// <summary>
        /// 현재 진행 또는 정산 대기 중인 route ID이다.
        /// </summary>
        public string ActiveRouteId { get; }

        /// <summary>
        /// 저장 데이터 기준 무역 진행 상태이다.
        /// </summary>
        public TradeProgressState State { get; }

        /// <summary>
        /// 현실 UTC 기준 진행률이다. 범위는 0~1이다.
        /// </summary>
        public float Progress01 { get; }

        /// <summary>
        /// 무역 시작 시각의 UTC ticks 값이다.
        /// </summary>
        public long TradeStartUtcTick { get; }

        /// <summary>
        /// 예상 도착 시각의 UTC ticks 값이다.
        /// </summary>
        public long ExpectedTradeEndUtcTick { get; }

        /// <summary>
        /// Progress01을 0~100 퍼센트로 변환한 값이다.
        /// </summary>
        public float ProgressPercent => Progress01 * 100f;

        /// <summary>
        /// 맵 진행 스냅샷 필드를 초기화한다.
        /// </summary>
        public TradeMapProgressSnapshot(
            bool hasActiveTrade,
            string activeTradeId,
            string activeRouteId,
            TradeProgressState state,
            float progress01,
            long tradeStartUtcTick,
            long expectedTradeEndUtcTick)
        {
            HasActiveTrade = hasActiveTrade;
            ActiveTradeId = activeTradeId ?? string.Empty;
            ActiveRouteId = activeRouteId ?? string.Empty;
            State = state;
            Progress01 = progress01;
            TradeStartUtcTick = tradeStartUtcTick;
            ExpectedTradeEndUtcTick = expectedTradeEndUtcTick;
        }
    }
}
