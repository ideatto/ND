/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 진행 중인 무역의 시간 기반 진행률 계산, 정산 생성, claim 후 초기화를 조율한다.
 * - Core JourneyRunner 결과를 SaveData, FrameworkEvents, Economy M1 bridge, 인게임 화면 상태에 반영한다.
 * - SettlementPending 대기 정산을 PendingSettlementSaveData로 영속화하고 재실행 시 runtime cache를 복구한다.
 * - Continue/Load 시 Traveling 무역의 오프라인 경과·식량·완료를 ApplyOfflineProgressOnLoad로 복구한다.
 *
 * Main Features
 * - 저장된 UTC tick을 기준으로 traveling caravan의 progress01을 갱신한다.
 * - SetProgress 전에 active caravan과 SaveData에 elapsedInGameSeconds를 동기화한다.
 * - 도착 또는 실패 조건을 만족하면 settlement 결과를 생성하고 settlement pending 상태로 전환한다.
 * - 정산 생성 직후 PendingSettlementSaveData와 SettlementPending을 같은 저장 단위로 기록한다.
 * - 로드 후 RestorePendingSettlement로 LastSettlementResult·Economy pending·TradeSettlementReady를 재구성한다.
 * - 로드 시 ApplyOfflineProgressOnLoad로 역행 감지·상한 clamp·오프라인 완료(TradeOfflineCompleted)를 처리한다.
 * - 정산 claim 성공 후 완료/실패 상태를 기록하고 caravan을 준비 상태로 되돌린다.
 * - debug 이벤트를 통해 active trade를 즉시 완료할 수 있다.
 *
 * Usage for Team Members
 * - 인게임 진행 갱신 또는 debug command에서 CheckProgressAndCompletion(...)을 호출한다.
 * - settlement UI claim은 SettlementUiBridge를 통해 ClaimSettlementAndReset()으로 연결된다.
 * - runtime caravan을 별도로 준비한 경우 SetActiveCaravan(...)으로 coordinator에 전달한다.
 * - CompleteLoadingAndEnterGame에서 SharedGameData 로드 이후 ApplyOfflineProgressOnLoad → RestorePendingSettlement 순으로 호출한다.
 *
 * Main Public APIs
 * - ActiveCaravan: 현재 진행 계산에 사용할 runtime caravan.
 * - CheckProgressAndCompletion(...): 진행률을 갱신하고 정산 가능 시 정산 이벤트를 발행한다.
 * - ApplyOfflineProgressOnLoad(...): Continue/Load 시 Traveling 오프라인 복구를 적용한다.
 * - ClaimSettlementAndReset(): pending settlement를 claim하고 저장 데이터를 준비 상태로 갱신한다.
 * - RestorePendingSettlement(...): 저장된 대기 정산으로 runtime cache를 복구한다.
 * - ForceCompleteActiveTrade(): 현재 active trade를 즉시 도착 처리한다.
 * - ClearPendingSettlementSave(...): SaveData의 pendingSettlement DTO를 비운다.
 * - TryGetMapProgress(...): 월드맵 등 읽기 전용 소비자를 위한 진행 스냅샷을 반환한다.
 *
 * Important Notes
 * - 생성자에서 FrameworkEvents.CompleteTradeRequested를 구독한다.
 * - LastSettlementResult는 claim 전 UI 표시와 중복 정산 방지에 사용되는 runtime cache이다.
 * - settle 시 Economy M1 계산으로 JourneyResultData 금액 필드를 채운 뒤 pendingSettlement에 저장한다.
 * - restore 시 Economy pending은 TryCalculateAndFill로 재구성하고, UI 표시 금액은 저장값을 우선한다.
 * - claim 시 Economy pending 결과를 SaveData 화폐에 반영하고 pendingSettlement를 clear한다.
 * - 오프라인 elapsed는 tradeStart→evaluationUtc 절대값 overwrite이므로 재로드 시 이중 소모되지 않는다.
 * - TryGetMapProgress는 저장·정산·출발을 변경하지 않는다.
 * - Related Documentation: Docs/Personal_Documents/CSU/0712_m3-offline-progress-pipeline.md
 * - Related Documentation: Docs/Guide/Framework_World_Map_API_Guide.md
 */
using System;
using UnityEngine;

namespace ND.Framework
{
    public enum ClaimSettlementFailureReason
    {
        None = 0,
        InvalidCaravanId,
        InvalidTradeId,
        CaravanNotFound,
        TradeProgressNotFound,
        PendingSettlementNotFound,
        AmbiguousPendingSettlement,
        TradeIdMismatch,
        InvalidTradeState,
        AlreadyClaimed,
        SettlementDataInvalid,
        EconomyApplyFailed,
        TownApplyFailed,
        CoreClaimRejected,
        SaveFailed,
        RollbackFailed
    }

