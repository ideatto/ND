/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - JourneyResultData와 PendingSettlementSaveData 사이의 저장·복원 변환을 담당한다.
 *
 * Main Features
 * - Settle 직후 확정 결과를 저장 DTO로 복사한다.
 * - 로드 시 hasResult·claimed·resultVersion을 검증한 뒤 runtime JourneyResultData를 재구성한다.
 * - pending DTO를 빈 상태로 초기화하는 helper를 제공한다.
 *
 * Usage for Team Members
 * - TradeProgressCoordinator.SettleActiveTrade / RestorePendingSettlement에서만 호출한다.
 *
 * Important Notes
 * - TryToRuntime은 검증 실패 시 false를 반환하며 JourneyResultData를 만들지 않는다.
 * - Related Documentation: Docs/Personal_Documents/CSU/0712_m3-pending-settlement-persist.md
 */
namespace ND.Framework
{
    /// <summary>
    /// JourneyResultData와 PendingSettlementSaveData를 변환하는 mapper이다.
    /// </summary>
    public static class PendingSettlementSaveDataMapper
    {
        /// <summary>
        /// 정산 결과를 저장 DTO로 변환한다.
        /// </summary>
        /// <param name="result">Core·Economy가 채운 확정 정산 결과.</param>
        /// <param name="tradeId">활성 무역 ID.</param>
        /// <param name="routeId">활성 route ID.</param>
        /// <returns>hasResult=true, claimed=false인 새 PendingSettlementSaveData. result가 null이면 빈 DTO.</returns>
        public static PendingSettlementSaveData ToSave(JourneyResultData result, string tradeId, string routeId)
        {
            if (result == null)
            {
                return CreateEmpty();
            }

            return new PendingSettlementSaveData
            {
                hasResult = true,
                tradeId = tradeId ?? string.Empty,
                routeId = routeId ?? string.Empty,
                resultVersion = PendingSettlementSaveData.CurrentResultVersion,
                grade = result.grade,
                failureReason = result.failureReason,
                cargoLost = result.cargoLost,
                durabilityLost = result.durabilityLost,
                travelSeconds = result.travelSeconds,
                foodConsumed = result.foodConsumed,
                foodLost = result.foodLost,
                eventsOccurred = result.eventsOccurred,
                battlesFought = result.battlesFought,
                lostMercenaryInstanceIds = new System.Collections.Generic.List<string>(
                    result.lostMercenaryInstanceIds ?? new System.Collections.Generic.List<string>()),
                wagonDestroyed = result.wagonDestroyed,
                destroyedWagonInstanceId = result.destroyedWagonInstanceId ?? string.Empty,
                departureLoad = result.departureLoad,
                finalEfficientLoad = result.finalEfficientLoad,
                overloadRatio = result.overloadRatio,
                revenue = result.revenue,
                cost = result.cost,
                netProfit = result.netProfit,
                claimed = false
            };
        }

        /// <summary>
        /// 저장된 pending 정산을 runtime JourneyResultData로 복원한다.
        /// </summary>
        /// <param name="pending">저장 DTO.</param>
        /// <param name="result">복원된 JourneyResultData. 실패 시 null.</param>
        /// <returns>
        /// hasResult=true, claimed=false, resultVersion 지원일 때 true.
        /// 검증 실패 시 false이며 result는 null이다.
        /// </returns>
        public static bool TryToRuntime(PendingSettlementSaveData pending, out JourneyResultData result)
        {
            result = null;
            if (pending == null || !pending.hasResult)
            {
                return false;
            }

            if (pending.claimed)
            {
                return false;
            }

            if (pending.resultVersion != PendingSettlementSaveData.CurrentResultVersion)
            {
                return false;
            }

            result = new JourneyResultData
            {
                grade = pending.grade,
                failureReason = pending.failureReason,
                cargoLost = pending.cargoLost,
                durabilityLost = pending.durabilityLost,
                travelSeconds = pending.travelSeconds,
                foodConsumed = pending.foodConsumed,
                foodLost = pending.foodLost,
                eventsOccurred = pending.eventsOccurred,
                battlesFought = pending.battlesFought,
                lostMercenaryInstanceIds = new System.Collections.Generic.List<string>(
                    pending.lostMercenaryInstanceIds ?? new System.Collections.Generic.List<string>()),
                wagonDestroyed = pending.wagonDestroyed,
                destroyedWagonInstanceId = pending.destroyedWagonInstanceId ?? string.Empty,
                departureLoad = pending.departureLoad,
                finalEfficientLoad = pending.finalEfficientLoad,
                overloadRatio = pending.overloadRatio,
                revenue = pending.revenue,
                cost = pending.cost,
                netProfit = pending.netProfit
            };
            return true;
        }

        /// <summary>
        /// 빈 pending settlement DTO를 생성한다.
        /// </summary>
        public static PendingSettlementSaveData CreateEmpty()
        {
            return new PendingSettlementSaveData
            {
                hasResult = false,
                tradeId = string.Empty,
                routeId = string.Empty,
                resultVersion = PendingSettlementSaveData.CurrentResultVersion,
                claimed = false
            };
        }

        /// <summary>
        /// 저장 데이터의 pending settlement를 빈 상태로 초기화한다.
        /// </summary>
        public static void Clear(SaveData saveData)
        {
            if (saveData == null)
            {
                return;
            }

            saveData.pendingSettlement = CreateEmpty();
        }
    }
}
