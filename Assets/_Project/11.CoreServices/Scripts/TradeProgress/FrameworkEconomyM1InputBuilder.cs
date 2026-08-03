/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - ND.Framework.SaveData와 SharedGameData를 EconomyM1LoopInput으로 조립한다.
 * - M1 단계에서는 첫 번째 유효 cargo entry를 단일 품목 정산 입력으로 사용한다.
 *
 * Main Features
 * - route 비용, season/disaster, growth level, cargo quantity를 Economy 입력으로 변환한다.
 * - 무역 정산에서는 실제 수리를 수행하지 않으므로 CartRepairCost를 청구하지 않는다.
 *
 * Important Notes
 * - 다품목 정산은 후속 작업이며, 입력 조립 실패 시 null을 반환한다.
 * - durability 손실은 Caravan에 유지되며 Prepare 상태의 별도 수리 흐름에서 처리한다.
 */
using ND.Economy;

namespace ND.Framework
{
    /// <summary>
    /// Framework 저장 데이터를 Economy M1 loop 입력으로 변환하는 builder이다.
    /// </summary>
    public static class FrameworkEconomyM1InputBuilder
    {
        /// <summary>
        /// SaveData와 Core 정산 결과를 기반으로 Economy M1 입력을 조립한다.
        /// </summary>
        /// <returns>필수 참조가 준비되었으면 입력 객체, 아니면 null.</returns>
        public static EconomyM1LoopInput TryBuild(
            SaveData saveData,
            CaravanData caravan,
            JourneyResultData journeyResult,
            ISharedGameDataProvider sharedGameData)
        {
            return TryBuild(saveData, saveData != null ? saveData.tradeProgress : null,
                caravan, journeyResult, sharedGameData);
        }

        /// <summary>명시된 progress entry를 사용해 Economy M1 입력을 조립한다.</summary>
        public static EconomyM1LoopInput TryBuild(
            SaveData saveData,
            TradeProgressSaveData progress,
            CaravanData caravan,
            JourneyResultData journeyResult,
            ISharedGameDataProvider sharedGameData)
        {
            if (saveData == null || caravan == null || journeyResult == null || sharedGameData == null || !sharedGameData.IsLoaded)
            {
                return null;
            }

            if (progress == null || string.IsNullOrWhiteSpace(progress.activeTradeId)
                || saveData.player == null)
            {
                return null;
            }

            var routeId = progress.activeRouteId ?? string.Empty;
            SharedRouteDefinition routeDefinition;
            if (string.IsNullOrEmpty(routeId) || !sharedGameData.TryGetRoute(routeId, out routeDefinition))
            {
                FrameworkLog.Warning($"Economy M1 input build skipped because route '{routeId}' was not found in shared game data.");
                return null;
            }

            long mercenaryCost = routeDefinition.BaseMercenaryCost;
            if (TryGetExactPreparation(
                    saveData,
                    progress.caravanId,
                    progress.activeTradeId,
                    out TradePreparationCommitSaveData preparation))
            {
                mercenaryCost = System.Math.Max(0L, preparation.mercenaryCost);
            }

            return new EconomyM1LoopInput
            {
                CalculateItemTrade = false,
                CurrencyState = new CurrencyState
                {
                    TradeMoney = saveData.player.tradingCurrency,
                    DevelopmentCurrency = saveData.player.developmentCurrency
                },
                TradeId = progress.activeTradeId,
                // 먹이는 출발 마켓에서 일반 상품처럼 구매되어 이미 tradingCurrency에 반영된다.
                // 경로 기본 식량비까지 정산에서 다시 차감하면 같은 먹이를 이중 결제하게 된다.
                FoodCost = 0L,
                MercenaryCost = mercenaryCost,
                // 정산에서는 수리하지 않으므로 수리비도 청구하지 않는다.
                // 감소한 내구도는 Caravan에 유지되고 별도 수리 흐름에서 처리한다.
                CartRepairCost = 0L,
                LoanRepayment = 0L,
                DevelopmentCurrencyReward = 0L,
                PurchaseGrowth = false,
                PlayerGrowthLevel = saveData.player.playerGrowthLevel,
                CaravanGrowthLevel = saveData.player.caravanGrowthLevel
            };
        }

        private static bool TryGetExactPreparation(
            SaveData saveData,
            string caravanId,
            string tradeId,
            out TradePreparationCommitSaveData preparation)
        {
            preparation = null;
            if (saveData?.tradePreparationCommits == null
                || string.IsNullOrWhiteSpace(caravanId)
                || string.IsNullOrWhiteSpace(tradeId))
            {
                return false;
            }

            foreach (TradePreparationCommitSaveData candidate in saveData.tradePreparationCommits)
            {
                if (candidate == null || !candidate.hasCommit
                    || !string.Equals(candidate.caravanId, caravanId, System.StringComparison.Ordinal)
                    || !string.Equals(candidate.tradeId, tradeId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                if (preparation != null)
                {
                    FrameworkLog.Error(
                        $"Economy input build rejected an ambiguous preparation commit. CaravanId: {caravanId}, TradeId: {tradeId}");
                    preparation = null;
                    return false;
                }

                preparation = candidate;
            }

            return preparation != null;
        }

    }
}
