using System;

namespace ND.Framework
{
    public sealed class TradeStartService
    {
        private readonly Func<SaveData> getCurrentSaveData;
        private readonly ISaveService saveService;
        private readonly TradeProgressRecorder tradeProgressRecorder;
        private readonly InGameScreenStateRouter inGameScreenRouter;
        private readonly Action clearSettlementCache;

        public bool LastRecordSucceeded { get; private set; }

        public TradeStartService(
            Func<SaveData> getCurrentSaveData,
            ISaveService saveService,
            TradeProgressRecorder tradeProgressRecorder,
            InGameScreenStateRouter inGameScreenRouter = null,
            Action clearSettlementCache = null)
        {
            this.getCurrentSaveData = getCurrentSaveData;
            this.saveService = saveService;
            this.tradeProgressRecorder = tradeProgressRecorder;
            this.inGameScreenRouter = inGameScreenRouter;
            this.clearSettlementCache = clearSettlementCache;
        }

        public DepartureValidationResult TryStartTrade(
            CaravanData caravan,
            float distanceKm,
            string tradeId,
            string routeId,
            bool saveImmediately = true)
        {
            LastRecordSucceeded = false;

            var result = CaravanValidator.Validate(caravan);
            if (!result.canDepart)
            {
                return result;
            }

            if (tradeProgressRecorder == null)
            {
                FrameworkLog.Warning("Trade start time was not recorded because trade progress recorder is null.");
                return CreateFrameworkBlockedResult();
            }

            var saveData = getCurrentSaveData != null ? getCurrentSaveData() : null;
            var expectedSeconds = CaravanCalculator.GetTravelSeconds(caravan, distanceKm);
            var expectedDuration = TimeSpan.FromSeconds(Math.Max(0f, expectedSeconds));

            LastRecordSucceeded = tradeProgressRecorder.RecordStartedTrade(
                saveData,
                tradeId,
                routeId,
                expectedDuration);

            if (!LastRecordSucceeded)
            {
                return CreateFrameworkBlockedResult();
            }

            result = JourneyRunner.TryDepart(caravan, distanceKm);
            if (!result.canDepart)
            {
                FrameworkLog.Warning("Trade start was recorded but Core departure failed during final validation.");
                return result;
            }

            if (saveData != null)
            {
                clearSettlementCache?.Invoke();
                CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            }

            inGameScreenRouter?.RequestScreen(InGameScreenState.Traveling);

            if (saveImmediately)
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

        private static DepartureValidationResult CreateFrameworkBlockedResult()
        {
            return new DepartureValidationResult { canDepart = false };
        }
    }
}
