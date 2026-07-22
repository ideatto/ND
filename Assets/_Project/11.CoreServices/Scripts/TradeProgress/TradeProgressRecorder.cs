/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 무역 진행 상태와 시간 정보를 SaveData.tradeProgress에 기록한다.
 * - 무역 출발, 정산 대기, 완료, 실패 상태 전환을 저장 데이터에 반영한다.
 *
 * Main Features
 * - UTC tick 기준 무역 시작/예상 종료 시각을 기록한다.
 * - 이미 진행 중인 무역의 중복 기록과 다른 무역 덮어쓰기를 차단한다.
 * - 정산 대기, 완료, 실패 상태를 저장 데이터에 기록한다.
 *
 * Usage for Team Members
 * - TradeStartService가 출발 확정 전에 caravan ID를 지정해 RecordStartedTrade(...)를 호출한다.
 * - TradeProgressCoordinator가 정산 생성과 claim 후 상태 기록에 사용한다.
 *
 * Main Public APIs
 * - RecordStartedTrade(...): active trade ID, route ID, 시작/종료 tick을 기록한다.
 * - MarkSettlementPending(...): traveling 상태를 settlement pending으로 전환한다.
 * - MarkCompleted(...): 정산 claim 이후 완료 상태를 기록한다.
 * - MarkFailed(...): 정산 claim 이후 실패 상태를 기록한다.
 *
 * Important Notes
 * - RecordStartedTrade는 saveData와 gameTimeProvider가 유효해야 성공한다.
 * - 같은 trade ID가 이미 traveling으로 기록되어 있으면 중복 호출로 보고 false를 반환한다.
 */
using System;

namespace ND.Framework
{
    /// <summary>
    /// 무역 진행 상태와 시간 정보를 저장 데이터에 기록하는 서비스이다.
    /// </summary>
    public sealed class TradeProgressRecorder
    {
        private readonly IGameTimeProvider gameTimeProvider;
        private readonly IInGameTimeProvider inGameTimeProvider;

        /// <summary>
        /// 시간 제공자를 주입받아 recorder를 생성한다.
        /// </summary>
        /// <param name="gameTimeProvider">UTC 현재 시각을 제공하는 서비스.</param>
        /// <param name="inGameTimeProvider">출발 시점 인게임 배율을 읽을 서비스. null이면 gameTimeProvider에서 조회한다.</param>
        public TradeProgressRecorder(IGameTimeProvider gameTimeProvider, IInGameTimeProvider inGameTimeProvider = null)
        {
            this.gameTimeProvider = gameTimeProvider;
            this.inGameTimeProvider = inGameTimeProvider ?? gameTimeProvider as IInGameTimeProvider;
        }

        /// <summary>
        /// 무역 출발 정보를 저장 데이터에 기록한다.
        /// </summary>
        /// <param name="saveData">기록 대상 저장 데이터.</param>
        /// <param name="tradeId">시작할 무역 ID. null 또는 빈 문자열이면 실패한다.</param>
        /// <param name="routeId">무역 route ID. null이면 빈 문자열로 저장된다.</param>
        /// <param name="expectedDuration">예상 이동 시간. 음수면 0으로 보정된다.</param>
        /// <returns>저장 데이터 기록에 성공하면 true, 입력 오류나 중복 진행 중이면 false.</returns>
        /// <remarks>
        /// 성공 시 saveData.tradeProgress가 Traveling 상태로 변경되고 UTC 시작/종료 tick이 기록된다.
        /// </remarks>
        public bool RecordStartedTrade(
            SaveData saveData,
            string tradeId,
            string routeId,
            TimeSpan expectedDuration)
        {
            return RecordStartedTrade(
                saveData,
                saveData != null ? saveData.selectedCaravanId : string.Empty,
                tradeId,
                routeId,
                expectedDuration);
        }

