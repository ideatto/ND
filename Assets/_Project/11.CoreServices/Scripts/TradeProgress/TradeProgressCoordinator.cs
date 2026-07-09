using System;

namespace ND.Framework
{
    public sealed class TradeProgressCoordinator
    {
        private readonly Func<SaveData> getCurrentSaveData;
        private readonly ISaveService saveService;
        private readonly IGameTimeProvider gameTimeProvider;
        private readonly TradeProgressRecorder tradeProgressRecorder;
        private readonly InGameScreenStateRouter inGameScreenRouter;

        private CaravanData activeCaravan;

        public TradeProgressCoordinator(
            Func<SaveData> getCurrentSaveData,
            ISaveService saveService,
            IGameTimeProvider gameTimeProvider,
            TradeProgressRecorder tradeProgressRecorder,
            InGameScreenStateRouter inGameScreenRouter = null)
        {
            this.getCurrentSaveData = getCurrentSaveData;
            this.saveService = saveService;
            this.gameTimeProvider = gameTimeProvider;
            this.tradeProgressRecorder = tradeProgressRecorder;
            this.inGameScreenRouter = inGameScreenRouter;

            FrameworkEvents.CompleteTradeRequested += ForceCompleteActiveTrade;
        }

        public string LastSettlementTradeId { get; private set; } = string.Empty;
        public JourneyResultData LastSettlementResult { get; private set; }

        public CaravanData ActiveCaravan
        {
            get
            {
                EnsureActiveCaravan();
                return activeCaravan;
            }
        }

        public void SetActiveCaravan(CaravanData caravan)
        {
            activeCaravan = caravan;
        }

        public bool CheckProgressAndCompletion(bool saveProgress = true)
        {
            var saveData = GetSaveData();
            if (!CanUpdateTravelingTrade(saveData))
            {
                return false;
            }

            var caravan = EnsureActiveCaravan();
            if (caravan == null)
            {
                FrameworkLog.Warning("Trade progress check skipped because active caravan is missing.");
                return false;
            }

            var progress = CalculateProgress(saveData.tradeProgress);
            JourneyRunner.SetProgress(caravan, progress);
            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);

            if (!JourneyRunner.IsArrived(caravan) && caravan.runFatalReason == JourneyFailureReason.None)
            {
                if (saveProgress)
                {
                    saveService?.Save(saveData);
                }

                return false;
            }

            return SettleActiveTrade(saveData, caravan);
        }

        public bool ClaimSettlementAndReset()
        {
            var saveData = GetSaveData();
            var caravan = EnsureActiveCaravan();
            if (saveData == null || caravan == null)
            {
                return false;
            }

            if (!CanClaimCachedSettlement(saveData))
            {
                return false;
            }

            if (!JourneyRunner.ClaimSettlement(caravan))
            {
                return false;
            }

            if (LastSettlementResult != null && LastSettlementResult.grade == JourneyResultGrade.Failed)
            {
                tradeProgressRecorder?.MarkFailed(saveData);
            }
            else
            {
                tradeProgressRecorder?.MarkCompleted(saveData);
            }

            JourneyRunner.ResetToPrepare(caravan);
            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            saveService?.Save(saveData);
            inGameScreenRouter?.RequestScreen(InGameScreenState.Preparation);
            ClearSettlementCache();

            return true;
        }

        public void ClearSettlementCache()
        {
            LastSettlementTradeId = string.Empty;
            LastSettlementResult = null;
        }

        public void ForceCompleteActiveTrade()
        {
            var saveData = GetSaveData();
            if (!CanUpdateTravelingTrade(saveData))
            {
                return;
            }

            var caravan = EnsureActiveCaravan();
            if (caravan == null)
            {
                FrameworkLog.Warning("Immediate trade completion skipped because active caravan is missing.");
                return;
            }

            JourneyRunner.SetProgress(caravan, JourneyRunner.ArrivalProgress);
            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            SettleActiveTrade(saveData, caravan);
        }

        private bool SettleActiveTrade(SaveData saveData, CaravanData caravan)
        {
            var result = JourneyRunner.Settle(caravan);
            if (result == null)
            {
                return false;
            }

            LastSettlementTradeId = saveData.tradeProgress.activeTradeId ?? string.Empty;
            LastSettlementResult = result;
            tradeProgressRecorder?.MarkSettlementPending(saveData);
            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            saveService?.Save(saveData);
            FrameworkEvents.RaiseTradeSettlementReady(LastSettlementTradeId, result);
            inGameScreenRouter?.RequestScreen(InGameScreenState.Settlement);

            return true;
        }

        private bool CanClaimCachedSettlement(SaveData saveData)
        {
            if (LastSettlementResult == null)
            {
                FrameworkLog.Warning("Settlement claim blocked because cached settlement result is missing.");
                return false;
            }

            if (saveData.tradeProgress == null)
            {
                FrameworkLog.Warning("Settlement claim blocked because trade progress save data is missing.");
                return false;
            }

            if (saveData.tradeProgress.state != TradeProgressState.SettlementPending)
            {
                FrameworkLog.Warning($"Settlement claim blocked because trade state is {saveData.tradeProgress.state}.");
                return false;
            }

            var activeTradeId = saveData.tradeProgress.activeTradeId ?? string.Empty;
            if (string.IsNullOrEmpty(LastSettlementTradeId) || LastSettlementTradeId != activeTradeId)
            {
                FrameworkLog.Warning(
                    $"Settlement claim blocked because cached trade ID does not match active trade ID. Cached: {LastSettlementTradeId}, Active: {activeTradeId}");
                return false;
            }

            return true;
        }

        private CaravanData EnsureActiveCaravan()
        {
            if (activeCaravan != null)
            {
                return activeCaravan;
            }

            var saveData = GetSaveData();
            if (saveData == null || saveData.caravan == null)
            {
                return null;
            }

            activeCaravan = CaravanSaveDataMapper.ToRuntime(saveData.caravan);
            return activeCaravan;
        }

        private SaveData GetSaveData()
        {
            return getCurrentSaveData != null ? getCurrentSaveData() : null;
        }

        private bool CanUpdateTravelingTrade(SaveData saveData)
        {
            if (saveData == null || saveData.tradeProgress == null)
            {
                return false;
            }

            if (saveData.tradeProgress.state != TradeProgressState.Traveling)
            {
                return false;
            }

            if (gameTimeProvider == null)
            {
                FrameworkLog.Warning("Trade progress check skipped because game time provider is missing.");
                return false;
            }

            return true;
        }

        private float CalculateProgress(TradeProgressSaveData progress)
        {
            var startTicks = progress.tradeStartUtcTick;
            var endTicks = progress.expectedTradeEndUtcTick;
            if (startTicks <= 0 || endTicks <= startTicks)
            {
                return JourneyRunner.ArrivalProgress;
            }

            var startUtc = new DateTime(startTicks, DateTimeKind.Utc);
            var endUtc = new DateTime(endTicks, DateTimeKind.Utc);
            var totalSeconds = (endUtc - startUtc).TotalSeconds;
            if (totalSeconds <= 0d)
            {
                return JourneyRunner.ArrivalProgress;
            }

            var elapsedSeconds = (gameTimeProvider.CurrentUtc - startUtc).TotalSeconds;
            return (float)(elapsedSeconds / totalSeconds);
        }
    }
}
