using System;

namespace ND.Framework
{
    public sealed class TradeStartService
    {
        private readonly Func<SaveData> getCurrentSaveData;
        private readonly ISaveService saveService;
        private readonly TradeProgressRecorder tradeProgressRecorder;
        private readonly InGameScreenStateRouter inGameScreenRouter;

        public bool LastRecordSucceeded { get; private set; }

        public TradeStartService(
            Func<SaveData> getCurrentSaveData,
            ISaveService saveService,
            TradeProgressRecorder tradeProgressRecorder,
            InGameScreenStateRouter inGameScreenRouter = null)
        {
            this.getCurrentSaveData = getCurrentSaveData;
            this.saveService = saveService;
            this.tradeProgressRecorder = tradeProgressRecorder;
            this.inGameScreenRouter = inGameScreenRouter;
        }

        public DepartureValidationResult TryStartTrade(
            CaravanData caravan,
            float distanceKm,
            string tradeId,
            string routeId,
            bool saveImmediately = true)
        {
            LastRecordSucceeded = false;

            var result = JourneyRunner.TryDepart(caravan, distanceKm);
            if (!result.canDepart)
            {
                return result;
            }

            if (tradeProgressRecorder == null)
            {
                FrameworkLog.Warning("Trade start time was not recorded because trade progress recorder is null.");
                return result;
            }

            var saveData = getCurrentSaveData != null ? getCurrentSaveData() : null;
            var expectedDuration = TimeSpan.FromSeconds(Math.Max(0f, caravan.totalSeconds));

            LastRecordSucceeded = tradeProgressRecorder.RecordStartedTrade(
                saveData,
                tradeId,
                routeId,
                expectedDuration);

            if (LastRecordSucceeded && saveData != null)
            {
                CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            }

            if (LastRecordSucceeded)
            {
                inGameScreenRouter?.RequestScreen(InGameScreenState.Traveling);
            }

            if (LastRecordSucceeded && saveImmediately)
            {
                if (saveService == null)
                {
                    FrameworkLog.Warning("Trade start time was recorded but save was skipped because save service is null.");
                    return result;
                }

                saveService.Save(saveData);
            }

            return result;
        }
    }
}
