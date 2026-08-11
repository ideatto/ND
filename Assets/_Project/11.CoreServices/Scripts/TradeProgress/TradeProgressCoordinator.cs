
/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - 진행 중인 무역의 시간 기반 진행률 계산, 정산 생성, claim 후 초기화를 조율한다.
 * - Core JourneyRunner 결과를 SaveData, FrameworkEvents, Economy M1 bridge, 인게임 화면 상태에 반영한다.
 * - SettlementPending 대기 정산을 PendingSettlementSaveData로 영속화하고 재실행 시 runtime cache를 복구한다.
 * - Continue/Load 시 Traveling 무역의 오프라인 경과·식량·완료를 ApplyOfflineProgressOnLoad로 복구한다.
 * - Online/Offline 진행에서 거리 기반 route event를 처리하고 저장 성공 후 발생 로그를 발행한다.
 * - 명시적 caravan/trade/event 대상 forced route event와 저장 실패 rollback을 제공한다.
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
 * - TryProcessForcedRouteEvent(...): 명시된 traveling caravan에 route event를 transaction으로 적용한다.
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
using System.Collections.Generic;
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

    public enum ForcedRouteEventFailureReason
    {
        None,
        InvalidCaravanId,
        InvalidTradeId,
        InvalidEventId,
        ProgressNotFound,
        CaravanNotFound,
        RuntimeCaravanNotFound,
        NotTraveling,
        TradeMismatch,
        RouteNotFound,
        EventNotFound,
        AlreadyFatal,
        EventApplicationFailed,
        SaveFailed,
        RollbackFailed
    }

    public readonly struct ForcedRouteEventResult
    {
        private ForcedRouteEventResult(
            bool succeeded,
            ForcedRouteEventFailureReason failureReason,
            string caravanId,
            string tradeId,
            string eventId,
            SaveResult saveResult)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            CaravanId = caravanId ?? string.Empty;
            TradeId = tradeId ?? string.Empty;
            EventId = eventId ?? string.Empty;
            SaveResult = saveResult;
        }

        public bool Succeeded { get; }
        public ForcedRouteEventFailureReason FailureReason { get; }
        public string CaravanId { get; }
        public string TradeId { get; }
        public string EventId { get; }
        public SaveResult SaveResult { get; }

        public static ForcedRouteEventResult Success(
            string caravanId, string tradeId, string eventId, SaveResult saveResult)
            => new ForcedRouteEventResult(
                true, ForcedRouteEventFailureReason.None, caravanId, tradeId, eventId, saveResult);

        public static ForcedRouteEventResult Failure(
            ForcedRouteEventFailureReason reason,
            string caravanId,
            string tradeId,
            string eventId,
            SaveResult saveResult = null)
            => new ForcedRouteEventResult(false, reason, caravanId, tradeId, eventId, saveResult);
    }

    public enum ForcedTradeCompletionFailureReason
    {
        None,
        InvalidCaravanId,
        InvalidTradeId,
        TradeProgressNotFound,
        NotTraveling,
        TradeIdentityMismatch,
        CaravanNotFound,
        RuntimeCaravanNotFound,
        RuntimeIdentityMismatch,
        DuplicatePendingSettlement,
        RequiredDependencyMissing,
        SettlementFailed,
        SaveFailed,
        RollbackFailed
    }

    public readonly struct ForcedTradeCompletionResult
    {
        private ForcedTradeCompletionResult(
            bool succeeded,
            ForcedTradeCompletionFailureReason failureReason,
            string caravanId,
            string tradeId,
            SaveResult saveResult)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            CaravanId = caravanId ?? string.Empty;
            TradeId = tradeId ?? string.Empty;
            SaveResult = saveResult;
        }

        public bool Succeeded { get; }
        public ForcedTradeCompletionFailureReason FailureReason { get; }
        public string CaravanId { get; }
        public string TradeId { get; }
        public SaveResult SaveResult { get; }

        public static ForcedTradeCompletionResult Success(
            string caravanId,
            string tradeId,
            SaveResult saveResult)
            => new ForcedTradeCompletionResult(
                true,
                ForcedTradeCompletionFailureReason.None,
                caravanId,
                tradeId,
                saveResult);

        public static ForcedTradeCompletionResult Failure(
            ForcedTradeCompletionFailureReason reason,
            string caravanId,
            string tradeId,
            SaveResult saveResult = null)
            => new ForcedTradeCompletionResult(false, reason, caravanId, tradeId, saveResult);
    }

    /// <summary>
    /// 무역 진행률, 정산 생성, 정산 claim을 저장 데이터와 Core caravan 상태에 반영하는 coordinator이다.
    /// </summary>
    public sealed class TradeProgressCoordinator
    {
        /// <summary>
        /// 판매 transaction 저장 실패 시 Journey 상태를 원래 값으로 복구하기 위한 snapshot이다.
        /// UI는 내부 상태를 직접 변경하지 않고 이 객체를 Coordinator에 다시 전달한다.
        /// </summary>
        public sealed class ArrivalSaleTransitionSnapshot
        {
            internal SaveData SaveData;
            internal int SaveVersionBefore;
            internal long LastSavedUtcTicksBefore;
            internal CaravanSaveData Caravan;
            internal TradeProgressSaveData Progress;
            internal CaravanData RuntimeCaravan;
            internal JourneyState RuntimeStateBefore;
            internal JourneyState CaravanStateBefore;
            internal TradeProgressState ProgressStateBefore;
            internal string CaravanId;
            internal string TradeId;
        }

        private readonly Func<SaveData> getCurrentSaveData;
        private readonly ISaveService saveService;
        private readonly IGameTimeProvider gameTimeProvider;
        private readonly IInGameTimeProvider inGameTimeProvider;
        private readonly TradeProgressRecorder tradeProgressRecorder;
        private readonly InGameScreenStateRouter inGameScreenRouter;
        private readonly Func<ISharedGameDataProvider> getSharedGameData;
        private readonly global::ITradePrepareCommitCompletion tradePrepareCommitCompletion;
        private readonly global::ITradePrepareCommitSource tradePrepareCommitSource;
        private readonly global::IExactTradePrepareCommitStore exactTradePrepareCommitStore;
        private readonly Action<string> onTownVisited;
        private readonly EconomyM1SettlementBridge economySettlementBridge = new EconomyM1SettlementBridge();

        private readonly Dictionary<string, CaravanData> runtimeCaravans =
            new Dictionary<string, CaravanData>(StringComparer.Ordinal);

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
            global::ITradePrepareCommitSource tradePrepareCommitSource = null,
            Action<string> onTownVisited = null)
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
            this.onTownVisited = onTownVisited;
            this.exactTradePrepareCommitStore = tradePrepareCommitSource as global::IExactTradePrepareCommitStore
                ?? tradePrepareCommitCompletion as global::IExactTradePrepareCommitStore;

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

        /// <summary>The shared UTC endpoint consumed by the most recent offline restore attempt.</summary>
        public DateTime LastOfflineEvaluationUtc { get; private set; }

        /// <summary>
        /// 선택된 caravan ID에 대응하는 UI 호환용 runtime caravan 데이터이다.
        /// </summary>
        /// <remarks>
        /// 진행 계산은 이 facade가 아니라 progress의 caravan ID를 사용한다.
        /// </remarks>
        public CaravanData ActiveCaravan
        {
            get
            {
                return EnsureActiveCaravan();
            }
        }

        /// <summary>
        /// 기존 호출자 호환을 위해 전달된 caravan을 ID 기반 registry에 명시적으로 교체 등록한다.
        /// </summary>
        /// <param name="caravan">현재 active trade와 연결할 runtime caravan 데이터.</param>
        public void SetActiveCaravan(CaravanData caravan)
        {
            if (caravan != null
                && !string.IsNullOrWhiteSpace(caravan.caravanId)
                && SaveDataLookup.TryGetCaravan(GetSaveData(), caravan.caravanId, out _))
            {
                runtimeCaravans[caravan.caravanId] = caravan;
            }
        }

        /// <summary>등록된 동일 ID runtime caravan을 공유 참조로 반환한다.</summary>
        public bool TryGetRuntimeCaravan(string caravanId, out CaravanData caravan)
        {
            caravan = null;
            return !string.IsNullOrWhiteSpace(caravanId)
                && runtimeCaravans.TryGetValue(caravanId, out caravan);
        }

        /// <summary>동일 ID runtime을 반환하거나 저장 snapshot에서 생성해 등록한다.</summary>
        public CaravanData GetOrCreateRuntimeCaravan(string caravanId)
        {
            if (TryGetRuntimeCaravan(caravanId, out var caravan)) return caravan;
            if (!SaveDataLookup.TryGetCaravan(GetSaveData(), caravanId, out var caravanSave)) return null;
            caravan = CaravanSaveDataMapper.ToRuntime(caravanSave);
            return RegisterRuntimeCaravan(caravanId, caravan) ? caravan : null;
        }

        /// <summary>저장 데이터에 존재하며 ID가 일치하는 runtime만 중복 교체 없이 등록한다.</summary>
        public bool RegisterRuntimeCaravan(string caravanId, CaravanData caravan)
        {
            if (string.IsNullOrWhiteSpace(caravanId) || caravan == null
                || !string.Equals(caravanId, caravan.caravanId, StringComparison.Ordinal)
                || !SaveDataLookup.TryGetCaravan(GetSaveData(), caravanId, out _))
            {
                return false;
            }
            if (runtimeCaravans.TryGetValue(caravanId, out var existing))
            {
                return ReferenceEquals(existing, caravan);
            }
            runtimeCaravans.Add(caravanId, caravan);
            return true;
        }

        /// <summary>기존 registry를 비우고 현재 저장 데이터의 모든 caravan runtime을 다시 구성한다.</summary>
        public void RebuildRuntimeCaravans()
        {
            runtimeCaravans.Clear();
            var saveData = GetSaveData();
            if (saveData?.caravans == null) return;
            for (var index = 0; index < saveData.caravans.Count; index++)
            {
                var caravanSave = saveData.caravans[index];
                if (caravanSave == null || string.IsNullOrWhiteSpace(caravanSave.caravanId)) continue;
                if (!RegisterRuntimeCaravan(
                        caravanSave.caravanId,
                        CaravanSaveDataMapper.ToRuntime(caravanSave)))
                {
                    FrameworkLog.Warning(
                        $"Runtime caravan registration skipped. CaravanId: {caravanSave.caravanId}");
                }
            }

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
            return TryCreateMapProgressSnapshot(saveData?.tradeProgress, out snapshot);
        }

        public IReadOnlyList<TradeMapProgressSnapshot> GetMapProgressSnapshots()
        {
            var snapshots = new List<TradeMapProgressSnapshot>();
            SaveData saveData = GetSaveData();
            if (saveData?.tradeProgressEntries == null)
                return snapshots;
            foreach (TradeProgressSaveData progress in saveData.tradeProgressEntries)
            {
                if (TryCreateMapProgressSnapshot(progress, out TradeMapProgressSnapshot snapshot))
                    snapshots.Add(snapshot);
            }
            return snapshots;
        }

        private bool TryCreateMapProgressSnapshot(
            TradeProgressSaveData progress,
            out TradeMapProgressSnapshot snapshot)
        {
            snapshot = default;
            if (progress == null)
                return false;
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
                var caravan = GetOrCreateRuntimeCaravan(progress.caravanId);
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
                expectedTradeEndUtcTick: progress.expectedTradeEndUtcTick,
                caravanId: progress.caravanId);
            return true;
        }

        /// <summary>
        /// 모든 traveling progress entry의 저장 시간 정보를 기준으로 진행률을 갱신하고 필요하면 settlement를 생성한다.
        /// </summary>
        /// <param name="saveProgress">도착 전 진행률만 갱신된 경우 즉시 저장할지 여부.</param>
        /// <returns>이번 호출에서 하나 이상의 settlement가 생성되었으면 true.</returns>
        /// <remarks>
        /// 성공적인 settlement 생성 시 saveData, LastSettlementResult, 화면 상태가 변경되고 TradeSettlementReady 이벤트가 발행된다.
        /// </remarks>
        public bool CheckProgressAndCompletion(bool saveProgress = true)
        {
            var saveData = GetSaveData();
            if (saveData?.tradeProgressEntries == null || gameTimeProvider == null)
            {
                if (gameTimeProvider == null)
                {
                    FrameworkLog.Warning("Trade progress check skipped because game time provider is missing.");
                }
                return false;
            }

            if (inGameTimeProvider != null && inGameTimeProvider.IsGameTimePaused)
            {
                return false;
            }

            var entries = new List<TradeProgressSaveData>(saveData.tradeProgressEntries);
            var processed = new HashSet<TradeProgressSaveData>();
            var currentUtc = gameTimeProvider.CurrentUtc;
            var dirty = false;
            var settled = false;
            var routeEventStateChanged = false;
            var deferredEvents = new List<SettlementNotification>();
            var deferredRouteEvents = new List<RouteEventNotification>();

            for (var index = 0; index < entries.Count; index++)
            {
                var progress = entries[index];
                if (progress == null)
                {
                    FrameworkLog.Warning("Online trade progress skipped because the entry is null.");
                    continue;
                }
                if (progress.state != TradeProgressState.Traveling || !processed.Add(progress))
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(progress.caravanId)
                    || string.IsNullOrWhiteSpace(progress.activeTradeId))
                {
                    FrameworkLog.Warning(
                        $"Online trade progress skipped because an ID is missing. CaravanId: {FormatCaravanIdForLog(progress.caravanId)}, TradeId: {FormatTradeIdForLog(progress.activeTradeId)}");
                    continue;
                }

                try
                {
                    if (!TryProcessTravelingEntry(
                            saveData,
                            progress,
                            currentUtc,
                            isOfflineRestore: false,
                            deferredEvents,
                            deferredRouteEvents,
                            out var entryDirty,
                            out var entrySettled,
                            out var entryRouteEventStateChanged))
                    {
                        continue;
                    }

                    dirty |= entryDirty;
                    settled |= entrySettled;
                    routeEventStateChanged |= entryRouteEventStateChanged;
                }
                catch (Exception exception)
                {
                    FrameworkLog.Warning(
                        $"Online trade progress entry failed. CaravanId: {progress.caravanId}, TradeId: {progress.activeTradeId}, Error: {exception.Message}");
                }
            }

            var shouldSave = dirty && (saveProgress || settled || routeEventStateChanged);
            var saveSucceeded = !shouldSave;
            if (shouldSave)
            {
                var saveResult = saveService?.Save(saveData);
                saveSucceeded = saveResult != null && saveResult.Succeeded;
                if (!saveSucceeded)
                    FrameworkLog.Warning("Trade progress events were suppressed because the batch save failed.");
            }

            if (saveSucceeded)
            {
                PublishSettlementNotifications(saveData, deferredEvents, isOfflineRestore: false);
                PublishRouteEventNotifications(deferredRouteEvents);
            }

            return settled;
        }

        private bool TryProcessTravelingEntry(
            SaveData saveData,
            TradeProgressSaveData progress,
            DateTime evaluationUtc,
            bool isOfflineRestore,
            List<SettlementNotification> deferredEvents,
            List<RouteEventNotification> deferredRouteEvents,
            out bool dirty,
            out bool settled,
            out bool routeEventStateChanged)
        {
            dirty = false;
            settled = false;
            routeEventStateChanged = false;
            if (!SaveDataLookup.TryGetCaravan(saveData, progress.caravanId, out var caravanSave))
            {
                FrameworkLog.Warning(
                    $"{(isOfflineRestore ? "Offline" : "Online")} trade progress skipped because the Caravan save target is missing. CaravanId: {progress.caravanId}, TradeId: {progress.activeTradeId}");
                return false;
            }

            var runtimeCaravan = GetOrCreateRuntimeCaravan(progress.caravanId);
            if (runtimeCaravan == null
                || !string.Equals(runtimeCaravan.caravanId, progress.caravanId, StringComparison.Ordinal)
                || !string.Equals(caravanSave.caravanId, progress.caravanId, StringComparison.Ordinal))
            {
                FrameworkLog.Warning(
                    $"{(isOfflineRestore ? "Offline" : "Online")} trade progress skipped because a matching runtime Caravan was not found. CaravanId: {progress.caravanId}, TradeId: {progress.activeTradeId}");
                return false;
            }

            dirty |= SyncElapsedInGameSeconds(progress, caravanSave, runtimeCaravan, evaluationUtc);
            JourneyRunner.SetProgress(runtimeCaravan, CalculateProgress(progress, evaluationUtc));
            routeEventStateChanged = ProcessRouteEvents(
                saveData,
                progress,
                runtimeCaravan,
                evaluationUtc,
                isOfflineRestore,
                deferredRouteEvents);
            CaravanSaveDataMapper.CopyToSave(runtimeCaravan, caravanSave);
            dirty = true;

            if (!JourneyRunner.IsArrived(runtimeCaravan)
                && runtimeCaravan.runFatalReason == JourneyFailureReason.None)
            {
                return true;
            }

            settled = SettleTrade(
                saveData,
                progress,
                caravanSave,
                runtimeCaravan,
                progress.caravanId,
                progress.activeTradeId,
                deferredEvents);
            return true;
        }

        /// <summary>
        /// Continue/Load 시 모든 Traveling 무역에 오프라인 경과·진행도·식량을 적용하고 필요하면 정산을 생성한다.
        /// </summary>
        /// <param name="saveData">로드된 SaveData. null이면 CurrentSaveData를 사용한다.</param>
        /// <returns>
        /// 이번 호출에서 하나 이상의 오프라인 완료 settlement가 생성되면 true.
        /// </returns>
        /// <remarks>
        /// lastSavedUtcTicks 대비 시간 역행이면 TimeRollbackDetected를 발행하고 상태를 변경하지 않는다.
        /// evaluationUtc는 lastSaved + maxOfflineRealSeconds로 상한한다.
        /// 각 오프라인 settle 성공 시 해당 trade ID로 TradeOfflineCompleted를 한 번 발행한다.
        /// SettlementPending 복구는 RestorePendingSettlement가 담당하므로 이 메서드는 Traveling만 처리한다.
        /// entry 하나의 오류는 나머지 entry 복구를 중단하지 않는다.
        /// </remarks>
        public bool ApplyOfflineProgressOnLoad(SaveData saveData = null)
        {
            saveData = saveData ?? GetSaveData();
            if (saveData == null || gameTimeProvider == null)
            {
                if (gameTimeProvider == null)
                    FrameworkLog.Warning("Offline progress skipped because game time provider is missing.");
                return false;
            }

            var loadUtc = gameTimeProvider.CurrentUtc;
            var context = ResolveOfflineRestoreContext(saveData, loadUtc);
            if (context.ClockRollbackDetected)
            {
                FrameworkEvents.RaiseTimeRollbackDetected();
                FrameworkLog.Warning(
                    "Offline progress skipped because load UTC is earlier than lastSavedUtcTicks.");
                return false;
            }

            var snapshot = JsonUtility.ToJson(saveData);
            var result = PrepareOfflineProgressOnLoad(saveData, context);
            if (!result.Changed)
            {
                result.Publish(this, saveData);
                return result.Settled;
            }

            SaveResult saveResult = null;
            try
            {
                saveResult = saveService?.Save(saveData);
            }
            catch (Exception exception)
            {
                FrameworkLog.Error($"Offline progress save threw an exception: {exception.Message}");
            }

            if (saveResult == null || !saveResult.Succeeded)
            {
                JsonUtility.FromJsonOverwrite(snapshot, saveData);
                result.RollbackRuntime(this);
                FrameworkLog.Warning("Offline progress events were suppressed because the batch save failed.");
                return false;
            }

            result.Publish(this, saveData);
            return result.Settled;
        }

        internal TradeOfflineRestoreResult PrepareOfflineProgressOnLoad(
            SaveData saveData,
            OfflineRestoreContext context)
        {
            LastOfflineEvaluationUtc = context.EvaluationUtc;
            if (saveData?.tradeProgressEntries == null || context.ClockRollbackDetected)
            {
                return new TradeOfflineRestoreResult();
            }

            var entries = new List<TradeProgressSaveData>(saveData.tradeProgressEntries);
            var previousSettlementTradeId = LastSettlementTradeId;
            var previousSettlementResult = LastSettlementResult;
            var processed = new HashSet<TradeProgressSaveData>();
            var dirty = false;
            var settled = false;
            var deferredEvents = new List<SettlementNotification>();
            var deferredRouteEvents = new List<RouteEventNotification>();

            for (var index = 0; index < entries.Count; index++)
            {
                var progress = entries[index];
                if (progress == null)
                {
                    FrameworkLog.Warning("Offline trade restore skipped because the entry is null.");
                    continue;
                }
                if (progress.state != TradeProgressState.Traveling || !processed.Add(progress))
                    continue;
                if (string.IsNullOrWhiteSpace(progress.caravanId)
                    || string.IsNullOrWhiteSpace(progress.activeTradeId))
                {
                    FrameworkLog.Warning(
                        $"Offline trade restore skipped because an ID is missing. CaravanId: {FormatCaravanIdForLog(progress.caravanId)}, TradeId: {FormatTradeIdForLog(progress.activeTradeId)}");
                    continue;
                }
                if (!HasValidOfflineTimeRange(progress))
                {
                    FrameworkLog.Warning(
                        $"Offline trade restore skipped because UTC ticks are invalid. CaravanId: {progress.caravanId}, TradeId: {progress.activeTradeId}");
                    continue;
                }

                try
                {
                    if (!TryProcessTravelingEntry(
                             saveData,
                             progress,
                             context.EvaluationUtc,
                            isOfflineRestore: true,
                            deferredEvents,
                            deferredRouteEvents,
                            out var entryDirty,
                            out var entrySettled,
                            out _))
                    {
                        continue;
                    }

                    dirty |= entryDirty;
                    settled |= entrySettled;
                }
                catch (Exception exception)
                {
                    FrameworkLog.Warning(
                        $"Offline trade restore entry failed. CaravanId: {progress.caravanId}, TradeId: {progress.activeTradeId}, Error: {exception.Message}");
                }
            }

            return new TradeOfflineRestoreResult(
                dirty,
                settled,
                deferredEvents,
                deferredRouteEvents,
                previousSettlementTradeId,
                previousSettlementResult);
        }

        /// <summary>
        /// 명시된 caravan의 traveling trade에 route event를 적용하고 저장한다.
        /// </summary>
        /// <returns>
        /// 검증, runtime 적용, DTO 복사와 저장이 모두 성공한 경우에만 성공한다.
        /// 저장 실패 시 대상 runtime과 CaravanSaveData를 명령 직전 상태로 복구한다.
        /// </returns>
        public ForcedRouteEventResult TryProcessForcedRouteEvent(
            string caravanId,
            string tradeId,
            string eventId)
        {
            var normalizedCaravanId = caravanId?.Trim() ?? string.Empty;
            var normalizedTradeId = tradeId?.Trim() ?? string.Empty;
            var normalizedEventId = eventId?.Trim() ?? string.Empty;
            if (normalizedCaravanId.Length == 0)
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.InvalidCaravanId,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);
            if (normalizedTradeId.Length == 0)
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.InvalidTradeId,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);
            if (normalizedEventId.Length == 0)
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.InvalidEventId,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);

            var saveData = GetSaveData();
            if (!SaveDataLookup.TryGetTradeProgress(saveData, normalizedCaravanId, out var progress))
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.ProgressNotFound,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);
            if (progress.state != TradeProgressState.Traveling)
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.NotTraveling,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);
            if (!string.Equals(progress.activeTradeId, normalizedTradeId, StringComparison.Ordinal))
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.TradeMismatch,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);
            if (!SaveDataLookup.TryGetCaravan(saveData, normalizedCaravanId, out var caravanSave))
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.CaravanNotFound,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);
            if (!TryGetRuntimeCaravan(normalizedCaravanId, out var runtimeCaravan)
                || runtimeCaravan == null
                || !string.Equals(runtimeCaravan.caravanId, normalizedCaravanId, StringComparison.Ordinal)
                || !string.Equals(caravanSave.caravanId, normalizedCaravanId, StringComparison.Ordinal))
            {
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.RuntimeCaravanNotFound,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);
            }

            var sharedGameData = getSharedGameData != null ? getSharedGameData() : null;
            if (sharedGameData == null || !sharedGameData.IsLoaded
                || string.IsNullOrWhiteSpace(progress.activeRouteId)
                || !sharedGameData.TryGetRoute(progress.activeRouteId, out var route)
                || route == null)
            {
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.RouteNotFound,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);
            }
            if (!RouteContainsEvent(route, normalizedEventId))
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.EventNotFound,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);
            if (runtimeCaravan.runFatalReason != JourneyFailureReason.None)
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.AlreadyFatal,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);

            var runtimeSnapshot = JsonUtility.ToJson(runtimeCaravan);
            var saveSnapshot = JsonUtility.ToJson(caravanSave);
            var processResult = TradeRouteEventProcessor.ProcessForced(
                runtimeCaravan, route, normalizedTradeId, normalizedEventId);
            if (!processResult.Succeeded)
            {
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.EventApplicationFailed,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId);
            }

            var activityLogSnapshot = saveData.caravanActivityLogs != null
                ? new List<CaravanActivityLogEntrySaveData>(saveData.caravanActivityLogs)
                : new List<CaravanActivityLogEntrySaveData>();
            RecordRouteEventLogs(saveData, progress, route, processResult);
            CaravanSaveDataMapper.CopyToSave(runtimeCaravan, caravanSave);
            var saveResult = saveService?.Save(saveData)
                ?? SaveResult.Failure(
                    SaveFailureReason.InvalidData,
                    "Save service is missing.",
                    nameof(CaravanSaveData));
            if (!saveResult.Succeeded)
            {
                saveData.caravanActivityLogs = activityLogSnapshot;
                try
                {
                    JsonUtility.FromJsonOverwrite(runtimeSnapshot, runtimeCaravan);
                    JsonUtility.FromJsonOverwrite(saveSnapshot, caravanSave);
                }
                catch (Exception exception)
                {
                    FrameworkLog.Error(
                        $"Forced route event rollback failed. CaravanId: {normalizedCaravanId}, TradeId: {normalizedTradeId}, EventId: {normalizedEventId}, Error: {exception.Message}");
                    return ForcedRouteEventResult.Failure(
                        ForcedRouteEventFailureReason.RollbackFailed,
                        normalizedCaravanId, normalizedTradeId, normalizedEventId, saveResult);
                }

                FrameworkLog.Warning(
                    $"Forced route event rolled back because save failed. CaravanId: {normalizedCaravanId}, TradeId: {normalizedTradeId}, EventId: {normalizedEventId}");
                return ForcedRouteEventResult.Failure(
                    ForcedRouteEventFailureReason.SaveFailed,
                    normalizedCaravanId, normalizedTradeId, normalizedEventId, saveResult);
            }

            FrameworkLog.Info(
                $"Route event occurred after save. CaravanId: {normalizedCaravanId}, TradeId: {normalizedTradeId}, RouteId: {route.Id}, EventId: {normalizedEventId}, CheckIndex: -1, Forced: True, Offline: False, Fatal: {processResult.BecameFatal}");
            return ForcedRouteEventResult.Success(
                normalizedCaravanId, normalizedTradeId, normalizedEventId, saveResult);
        }

        /// <summary>
        /// 명시된 Caravan과 trade가 모두 일치하는 Traveling 무역 하나를 도착 정산 대기로 전환한다.
        /// </summary>
        /// <returns>
        /// 저장까지 성공한 경우에만 성공한다. 검증, 정산 생성 또는 저장 실패 시 성공 알림을 발행하지 않으며
        /// 이 호출이 변경한 저장 데이터, runtime Caravan, 정산 cache를 원래 상태로 복원한다.
        /// </returns>
        public ForcedTradeCompletionResult TryForceCompleteTrade(string caravanId, string tradeId)
        {
            var normalizedCaravanId = caravanId?.Trim() ?? string.Empty;
            var normalizedTradeId = tradeId?.Trim() ?? string.Empty;
            if (normalizedCaravanId.Length == 0)
                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.InvalidCaravanId,
                    normalizedCaravanId,
                    normalizedTradeId);
            if (normalizedTradeId.Length == 0)
                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.InvalidTradeId,
                    normalizedCaravanId,
                    normalizedTradeId);

            var saveData = GetSaveData();
            if (!SaveDataLookup.TryGetTradeProgress(saveData, normalizedCaravanId, out var progress))
                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.TradeProgressNotFound,
                    normalizedCaravanId,
                    normalizedTradeId);
            if (progress.state != TradeProgressState.Traveling)
                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.NotTraveling,
                    normalizedCaravanId,
                    normalizedTradeId);
            if (!string.Equals(progress.activeTradeId, normalizedTradeId, StringComparison.Ordinal))
                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.TradeIdentityMismatch,
                    normalizedCaravanId,
                    normalizedTradeId);
            if (!SaveDataLookup.TryGetCaravan(saveData, normalizedCaravanId, out var caravanSave))
                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.CaravanNotFound,
                    normalizedCaravanId,
                    normalizedTradeId);
            if (!TryGetRuntimeCaravan(normalizedCaravanId, out var runtimeCaravan)
                || runtimeCaravan == null)
            {
                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.RuntimeCaravanNotFound,
                    normalizedCaravanId,
                    normalizedTradeId);
            }
            if (!string.Equals(caravanSave.caravanId, normalizedCaravanId, StringComparison.Ordinal)
                || !string.Equals(runtimeCaravan.caravanId, normalizedCaravanId, StringComparison.Ordinal))
            {
                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.RuntimeIdentityMismatch,
                    normalizedCaravanId,
                    normalizedTradeId);
            }
            if (SaveDataLookup.TryGetPendingSettlement(
                    saveData, normalizedCaravanId, normalizedTradeId, out _))
            {
                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.DuplicatePendingSettlement,
                    normalizedCaravanId,
                    normalizedTradeId);
            }
            if (saveData.pendingSettlements == null || tradeProgressRecorder == null
                || saveService == null || gameTimeProvider == null)
            {
                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.RequiredDependencyMissing,
                    normalizedCaravanId,
                    normalizedTradeId);
            }

            var saveDataSnapshot = JsonUtility.ToJson(saveData);
            var runtimeCaravanSnapshot = JsonUtility.ToJson(runtimeCaravan);
            var previousSettlementTradeId = LastSettlementTradeId;
            var previousSettlementResult = LastSettlementResult;
            var deferredEvents = new List<SettlementNotification>();

            SyncElapsedInGameSeconds(progress, caravanSave, runtimeCaravan, gameTimeProvider.CurrentUtc);
            JourneyRunner.SetProgress(runtimeCaravan, JourneyRunner.ArrivalProgress);
            CaravanSaveDataMapper.CopyToSave(runtimeCaravan, caravanSave);
            if (!SettleTrade(
                    saveData,
                    progress,
                    caravanSave,
                    runtimeCaravan,
                    normalizedCaravanId,
                    normalizedTradeId,
                    deferredEvents))
            {
                if (!TryRestoreForcedTradeCompletion(
                        saveData,
                        runtimeCaravan,
                        saveDataSnapshot,
                        runtimeCaravanSnapshot,
                        previousSettlementTradeId,
                        previousSettlementResult,
                        normalizedCaravanId,
                        normalizedTradeId))
                {
                    return ForcedTradeCompletionResult.Failure(
                        ForcedTradeCompletionFailureReason.RollbackFailed,
                        normalizedCaravanId,
                        normalizedTradeId);
                }

                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.SettlementFailed,
                    normalizedCaravanId,
                    normalizedTradeId);
            }

            var saveResult = saveService.Save(saveData);
            if (saveResult == null || !saveResult.Succeeded)
            {
                if (!TryRestoreForcedTradeCompletion(
                        saveData,
                        runtimeCaravan,
                        saveDataSnapshot,
                        runtimeCaravanSnapshot,
                        previousSettlementTradeId,
                        previousSettlementResult,
                        normalizedCaravanId,
                        normalizedTradeId))
                {
                    FrameworkLog.Error(
                        $"Force trade arrival rollback failed. CaravanId: {normalizedCaravanId}, TradeId: {normalizedTradeId}, SaveFailure: {saveResult?.FailureReason}, Message: {saveResult?.Message}");
                    return ForcedTradeCompletionResult.Failure(
                        ForcedTradeCompletionFailureReason.RollbackFailed,
                        normalizedCaravanId,
                        normalizedTradeId,
                        saveResult);
                }

                FrameworkLog.Warning(
                    $"Force trade arrival rolled back because save failed. CaravanId: {normalizedCaravanId}, TradeId: {normalizedTradeId}, SaveFailure: {saveResult?.FailureReason}, Message: {saveResult?.Message}");
                return ForcedTradeCompletionResult.Failure(
                    ForcedTradeCompletionFailureReason.SaveFailed,
                    normalizedCaravanId,
                    normalizedTradeId,
                    saveResult);
            }

            PublishSettlementNotifications(saveData, deferredEvents, isOfflineRestore: false);
            FrameworkLog.Info(
                $"Force trade arrival succeeded. CaravanId: {normalizedCaravanId}, TradeId: {normalizedTradeId}, ProgressState: {progress.state}");
            return ForcedTradeCompletionResult.Success(
                normalizedCaravanId,
                normalizedTradeId,
                saveResult);
        }

        private bool TryRestoreForcedTradeCompletion(
            SaveData saveData,
            CaravanData runtimeCaravan,
            string saveDataSnapshot,
            string runtimeCaravanSnapshot,
            string previousSettlementTradeId,
            JourneyResultData previousSettlementResult,
            string caravanId,
            string tradeId)
        {
            try
            {
                JsonUtility.FromJsonOverwrite(saveDataSnapshot, saveData);
                JsonUtility.FromJsonOverwrite(runtimeCaravanSnapshot, runtimeCaravan);
                LastSettlementTradeId = previousSettlementTradeId;
                LastSettlementResult = previousSettlementResult;
                economySettlementBridge.ClearPending(caravanId, tradeId);
                return true;
            }
            catch (Exception exception)
            {
                FrameworkLog.Error(
                    $"Force trade arrival rollback failed. CaravanId: {caravanId}, TradeId: {tradeId}, Error: {exception.Message}");
                return false;
            }
        }

        private bool ProcessRouteEvents(
            SaveData saveData,
            TradeProgressSaveData progress,
            CaravanData runtimeCaravan,
            DateTime evaluationUtc,
            bool isOfflineRestore,
            List<RouteEventNotification> deferredNotifications)
        {
            var sharedGameData = getSharedGameData != null ? getSharedGameData() : null;
            if (sharedGameData == null || !sharedGameData.IsLoaded
                || string.IsNullOrWhiteSpace(progress.activeRouteId)
                || !sharedGameData.TryGetRoute(progress.activeRouteId, out var route)
                || route?.Events == null || route.Events.Length == 0
                || route.MaxEventCount <= 0 || route.Distance <= 0f)
            {
                return false;
            }

            var intervalKm = route.Distance / route.MaxEventCount;
            var banditEncounterMultiplier =
                QuestRuntimeService.ResolveBanditEncounterMultiplier(
                    saveData.world,
                    route,
                    evaluationUtc);
            var result = TradeRouteEventProcessor.Process(
                runtimeCaravan,
                route,
                progress.activeTradeId,
                intervalKm,
                route.BaseRiskLevel,
                banditEncounterMultiplier);
            if (!result.Succeeded)
            {
                FrameworkLog.Warning(
                    $"Route event processing failed. CaravanId: {progress.caravanId}, TradeId: {progress.activeTradeId}, RouteId: {progress.activeRouteId}, Stage: Process, Reason: {result.FailureReason}");
                return false;
            }

            RecordRouteEventLogs(saveData, progress, route, result);
            for (var index = 0; index < result.Occurrences.Count; index++)
            {
                var occurrence = result.Occurrences[index];
                deferredNotifications.Add(new RouteEventNotification(
                    progress.caravanId,
                    progress.activeTradeId,
                    progress.activeRouteId,
                    occurrence.EventId,
                    occurrence.CheckIndex,
                    isOfflineRestore,
                    occurrence.IsFatal));
            }
            return result.Changed;
        }

        private static List<CaravanActivityLogEntrySaveData> RecordRouteEventLogs(
            SaveData saveData,
            TradeProgressSaveData progress,
            SharedRouteDefinition route,
            RouteEventProcessResult processResult)
        {
            var addedEntries = new List<CaravanActivityLogEntrySaveData>();
            if (saveData == null || progress == null || route?.Events == null
                || processResult?.Occurrences == null)
            {
                return addedEntries;
            }

            for (var occurrenceIndex = 0;
                 occurrenceIndex < processResult.Occurrences.Count;
                 occurrenceIndex++)
            {
                var occurrence = processResult.Occurrences[occurrenceIndex];
                if (occurrence == null)
                {
                    FrameworkLog.Warning(
                        $"Route event activity log skipped because the occurrence is missing. CaravanId: {progress.caravanId}, TradeId: {progress.activeTradeId}, RouteId: {progress.activeRouteId}");
                    continue;
                }
                SharedRouteEventDefinition definition = null;
                for (var definitionIndex = 0; definitionIndex < route.Events.Length; definitionIndex++)
                {
                    var candidate = route.Events[definitionIndex];
                    if (candidate != null
                        && string.Equals(candidate.Id, occurrence.EventId, StringComparison.Ordinal))
                    {
                        definition = candidate;
                        break;
                    }
                }

                if (definition == null)
                {
                    FrameworkLog.Warning(
                        $"Route event activity log skipped because its definition is missing. CaravanId: {progress.caravanId}, TradeId: {progress.activeTradeId}, RouteId: {progress.activeRouteId}, EventId: {occurrence.EventId}");
                    continue;
                }
                if (definition.EventType != RouteEvent.Combat)
                {
                    continue;
                }

                var entry = CaravanActivityLog.Add(
                    saveData,
                    CaravanActivityLogType.CombatEncounter,
                    progress.caravanId,
                    progress.activeTradeId,
                    progress.activeRouteId,
                    routeEventId: occurrence.EventId);
                if (entry != null)
                {
                    addedEntries.Add(entry);
                }

                var outcomeEntry = CaravanActivityLog.Add(
                    saveData,
                    occurrence.CombatVictory == true
                        ? CaravanActivityLogType.CombatVictory
                        : CaravanActivityLogType.CombatDefeat,
                    progress.caravanId,
                    progress.activeTradeId,
                    progress.activeRouteId,
                    routeEventId: occurrence.EventId);
                if (outcomeEntry != null)
                {
                    addedEntries.Add(outcomeEntry);
                }
            }

            return addedEntries;
        }

        private static bool RouteContainsEvent(SharedRouteDefinition route, string eventId)
        {
            if (route?.Events == null) return false;
            for (var index = 0; index < route.Events.Length; index++)
            {
                var candidate = route.Events[index];
                if (candidate != null
                    && string.Equals(candidate.Id, eventId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string FormatCaravanIdForLog(string caravanId)
        {
            return string.IsNullOrWhiteSpace(caravanId) ? "<empty>" : caravanId;
        }

        private static string FormatTradeIdForLog(string tradeId)
        {
            return string.IsNullOrWhiteSpace(tradeId) ? "<empty>" : tradeId;
        }

        private static bool HasValidOfflineTimeRange(TradeProgressSaveData progress)
        {
            if (progress == null || progress.tradeStartUtcTick <= 0
                || progress.expectedTradeEndUtcTick <= progress.tradeStartUtcTick)
                return false;
            try
            {
                var startUtc = new DateTime(progress.tradeStartUtcTick, DateTimeKind.Utc);
                var endUtc = new DateTime(progress.expectedTradeEndUtcTick, DateTimeKind.Utc);
                var seconds = (endUtc - startUtc).TotalSeconds;
                return seconds > 0d && !double.IsNaN(seconds) && !double.IsInfinity(seconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
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
            var caravan = GetRuntimeForProgress(saveData);
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
            if (!economySettlementBridge.TryApplyPendingEconomy(
                    saveData, caravan, saveData.tradeProgress.caravanId, activeTradeId))
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

            // Successful arrivals already own the destination while Selling. Re-applying the
            // resolved town here keeps legacy pending settlements compatible and leaves failed
            // trades at their origin; this is no longer the normal location-change moment.
            caravan.currentTownId = destinationTownId;

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
            if (!CopyRuntimeToOwnedSave(saveData, caravan)) return false;
            var saveResult = saveService != null ? saveService.Save(saveData) : null;
            if (saveResult == null || !saveResult.Succeeded)
            {
                RestoreClaimSnapshot(saveData, caravan, saveDataSnapshot, runtimeCaravanSnapshot);
                FrameworkLog.Warning("Settlement claim rolled back because save did not succeed.");
                return false;
            }

            economySettlementBridge.ClearPending(saveData.tradeProgress.caravanId, activeTradeId);
            ClearSettlementCache();
            FrameworkEvents.RaiseTradingCurrencyChanged(saveData.player.tradingCurrency);
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

            var caravan = GetOrCreateRuntimeCaravan(caravanId);
            if (caravan == null)
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.SettlementDataInvalid);

            string settlementTownId;
            if (settlementResult.grade == JourneyResultGrade.Failed)
            {
                settlementTownId = caravanSave.currentTownId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(settlementTownId))
                    return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.TownApplyFailed);
            }
            else if (!TryResolveClaimDestination(
                         saveData,
                         caravanId,
                         tradeId,
                         progress,
                         out settlementTownId))
            {
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.TownApplyFailed);
            }

            // The destination market commits arrival sales directly to SaveData after the
            // journey runtime has entered Settling. Reconcile those market-owned fields before
            // Claim copies the runtime Caravan back, otherwise sold cargo can be restored.
            CaravanSaveDataMapper.CopyMarketInventoryToRuntime(caravanSave, caravan);

            var saveDataSnapshot = JsonUtility.ToJson(saveData);
            var runtimeCaravanSnapshot = JsonUtility.ToJson(caravan);
            // Runtime 매핑 데이터에 개체 ID가 누락될 수 있으므로, 손실 대상은 Claim이
            // SaveData를 변경하기 전에 authoritative 저장 구성에서 확정해 둔다.
            var failedWagonInstanceId = caravanSave.wagon?.instanceId ?? string.Empty;
            var failedAnimalInstanceIds = new List<string>();
            if (caravanSave.animals != null)
            {
                for (var index = 0; index < caravanSave.animals.Count; index++)
                {
                    var instanceId = caravanSave.animals[index]?.instanceId;
                    if (!string.IsNullOrWhiteSpace(instanceId))
                    {
                        failedAnimalInstanceIds.Add(instanceId);
                    }
                }
            }
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
                || !economySettlementBridge.TryApplyPendingEconomy(
                    saveData, caravan, caravanId, tradeId))
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

            // A failed trade consumes the complete transport composition. This remains inside
            // the Claim snapshot transaction so a failed persistence attempt restores both the
            // owned inventories and the runtime Caravan without leaving a partial loss behind.
            if (settlementResult.grade == JourneyResultGrade.Failed)
            {
                FailedTradeTransportLoss.Apply(
                    saveData,
                    caravan,
                    failedWagonInstanceId,
                    failedAnimalInstanceIds);
            }

            // Successful Selling already persisted this destination. Claim re-applies it for
            // legacy pending data; a failed journey instead retains its saved origin.
            caravan.currentTownId = settlementTownId;
            if (exactTradePrepareCommitStore == null
                || !exactTradePrepareCommitStore.TryComplete(caravanId, tradeId, out _))
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

            // Lucky state belongs to the completed trade and is consumed only after the
            // authoritative framework save succeeds. The store's persistence is best-effort.
            WeatherLuckyStore.Consume(tradeId);
            economySettlementBridge.ClearPending(caravanId, tradeId);
            if (LastSettlementTradeId == tradeId) ClearSettlementCache();
            FrameworkEvents.RaiseTradingCurrencyChanged(saveData.player.tradingCurrency);
            inGameScreenRouter?.RequestScreen(InGameScreenState.Town);
            onTownVisited?.Invoke(settlementTownId);
            return ClaimSettlementResult.Success(saveResult);
        }

        /// <summary>
        /// 판매 transaction과 같은 저장 단위에서 해당 Caravan을
        /// Selling에서 Settling으로 전환할 준비를 한다.
        /// 이 메서드는 상태만 stage하며 직접 저장하거나 이벤트를 발행하지 않는다.
        /// </summary>
        public bool TryStageArrivalSaleCompletion(
            string caravanId,
            string tradeId,
            out ArrivalSaleTransitionSnapshot snapshot)
        {
            snapshot = null;

            if (string.IsNullOrWhiteSpace(caravanId)
                || string.IsNullOrWhiteSpace(tradeId))
            {
                return false;
            }

            SaveData saveData = GetSaveData();

            if (!SaveDataLookup.TryGetCaravan(
                    saveData,
                    caravanId,
                    out CaravanSaveData caravan)
                || !SaveDataLookup.TryGetTradeProgress(
                    saveData,
                    caravanId,
                    out TradeProgressSaveData progress)
                || !SaveDataLookup.TryGetPendingSettlement(
                    saveData,
                    caravanId,
                    tradeId,
                    out PendingSettlementSaveData pending)
                || caravan == null
                || progress == null
                || pending == null
                || !pending.hasResult
                || pending.grade == JourneyResultGrade.Failed
                || !string.Equals(
                    caravan.caravanId,
                    caravanId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    progress.caravanId,
                    caravanId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    progress.activeTradeId,
                    tradeId,
                    StringComparison.Ordinal)
                || caravan.state != JourneyState.Selling
                || progress.state != TradeProgressState.Selling)
            {
                return false;
            }

            CaravanData runtimeCaravan = GetOrCreateRuntimeCaravan(caravanId);
            if (runtimeCaravan == null || !string.Equals(runtimeCaravan.caravanId, caravanId, StringComparison.Ordinal) || runtimeCaravan.state != JourneyState.Selling)
            {
                return false;
            }

            snapshot = new ArrivalSaleTransitionSnapshot
            {
                SaveData = saveData,
                SaveVersionBefore = saveData.version,
                LastSavedUtcTicksBefore = saveData.lastSavedUtcTicks,
                Caravan = caravan,
                Progress = progress,
                RuntimeCaravan = runtimeCaravan,
                RuntimeStateBefore = runtimeCaravan.state,
                CaravanStateBefore = caravan.state,
                ProgressStateBefore = progress.state,
                CaravanId = caravanId,
                TradeId = tradeId
            };

            caravan.state = JourneyState.Settling;
            progress.state = TradeProgressState.SettlementPending;
            runtimeCaravan.state = JourneyState.Settling;

            return true;
        }

        public bool TryCommitEmptyArrivalSaleCompletion(string caravanId, string tradeId, out bool saveFailed)
        {
            saveFailed = false;

            SaveData saveData = GetSaveData();
            if (saveData == null)
            {
                return false;
            }

            string saveDataSnapshot = JsonUtility.ToJson(saveData);

            if (!TryStageArrivalSaleCompletion(
                    caravanId,
                    tradeId,
                    out ArrivalSaleTransitionSnapshot snapshot))
            {
                return false;
            }

            SaveResult saveResult = saveService != null
                ? saveService.Save(saveData)
                : null;

            if (saveResult == null || !saveResult.Succeeded)
            {
                RollbackArrivalSaleCompletion(snapshot);
                JsonUtility.FromJsonOverwrite(
                    saveDataSnapshot,
                    saveData);

                saveFailed = true;
                return false;
            }

            PublishArrivalSaleCompletion(snapshot);
            return true;
        }

        /// <summary>
        /// 판매 transaction 저장 실패 시 stage 전 상태로 복구한다.
        /// </summary>
        public void RollbackArrivalSaleCompletion(
            ArrivalSaleTransitionSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (snapshot.Caravan != null)
            {
                snapshot.Caravan.state =
                    snapshot.CaravanStateBefore;
            }

            if (snapshot.Progress != null)
            {
                snapshot.Progress.state =
                    snapshot.ProgressStateBefore;
            }

            if (snapshot.RuntimeCaravan != null)
            {
                snapshot.RuntimeCaravan.state = snapshot.RuntimeStateBefore;
            }

            if (snapshot.SaveData != null)
            {
                snapshot.SaveData.version =
                    snapshot.SaveVersionBefore;
                snapshot.SaveData.lastSavedUtcTicks =
                    snapshot.LastSavedUtcTicksBefore;
            }
        }

        /// <summary>
        /// 판매 transaction 저장 성공 후 Settling 상태 변경을 알린다.
        /// </summary>
        public void PublishArrivalSaleCompletion(
            ArrivalSaleTransitionSnapshot snapshot)
        {
            if (snapshot == null
                || string.IsNullOrWhiteSpace(snapshot.CaravanId)
                || string.IsNullOrWhiteSpace(snapshot.TradeId))
            {
                return;
            }

            FrameworkLog.Info(
                $"Arrival sale completion saved. " +
                $"CaravanId: {snapshot.CaravanId}, " +
                $"TradeId: {snapshot.TradeId}");

            FrameworkEvents.RaiseCaravanJourneyStateChanged(
                snapshot.CaravanId,
                JourneyState.Settling);
        }

        /// <summary>모든 durable pending settlement를 저장 데이터와 분리된 읽기 전용 목록으로 반환한다.</summary>
        public IReadOnlyList<PendingSettlementSaveData> GetPendingSettlements()
        {
            var copies = new List<PendingSettlementSaveData>();
            var entries = GetSaveData()?.pendingSettlements;
            if (entries != null)
            {
                for (var i = 0; i < entries.Count; i++)
                {
                    var copy = PendingSettlementSaveDataMapper.Copy(entries[i]);
                    if (copy != null) copies.Add(copy);
                }
            }
            return copies.AsReadOnly();
        }

        /// <summary>정확한 Caravan과 Trade 복합 ID로 durable pending settlement 복사본을 조회한다.</summary>
        public bool TryGetPendingSettlement(
            string caravanId,
            string tradeId,
            out PendingSettlementSaveData pending)
        {
            pending = null;
            if (string.IsNullOrWhiteSpace(caravanId) || string.IsNullOrWhiteSpace(tradeId)
                || !SaveDataLookup.TryGetPendingSettlement(
                    GetSaveData(), caravanId, tradeId, out var authoritative))
            {
                return false;
            }

            pending = PendingSettlementSaveDataMapper.Copy(authoritative);
            return pending != null;
        }

        /// <summary>정확한 durable pending 결과를 Core 상태 변경 없이 runtime 결과로 재구성한다.</summary>
        public bool TryGetPendingSettlementResult(
            string caravanId,
            string tradeId,
            out JourneyResultData result)
        {
            result = null;
            return !string.IsNullOrWhiteSpace(caravanId)
                   && !string.IsNullOrWhiteSpace(tradeId)
                   && SaveDataLookup.TryGetPendingSettlement(
                       GetSaveData(), caravanId, tradeId, out var pending)
                   && PendingSettlementSaveDataMapper.TryToRuntime(pending, out result);
        }

        /// <summary>
        /// Preserves the settlement UI compatibility query while the authoritative durable
        /// lookup remains keyed by the exact Caravan and trade identity.
        /// </summary>
        public bool TryGetPendingEconomyResult(
            string tradeId,
            out ND.Economy.EconomyM1LoopResult result)
        {
            return economySettlementBridge.TryGetPendingResult(tradeId, out result);
        }

        public bool TryGetPendingEconomyResult(
            string caravanId,
            string tradeId,
            out ND.Economy.EconomyM1LoopResult result)
        {
            return economySettlementBridge.TryGetPendingResult(caravanId, tradeId, out result);
        }

        private bool TryResolveClaimDestination(
            SaveData saveData,
            string caravanId,
            string tradeId,
            TradeProgressSaveData progress,
            out string destinationTownId)
        {
            destinationTownId = string.Empty;
            if (saveData.player == null) return false;

            if (progress == null
                || !string.Equals(progress.caravanId, caravanId, StringComparison.Ordinal)
                || !string.Equals(progress.activeTradeId, tradeId, StringComparison.Ordinal))
                return false;

            if (exactTradePrepareCommitStore == null
                || !exactTradePrepareCommitStore.TryGet(caravanId, tradeId, out var commit)
                || commit == null
                || !string.Equals(commit.caravanId, caravanId, StringComparison.Ordinal)
                || !string.Equals(commit.tradeId, tradeId, StringComparison.Ordinal))
            {
                FrameworkLog.Warning(
                    $"Settlement claim destination lookup failed. CaravanId: {caravanId}, TradeId: {tradeId}");
                return false;
            }

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
        /// <summary>
        /// 모든 durable pending settlement를 독립 검증하고 runtime Caravan 및 선택된 호환 cache를 복구한다.
        /// 복구는 저장 데이터, Economy 또는 완료 이벤트를 변경하지 않는다.
        /// </summary>
        public bool RestorePendingSettlements(SaveData saveData = null)
        {
            saveData = saveData ?? GetSaveData();
            if (saveData?.pendingSettlements == null) return false;

            var restoredAny = false;
            JourneyResultData selectedResult = null;
            var selectedTradeId = string.Empty;
            var entries = new List<PendingSettlementSaveData>(saveData.pendingSettlements);
            for (var i = 0; i < entries.Count; i++)
            {
                var pending = entries[i];
                var caravanId = pending?.caravanId ?? string.Empty;
                var tradeId = pending?.tradeId ?? string.Empty;
                if (string.IsNullOrWhiteSpace(caravanId) || string.IsNullOrWhiteSpace(tradeId)
                    || !SaveDataLookup.TryGetCaravan(saveData, caravanId, out _)
                    || !SaveDataLookup.TryGetTradeProgress(saveData, caravanId, out var progress)
                    || progress.state != TradeProgressState.SettlementPending
                    || !string.Equals(progress.activeTradeId, tradeId, StringComparison.Ordinal)
                    || exactTradePrepareCommitStore == null
                    || !exactTradePrepareCommitStore.TryGet(caravanId, tradeId, out var commit)
                    || commit == null
                    || !string.Equals(commit.caravanId, caravanId, StringComparison.Ordinal)
                    || !string.Equals(commit.tradeId, tradeId, StringComparison.Ordinal)
                    || !PendingSettlementSaveDataMapper.TryToRuntime(pending, out var result))
                {
                    FrameworkLog.Warning(
                        $"PendingValidation failed. CaravanId: {FormatCaravanIdForLog(caravanId)}, TradeId: {FormatTradeIdForLog(tradeId)}, Reason: durable owner, progress, commit, or result mismatch.");
                    continue;
                }

                var caravan = GetOrCreateRuntimeCaravan(caravanId);
                if (caravan == null || caravan.state != JourneyState.Settling || caravan.settlementClaimed)
                {
                    FrameworkLog.Warning(
                        $"PendingRestore failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: runtime Caravan is unavailable or not claimable.");
                    continue;
                }

                restoredAny = true;
                if (string.Equals(saveData.selectedCaravanId, caravanId, StringComparison.Ordinal))
                {
                    selectedTradeId = tradeId;
                    selectedResult = result;
                }
                FrameworkLog.Info($"PendingRestore succeeded. CaravanId: {caravanId}, TradeId: {tradeId}");
            }

            LastSettlementTradeId = selectedTradeId;
            LastSettlementResult = selectedResult;
            return restoredAny;
        }

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

            var caravan = GetRuntimeForProgress(saveData);
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
            if (LastSettlementResult.grade == JourneyResultGrade.Failed)
            {
                // A failed journey never reaches a destination market, so it has no arrival
                // sale step and proceeds directly to failure settlement presentation.
                inGameScreenRouter?.RequestScreen(InGameScreenState.Settlement);
            }
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
            var caravan = GetRuntimeForProgress(saveData);
            if (caravan == null)
            {
                FrameworkLog.Warning("Immediate trade completion skipped because active caravan is missing.");
                return;
            }

            // 정산 시 foodConsumed 계산이 올바르도록 도착 처리 전 인게임 경과를 동기화한다.
            SyncElapsedInGameSeconds(saveData, caravan, gameTimeProvider.CurrentUtc);

            // Core progress를 도착값으로 맞춘 뒤 동일한 settlement 생성 경로를 재사용한다.
            JourneyRunner.SetProgress(caravan, JourneyRunner.ArrivalProgress);
            if (!CopyRuntimeToOwnedSave(saveData, caravan)) return;
            SettleActiveTrade(saveData, caravan);
        }

        private OfflineRestoreContext ResolveOfflineRestoreContext(SaveData saveData, DateTime loadUtc)
        {
            var lastSavedUtcTicks = saveData != null ? saveData.lastSavedUtcTicks : 0L;
            if (gameTimeProvider is GameTimeService gameTimeService)
            {
                return gameTimeService.ResolveOfflineRestoreContext(lastSavedUtcTicks, loadUtc);
            }

            var conversionPolicy = new InGameTimeConversionPolicy();
            var maxOffline = InGameTimePolicyConfig.DefaultMaxOfflineRealSeconds;
            return conversionPolicy.ResolveOfflineRestoreContext(
                lastSavedUtcTicks,
                loadUtc,
                maxOffline);
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

            // 저장 실패 시 SaveData와 runtime Caravan을 모두 원래 상태로 복구하기 위한 snapshot이다.
            string saveDataSnapshot = JsonUtility.ToJson(saveData);
            string runtimeCaravanSnapshot = JsonUtility.ToJson(caravan);
            string previousSettlementTradeId = LastSettlementTradeId;
            JourneyResultData previousSettlementResult = LastSettlementResult;

            // Core settlement 결과가 없으면 UI에 표시하거나 claim할 데이터가 없으므로 중단한다.
            var result = JourneyRunner.Settle(caravan);
            if (result == null)
            {
                FrameworkLog.Warning(
                    "Trade settlement was not created because Core returned no result.");
                return false;
            }

            // 저장 데이터를 settlement pending으로 전환한 뒤 실제 상태가 바뀌었는지 검증한다.
            TradeProgressState expectedProgressState;

            if (result.grade == JourneyResultGrade.Failed)
            {
                tradeProgressRecorder.MarkSettlementPending(saveData);
                expectedProgressState = TradeProgressState.SettlementPending;
            }
            else
            {
                tradeProgressRecorder.MarkSelling(saveData.tradeProgress);
                expectedProgressState = TradeProgressState.Selling;
            }

            if (saveData.tradeProgress.state != expectedProgressState)
            {
                TradeProgressState actualProgressState =
                    saveData.tradeProgress.state;

                JsonUtility.FromJsonOverwrite(saveDataSnapshot, saveData);

                JsonUtility.FromJsonOverwrite(runtimeCaravanSnapshot, caravan);

                LastSettlementTradeId = previousSettlementTradeId;
                LastSettlementResult = previousSettlementResult;

                FrameworkLog.Warning(
                    $"Trade settlement was rolled back because trade state is " +
                    $"{actualProgressState}. Expected: {expectedProgressState}.");

                return false;
            }

            // 생성된 결과를 runtime cache와 저장 데이터에 반영하고 UI 계층에 settlement 준비를 알린다.
            var settlementTradeId = saveData.tradeProgress.activeTradeId ?? string.Empty;
            var settlementRouteId = saveData.tradeProgress.activeRouteId ?? string.Empty;
            LastSettlementTradeId = settlementTradeId;
            LastSettlementResult = result;

            // A successful arrival owns the Caravan location from the moment Selling begins.
            // Keep this inside the same snapshot/save boundary as the state transition so a
            // failed save restores both JourneyState and currentTownId together.
            if (result.grade != JourneyResultGrade.Failed)
            {
                string destinationTownId = ResolveArrivalDestinationTownId(
                    saveData.tradeProgress.caravanId,
                    settlementTradeId,
                    settlementRouteId);
                if (string.IsNullOrWhiteSpace(destinationTownId))
                {
                    JsonUtility.FromJsonOverwrite(saveDataSnapshot, saveData);
                    JsonUtility.FromJsonOverwrite(runtimeCaravanSnapshot, caravan);
                    LastSettlementTradeId = previousSettlementTradeId;
                    LastSettlementResult = previousSettlementResult;
                    FrameworkLog.Warning("Trade arrival was rolled back because the destination town could not be resolved.");
                    return false;
                }

                caravan.currentTownId = destinationTownId;
            }

            var sharedGameData = getSharedGameData != null ? getSharedGameData() : null;
            if (sharedGameData == null || !sharedGameData.IsLoaded)
            {
                FrameworkLog.Warning("Economy M1 settlement preview skipped because shared game data is not loaded.");
            }
            else if (!economySettlementBridge.TryCalculateAndFill(saveData, caravan, result, sharedGameData))
            {
                FrameworkLog.Warning("Economy M1 settlement preview failed. Core settlement grade is still available.");
            }

            // 도착 상태와 확정 정산 결과를 같은 저장 단위에 기록한다.
            // 정상 도착은 Selling, 실패는 SettlementPending 상태다.
            saveData.pendingSettlement = PendingSettlementSaveDataMapper.ToSave(result, settlementTradeId, settlementRouteId);

            if (!CopyRuntimeToOwnedSave(saveData, caravan))
            {
                JsonUtility.FromJsonOverwrite(saveDataSnapshot, saveData);

                JsonUtility.FromJsonOverwrite(runtimeCaravanSnapshot, caravan);

                LastSettlementTradeId = previousSettlementTradeId;
                LastSettlementResult = previousSettlementResult;

                economySettlementBridge?.ClearPending(saveData.tradeProgress.caravanId, settlementTradeId);

                FrameworkLog.Warning(
                    $"Trade settlement was rolled back because the runtime Caravan " +
                    $"could not be copied to SaveData. " +
                    $"CaravanId: {saveData.tradeProgress.caravanId}, " +
                    $"TradeId: {settlementTradeId}");

                return false;
            }

            SaveResult saveResult = saveService != null
                ? saveService.Save(saveData)
                : null;

            if (saveResult == null || !saveResult.Succeeded)
            {
                JsonUtility.FromJsonOverwrite(
                    saveDataSnapshot,
                    saveData);

                JsonUtility.FromJsonOverwrite(
                    runtimeCaravanSnapshot,
                    caravan);

                LastSettlementTradeId = previousSettlementTradeId;
                LastSettlementResult = previousSettlementResult;

                economySettlementBridge?.ClearPending(
                    saveData.tradeProgress.caravanId,
                    settlementTradeId);

                FrameworkLog.Warning(
                    $"Trade settlement was rolled back because save failed. " +
                    $"CaravanId: {saveData.tradeProgress.caravanId}, " +
                    $"TradeId: {settlementTradeId}");

                return false;
            }

            // 저장 성공 이후에만 Journey 상태 변경을 알린다.
            FrameworkEvents.RaiseCaravanJourneyStateChanged(
                saveData.tradeProgress.caravanId,
                caravan.state);

            FrameworkEvents.RaiseTradeSettlementReady(
                saveData.tradeProgress.caravanId,
                settlementTradeId,
                result);

            if (result.grade == JourneyResultGrade.Failed)
            {
                // 정상 도착은 판매 UI를 기다린다.
                // 실패만 기존 정산 화면으로 이동한다.
                inGameScreenRouter?.RequestScreen(InGameScreenState.Settlement);
            }

            return true;
        }

        private bool SettleTrade(
            SaveData saveData,
            TradeProgressSaveData progress,
            CaravanSaveData caravanSave,
            CaravanData runtimeCaravan,
            string caravanId,
            string tradeId,
            List<SettlementNotification> deferredEvents)
        {
            if (saveData == null || progress == null || caravanSave == null || runtimeCaravan == null
                || progress.state != TradeProgressState.Traveling
                || string.IsNullOrWhiteSpace(caravanId) || string.IsNullOrWhiteSpace(tradeId)
                || !string.Equals(progress.caravanId, caravanId, StringComparison.Ordinal)
                || !string.Equals(progress.activeTradeId, tradeId, StringComparison.Ordinal)
                || !string.Equals(caravanSave.caravanId, caravanId, StringComparison.Ordinal)
                || !string.Equals(runtimeCaravan.caravanId, caravanId, StringComparison.Ordinal))
            {
                FrameworkLog.Warning(
                    $"Trade settlement blocked because explicit targets do not match. CaravanId: {caravanId}, TradeId: {tradeId}");
                return false;
            }

            if (SaveDataLookup.TryGetPendingSettlement(saveData, caravanId, tradeId, out _))
            {
                FrameworkLog.Warning(
                    $"Duplicate pending settlement blocked. CaravanId: {caravanId}, TradeId: {tradeId}");
                return false;
            }
            if (saveData.pendingSettlements == null)
            {
                FrameworkLog.Warning("Trade settlement blocked because the canonical pending collection is missing.");
                return false;
            }
            if (tradeProgressRecorder == null)
            {
                FrameworkLog.Warning("Trade settlement was not created because trade progress recorder is missing.");
                return false;
            }

            // Resolve a successful arrival before Core mutates Journey state. Failed journeys
            // intentionally retain their origin and must still settle even if route data was lost.
            var arrivalTownId = runtimeCaravan.runFatalReason == JourneyFailureReason.None
                ? ResolveArrivalDestinationTownId(caravanId, tradeId, progress.activeRouteId)
                : string.Empty;
            if (runtimeCaravan.runFatalReason == JourneyFailureReason.None
                && string.IsNullOrWhiteSpace(arrivalTownId))
            {
                FrameworkLog.Warning(
                    $"Trade arrival blocked because its destination town could not be resolved. CaravanId: {caravanId}, TradeId: {tradeId}");
                return false;
            }

            var result = JourneyRunner.Settle(runtimeCaravan);
            if (result == null)
            {
                FrameworkLog.Warning("Trade settlement was not created because Core returned no result.");
                return false;
            }

            TradeProgressState expectedProgressState;

            if (result.grade == JourneyResultGrade.Failed)
            {
                tradeProgressRecorder.MarkSettlementPending(progress);
                expectedProgressState = TradeProgressState.SettlementPending;
            }
            else
            {
                tradeProgressRecorder.MarkSelling(progress);
                expectedProgressState = TradeProgressState.Selling;
            }

            if (progress.state != expectedProgressState)
            {
                FrameworkLog.Warning(
                    $"Trade settlement was not published because trade state is " +
                    $"{progress.state}. Expected: {expectedProgressState}, " +
                    $"CaravanId: {caravanId}, TradeId: {tradeId}");

                return false;
            }

            if (result.grade != JourneyResultGrade.Failed)
            {
                // Selling is already a destination-town state. Persist the new location with
                // the arrival settlement rather than delaying it until S9 Claim.
                runtimeCaravan.currentTownId = arrivalTownId;
            }

            var sharedGameData = getSharedGameData != null ? getSharedGameData() : null;
            if (sharedGameData == null || !sharedGameData.IsLoaded)
            {
                FrameworkLog.Warning("Economy M1 settlement preview skipped because shared game data is not loaded.");
            }
            else if (!economySettlementBridge.TryCalculateAndFill(
                         saveData, progress, runtimeCaravan, result, sharedGameData))
            {
                FrameworkLog.Warning("Economy M1 settlement preview failed. Core settlement grade is still available.");
            }

            var pending = PendingSettlementSaveDataMapper.ToSave(
                result, tradeId, progress.activeRouteId ?? string.Empty);
            pending.caravanId = caravanId;
            if (exactTradePrepareCommitStore != null
                && exactTradePrepareCommitStore.TryGet(caravanId, tradeId, out var preparation))
            {
                PendingSettlementSaveDataMapper.ApplyPreparation(pending, preparation);
            }
            else
            {
                FrameworkLog.Warning(
                    $"Settlement receipt was created without a preparation commit. CaravanId: {caravanId}, TradeId: {tradeId}");
            }
            saveData.pendingSettlements.Add(pending);
            CaravanSaveDataMapper.CopyToSave(runtimeCaravan, caravanSave);
            if (result.grade != JourneyResultGrade.Failed)
            {
                CaravanActivityLog.Add(
                    saveData,
                    CaravanActivityLogType.Arrival,
                    progress.caravanId,
                    tradeId,
                    progress.activeRouteId,
                    arrivalTownId);
            }

            LastSettlementTradeId = tradeId;
            LastSettlementResult = result;
            deferredEvents.Add(new SettlementNotification(caravanId, tradeId, result));
            return true;
        }

        private string ResolveArrivalDestinationTownId(
            string caravanId,
            string tradeId,
            string routeId)
        {
            if (exactTradePrepareCommitStore != null
                && exactTradePrepareCommitStore.TryGet(caravanId, tradeId, out var commit)
                && commit != null
                && string.Equals(commit.caravanId, caravanId, StringComparison.Ordinal)
                && string.Equals(commit.tradeId, tradeId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(commit.selectedDestinationTownId))
            {
                return commit.selectedDestinationTownId;
            }

            var sharedGameData = getSharedGameData != null ? getSharedGameData() : null;
            return sharedGameData != null
                && sharedGameData.TryGetRoute(routeId, out var route)
                && route != null
                ? route.ToTownId ?? string.Empty
                : string.Empty;
        }

        private void PublishSettlementNotifications(
            SaveData saveData,
            List<SettlementNotification> notifications,
            bool isOfflineRestore)
        {
            for (var index = 0; index < notifications.Count; index++)
            {
                var notification = notifications[index];
                if (SaveDataLookup.TryGetCaravan(saveData, notification.CaravanId, out CaravanSaveData savedCaravan))
                {
                    FrameworkEvents.RaiseCaravanJourneyStateChanged(
                        notification.CaravanId,
                        savedCaravan.state);
                }
                FrameworkEvents.RaiseTradeSettlementReady(
                    notification.CaravanId, notification.TradeId, notification.Result);
                if (isOfflineRestore)
                    FrameworkEvents.RaiseTradeOfflineCompleted(notification.TradeId);
                if (notification.Result.grade == JourneyResultGrade.Failed
                    && notification.CaravanId == saveData.selectedCaravanId)
                {
                    inGameScreenRouter?.RequestScreen(InGameScreenState.Settlement);
                }
            }
        }

        private static void PublishRouteEventNotifications(List<RouteEventNotification> notifications)
        {
            for (var index = 0; index < notifications.Count; index++)
            {
                var notification = notifications[index];
                FrameworkLog.Info(
                    $"Route event occurred after save. CaravanId: {notification.CaravanId}, TradeId: {notification.TradeId}, RouteId: {notification.RouteId}, EventId: {notification.EventId}, CheckIndex: {notification.CheckIndex}, Forced: False, Offline: {notification.IsOffline}, Fatal: {notification.IsFatal}");
            }
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
            // 선택된 caravan runtime이 이미 등록되어 있으면 동일한 공유 객체를 반환한다.
            if (TryGetRuntimeCaravan(GetSaveData()?.selectedCaravanId, out var selectedCaravan))
            {
                return selectedCaravan;
            }

            // 선택된 runtime이 없을 때는 동일 ID 저장 snapshot으로 생성해 registry에 등록한다.
            var saveData = GetSaveData();
            if (saveData == null || saveData.caravan == null)
            {
                return null;
            }

            return GetOrCreateRuntimeCaravan(saveData.selectedCaravanId);
        }

        private CaravanData GetRuntimeForProgress(SaveData saveData)
        {
            var caravanId = saveData?.tradeProgress?.caravanId;
            var caravan = GetOrCreateRuntimeCaravan(caravanId);
            if (caravan == null)
            {
                FrameworkLog.Warning($"Runtime caravan lookup failed. CaravanId: {caravanId}");
            }
            return caravan;
        }

        private static bool CopyRuntimeToOwnedSave(SaveData saveData, CaravanData caravan)
        {
            if (caravan == null
                || !SaveDataLookup.TryGetCaravan(saveData, caravan.caravanId, out var caravanSave))
            {
                FrameworkLog.Warning(
                    $"Runtime caravan save target lookup failed. CaravanId: {caravan?.caravanId}");
                return false;
            }
            CaravanSaveDataMapper.CopyToSave(caravan, caravanSave);
            return true;
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
        /// 명시된 progress 기준 인게임 경과 초를 해당 runtime 및 save Caravan에만 기록한다.
        /// </summary>
        /// <remarks>
        /// JourneyRunner.SetProgress 이전에 호출해야 Core 식량 소모 계산이 올바른 elapsed를 사용한다.
        /// evaluationUtc를 넘겨 온라인(CurrentUtc)과 오프라인(상한 clamp) 경로를 공유한다.
        /// </remarks>
        private void SyncElapsedInGameSeconds(SaveData saveData, CaravanData caravan, DateTime evaluationUtc)
        {
            if (saveData?.tradeProgress == null
                || !SaveDataLookup.TryGetCaravan(saveData, saveData.tradeProgress.caravanId, out var caravanSave))
                return;
            SyncElapsedInGameSeconds(saveData.tradeProgress, caravanSave, caravan, evaluationUtc);
        }

        private bool SyncElapsedInGameSeconds(
            TradeProgressSaveData progress,
            CaravanSaveData caravanSave,
            CaravanData runtimeCaravan,
            DateTime evaluationUtc)
        {
            if (progress == null || caravanSave == null || runtimeCaravan == null
                || inGameTimeProvider == null
                || !string.Equals(progress.caravanId, caravanSave.caravanId, StringComparison.Ordinal)
                || !string.Equals(progress.caravanId, runtimeCaravan.caravanId, StringComparison.Ordinal))
                return false;

            var elapsed = (float)inGameTimeProvider.GetElapsedInGameSecondsForActiveTrade(
                progress, evaluationUtc);
            var changed = !Mathf.Approximately(runtimeCaravan.elapsedInGameSeconds, elapsed)
                || !Mathf.Approximately(caravanSave.elapsedInGameSeconds, elapsed);
            runtimeCaravan.elapsedInGameSeconds = elapsed;
            caravanSave.elapsedInGameSeconds = elapsed;
            return changed;
        }

        internal readonly struct SettlementNotification
        {
            public SettlementNotification(string caravanId, string tradeId, JourneyResultData result)
            {
                CaravanId = caravanId;
                TradeId = tradeId;
                Result = result;
            }

            public string CaravanId { get; }
            public string TradeId { get; }
            public JourneyResultData Result { get; }
        }

        internal readonly struct RouteEventNotification
        {
            public RouteEventNotification(
                string caravanId,
                string tradeId,
                string routeId,
                string eventId,
                int checkIndex,
                bool isOffline,
                bool isFatal)
            {
                CaravanId = caravanId;
                TradeId = tradeId;
                RouteId = routeId;
                EventId = eventId;
                CheckIndex = checkIndex;
                IsOffline = isOffline;
                IsFatal = isFatal;
            }

            public string CaravanId { get; }
            public string TradeId { get; }
            public string RouteId { get; }
            public string EventId { get; }
            public int CheckIndex { get; }
            public bool IsOffline { get; }
            public bool IsFatal { get; }
        }

        internal sealed class TradeOfflineRestoreResult
        {
            private readonly List<SettlementNotification> settlementNotifications;
            private readonly List<RouteEventNotification> routeEventNotifications;

            public TradeOfflineRestoreResult()
                : this(
                    false,
                    false,
                    new List<SettlementNotification>(),
                    new List<RouteEventNotification>(),
                    string.Empty,
                    null)
            {
            }

            internal TradeOfflineRestoreResult(
                bool changed,
                bool settled,
                List<SettlementNotification> settlementNotifications,
                List<RouteEventNotification> routeEventNotifications,
                string previousSettlementTradeId,
                JourneyResultData previousSettlementResult)
            {
                Changed = changed;
                Settled = settled;
                this.settlementNotifications = settlementNotifications;
                this.routeEventNotifications = routeEventNotifications;
                PreviousSettlementTradeId = previousSettlementTradeId;
                PreviousSettlementResult = previousSettlementResult;
            }

            public bool Changed { get; }
            public bool Settled { get; }
            private string PreviousSettlementTradeId { get; }
            private JourneyResultData PreviousSettlementResult { get; }

            public void Publish(
                TradeProgressCoordinator coordinator,
                SaveData saveData)
            {
                coordinator.PublishSettlementNotifications(
                    saveData,
                    settlementNotifications,
                    isOfflineRestore: true);
                PublishRouteEventNotifications(routeEventNotifications);
            }

            public void RollbackRuntime(TradeProgressCoordinator coordinator)
            {
                coordinator.RebuildRuntimeCaravans();
                coordinator.LastSettlementTradeId = PreviousSettlementTradeId;
                coordinator.LastSettlementResult = PreviousSettlementResult;
                coordinator.economySettlementBridge.ClearPending();
            }
        }
    }
}