        /// <summary>
        /// 지정한 caravan의 무역 출발 정보를 저장 데이터에 기록한다.
        /// </summary>
        /// <param name="saveData">기록 대상 저장 데이터.</param>
        /// <param name="caravanId">진행 상태를 소유할 caravan ID.</param>
        /// <param name="tradeId">시작할 무역 ID.</param>
        /// <param name="routeId">유효성이 확인된 route ID.</param>
        /// <param name="expectedDuration">예상 이동 시간.</param>
        /// <returns>대상 caravan의 진행 상태 기록에 성공하면 true.</returns>
        public bool RecordStartedTrade(
            SaveData saveData,
            string caravanId,
            string tradeId,
            string routeId,
            TimeSpan expectedDuration)
        {
            // 저장 데이터가 없으면 active trade ID를 복구할 수 없으므로 출발 기록을 거부한다.
            if (saveData == null)
            {
                FrameworkLog.Warning("Trade start time was not recorded because save data is null.");
                return false;
            }

            // 시간 제공자가 없으면 시작/종료 tick을 신뢰할 수 없으므로 기록을 중단한다.
            if (gameTimeProvider == null)
            {
                FrameworkLog.Warning("Trade start time was not recorded because game time provider is null.");
                return false;
            }

            CaravanSaveData caravan;
            if (!SaveDataLookup.TryGetCaravan(saveData, caravanId, out caravan))
            {
                FrameworkLog.Warning($"Trade start time was not recorded because caravan was not found. CaravanId: {caravanId}");
                return false;
            }

            TradeProgressSaveData progress;
            var hasProgress = SaveDataLookup.TryGetTradeProgress(saveData, caravanId, out progress);
            if (!hasProgress)
            {
                progress = new TradeProgressSaveData { caravanId = caravanId };
            }
            var normalizedTradeId = tradeId ?? string.Empty;
            // 식별자 없는 무역은 이후 정산과 UI 이벤트를 연결할 수 없으므로 차단한다.
            if (string.IsNullOrEmpty(normalizedTradeId))
            {
                FrameworkLog.Warning("Trade start time was not recorded because trade ID is empty.");
                return false;
            }

            // 음수 duration은 잘못된 계산 결과지만 즉시 도착으로 처리할 수 있도록 0으로 보정한다.
            if (expectedDuration < TimeSpan.Zero)
            {
                FrameworkLog.Warning("Negative trade duration was clamped to zero.");
                expectedDuration = TimeSpan.Zero;
            }

            // 같은 무역이 이미 기록되어 있으면 중복 출발 요청으로 판단해 기존 tick을 유지한다.
            if (IsSameTravelingTradeAlreadyRecorded(progress, normalizedTradeId))
            {
                return false;
            }

            // 다른 active trade가 이동 중이면 저장 데이터를 덮어써 복구할 수 없으므로 새 기록을 거부한다.
            if (IsDifferentTradeAlreadyTraveling(progress, normalizedTradeId))
            {
                FrameworkLog.Warning($"Trade start time was not overwritten. ActiveTradeId: {progress.activeTradeId}");
                return false;
            }

            var startUtc = gameTimeProvider.CurrentUtc;
            var expectedEndUtc = startUtc + expectedDuration;

            // 저장 데이터의 active trade 상태를 traveling으로 전환하고 시간 기준점을 UTC tick으로 기록한다.
            progress.activeTradeId = normalizedTradeId;
            progress.activeRouteId = routeId ?? string.Empty;
            progress.state = TradeProgressState.Traveling;
            progress.tradeStartUtcTick = startUtc.Ticks;
            progress.expectedTradeEndUtcTick = expectedEndUtc.Ticks;
            progress.inGameTimeMultiplierAtStart = inGameTimeProvider != null
                ? inGameTimeProvider.InGameTimeMultiplier
                : 1f;
            caravan.elapsedInGameSeconds = 0f;
            SaveDataLookup.SetTradeProgress(saveData, caravanId, progress);

            return true;
        }

        /// <summary>
        /// traveling 상태의 무역을 정산 대기 상태로 전환한다.
        /// </summary>
        /// <param name="saveData">변경할 저장 데이터.</param>
        /// <remarks>
        /// 현재 상태가 Traveling일 때만 SettlementPending으로 변경한다.
        /// </remarks>
        public void MarkSettlementPending(SaveData saveData)
        {
            // 저장 데이터가 준비되지 않았으면 상태 전환을 안전하게 생략한다.
            if (saveData == null || saveData.tradeProgress == null)
            {
                return;
            }

            // traveling이 아닌 상태를 정산 대기로 승격하면 중복 정산이 생길 수 있어 제한한다.
            if (saveData.tradeProgress.state == TradeProgressState.Traveling)
            {
                saveData.tradeProgress.state = TradeProgressState.SettlementPending;
            }
        }

        /// <summary>
        /// 무역 진행 상태를 완료로 기록한다.
        /// </summary>
        /// <param name="saveData">변경할 저장 데이터.</param>
        public void MarkCompleted(SaveData saveData)
        {
            // 저장 데이터가 없으면 호출자가 실패 검증을 수행할 수 있도록 조용히 반환한다.
            if (saveData == null || saveData.tradeProgress == null)
            {
                return;
            }

            saveData.tradeProgress.state = TradeProgressState.Completed;
        }

        /// <summary>
        /// 무역 진행 상태를 실패로 기록한다.
        /// </summary>
        /// <param name="saveData">변경할 저장 데이터.</param>
        public void MarkFailed(SaveData saveData)
        {
            // 저장 데이터가 없으면 호출자가 실패 검증을 수행할 수 있도록 조용히 반환한다.
            if (saveData == null || saveData.tradeProgress == null)
            {
                return;
            }

            saveData.tradeProgress.state = TradeProgressState.Failed;
        }

        private static bool IsSameTravelingTradeAlreadyRecorded(TradeProgressSaveData progress, string tradeId)
        {
            return progress.state == TradeProgressState.Traveling
                && progress.activeTradeId == tradeId
                && progress.tradeStartUtcTick > 0;
        }

        private static bool IsDifferentTradeAlreadyTraveling(TradeProgressSaveData progress, string tradeId)
        {
            return progress.state == TradeProgressState.Traveling
                && !string.IsNullOrEmpty(progress.activeTradeId)
                && progress.activeTradeId != tradeId;
        }
    }
}
