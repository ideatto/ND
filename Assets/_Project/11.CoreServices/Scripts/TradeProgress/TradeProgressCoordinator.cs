/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 진행 중인 무역의 시간 기반 진행률 계산, 정산 생성, claim 후 초기화를 조율한다.
 * - Core JourneyRunner 결과를 SaveData, FrameworkEvents, 인게임 화면 상태에 반영한다.
 *
 * Main Features
 * - 저장된 UTC tick을 기준으로 traveling caravan의 progress01을 갱신한다.
 * - 도착 또는 실패 조건을 만족하면 settlement 결과를 생성하고 settlement pending 상태로 전환한다.
 * - 정산 claim 성공 후 완료/실패 상태를 기록하고 caravan을 준비 상태로 되돌린다.
 * - debug 이벤트를 통해 active trade를 즉시 완료할 수 있다.
 *
 * Usage for Team Members
 * - 인게임 진행 갱신 또는 debug command에서 CheckProgressAndCompletion(...)을 호출한다.
 * - settlement UI claim은 SettlementUiBridge를 통해 ClaimSettlementAndReset()으로 연결된다.
 * - runtime caravan을 별도로 준비한 경우 SetActiveCaravan(...)으로 coordinator에 전달한다.
 *
 * Main Public APIs
 * - ActiveCaravan: 현재 진행 계산에 사용할 runtime caravan.
 * - CheckProgressAndCompletion(...): 진행률을 갱신하고 정산 가능 시 정산 이벤트를 발행한다.
 * - ClaimSettlementAndReset(): pending settlement를 claim하고 저장 데이터를 준비 상태로 갱신한다.
 * - ForceCompleteActiveTrade(): 현재 active trade를 즉시 도착 처리한다.
 *
 * Important Notes
 * - 생성자에서 FrameworkEvents.CompleteTradeRequested를 구독한다.
 * - LastSettlementResult는 claim 전 UI 표시와 중복 정산 방지에 사용되는 runtime cache이다.
 * - saveData.tradeProgress가 Traveling 상태일 때만 진행률 갱신과 정산 생성이 가능하다.
 */
using System;

namespace ND.Framework
{
    /// <summary>
    /// 무역 진행률, 정산 생성, 정산 claim을 저장 데이터와 Core caravan 상태에 반영하는 coordinator이다.
    /// </summary>
    public sealed class TradeProgressCoordinator
    {
        private readonly Func<SaveData> getCurrentSaveData;
        private readonly ISaveService saveService;
        private readonly IGameTimeProvider gameTimeProvider;
        private readonly TradeProgressRecorder tradeProgressRecorder;
        private readonly InGameScreenStateRouter inGameScreenRouter;

        private CaravanData activeCaravan;

        /// <summary>
        /// coordinator에 필요한 저장 데이터 접근자와 무역 진행 의존성을 주입한다.
        /// </summary>
        /// <param name="getCurrentSaveData">현재 SaveData를 반환하는 접근자.</param>
        /// <param name="saveService">진행률과 정산 상태를 저장할 서비스.</param>
        /// <param name="gameTimeProvider">현재 UTC 시각을 제공하는 서비스.</param>
        /// <param name="tradeProgressRecorder">무역 상태 전환을 저장 데이터에 기록하는 recorder.</param>
        /// <param name="inGameScreenRouter">정산/준비 화면 전환을 요청할 router.</param>
        /// <remarks>
        /// 생성 시 CompleteTradeRequested 이벤트를 구독하므로 coordinator 수명은 FrameworkRoot와 같아야 한다.
        /// </remarks>
        public TradeProgressCoordinator(
            Func<SaveData> getCurrentSaveData,
            ISaveService saveService,
            IGameTimeProvider gameTimeProvider,
            TradeProgressRecorder tradeProgressRecorder,
            InGameScreenStateRouter inGameScreenRouter = null)
        {
            this.getCurrentSaveData = getCurrentSaveData;
            this.saveService = saveService;
            this.gameTimeProvider = gameTimeProvider;
            this.tradeProgressRecorder = tradeProgressRecorder;
            this.inGameScreenRouter = inGameScreenRouter;

            FrameworkEvents.CompleteTradeRequested += ForceCompleteActiveTrade;
        }

        /// <summary>
        /// 마지막으로 생성된 settlement 결과가 연결된 trade ID이다.
        /// </summary>
        public string LastSettlementTradeId { get; private set; } = string.Empty;

        /// <summary>
        /// 마지막으로 생성되어 claim 대기 중인 settlement 결과이다.
        /// </summary>
        public JourneyResultData LastSettlementResult { get; private set; }

