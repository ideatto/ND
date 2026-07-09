using System;

namespace ND.Framework
{
    public static class FrameworkEvents
    {
        public static event Action<SaveData> LoadCompleted;
        public static event Action<string> SceneChanged;
        public static event Action<string> TradeOfflineCompleted;
        public static event Action TimeRollbackDetected;
        public static event Action CompleteTradeRequested;
        public static event Action<string, JourneyResultData> TradeSettlementReady;
        public static event Action<InGameScreenState> InGameScreenChanged;

        public static void RaiseLoadCompleted(SaveData data)
        {
            FrameworkLog.Info("LoadCompleted event raised.");
            LoadCompleted?.Invoke(data);
        }

        public static void RaiseSceneChanged(string sceneName)
        {
            FrameworkLog.Info($"Scene changed: {sceneName}");
            SceneChanged?.Invoke(sceneName);
        }

        public static void RaiseTradeOfflineCompleted(string tradeId)
        {
            FrameworkLog.Info($"TradeOfflineCompleted event raised. TradeId: {tradeId}");
            TradeOfflineCompleted?.Invoke(tradeId);
        }

        public static void RaiseTimeRollbackDetected()
        {
            FrameworkLog.Warning("TimeRollbackDetected event raised.");
            TimeRollbackDetected?.Invoke();
        }

        public static void RaiseCompleteTradeRequested()
        {
            FrameworkLog.Info("CompleteTradeRequested event raised.");
            CompleteTradeRequested?.Invoke();
        }

        public static void RaiseTradeSettlementReady(string tradeId, JourneyResultData result)
        {
            FrameworkLog.Info($"TradeSettlementReady event raised. TradeId: {tradeId}");
            TradeSettlementReady?.Invoke(tradeId, result);
        }

        public static void RaiseInGameScreenChanged(InGameScreenState screenState)
        {
            FrameworkLog.Info($"InGameScreenChanged event raised. ScreenState: {screenState}");
            InGameScreenChanged?.Invoke(screenState);
        }
    }
}
