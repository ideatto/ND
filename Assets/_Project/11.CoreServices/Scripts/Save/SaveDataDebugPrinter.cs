/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 현재 FrameworkRoot가 보유한 SaveData를 debug log로 출력한다.
 * - 저장 데이터와 무역 진행 tick 값을 Unity Editor 또는 development build에서 점검할 수 있게 한다.
 *
 * Main Features
 * - 전체 SaveData JSON 출력 ContextMenu를 제공한다.
 * - TradeProgressSaveData의 ID, 상태, UTC tick, ISO UTC 시각을 출력한다.
 * - PendingSettlementSaveData의 hasResult·tradeId·grade·claimed를 출력한다.
 *
 * Usage for Team Members
 * - 디버그용 GameObject에 component로 추가한 뒤 ContextMenu 항목을 실행한다.
 * - 저장 데이터 검증 용도로만 사용하고 gameplay flow 제어에는 사용하지 않는다.
 *
 * Main Public APIs
 * - PrintFullSaveData(): 현재 SaveData 전체를 JSON으로 출력한다.
 * - PrintTradeProgress(): 현재 무역 진행 저장 데이터를 출력한다.
 * - PrintPendingSettlement(): 현재 대기 정산 저장 데이터를 출력한다.
 *
 * Important Notes
 * - 출력 로직은 UNITY_EDITOR 또는 DEVELOPMENT_BUILD에서만 동작한다.
 * - FrameworkRoot.Instance 또는 CurrentSaveData가 없으면 warning만 남긴다.
 */
using System;
using UnityEngine;

namespace ND.Framework
{
    /// <summary>
    /// 현재 저장 데이터를 debug log로 출력하는 개발용 MonoBehaviour이다.
    /// </summary>
    public sealed class SaveDataDebugPrinter : MonoBehaviour
    {
        /// <summary>
        /// 현재 SaveData 전체를 pretty JSON으로 출력한다.
        /// </summary>
        /// <remarks>
        /// Editor 또는 development build에서만 실제 출력이 수행된다.
        /// </remarks>
        [ContextMenu("Framework/Print Full Save Data")]
        public void PrintFullSaveData()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // FrameworkRoot가 준비되지 않은 상태에서는 출력할 runtime 저장 데이터가 없으므로 중단한다.
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

        /// <summary>
        /// 현재 TradeProgressSaveData를 사람이 읽기 쉬운 형태로 출력한다.
        /// </summary>
        [ContextMenu("Framework/Print Trade Progress Save Data")]
        public void PrintTradeProgress()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 무역 진행 데이터가 없으면 tick 변환을 시도하지 않고 원인을 로그로 알린다.
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

        /// <summary>
        /// 현재 PendingSettlementSaveData를 사람이 읽기 쉬운 형태로 출력한다.
        /// </summary>
        [ContextMenu("Framework/Print Pending Settlement Save Data")]
        public void PrintPendingSettlement()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var saveData = GetCurrentSaveData();
            if (saveData == null || saveData.pendingSettlement == null)
            {
                FrameworkLog.Warning("No pending settlement save data is available.");
                return;
            }

            var pending = saveData.pendingSettlement;
            FrameworkLog.Info(
                "Pending settlement save data:\n"
                + $"HasResult: {pending.hasResult}\n"
                + $"TradeId: {pending.tradeId}\n"
                + $"RouteId: {pending.routeId}\n"
                + $"ResultVersion: {pending.resultVersion}\n"
                + $"Grade: {pending.grade}\n"
                + $"FailureReason: {pending.failureReason}\n"
                + $"Revenue: {pending.revenue}\n"
                + $"Cost: {pending.cost}\n"
                + $"NetProfit: {pending.netProfit}\n"
                + $"Claimed: {pending.claimed}");
#endif
        }

        private static SaveData GetCurrentSaveData()
        {
            return FrameworkRoot.Instance != null ? FrameworkRoot.Instance.CurrentSaveData : null;
        }

        private static string FormatUtcTicks(long ticks)
        {
            // 기록되지 않은 tick은 DateTime 변환 대신 명시적인 상태 문자열로 출력한다.
            if (ticks <= 0)
            {
                return "not recorded";
            }

            // 잘못된 tick 값이 debug printer에서 예외를 만들지 않도록 범위를 확인한다.
            if (ticks > DateTime.MaxValue.Ticks)
            {
                return "invalid ticks";
            }

            return new DateTime(ticks, DateTimeKind.Utc).ToString("O");
        }
    }
}