    public sealed class ClaimSettlementResult
    {
        private ClaimSettlementResult(bool succeeded, ClaimSettlementFailureReason failureReason, SaveResult saveResult)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            SaveResult = saveResult;
        }

        public bool Succeeded { get; }
        public ClaimSettlementFailureReason FailureReason { get; }
        public SaveResult SaveResult { get; }

        public static ClaimSettlementResult Success(SaveResult saveResult)
            => new ClaimSettlementResult(true, ClaimSettlementFailureReason.None, saveResult);

        public static ClaimSettlementResult Failure(ClaimSettlementFailureReason reason, SaveResult saveResult = null)
            => new ClaimSettlementResult(false, reason, saveResult);
    }

    /// <summary>
    /// 무역 진행률, 정산 생성, 정산 claim을 저장 데이터와 Core caravan 상태에 반영하는 coordinator이다.
    /// </summary>
    public sealed class TradeProgressCoordinator
    {
        private readonly Func<SaveData> getCurrentSaveData;
        private readonly ISaveService saveService;
        private readonly IGameTimeProvider gameTimeProvider;
        private readonly IInGameTimeProvider inGameTimeProvider;
        private readonly TradeProgressRecorder tradeProgressRecorder;
        private readonly InGameScreenStateRouter inGameScreenRouter;
        private readonly Func<ISharedGameDataProvider> getSharedGameData;
        private readonly global::ITradePrepareCommitCompletion tradePrepareCommitCompletion;
        private readonly global::ITradePrepareCommitSource tradePrepareCommitSource;
        private readonly EconomyM1SettlementBridge economySettlementBridge = new EconomyM1SettlementBridge();

        private CaravanData activeCaravan;

        /// <summary>
        /// coordinator에 필요한 저장 데이터 접근자와 무역 진행 의존성을 주입한다.
        /// </summary>
        /// <param name="getCurrentSaveData">현재 SaveData를 반환하는 접근자.</param>
        /// <param name="saveService">진행률과 정산 상태를 저장할 서비스.</param>
        /// <param name="gameTimeProvider">현재 UTC 시각을 제공하는 서비스.</param>
        /// <param name="tradeProgressRecorder">무역 상태 전환을 저장 데이터에 기록하는 recorder.</param>
        /// <param name="inGameScreenRouter">정산/준비 화면 전환을 요청할 router.</param>
        /// <param name="inGameTimeProvider">인게임 배율·pause·경과 시간 변환을 제공하는 서비스. null이면 gameTimeProvider에서 조회한다.</param>
        /// <param name="getSharedGameData">Economy M1 입력 조립에 사용할 공용 기준 데이터 provider 접근자.</param>
        /// <remarks>
        /// 생성 시 CompleteTradeRequested 이벤트를 구독하므로 coordinator 수명은 FrameworkRoot와 같아야 한다.
        /// </remarks>
        public TradeProgressCoordinator(
            Func<SaveData> getCurrentSaveData,
            ISaveService saveService,
            IGameTimeProvider gameTimeProvider,
            TradeProgressRecorder tradeProgressRecorder,
            InGameScreenStateRouter inGameScreenRouter = null,
            IInGameTimeProvider inGameTimeProvider = null,
            Func<ISharedGameDataProvider> getSharedGameData = null,
            global::ITradePrepareCommitCompletion tradePrepareCommitCompletion = null,
            global::ITradePrepareCommitSource tradePrepareCommitSource = null)
        {
            this.getCurrentSaveData = getCurrentSaveData;
            this.saveService = saveService;
            this.gameTimeProvider = gameTimeProvider;
            this.inGameTimeProvider = inGameTimeProvider ?? gameTimeProvider as IInGameTimeProvider;
            this.tradeProgressRecorder = tradeProgressRecorder;
            this.inGameScreenRouter = inGameScreenRouter;
            this.getSharedGameData = getSharedGameData;
            this.tradePrepareCommitCompletion = tradePrepareCommitCompletion;
            this.tradePrepareCommitSource = tradePrepareCommitSource;

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
        /// 월드맵 등 읽기 전용 표시를 위한 무역 진행 스냅샷을 반환한다.
        /// </summary>
        /// <param name="snapshot">조회에 성공하면 채워지는 진행 스냅샷.</param>
        /// <returns>
        /// SaveData와 tradeProgress가 있고 Traveling 또는 SettlementPending 상태이면 true를 반환한다.
        /// SaveData가 없거나 맵에 표시할 active trade가 없으면 false를 반환하며 snapshot은 기본값이다.
        /// </returns>
        /// <remarks>
        /// 저장 데이터, 정산, 무역 출발 상태는 변경하지 않는다.
        /// Traveling 중 pause이면 ActiveCaravan.progress01을 우선해 화면 진행이 멈추도록 한다.
        /// SettlementPending이면 Progress01은 1로 고정한다.
        /// Progress01 계산은 내부 CalculateProgress와 동일한 UTC tick 공식을 사용한다.
        /// </remarks>
        public bool TryGetMapProgress(out TradeMapProgressSnapshot snapshot)
        {
            snapshot = default;

            var saveData = GetSaveData();
            if (saveData?.tradeProgress == null)
            {
                return false;
            }

            var progress = saveData.tradeProgress;
            var state = progress.state;
            if (state != TradeProgressState.Traveling && state != TradeProgressState.SettlementPending)
            {
                return false;
            }

            float progress01;
            if (state == TradeProgressState.SettlementPending)
            {
                progress01 = JourneyRunner.ArrivalProgress;
            }
            else if (inGameTimeProvider != null && inGameTimeProvider.IsGameTimePaused)
            {
                var caravan = EnsureActiveCaravan();
                progress01 = caravan != null
                    ? caravan.progress01
                    : CalculateProgress(progress, gameTimeProvider != null ? gameTimeProvider.CurrentUtc : DateTime.UtcNow);
            }
            else if (gameTimeProvider != null)
            {
                progress01 = CalculateProgress(progress, gameTimeProvider.CurrentUtc);
            }
            else
            {
                progress01 = CalculateProgress(progress, DateTime.UtcNow);
            }

            if (progress01 < 0f)
            {
                progress01 = 0f;
            }
            else if (progress01 > JourneyRunner.ArrivalProgress)
            {
                progress01 = JourneyRunner.ArrivalProgress;
            }

            snapshot = new TradeMapProgressSnapshot(
                hasActiveTrade: true,
                activeTradeId: progress.activeTradeId,
                activeRouteId: progress.activeRouteId,
                state: state,
                progress01: progress01,
                tradeStartUtcTick: progress.tradeStartUtcTick,
                expectedTradeEndUtcTick: progress.expectedTradeEndUtcTick);
            return true;
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

            // pause 중에는 현실 진행률과 인게임 경과 시간을 모두 갱신하지 않는다.
            if (inGameTimeProvider != null && inGameTimeProvider.IsGameTimePaused)
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

            // 식량 소모는 인게임 경과 초를 사용하므로 SetProgress 전에 runtime caravan에 반영한다.
            SyncElapsedInGameSeconds(saveData, caravan, gameTimeProvider.CurrentUtc);

            // 저장된 UTC 시작/종료 tick과 현재 시간을 비교해 Core caravan 진행률을 갱신한다.
            var progress = CalculateProgress(saveData.tradeProgress, gameTimeProvider.CurrentUtc);
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
        /// Continue/Load 시 Traveling 무역에 오프라인 경과·진행도·식량을 적용하고 필요하면 정산을 생성한다.
        /// </summary>
        /// <param name="saveData">로드된 SaveData. null이면 CurrentSaveData를 사용한다.</param>
        /// <returns>
        /// 이번 호출에서 오프라인 완료로 settlement가 생성되면 true.
        /// Traveling이 아니거나 역행으로 스킵하거나 아직 이동 중이면 false.
        /// </returns>
        /// <remarks>
        /// lastSavedUtcTicks 대비 시간 역행이면 TimeRollbackDetected를 발행하고 상태를 변경하지 않는다.
        /// evaluationUtc는 lastSaved + maxOfflineRealSeconds로 상한한다.
        /// 오프라인 settle 성공 시 TradeOfflineCompleted를 한 번 발행한다.
        /// SettlementPending 복구는 RestorePendingSettlement가 담당하므로 이 메서드는 Traveling만 처리한다.
        /// </remarks>
        public bool ApplyOfflineProgressOnLoad(SaveData saveData = null)
        {
            saveData = saveData ?? GetSaveData();
            if (!CanUpdateTravelingTrade(saveData))
            {
                return false;
            }

            var loadUtc = gameTimeProvider.CurrentUtc;
            var isRollback = ResolveOfflineEvaluationUtc(saveData, loadUtc, out var evaluationUtc);
            if (isRollback)
            {
                FrameworkEvents.RaiseTimeRollbackDetected();
                FrameworkLog.Warning(
                    "Offline progress skipped because load UTC is earlier than lastSavedUtcTicks.");
                return false;
            }

            var caravan = EnsureActiveCaravan();
            if (caravan == null)
            {
                FrameworkLog.Warning("Offline progress skipped because active caravan is missing.");
                return false;
            }

            SyncElapsedInGameSeconds(saveData, caravan, evaluationUtc);
            var progress = CalculateProgress(saveData.tradeProgress, evaluationUtc);
            JourneyRunner.SetProgress(caravan, progress);
            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);

            if (!JourneyRunner.IsArrived(caravan) && caravan.runFatalReason == JourneyFailureReason.None)
            {
                saveService?.Save(saveData);
                FrameworkLog.Info(
                    $"Offline progress applied while still traveling. EvaluationUtc: {evaluationUtc:o}, ElapsedInGameSeconds: {caravan.elapsedInGameSeconds}");
                return false;
            }

            var tradeId = saveData.tradeProgress.activeTradeId ?? string.Empty;
            var settled = SettleActiveTrade(saveData, caravan);
            if (settled)
            {
                FrameworkEvents.RaiseTradeOfflineCompleted(tradeId);
                FrameworkLog.Info($"Offline trade completed and settled. TradeId: {tradeId}");
            }

            return settled;
        }

        /// <summary>
        /// cache된 settlement를 claim하고 저장 데이터와 runtime caravan을 준비 상태로 되돌린다.
        /// </summary>
        /// <returns>검증, claim staging, 저장, town 전환이 모두 성공하면 true.</returns>
        /// <remarks>
        /// 성공 시 목적지 마을 위치가 저장되고 settlement cache와 pending/commit이 삭제된 뒤 Town으로 전환된다.
        /// 저장 실패 시 SaveData와 runtime caravan을 claim 직전 snapshot으로 복구한다.
        /// </remarks>
        private bool ClaimSettlementAndResetLegacy()
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

            if (!TryResolveClaimDestination(saveData, out var destinationTownId))
            {
                return false;
            }

            // recorder가 없으면 claim 이후 완료/실패 상태를 저장 데이터에 기록할 수 없다.
            if (tradeProgressRecorder == null)
            {
                FrameworkLog.Warning("Settlement claim blocked because trade progress recorder is missing.");
                return false;
            }

            var saveDataSnapshot = JsonUtility.ToJson(saveData);
            var runtimeCaravanSnapshot = JsonUtility.ToJson(caravan);

            // Core가 settlement claim을 거부하면 framework 상태 전환도 진행하지 않는다.
            if (!JourneyRunner.ClaimSettlement(caravan))
            {
                FrameworkLog.Warning("Settlement claim blocked because Core rejected the active settlement.");
                return false;
            }

            var activeTradeId = saveData.tradeProgress.activeTradeId ?? string.Empty;
            if (!economySettlementBridge.TryApplyPendingEconomy(saveData, caravan, activeTradeId))
            {
                RestoreClaimSnapshot(saveData, caravan, saveDataSnapshot, runtimeCaravanSnapshot);
                FrameworkLog.Warning("Settlement claim rolled back because Economy M1 currency apply did not complete.");
                return false;
            }

            // settlement 결과 등급에 따라 최종 저장 상태를 Completed 또는 Failed로 기록한다.
            var finalStateRecorded = LastSettlementResult.grade == JourneyResultGrade.Failed
                ? MarkFailed(saveData)
                : MarkCompleted(saveData);
            if (!finalStateRecorded)
            {
                RestoreClaimSnapshot(saveData, caravan, saveDataSnapshot, runtimeCaravanSnapshot);
                return false;
            }

            // claim 이후 caravan을 preparation 상태로 되돌려 다음 무역 출발이 가능한 저장 상태를 만든다.
            if (!JourneyRunner.ResetToPrepare(caravan))
            {
                RestoreClaimSnapshot(saveData, caravan, saveDataSnapshot, runtimeCaravanSnapshot);
                FrameworkLog.Warning("Settlement was claimed but Core did not return the caravan to preparation.");
                return false;
            }

            saveData.player.currentTownId = destinationTownId;

            // 대기 정산과 준비 commit 정리를 같은 저장 단위에 stage한다.
            PendingSettlementSaveDataMapper.Clear(saveData);
            if (tradePrepareCommitCompletion == null ||
                !tradePrepareCommitCompletion.TryComplete(activeTradeId, out _))
            {
                RestoreClaimSnapshot(saveData, caravan, saveDataSnapshot, runtimeCaravanSnapshot);
                FrameworkLog.Warning($"Settlement claim rolled back because trade preparation commit '{activeTradeId}' could not be completed.");
                return false;
            }

            // reset된 runtime caravan을 저장 데이터에 반영한 뒤 원자 저장 결과를 확인한다.
            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            var saveResult = saveService != null ? saveService.Save(saveData) : null;
            if (saveResult == null || !saveResult.Succeeded)
            {
                RestoreClaimSnapshot(saveData, caravan, saveDataSnapshot, runtimeCaravanSnapshot);
                FrameworkLog.Warning("Settlement claim rolled back because save did not succeed.");
                return false;
            }

            ClearSettlementCache();
            inGameScreenRouter?.RequestScreen(InGameScreenState.Town);

            return true;
        }

        [Obsolete("Use ClaimSettlement(caravanId, tradeId).")]
        public bool ClaimSettlementAndReset()
        {
            var saveData = GetSaveData();
            if (saveData == null || string.IsNullOrWhiteSpace(saveData.selectedCaravanId)) return false;

            PendingSettlementSaveData pending;
            if (!SaveDataLookup.TryGetPendingSettlement(saveData, saveData.selectedCaravanId, null, out pending)
                || pending == null || string.IsNullOrWhiteSpace(pending.tradeId)) return false;

            return ClaimSettlement(saveData.selectedCaravanId, pending.tradeId).Succeeded;
        }

        /// <summary>Claims exactly one pending settlement identified by caravan and trade IDs.</summary>
        /// <returns>The concrete outcome, including save failure details when persistence was attempted.</returns>
        public ClaimSettlementResult ClaimSettlement(string caravanId, string tradeId)
        {
            if (string.IsNullOrWhiteSpace(caravanId))
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.InvalidCaravanId);
            if (string.IsNullOrWhiteSpace(tradeId))
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.InvalidTradeId);

            var saveData = GetSaveData();
            CaravanSaveData caravanSave;
            if (!SaveDataLookup.TryGetCaravan(saveData, caravanId, out caravanSave))
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.CaravanNotFound);

            TradeProgressSaveData progress;
            if (!SaveDataLookup.TryGetTradeProgress(saveData, caravanId, out progress))
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.TradeProgressNotFound);

            PendingSettlementSaveData pending = null;
            var matches = 0;
            if (saveData.pendingSettlements != null)
            {
                for (var i = 0; i < saveData.pendingSettlements.Count; i++)
                {
                    var candidate = saveData.pendingSettlements[i];
                    if (candidate == null || candidate.caravanId != caravanId || candidate.tradeId != tradeId) continue;
                    pending = candidate;
                    matches++;
                }
            }
            if (matches == 0)
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.PendingSettlementNotFound);
            if (matches > 1)
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.AmbiguousPendingSettlement);
            if (!string.Equals(progress.activeTradeId, tradeId, StringComparison.Ordinal))
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.TradeIdMismatch);
            if (progress.state != TradeProgressState.SettlementPending)
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.InvalidTradeState);
            if (pending.claimed)
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.AlreadyClaimed);
            if (!PendingSettlementSaveDataMapper.TryToRuntime(pending, out var settlementResult))
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.SettlementDataInvalid);

            var caravan = caravanId == saveData.selectedCaravanId
                ? EnsureActiveCaravan()
                : CaravanSaveDataMapper.ToRuntime(caravanSave);
            if (caravan == null)
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.SettlementDataInvalid);
            if (!TryResolveClaimDestination(saveData, progress, out var destinationTownId))
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.TownApplyFailed);

            var saveDataSnapshot = JsonUtility.ToJson(saveData);
            var runtimeCaravanSnapshot = JsonUtility.ToJson(caravan);
            var selectedCaravanIdBeforeClaim = saveData.selectedCaravanId;
            saveData.selectedCaravanId = caravanId;
            if (!JourneyRunner.ClaimSettlement(caravan))
            {
                RestoreClaimSnapshot(saveData, caravan, saveDataSnapshot, runtimeCaravanSnapshot);
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.CoreClaimRejected);
            }

            var sharedGameData = getSharedGameData != null ? getSharedGameData() : null;
            if (sharedGameData == null || !sharedGameData.IsLoaded
                || !economySettlementBridge.TryCalculateAndFill(saveData, caravan, settlementResult, sharedGameData)
                || !economySettlementBridge.TryApplyPendingEconomy(saveData, caravan, tradeId))
            {
                RestoreClaimSnapshot(saveData, caravan, saveDataSnapshot, runtimeCaravanSnapshot);
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.EconomyApplyFailed);
            }

            progress.state = settlementResult.grade == JourneyResultGrade.Failed
                ? TradeProgressState.Failed
                : TradeProgressState.Completed;
            if (!JourneyRunner.ResetToPrepare(caravan))
            {
                RestoreClaimSnapshot(saveData, caravan, saveDataSnapshot, runtimeCaravanSnapshot);
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.CoreClaimRejected);
            }

            saveData.player.currentTownId = destinationTownId;
            if (tradePrepareCommitCompletion == null || !tradePrepareCommitCompletion.TryComplete(tradeId, out _))
            {
                RestoreClaimSnapshot(saveData, caravan, saveDataSnapshot, runtimeCaravanSnapshot);
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.TownApplyFailed);
            }

            saveData.pendingSettlements.Remove(pending);
            CaravanSaveDataMapper.CopyToSave(caravan, caravanSave);
            saveData.selectedCaravanId = selectedCaravanIdBeforeClaim;
            var saveResult = saveService != null ? saveService.Save(saveData) : null;
            if (saveResult == null || !saveResult.Succeeded)
            {
                RestoreClaimSnapshot(saveData, caravan, saveDataSnapshot, runtimeCaravanSnapshot);
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.SaveFailed, saveResult);
            }

            if (LastSettlementTradeId == tradeId) ClearSettlementCache();
            inGameScreenRouter?.RequestScreen(InGameScreenState.Town);
            return ClaimSettlementResult.Success(saveResult);
        }

        private bool TryResolveClaimDestination(
            SaveData saveData,
            TradeProgressSaveData progress,
            out string destinationTownId)
        {
            destinationTownId = string.Empty;
            if (saveData.player == null) return false;

            var activeTradeId = progress.activeTradeId ?? string.Empty;
            if (tradePrepareCommitSource == null
                || !tradePrepareCommitSource.TryGet(activeTradeId, out var commit) || commit == null) return false;

            destinationTownId = commit.selectedDestinationTownId ?? string.Empty;
            var activeRouteId = progress.activeRouteId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(destinationTownId) || string.IsNullOrWhiteSpace(activeRouteId)) return false;

            var sharedGameData = getSharedGameData != null ? getSharedGameData() : null;
            if (sharedGameData == null || !sharedGameData.IsLoaded
                || !sharedGameData.TryGetRoute(activeRouteId, out var route) || route == null
                || !string.Equals(destinationTownId, route.ToTownId, StringComparison.Ordinal)) return false;

            return true;
        }

        private bool TryResolveClaimDestination(SaveData saveData, out string destinationTownId)
        {
            destinationTownId = string.Empty;
            if (saveData.player == null)
            {
                FrameworkLog.Warning("Settlement claim blocked because player save data is missing.");
                return false;
            }

            var progress = saveData.tradeProgress;
            var activeTradeId = progress.activeTradeId ?? string.Empty;
            if (tradePrepareCommitSource == null ||
                !tradePrepareCommitSource.TryGet(activeTradeId, out var commit) || commit == null)
            {
                FrameworkLog.Warning("Settlement claim blocked because the trade preparation commit is missing.");
                return false;
            }

            destinationTownId = commit.selectedDestinationTownId ?? string.Empty;
            var activeRouteId = progress.activeRouteId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(destinationTownId) || string.IsNullOrWhiteSpace(activeRouteId))
            {
                FrameworkLog.Warning("Settlement claim blocked because destination town or active route ID is empty.");
                return false;
            }

            var sharedGameData = getSharedGameData != null ? getSharedGameData() : null;
            if (sharedGameData == null || !sharedGameData.IsLoaded ||
                !sharedGameData.TryGetRoute(activeRouteId, out var route) || route == null ||
                string.IsNullOrWhiteSpace(route.ToTownId))
            {
                FrameworkLog.Warning($"Settlement claim blocked because route '{activeRouteId}' was not found.");
                return false;
            }

            if (!string.Equals(destinationTownId, route.ToTownId, StringComparison.Ordinal))
            {
                FrameworkLog.Warning(
                    $"Settlement claim blocked because destination does not match route. Commit: {destinationTownId}, Route: {route.ToTownId}");
                destinationTownId = string.Empty;
                return false;
            }

            return true;
        }

        private void RestoreClaimSnapshot(
            SaveData saveData,
            CaravanData caravan,
            string saveDataSnapshot,
            string runtimeCaravanSnapshot)
        {
            JsonUtility.FromJsonOverwrite(saveDataSnapshot, saveData);
            JsonUtility.FromJsonOverwrite(runtimeCaravanSnapshot, caravan);

            var sharedGameData = getSharedGameData != null ? getSharedGameData() : null;
            if (sharedGameData != null && sharedGameData.IsLoaded && LastSettlementResult != null)
            {
                economySettlementBridge.TryCalculateAndFill(saveData, caravan, LastSettlementResult, sharedGameData);
            }
        }

        /// <summary>
        /// 저장된 PendingSettlementSaveData로 runtime settlement cache를 복구한다.
        /// </summary>
        /// <param name="saveData">로드된 SaveData. null이면 CurrentSaveData를 사용한다.</param>
        /// <returns>
        /// SettlementPending과 pendingSettlement 검증에 성공하고 LastSettlementResult·Economy pending을 재구성하면 true.
        /// 상태 불일치·결과 누락·claimed·버전 불일치·caravan 상태 불일치 시 false이며 Completed로 강등하지 않는다.
        /// </returns>
        /// <remarks>
        /// SharedGameData가 로드된 뒤 호출해야 Economy pending 재계산이 가능하다.
        /// 성공 시 TradeSettlementReady를 다시 발행해 SettlementUiBridge cache를 갱신한다.
        /// </remarks>
        public bool RestorePendingSettlement(SaveData saveData = null)
        {
            saveData = saveData ?? GetSaveData();
            if (saveData == null || saveData.tradeProgress == null)
            {
                FrameworkLog.Warning("Pending settlement restore blocked because trade progress save data is missing.");
                return false;
            }

            if (saveData.tradeProgress.state != TradeProgressState.SettlementPending)
            {
                return false;
            }

            var pending = saveData.pendingSettlement;
            if (pending == null || !pending.hasResult)
            {
                FrameworkLog.Error(
                    "Pending settlement restore blocked because SettlementPending has no saved result. Claim remains blocked.");
                return false;
            }

            if (pending.claimed)
            {
                FrameworkLog.Error("Pending settlement restore blocked because pending settlement is already claimed.");
                return false;
            }

            if (pending.resultVersion != PendingSettlementSaveData.CurrentResultVersion)
            {
                FrameworkLog.Error(
                    $"Pending settlement restore blocked because resultVersion {pending.resultVersion} is unsupported. Expected {PendingSettlementSaveData.CurrentResultVersion}.");
                return false;
            }

            var activeTradeId = saveData.tradeProgress.activeTradeId ?? string.Empty;
            if (string.IsNullOrEmpty(pending.tradeId) || pending.tradeId != activeTradeId)
            {
                FrameworkLog.Error(
                    $"Pending settlement restore blocked because trade ID mismatch. Pending: {pending.tradeId}, Active: {activeTradeId}");
                return false;
            }

            if (!PendingSettlementSaveDataMapper.TryToRuntime(pending, out var restoredResult) || restoredResult == null)
            {
                FrameworkLog.Error("Pending settlement restore blocked because pending settlement could not be mapped to JourneyResultData.");
                return false;
            }

            var caravan = EnsureActiveCaravan();
            if (caravan == null)
            {
                FrameworkLog.Error("Pending settlement restore blocked because active caravan is missing.");
                return false;
            }

            if (caravan.state != JourneyState.Settling || caravan.settlementClaimed)
            {
                FrameworkLog.Error(
                    $"Pending settlement restore blocked because caravan state is invalid. State: {caravan.state}, Claimed: {caravan.settlementClaimed}");
                return false;
            }

            LastSettlementTradeId = pending.tradeId;
            LastSettlementResult = restoredResult;

            var sharedGameData = getSharedGameData != null ? getSharedGameData() : null;
            if (sharedGameData == null || !sharedGameData.IsLoaded)
            {
                FrameworkLog.Warning("Pending settlement Economy rebuild skipped because shared game data is not loaded. Claim currency apply may fail.");
            }
            else if (!economySettlementBridge.TryCalculateAndFill(saveData, caravan, restoredResult, sharedGameData))
            {
                FrameworkLog.Warning("Pending settlement Economy rebuild failed. Saved display amounts are kept for UI.");
            }
            else
            {
                // UI에는 저장 시점의 확정 금액을 우선 표시하고, Claim apply는 재계산된 Economy pending을 사용한다.
                if (restoredResult.revenue != pending.revenue
                    || restoredResult.cost != pending.cost
                    || restoredResult.netProfit != pending.netProfit)
                {
                    FrameworkLog.Warning(
                        "Pending settlement Economy amounts differ from saved values. UI uses saved amounts; claim apply uses recalculated Economy pending.");
                }

                restoredResult.revenue = pending.revenue;
                restoredResult.cost = pending.cost;
                restoredResult.netProfit = pending.netProfit;
            }

            FrameworkEvents.RaiseTradeSettlementReady(pending.caravanId, LastSettlementTradeId, LastSettlementResult);
            FrameworkLog.Info($"Pending settlement restored. TradeId: {LastSettlementTradeId}, Grade: {LastSettlementResult.grade}");
            return true;
        }

        /// <summary>
        /// runtime settlement cache를 삭제한다.
        /// </summary>
        public void ClearSettlementCache()
        {
            LastSettlementTradeId = string.Empty;
            LastSettlementResult = null;
            economySettlementBridge.ClearPending();
        }

        /// <summary>
        /// SaveData의 pendingSettlement DTO를 빈 상태로 초기화한다.
        /// </summary>
        /// <param name="saveData">대상 SaveData. null이면 CurrentSaveData를 사용한다.</param>
        public void ClearPendingSettlementSave(SaveData saveData = null)
        {
            PendingSettlementSaveDataMapper.Clear(saveData ?? GetSaveData());
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

            // 정산 시 foodConsumed 계산이 올바르도록 도착 처리 전 인게임 경과를 동기화한다.
            SyncElapsedInGameSeconds(saveData, caravan, gameTimeProvider.CurrentUtc);

            // Core progress를 도착값으로 맞춘 뒤 동일한 settlement 생성 경로를 재사용한다.
            JourneyRunner.SetProgress(caravan, JourneyRunner.ArrivalProgress);
            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            SettleActiveTrade(saveData, caravan);
        }

        private bool ResolveOfflineEvaluationUtc(SaveData saveData, DateTime loadUtc, out DateTime evaluationUtc)
        {
            var lastSavedUtcTicks = saveData != null ? saveData.lastSavedUtcTicks : 0L;
            if (gameTimeProvider is GameTimeService gameTimeService)
            {
                return gameTimeService.TryResolveOfflineEvaluationUtc(lastSavedUtcTicks, loadUtc, out evaluationUtc);
            }

            var conversionPolicy = new InGameTimeConversionPolicy();
            var maxOffline = InGameTimePolicyConfig.DefaultMaxOfflineRealSeconds;
            return conversionPolicy.TryResolveOfflineEvaluationUtc(
                lastSavedUtcTicks,
                loadUtc,
                maxOffline,
                out evaluationUtc);
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
            var settlementRouteId = saveData.tradeProgress.activeRouteId ?? string.Empty;
            LastSettlementTradeId = settlementTradeId;
            LastSettlementResult = result;

            var sharedGameData = getSharedGameData != null ? getSharedGameData() : null;
            ApplyRouteMinimumFoodConsumption(saveData, caravan, result, sharedGameData);
            if (sharedGameData == null || !sharedGameData.IsLoaded)
            {
                FrameworkLog.Warning("Economy M1 settlement preview skipped because shared game data is not loaded.");
            }
            else if (!economySettlementBridge.TryCalculateAndFill(saveData, caravan, result, sharedGameData))
            {
                FrameworkLog.Warning("Economy M1 settlement preview failed. Core settlement grade is still available.");
            }

            // SettlementPending과 확정 정산 결과를 같은 저장 단위에 기록한다.
            saveData.pendingSettlement = PendingSettlementSaveDataMapper.ToSave(result, settlementTradeId, settlementRouteId);

            CaravanSaveDataMapper.CopyToSave(caravan, saveData.caravan);
            saveService?.Save(saveData);
            FrameworkEvents.RaiseTradeSettlementReady(saveData.tradeProgress.caravanId, settlementTradeId, result);

            // The ready event may auto-claim the settlement and route to Town. Only request
            // Settlement while the same trade is still waiting to be claimed.
            if (saveData.tradeProgress != null
                && saveData.tradeProgress.state == TradeProgressState.SettlementPending)
            {
                inGameScreenRouter?.RequestScreen(InGameScreenState.Settlement);
            }

            return true;
        }

        private static void ApplyRouteMinimumFoodConsumption(
            SaveData saveData,
            CaravanData caravan,
            JourneyResultData result,
            ISharedGameDataProvider sharedGameData)
        {
            if (saveData?.tradeProgress == null || caravan == null || result == null
                || sharedGameData == null || !sharedGameData.IsLoaded)
            {
                return;
            }

            string routeId = saveData.tradeProgress.activeRouteId ?? string.Empty;
            if (string.IsNullOrEmpty(routeId)
                || !sharedGameData.TryGetRoute(routeId, out SharedRouteDefinition route))
            {
                return;
            }

            int minimumConsumed = Mathf.CeilToInt(
                Mathf.Max(0, route.BaseRequiredFoodQuantity) * Mathf.Clamp01(caravan.progress01));
            int alreadyConsumed = Mathf.Max(0, Mathf.RoundToInt(result.foodConsumed));
            int additionalConsumption = Mathf.Min(
                Mathf.Max(0, minimumConsumed - alreadyConsumed),
                Mathf.Max(0, caravan.foodAmount));

            caravan.foodAmount -= additionalConsumption;
            result.foodConsumed = alreadyConsumed + additionalConsumption;
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

            var pending = saveData.pendingSettlement;
            if (pending == null || !pending.hasResult)
            {
                FrameworkLog.Warning("Settlement claim blocked because pending settlement is missing.");
                return false;
            }

            if (pending.claimed)
            {
                FrameworkLog.Warning("Settlement claim blocked because pending settlement is already claimed.");
                return false;
            }

            if (string.IsNullOrEmpty(pending.tradeId) || pending.tradeId != activeTradeId)
            {
                FrameworkLog.Warning(
                    $"Settlement claim blocked because pending settlement trade ID does not match active trade ID. Pending: {pending.tradeId}, Active: {activeTradeId}");
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

        private float CalculateProgress(TradeProgressSaveData progress, DateTime evaluationUtc)
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
            var elapsedSeconds = (evaluationUtc - startUtc).TotalSeconds;
            return (float)(elapsedSeconds / totalSeconds);
        }

        /// <summary>
        /// active trade 기준 인게임 경과 초를 runtime caravan과 SaveData에 동시 기록한다.
        /// </summary>
        /// <remarks>
        /// JourneyRunner.SetProgress 이전에 호출해야 Core 식량 소모 계산이 올바른 elapsed를 사용한다.
        /// evaluationUtc를 넘겨 온라인(CurrentUtc)과 오프라인(상한 clamp) 경로를 공유한다.
        /// </remarks>
        private void SyncElapsedInGameSeconds(SaveData saveData, CaravanData caravan, DateTime evaluationUtc)
        {
            if (saveData?.caravan == null || saveData.tradeProgress == null || caravan == null
                || inGameTimeProvider == null)
            {
                return;
            }

            var elapsedInGameSeconds = (float)inGameTimeProvider.GetElapsedInGameSecondsForActiveTrade(
                saveData.tradeProgress,
                evaluationUtc);
            caravan.elapsedInGameSeconds = elapsedInGameSeconds;
            saveData.caravan.elapsedInGameSeconds = elapsedInGameSeconds;
        }
    }
}