        /// <summary>
        /// 현재 진행 계산과 정산에 사용할 runtime caravan 데이터이다.
        /// </summary>
        /// <remarks>
        /// 명시적으로 설정된 caravan이 없으면 SaveData.caravan에서 복원한다.
        /// </remarks>
        public CaravanData ActiveCaravan
        {
            get
            {
                EnsureActiveCaravan();
                return activeCaravan;
            }
        }

        /// <summary>
        /// 진행 계산에 사용할 runtime caravan 참조를 설정한다.
        /// </summary>
        /// <param name="caravan">현재 active trade와 연결할 runtime caravan 데이터.</param>
        public void SetActiveCaravan(CaravanData caravan)
        {
            activeCaravan = caravan;
        }

        /// <summary>
        /// 저장된 무역 시간 정보를 기준으로 진행률을 갱신하고 필요하면 settlement를 생성한다.
        /// </summary>
        /// <param name="saveProgress">도착 전 진행률만 갱신된 경우 즉시 저장할지 여부.</param>
        /// <returns>이번 호출에서 settlement가 생성되었으면 true, 아직 이동 중이거나 갱신할 수 없으면 false.</returns>
        /// <remarks>
        /// 성공적인 settlement 생성 시 saveData, LastSettlementResult, 화면 상태가 변경되고 TradeSettlementReady 이벤트가 발행된다.
        /// </remarks>
        public bool CheckProgressAndCompletion(bool saveProgress = true)
        {
            var saveData = GetSaveData();
            // traveling 상태가 아니면 진행률 계산이나 settlement 생성을 수행하지 않는다.
            if (!CanUpdateTravelingTrade(saveData))
            {
                return false;
            }

            // runtime caravan이 없으면 저장된 caravan 상태를 복원해 진행률 계산 대상으로 사용한다.
            var caravan = EnsureActiveCaravan();
            if (caravan == null)
            {
                FrameworkLog.Warning("Trade progress check skipped because active caravan is missing.");
                return false;
            }

            // 저장된 UTC 시작/종료 tick과 현재 시간을 비교해 Core caravan 진행률을 갱신한다.
            var progress = CalculateProgress(saveData.tradeProgress);
            JourneyRunner.SetProgress(caravan, progress);
            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);

            // 아직 도착하지 않았고 치명적 실패도 없으면 현재 진행률만 저장하고 settlement 생성은 미룬다.
            if (!JourneyRunner.IsArrived(caravan) && caravan.runFatalReason == JourneyFailureReason.None)
            {
                if (saveProgress)
                {
                    saveService?.Save(saveData);
                }

                return false;
            }

            return SettleActiveTrade(saveData, caravan);
        }

        /// <summary>
        /// cache된 settlement를 claim하고 저장 데이터와 runtime caravan을 준비 상태로 되돌린다.
        /// </summary>
        /// <returns>claim, 상태 기록, caravan reset, 저장이 모두 성공하면 true.</returns>
        /// <remarks>
        /// 성공 시 settlement cache가 삭제되고 InGameScreenState.Preparation으로 전환된다.
        /// </remarks>
        public bool ClaimSettlementAndReset()
        {
            var saveData = GetSaveData();
            var caravan = EnsureActiveCaravan();
            // 저장 데이터 또는 caravan이 없으면 claim 결과를 저장하거나 reset할 수 없다.
            if (saveData == null || caravan == null)
            {
                return false;
            }

            // UI가 보유한 settlement cache와 저장 데이터 상태가 일치하는지 먼저 확인한다.
            if (!CanClaimCachedSettlement(saveData))
            {
                return false;
            }

            // recorder가 없으면 claim 이후 완료/실패 상태를 저장 데이터에 기록할 수 없다.
            if (tradeProgressRecorder == null)
            {
                FrameworkLog.Warning("Settlement claim blocked because trade progress recorder is missing.");
                return false;
            }

            // Core가 settlement claim을 거부하면 framework 상태 전환도 진행하지 않는다.
            if (!JourneyRunner.ClaimSettlement(caravan))
            {
                FrameworkLog.Warning("Settlement claim blocked because Core rejected the active settlement.");
                return false;
            }

            // settlement 결과 등급에 따라 최종 저장 상태를 Completed 또는 Failed로 기록한다.
            var finalStateRecorded = LastSettlementResult.grade == JourneyResultGrade.Failed
                ? MarkFailed(saveData)
                : MarkCompleted(saveData);
            if (!finalStateRecorded)
            {
                return false;
            }

            // claim 이후 caravan을 preparation 상태로 되돌려 다음 무역 출발이 가능한 저장 상태를 만든다.
            if (!JourneyRunner.ResetToPrepare(caravan))
            {
                FrameworkLog.Warning("Settlement was claimed but Core did not return the caravan to preparation.");
                return false;
            }

            // reset된 runtime caravan을 저장 데이터에 반영하고 UI를 preparation 화면으로 복귀시킨다.
            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            saveService?.Save(saveData);
            inGameScreenRouter?.RequestScreen(InGameScreenState.Preparation);
            ClearSettlementCache();

            return true;
        }

