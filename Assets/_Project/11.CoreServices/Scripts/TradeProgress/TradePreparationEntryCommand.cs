using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// Atomically changes a settled Town save into a fresh Preparation save.
    /// UI entry points call this command through FrameworkRoot and only open
    /// preparation views after the save succeeds.
    /// </summary>
    public static class TradePreparationEntryCommand
    {
        public static bool TryExecute(
            SaveData saveData,
            ISaveService saveService,
            InGameScreenStateRouter screenRouter)
        {
            if (saveData == null || saveData.tradeProgress == null || saveService == null ||
                InGameScreenStateRouter.MapFromSaveData(saveData) != InGameScreenState.Town)
            {
                FrameworkLog.Warning("Trade preparation entry blocked because the current state is not Town.");
                return false;
            }

            if ((saveData.pendingSettlement != null && saveData.pendingSettlement.hasResult) ||
                (saveData.tradePreparationCommit != null && saveData.tradePreparationCommit.hasCommit))
            {
                FrameworkLog.Warning("Trade preparation entry blocked because settlement data is still pending.");
                return false;
            }

            string progressSnapshot = JsonUtility.ToJson(saveData.tradeProgress);
            float elapsedSnapshot = saveData.caravan != null
                ? saveData.caravan.elapsedInGameSeconds
                : 0f;

            ResetForPreparation(saveData);
            SaveResult result = saveService.Save(saveData);
            if (result == null || !result.Succeeded)
            {
                JsonUtility.FromJsonOverwrite(progressSnapshot, saveData.tradeProgress);
                if (saveData.caravan != null)
                    saveData.caravan.elapsedInGameSeconds = elapsedSnapshot;
                FrameworkLog.Warning("Trade preparation entry rolled back because save did not succeed.");
                return false;
            }

            screenRouter?.RequestScreen(InGameScreenState.Preparation);
            FrameworkLog.Info("Town trade preparation started.");
            return true;
        }

        private static void ResetForPreparation(SaveData saveData)
        {
            saveData.tradeProgress.activeTradeId = string.Empty;
            saveData.tradeProgress.activeRouteId = string.Empty;
            saveData.tradeProgress.state = TradeProgressState.Preparing;
            saveData.tradeProgress.tradeStartUtcTick = 0L;
            saveData.tradeProgress.expectedTradeEndUtcTick = 0L;
            saveData.tradeProgress.inGameTimeMultiplierAtStart = 1f;
            if (saveData.caravan != null)
                saveData.caravan.elapsedInGameSeconds = 0f;
        }
    }
}
