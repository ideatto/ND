using System;

namespace ND.Framework
{
    public sealed class TradeProgressRecorder
    {
        private readonly IGameTimeProvider gameTimeProvider;

        public TradeProgressRecorder(IGameTimeProvider gameTimeProvider)
        {
            this.gameTimeProvider = gameTimeProvider;
        }

        public bool RecordStartedTrade(
            SaveData saveData,
            string tradeId,
            string routeId,
            TimeSpan expectedDuration)
        {
            if (saveData == null)
            {
                FrameworkLog.Warning("Trade start time was not recorded because save data is null.");
                return false;
            }

            if (gameTimeProvider == null)
            {
                FrameworkLog.Warning("Trade start time was not recorded because game time provider is null.");
                return false;
            }

            if (saveData.tradeProgress == null)
            {
                saveData.tradeProgress = new TradeProgressSaveData();
            }

            var progress = saveData.tradeProgress;
            var normalizedTradeId = tradeId ?? string.Empty;

            if (expectedDuration < TimeSpan.Zero)
            {
                FrameworkLog.Warning("Negative trade duration was clamped to zero.");
                expectedDuration = TimeSpan.Zero;
            }

            if (IsSameTravelingTradeAlreadyRecorded(progress, normalizedTradeId))
            {
                return false;
            }

            if (IsDifferentTradeAlreadyTraveling(progress, normalizedTradeId))
            {
                FrameworkLog.Warning($"Trade start time was not overwritten. ActiveTradeId: {progress.activeTradeId}");
                return false;
            }

            var startUtc = gameTimeProvider.CurrentUtc;
            var expectedEndUtc = startUtc + expectedDuration;

            progress.activeTradeId = normalizedTradeId;
            progress.activeRouteId = routeId ?? string.Empty;
            progress.state = TradeProgressState.Traveling;
            progress.tradeStartUtcTick = startUtc.Ticks;
            progress.expectedTradeEndUtcTick = expectedEndUtc.Ticks;

            return true;
        }

        public void MarkSettlementPending(SaveData saveData)
        {
            if (saveData == null || saveData.tradeProgress == null)
            {
                return;
            }

            if (saveData.tradeProgress.state == TradeProgressState.Traveling)
            {
                saveData.tradeProgress.state = TradeProgressState.SettlementPending;
            }
        }

        public void MarkCompleted(SaveData saveData)
        {
            if (saveData == null || saveData.tradeProgress == null)
            {
                return;
            }

            saveData.tradeProgress.state = TradeProgressState.Completed;
        }

        public void MarkFailed(SaveData saveData)
        {
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