        /// <summary>
        /// runtime settlement cache를 삭제한다.
        /// </summary>
        public void ClearSettlementCache()
        {
            LastSettlementTradeId = string.Empty;
            LastSettlementResult = null;
        }

        /// <summary>
        /// 현재 traveling trade를 즉시 도착 처리하고 settlement 생성을 시도한다.
        /// </summary>
        /// <remarks>
        /// debug command 또는 CompleteTradeRequested 이벤트에서 사용한다.
        /// </remarks>
        public void ForceCompleteActiveTrade()
        {
            var saveData = GetSaveData();
            // traveling 상태가 아니면 강제 완료 대상이 없으므로 무시한다.
            if (!CanUpdateTravelingTrade(saveData))
            {
                return;
            }

            // 진행 대상 caravan이 없으면 Core 도착 처리를 수행할 수 없다.
            var caravan = EnsureActiveCaravan();
            if (caravan == null)
            {
                FrameworkLog.Warning("Immediate trade completion skipped because active caravan is missing.");
                return;
            }

            // Core progress를 도착값으로 맞춘 뒤 동일한 settlement 생성 경로를 재사용한다.
            JourneyRunner.SetProgress(caravan, JourneyRunner.ArrivalProgress);
            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            SettleActiveTrade(saveData, caravan);
        }

        private bool SettleActiveTrade(SaveData saveData, CaravanData caravan)
        {
            // settlement는 traveling 상태의 active trade에서 한 번만 생성되어야 한다.
            if (!CanCreateSettlement(saveData))
            {
                return false;
            }

            // 상태 전환 recorder가 없으면 정산 결과를 저장 상태와 연결할 수 없다.
            if (tradeProgressRecorder == null)
            {
                FrameworkLog.Warning("Trade settlement was not created because trade progress recorder is missing.");
                return false;
            }

            // Core settlement 결과가 없으면 UI에 표시하거나 claim할 데이터가 없으므로 중단한다.
            var result = JourneyRunner.Settle(caravan);
            if (result == null)
            {
                FrameworkLog.Warning("Trade settlement was not created because Core returned no result.");
                return false;
            }

            // 저장 데이터를 settlement pending으로 전환한 뒤 실제 상태가 바뀌었는지 검증한다.
            tradeProgressRecorder.MarkSettlementPending(saveData);
            if (saveData.tradeProgress.state != TradeProgressState.SettlementPending)
            {
                FrameworkLog.Warning($"Trade settlement was not published because trade state is {saveData.tradeProgress.state}.");
                return false;
            }

            // 생성된 결과를 runtime cache와 저장 데이터에 반영하고 UI 계층에 settlement 준비를 알린다.
            var settlementTradeId = saveData.tradeProgress.activeTradeId ?? string.Empty;
            LastSettlementTradeId = settlementTradeId;
            LastSettlementResult = result;
            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            saveService?.Save(saveData);
            FrameworkEvents.RaiseTradeSettlementReady(settlementTradeId, result);
            inGameScreenRouter?.RequestScreen(InGameScreenState.Settlement);

            return true;
        }

        private bool CanClaimCachedSettlement(SaveData saveData)
        {
            // claim은 coordinator가 직전에 생성한 settlement result가 남아 있을 때만 허용한다.
            if (LastSettlementResult == null)
            {
                FrameworkLog.Warning("Settlement claim blocked because cached settlement result is missing.");
                return false;
            }

            // 저장 데이터가 settlement pending이 아니면 UI cache가 stale 상태일 수 있으므로 거부한다.
            if (saveData.tradeProgress == null)
            {
                FrameworkLog.Warning("Settlement claim blocked because trade progress save data is missing.");
                return false;
            }

            if (saveData.tradeProgress.state != TradeProgressState.SettlementPending)
            {
                FrameworkLog.Warning($"Settlement claim blocked because trade state is {saveData.tradeProgress.state}.");
                return false;
            }

            // cache된 trade ID와 현재 active trade ID가 다르면 다른 무역의 정산으로 보고 차단한다.
            var activeTradeId = saveData.tradeProgress.activeTradeId ?? string.Empty;
            if (string.IsNullOrEmpty(LastSettlementTradeId) || LastSettlementTradeId != activeTradeId)
            {
                FrameworkLog.Warning(
                    $"Settlement claim blocked because cached trade ID does not match active trade ID. Cached: {LastSettlementTradeId}, Active: {activeTradeId}");
                return false;
            }

            return true;
        }

