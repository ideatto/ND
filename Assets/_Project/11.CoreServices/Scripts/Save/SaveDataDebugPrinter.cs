using System;
using UnityEngine;

namespace ND.Framework
{
    public sealed class SaveDataDebugPrinter : MonoBehaviour
    {
        [ContextMenu("Framework/Print Full Save Data")]
        public void PrintFullSaveData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = GetCurrentSaveData();
            if (saveData == null)
            {
                FrameworkLog.Warning("No current save data is available.");
                return;
            }

            var json = JsonUtility.ToJson(saveData, true);
            FrameworkLog.Info($"Current save data:\n{json}");
#endif
        }

        [ContextMenu("Framework/Print Trade Progress Save Data")]
        public void PrintTradeProgress()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = GetCurrentSaveData();
            if (saveData == null || saveData.tradeProgress == null)
            {
                FrameworkLog.Warning("No trade progress save data is available.");
                return;
            }

            var progress = saveData.tradeProgress;
            var startUtc = FormatUtcTicks(progress.tradeStartUtcTick);
            var expectedEndUtc = FormatUtcTicks(progress.expectedTradeEndUtcTick);

            FrameworkLog.Info(
                "Trade progress save data:\n"
                + $"ActiveTradeId: {progress.activeTradeId}\n"
                + $"ActiveRouteId: {progress.activeRouteId}\n"
                + $"State: {progress.state}\n"
                + $"TradeStartUtcTick: {progress.tradeStartUtcTick}\n"
                + $"TradeStartUtc: {startUtc}\n"
                + $"ExpectedTradeEndUtcTick: {progress.expectedTradeEndUtcTick}\n"
                + $"ExpectedTradeEndUtc: {expectedEndUtc}");
#endif
        }

        private static SaveData GetCurrentSaveData()
        {
            return FrameworkRoot.Instance != null ? FrameworkRoot.Instance.CurrentSaveData : null;
        }

        private static string FormatUtcTicks(long ticks)
        {
            if (ticks <= 0)
            {
                return "not recorded";
            }

            if (ticks > DateTime.MaxValue.Ticks)
            {
                return "invalid ticks";
            }

            return new DateTime(ticks, DateTimeKind.Utc).ToString("O");
        }
    }
}
