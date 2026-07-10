/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - Core caravan 출발 검증과 framework 저장 데이터 기록을 연결한다.
 * - 무역 출발 시 active trade 상태, caravan 저장 데이터, 인게임 화면 상태를 갱신한다.
 *
 * Main Features
 * - CaravanValidator와 JourneyRunner를 이용해 출발 가능 여부를 검증한다.
 * - TradeProgressRecorder를 통해 시작 시간과 예상 종료 시간을 저장한다.
 * - 출발 성공 시 caravan 상태를 SaveData에 복사하고 traveling 화면으로 전환한다.
 *
 * Usage for Team Members
 * - UI 또는 debug harness는 TryStartTrade(...)를 호출해 출발을 요청한다.
 * - 반환된 DepartureValidationResult.canDepart와 LastRecordSucceeded를 함께 확인하면 Core 출발과 framework 기록 성공 여부를 구분할 수 있다.
 *
 * Main Public APIs
 * - LastRecordSucceeded: 마지막 출발 요청의 framework 기록 성공 여부.
 * - TryStartTrade(...): caravan 출발과 저장 데이터 기록을 시도한다.
 *
 * Important Notes
 * - tradeId가 기록되지 않으면 출발을 framework blocked 결과로 거부한다.
 * - Core 출발이 최종 실패한 경우 이미 기록된 tradeProgress를 되돌리지 않는다.
 */
using System;

namespace ND.Framework
{
    /// <summary>
    /// 무역 출발 요청을 Core 검증, 저장 데이터 기록, 화면 전환으로 연결하는 서비스이다.
    /// </summary>
    public sealed class TradeStartService
    {
        private readonly Func<SaveData> getCurrentSaveData;
        private readonly ISaveService saveService;
        private readonly TradeProgressRecorder tradeProgressRecorder;
        private readonly InGameScreenStateRouter inGameScreenRouter;
        private readonly Action clearSettlementCache;

        /// <summary>
        /// 마지막 TryStartTrade 호출에서 TradeProgressRecorder 기록이 성공했는지 나타낸다.
        /// </summary>
        public bool LastRecordSucceeded { get; private set; }

        /// <summary>
        /// 무역 출발 서비스에 필요한 저장 데이터 접근자와 framework 서비스를 주입한다.
        /// </summary>
        /// <param name="getCurrentSaveData">현재 SaveData를 반환하는 접근자.</param>
        /// <param name="saveService">출발 성공 후 저장을 수행할 서비스.</param>
        /// <param name="tradeProgressRecorder">active trade 시간과 상태를 기록하는 recorder.</param>
        /// <param name="inGameScreenRouter">출발 성공 후 traveling 화면으로 전환할 router.</param>
        /// <param name="clearSettlementCache">새 출발 전 이전 정산 cache를 비우는 callback.</param>
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

        /// <summary>
        /// caravan 무역 출발을 검증하고 저장 데이터와 화면 상태를 갱신한다.
        /// </summary>
        /// <param name="caravan">출발할 runtime caravan 데이터.</param>
        /// <param name="distanceKm">이동할 거리(km).</param>
        /// <param name="tradeId">active trade로 기록할 고유 ID.</param>
        /// <param name="routeId">active route로 기록할 ID.</param>
        /// <param name="saveImmediately">출발 성공 후 즉시 저장할지 여부.</param>
        /// <returns>Core 출발 검증 결과. framework 기록 실패 시 canDepart가 false인 결과를 반환한다.</returns>
        /// <remarks>
        /// 성공 시 saveData.tradeProgress와 saveData.caravan이 변경되고 InGameScreenState.Traveling으로 전환된다.
        /// </remarks>
        public DepartureValidationResult TryStartTrade(
            CaravanData caravan,
            float distanceKm,
            string tradeId,
            string routeId,
            bool saveImmediately = true)
        {
            LastRecordSucceeded = false;

            // Core 출발 조건을 먼저 확인해 유효하지 않은 caravan이 저장 데이터에 기록되지 않게 한다.
            var result = CaravanValidator.Validate(caravan);
            if (!result.canDepart)
            {
                return result;
            }

            // recorder가 없으면 출발 시간을 복구할 수 없으므로 framework 단계에서 출발을 막는다.
            if (tradeProgressRecorder == null)
            {
                FrameworkLog.Warning("Trade start time was not recorded because trade progress recorder is null.");
                return CreateFrameworkBlockedResult();
            }

            var saveData = getCurrentSaveData != null ? getCurrentSaveData() : null;
            var expectedSeconds = CaravanCalculator.GetTravelSeconds(caravan, distanceKm);
            var expectedDuration = TimeSpan.FromSeconds(Math.Max(0f, expectedSeconds));

            // active trade ID와 예상 종료 시각을 저장 데이터에 먼저 기록해 이후 진행률 계산 기준을 만든다.
            LastRecordSucceeded = tradeProgressRecorder.RecordStartedTrade(
                saveData,
                tradeId,
                routeId,
                expectedDuration);

            // 저장 기록이 실패하면 Core 출발을 진행해도 framework가 상태를 추적할 수 없으므로 거부한다.
            if (!LastRecordSucceeded)
            {
                return CreateFrameworkBlockedResult();
            }

            // 기록 후 Core 최종 출발을 수행해 caravan runtime 상태를 traveling으로 전환한다.
            result = JourneyRunner.TryDepart(caravan, distanceKm);
            if (!result.canDepart)
            {
                FrameworkLog.Warning("Trade start was recorded but Core departure failed during final validation.");
                return result;
            }

            // 출발 성공 후 이전 정산 cache를 비우고 runtime caravan 상태를 저장 DTO에 반영한다.
            if (saveData != null)
            {
                clearSettlementCache?.Invoke();
                CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            }

            // UI가 즉시 traveling 화면으로 전환되도록 router에 상태 변경을 요청한다.
            inGameScreenRouter?.RequestScreen(InGameScreenState.Traveling);

            if (saveImmediately)
            {
                // 즉시 저장 옵션에서는 출발 기록과 caravan 상태가 디스크에 남도록 저장 서비스를 호출한다.
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