        private bool CanCreateSettlement(SaveData saveData)
        {
            // 저장 데이터와 trade progress가 없으면 settlement state를 기록할 위치가 없다.
            if (saveData == null || saveData.tradeProgress == null)
            {
                FrameworkLog.Warning("Trade settlement blocked because trade progress save data is missing.");
                return false;
            }

            // 이미 settlement pending 또는 완료된 trade에서 settlement가 중복 생성되지 않도록 제한한다.
            if (saveData.tradeProgress.state != TradeProgressState.Traveling)
            {
                FrameworkLog.Warning($"Trade settlement blocked because trade state is {saveData.tradeProgress.state}.");
                return false;
            }

            // active trade ID가 없으면 정산 이벤트와 UI cache를 안정적으로 연결할 수 없다.
            if (string.IsNullOrEmpty(saveData.tradeProgress.activeTradeId))
            {
                FrameworkLog.Warning("Trade settlement blocked because active trade ID is empty.");
                return false;
            }

            return true;
        }

        private bool MarkCompleted(SaveData saveData)
        {
            tradeProgressRecorder.MarkCompleted(saveData);
            // recorder 호출 후 실제 저장 상태를 확인해 silent failure를 claim 성공으로 처리하지 않는다.
            if (saveData.tradeProgress.state == TradeProgressState.Completed)
            {
                return true;
            }

            FrameworkLog.Warning($"Settlement completion state was not recorded. State: {saveData.tradeProgress.state}");
            return false;
        }

        private bool MarkFailed(SaveData saveData)
        {
            tradeProgressRecorder.MarkFailed(saveData);
            // recorder 호출 후 실제 저장 상태를 확인해 실패 정산이 완료 상태로 남지 않게 한다.
            if (saveData.tradeProgress.state == TradeProgressState.Failed)
            {
                return true;
            }

            FrameworkLog.Warning($"Settlement failure state was not recorded. State: {saveData.tradeProgress.state}");
            return false;
        }

        private CaravanData EnsureActiveCaravan()
        {
            // 이미 runtime caravan을 보유 중이면 저장 데이터에서 다시 복원하지 않는다.
            if (activeCaravan != null)
            {
                return activeCaravan;
            }

            // runtime 참조가 없을 때는 현재 저장 데이터의 caravan snapshot으로 복원한다.
            var saveData = GetSaveData();
            if (saveData == null || saveData.caravan == null)
            {
                return null;
            }

            activeCaravan = CaravanSaveDataMapper.ToRuntime(saveData.caravan);
            return activeCaravan;
        }

        private SaveData GetSaveData()
        {
            return getCurrentSaveData != null ? getCurrentSaveData() : null;
        }

        private bool CanUpdateTravelingTrade(SaveData saveData)
        {
            // traveling 상태가 아니면 시간 기반 진행률 갱신 대상이 아니다.
            if (saveData == null || saveData.tradeProgress == null)
            {
                return false;
            }

            if (saveData.tradeProgress.state != TradeProgressState.Traveling)
            {
                return false;
            }

            // 현재 시간을 알 수 없으면 저장된 tick과 비교할 기준이 없으므로 갱신을 막는다.
            if (gameTimeProvider == null)
            {
                FrameworkLog.Warning("Trade progress check skipped because game time provider is missing.");
                return false;
            }

            return true;
        }

        private float CalculateProgress(TradeProgressSaveData progress)
        {
            var startTicks = progress.tradeStartUtcTick;
            var endTicks = progress.expectedTradeEndUtcTick;
            // 저장된 시간 범위가 유효하지 않으면 즉시 도착으로 처리해 stuck traveling 상태를 피한다.
            if (startTicks <= 0 || endTicks <= startTicks)
            {
                return JourneyRunner.ArrivalProgress;
            }

            var startUtc = new DateTime(startTicks, DateTimeKind.Utc);
            var endUtc = new DateTime(endTicks, DateTimeKind.Utc);
            var totalSeconds = (endUtc - startUtc).TotalSeconds;
            // duration이 0 이하인 데이터는 진행률 계산이 불가능하므로 도착 상태로 본다.
            if (totalSeconds <= 0d)
            {
                return JourneyRunner.ArrivalProgress;
            }

            // clamp는 Core JourneyRunner.SetProgress가 담당하므로 여기서는 시간 비율만 계산한다.
            var elapsedSeconds = (gameTimeProvider.CurrentUtc - startUtc).TotalSeconds;
            return (float)(elapsedSeconds / totalSeconds);
        }
    }
}
