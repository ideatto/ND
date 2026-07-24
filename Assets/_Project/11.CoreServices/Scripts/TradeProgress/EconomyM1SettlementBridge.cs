/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Core 정산 결과 생성 후 Economy M1 계산을 수행하고 claim 시 화폐를 SaveData에 반영한다.
 *
 * Main Features
 * - settle 시 Economy 계산 및 JourneyResultData 금액 채움(preview).
 * - claim 시 FinalCurrencyState와 runtime stat을 SaveData·caravan에 반영.
 * - trade ID 기준 pending economy result cache를 관리한다.
 *
 * Important Notes
 * - settle 단계에서는 SaveData 화폐를 변경하지 않는다.
 * - Economy 실패 시 Core 정산 결과는 유지하고 금액 필드는 0으로 남을 수 있다.
 */
using ND.Economy;

namespace ND.Framework
{
    /// <summary>
    /// Core 정산과 Economy M1 loop 사이의 bridge이다.
    /// </summary>
    public sealed class EconomyM1SettlementBridge
    {
        private string pendingTradeId = string.Empty;
        private EconomyM1LoopResult pendingEconomyResult;

        /// <summary>
        /// Core 정산 결과에 Economy M1 금액을 계산해 반영하고 pending cache를 저장한다.
        /// </summary>
        /// <returns>Economy 계산과 JourneyResultData 금액 반영에 성공하면 true.</returns>
        public bool TryCalculateAndFill(
            SaveData saveData,
            CaravanData caravan,
            JourneyResultData journeyResult,
            ISharedGameDataProvider sharedGameData)
        {
            return TryCalculateAndFill(saveData, saveData != null ? saveData.tradeProgress : null,
                caravan, journeyResult, sharedGameData);
        }

        /// <summary>명시된 progress entry를 사용해 정산 금액을 계산하고 pending cache를 갱신한다.</summary>
        public bool TryCalculateAndFill(
            SaveData saveData,
            TradeProgressSaveData progress,
            CaravanData caravan,
            JourneyResultData journeyResult,
            ISharedGameDataProvider sharedGameData)
        {
            ClearPending();
            if (progress == null || string.IsNullOrWhiteSpace(progress.activeTradeId))
            {
                FrameworkLog.Warning(
                    "Economy M1 settlement calculation skipped because the explicit trade ID is missing.");
                return false;
            }

            var input = FrameworkEconomyM1InputBuilder.TryBuild(
                saveData, progress, caravan, journeyResult, sharedGameData);
            if (input == null)
            {
                return false;
            }

            var economyResult = EconomyM1LoopCalculator.Execute(input);
            if (!economyResult.Success)
            {
                FrameworkLog.Warning($"Economy M1 settlement calculation failed: {economyResult.ErrorCode}");
                return false;
            }

            if (!JourneyResultDataEconomyMapper.TryApplySettlement(journeyResult, economyResult))
            {
                FrameworkLog.Warning("Economy M1 settlement calculation succeeded but JourneyResultData mapping failed.");
                return false;
            }

            pendingTradeId = progress.activeTradeId;
            pendingEconomyResult = economyResult;
            return true;
        }

        /// <summary>
        /// cache된 Economy 결과를 SaveData와 runtime caravan에 반영한다.
        /// </summary>
        /// <returns>active trade ID가 일치하고 반영에 성공하면 true.</returns>
        public bool TryApplyPendingEconomy(SaveData saveData, CaravanData runtimeCaravan, string activeTradeId)
        {
            if (pendingEconomyResult == null || !pendingEconomyResult.Success)
            {
                FrameworkLog.Warning("Economy claim apply skipped because pending economy result is missing.");
                return false;
            }

            if (string.IsNullOrEmpty(pendingTradeId) || pendingTradeId != (activeTradeId ?? string.Empty))
            {
                FrameworkLog.Warning(
                    $"Economy claim apply blocked because pending trade ID does not match. Pending: {pendingTradeId}, Active: {activeTradeId}");
                return false;
            }

            var applied = RuntimeStatsSaveDataMapper.ApplyEconomyResult(saveData, runtimeCaravan, pendingEconomyResult);
            if (applied)
            {
                ClearPending();
            }

            return applied;
        }

        /// <summary>
        /// pending Economy cache를 삭제한다.
        /// </summary>
        public void ClearPending()
        {
            pendingTradeId = string.Empty;
            pendingEconomyResult = null;
        }
    }
}
