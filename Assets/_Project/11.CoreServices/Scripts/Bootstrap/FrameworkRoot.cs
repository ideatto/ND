/*
 * Technical Ownership
 * - Responsible Discipline: Framework & Integration
 *
 * Script Purpose
 * - CoreServices의 runtime root를 생성하고 framework service들을 조립한다.
 * - scene 전환, 저장 데이터, 무역 진행, settlement UI bridge의 접근 지점을 제공한다.
 *
 * Main Features
 * - BeforeSceneLoad 단계에서 FrameworkRoot GameObject를 자동 생성한다.
 * - GameTime, SaveService, SharedGameData, SceneFlow, TradeProgress, Economy M1 bridge, InGameScreenRouter 서비스를 초기화한다.
 * - 새 게임, 이어하기, 로딩 완료, title 복귀 flow를 제공한다.
 * - SettlementUiBridge를 통해 정산 결과를 UI 계층에 전달한다.
 *
 * Usage for Team Members
 * - 다른 CoreServices 시스템은 FrameworkRoot.Instance를 통해 framework service에 접근한다.
 * - gameplay 시작은 StartNewGame() 또는 ContinueGame()을 호출한 뒤 loading scene에서 CompleteLoadingAndEnterGame()으로 이어진다.
 * - settlement UI는 SettlementUiBridge 또는 SettlementUiDataAdapter를 통해 정산 결과를 표시하고 claim을 요청한다.
 *
 * Main Public APIs
 * - Instance: 현재 runtime root singleton.
 * - SharedGameData: 검증된 공용 기준 데이터 provider.
 * - StartNewGame(): 새 저장 데이터를 생성하고 loading scene으로 이동한다.
 * - ContinueGame(): 저장 데이터를 로드하고 loading scene으로 이동한다.
 * - CompleteLoadingAndEnterGame(): SharedGameData 로드 후 오프라인 복구·대기 정산 복구를 하고 in-game scene으로 이동한다.
 * - ReturnToTitle(): 현재 저장 데이터를 저장한 뒤 title scene으로 이동한다.
 *
 * Important Notes
 * - 중복 FrameworkRoot는 Awake에서 제거된다.
 * - CurrentSaveData는 서비스들이 공유하는 runtime 저장 데이터 참조이므로 직접 수정 시 저장 시점에 주의해야 한다.
 * - SettlementUiBridge는 FrameworkRoot GameObject에 runtime component로 추가된다.
 * - Loading 완료 시 ApplyOfflineProgressOnLoad → RestorePendingSettlement 순으로 호출한다.
 * - Online progress tick은 loading 복구와 load event 처리가 끝난 뒤에만 활성화된다.
 * - Related Documentation: Docs/Personal_Documents/CSU/0712_m3-offline-progress-pipeline.md
 */
using System;
using System.Collections.Generic;
using ND.Economy;
using UnityEngine;

namespace ND.Framework
{
    public enum CaravanCreationFailureReason
    {
        None,
        SaveDataUnavailable,
        InvalidSlotIndex,
        SlotAlreadyOccupied,
        /// <summary>영구 진행 데이터에서 아직 해금되지 않은 슬롯이다.</summary>
        SlotLocked,
        SaveFailed
    }

    /// <summary>Caravan 생성과 영속 저장 결과를 제공한다.</summary>
    public sealed class CaravanCreationResult
    {
        private CaravanCreationResult(
            bool succeeded,
            string caravanId,
            int slotIndex,
            CaravanCreationFailureReason failureReason,
            SaveResult saveResult)
        {
            Succeeded = succeeded;
            CaravanId = caravanId ?? string.Empty;
            SlotIndex = slotIndex;
            FailureReason = failureReason;
            SaveResult = saveResult;
        }

        public bool Succeeded { get; }
        public string CaravanId { get; }
        public int SlotIndex { get; }
        public CaravanCreationFailureReason FailureReason { get; }
        public SaveResult SaveResult { get; }

        internal static CaravanCreationResult Success(string caravanId, int slotIndex, SaveResult saveResult)
            => new CaravanCreationResult(true, caravanId, slotIndex, CaravanCreationFailureReason.None, saveResult);

        internal static CaravanCreationResult Failure(
            int slotIndex,
            CaravanCreationFailureReason failureReason,
            SaveResult saveResult = null)
            => new CaravanCreationResult(false, string.Empty, slotIndex, failureReason, saveResult);
    }

    /// <summary>Caravan 생성 검증, 상태 변경, 저장 및 실패 원복을 소유하는 Production command service이다.</summary>
    public sealed class CaravanManagementService
    {
        /// <summary>현재 Caravan Overview가 제공하는 영구 슬롯의 총 개수이다.</summary>
        public const int MaxCaravanSlotCount = 4;

        /// <summary>모든 신규 Caravan이 생성되는 고정 거점 Town ID이다.</summary>
        public const string InitialCaravanTownId = "BaseCamp";

        private readonly Func<SaveData> getSaveData;
        private readonly ISaveService saveService;
        private readonly Func<string, CaravanData> registerRuntimeCaravan;

        public CaravanManagementService(
            Func<SaveData> getSaveData,
            ISaveService saveService,
            Func<string, CaravanData> registerRuntimeCaravan = null)
        {
            this.getSaveData = getSaveData;
            this.saveService = saveService;
            this.registerRuntimeCaravan = registerRuntimeCaravan;
        }

        /// <summary>
        /// 비어 있는 영속 슬롯에 Caravan과 기본 TradeProgress를 만들고 한 번 저장한다.
        /// </summary>
        /// <returns>
        /// 저장까지 완료되면 생성 ID와 성공 SaveResult를 반환한다.
        /// 검증 실패는 메모리를 변경하거나 저장을 시도하지 않으며, 저장 실패는 전체 SaveData를 원복한다.
        /// </returns>
        public CaravanCreationResult CreateCaravan(int slotIndex)
        {
            var saveData = getSaveData != null ? getSaveData() : null;
            if (saveData == null)
            {
                return CaravanCreationResult.Failure(
                    slotIndex,
                    CaravanCreationFailureReason.SaveDataUnavailable);
            }

            // UI가 잘못된 인덱스를 전달해도 저장 목록에 표시 불가능한 Caravan을 만들지 않는다.
            if (slotIndex < 0 || slotIndex >= MaxCaravanSlotCount)
            {
                return CaravanCreationResult.Failure(
                    slotIndex,
                    CaravanCreationFailureReason.InvalidSlotIndex);
            }

            if (saveData.caravans != null)
            {
                for (var i = 0; i < saveData.caravans.Count; i++)
                {
                    var existing = saveData.caravans[i];
                    if (existing != null && existing.slotIndex == slotIndex)
                    {
                        return CaravanCreationResult.Failure(
                            slotIndex,
                            CaravanCreationFailureReason.SlotAlreadyOccupied);
                    }
                }
            }

            // 슬롯 점유와 해금은 별도 정책이다. Caravan 생성 자체가 다음 슬롯을 열지 않는다.
            if (saveData.world?.unlockedCaravanSlotIndices == null
                || !saveData.world.unlockedCaravanSlotIndices.Contains(slotIndex))
            {
                return CaravanCreationResult.Failure(
                    slotIndex,
                    CaravanCreationFailureReason.SlotLocked);
            }

            var snapshot = JsonUtility.ToJson(saveData);
            var selectedWasValid = SaveDataLookup.TryGetSelectedCaravan(saveData, out _);
            if (saveData.caravans == null) saveData.caravans = new List<CaravanSaveData>();
            if (saveData.tradeProgressEntries == null)
            {
                saveData.tradeProgressEntries = new List<TradeProgressSaveData>();
            }

            var caravan = new CaravanSaveData
            {
                caravanId = SaveDataLookup.NewCaravanId(),
                slotIndex = slotIndex,
                // 플레이어 위치와 무관하게 신규 Caravan은 항상 BaseCamp에서 시작한다.
                currentTownId = InitialCaravanTownId
            };
            saveData.caravans.Add(caravan);
            saveData.tradeProgressEntries.Add(new TradeProgressSaveData
            {
                caravanId = caravan.caravanId,
                state = TradeProgressState.None
            });

            if (!selectedWasValid)
            {
                saveData.selectedCaravanId = caravan.caravanId;
            }

            SaveResult saveResult = null;
            try
            {
                saveResult = saveService != null ? saveService.Save(saveData) : null;
            }
            catch (Exception exception)
            {
                FrameworkLog.Error($"Caravan creation save threw an exception: {exception.Message}");
            }

            if (saveResult == null || !saveResult.Succeeded)
            {
                JsonUtility.FromJsonOverwrite(snapshot, saveData);
                return CaravanCreationResult.Failure(
                    slotIndex,
                    CaravanCreationFailureReason.SaveFailed,
                    saveResult);
            }

            if (registerRuntimeCaravan != null
                && registerRuntimeCaravan(caravan.caravanId) == null)
            {
                JsonUtility.FromJsonOverwrite(snapshot, saveData);
                return CaravanCreationResult.Failure(
                    slotIndex,
                    CaravanCreationFailureReason.SaveFailed,
                    saveResult);
            }

            FrameworkEvents.RaiseCaravanCreated(caravan.caravanId, slotIndex);
            return CaravanCreationResult.Success(caravan.caravanId, slotIndex, saveResult);
        }
    }

    /// <summary>
    /// Framework service를 초기화하고 전역 접근 지점을 제공하는 runtime singleton이다.
    /// </summary>
    /// <remarks>
    /// Unity runtime 시작 전 자동 생성되며 DontDestroyOnLoad로 scene 전환 동안 유지된다.
    /// </remarks>
    public sealed class FrameworkRoot : MonoBehaviour
    {
        private const string RootObjectName = "FrameworkRoot";
        private const float TradeProgressCheckIntervalSeconds = 0.2f;

        private float nextTradeProgressCheckUnscaledTime;
        private bool isOnlineProgressTickEnabled;

        /// <summary>
        /// 현재 활성화된 FrameworkRoot 인스턴스이다.
        /// </summary>
        public static FrameworkRoot Instance { get; private set; }

        /// <summary>
        /// framework 시간 조회와 Unity time scale 제어를 담당하는 서비스이다.
        /// </summary>
        public GameTimeService GameTime { get; private set; }

        /// <summary>
        /// 저장 데이터 생성, 로드, 저장을 담당하는 서비스이다.
        /// </summary>
        public ISaveService SaveService { get; private set; }

        /// <summary>
        /// Unity scene 전환을 담당하는 서비스이다.
        /// </summary>
        public SceneFlowService SceneFlow { get; private set; }

        /// <summary>
        /// Sandbox seed data를 Framework 공용 기준 데이터로 로드하고 검증하는 서비스이다.
        /// </summary>
        public SharedGameDataService SharedGameDataService { get; private set; }

        /// <summary>
        /// 마지막으로 검증에 성공한 공용 기준 데이터 provider이다.
        /// </summary>
        public ISharedGameDataProvider SharedGameData { get; private set; }

        /// <summary>
        /// debug bridge가 호출하는 framework command 모음이다.
        /// </summary>
        public FrameworkDebugCommands DebugCommands { get; private set; }

        /// <summary>
        /// 무역 시작, 정산 대기, 완료/실패 상태를 저장 데이터에 기록하는 서비스이다.
        /// </summary>
        public TradeProgressRecorder TradeProgressRecorder { get; private set; }

        /// <summary>
        /// Core caravan 출발 검증과 저장 데이터 기록을 연결하는 서비스이다.
        /// </summary>
        public TradeStartService TradeStart { get; private set; }

        /// <summary>Caravan 생성, 저장 및 저장 실패 원복을 담당하는 Production command service이다.</summary>
        public CaravanManagementService CaravanManagement { get; private set; }

        /// <summary>마을 건물 배치 변경과 영속 저장을 하나의 SaveData 트랜잭션으로 처리한다.</summary>
        public BuildingPlacementCommand BuildingPlacement { get; private set; }

        /// <summary>구조 대출 발급·상환 및 상태 조회 command service이다.</summary>
        public RescueLoanCommandService RescueLoan { get; private set; }

        public FrameworkTradePrepareCommitStore TradePrepareCommitStore { get; private set; }

        /// <summary>
        /// 무역 진행률 계산, 정산 생성, claim 후 초기화를 조율하는 서비스이다.
        /// </summary>
        public TradeProgressCoordinator TradeProgressCoordinator { get; private set; }

        /// <summary>
        /// 저장 데이터의 무역 상태를 인게임 화면 상태로 변환하고 이벤트를 발행하는 router이다.
        /// </summary>
        public InGameScreenStateRouter InGameScreenRouter { get; private set; }

        /// <summary>
        /// 정산 결과 이벤트를 UI 표시와 claim 요청으로 연결하는 bridge component이다.
        /// </summary>
        public SettlementUiBridge SettlementUiBridge { get; private set; }

        /// <summary>
        /// 현재 runtime에서 공유 중인 저장 데이터 참조이다.
        /// </summary>
        public SaveData CurrentSaveData { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRootExists()
        {
            // scene에 root가 없어도 framework service가 항상 준비되도록 runtime 시작 전에 생성한다.
            if (Instance != null)
            {
                return;
            }

            var rootObject = new GameObject(RootObjectName);
            rootObject.AddComponent<FrameworkRoot>();
        }

        private void Awake()
        {
            // singleton service가 중복 초기화되면 저장 데이터와 event 구독이 갈라지므로 중복 root를 제거한다.
            if (Instance != null && Instance != this)
            {
                FrameworkLog.Warning("Duplicate FrameworkRoot destroyed.");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeServices();
        }

        private void Update()
        {
            if (!CanRunOnlineProgressTick(
                    isOnlineProgressTickEnabled,
                    CurrentSaveData,
                    SharedGameData)
                || TradeProgressCoordinator == null
                || Time.unscaledTime < nextTradeProgressCheckUnscaledTime)
            {
                return;
            }

            nextTradeProgressCheckUnscaledTime =
                Time.unscaledTime + TradeProgressCheckIntervalSeconds;

            // Runtime progress is derived from the saved UTC range. Intermediate frames do
            // not need a disk write; settlement creation saves the completed state itself.
            TradeProgressCoordinator.CheckProgressAndCompletion(saveProgress: false);
        }

        private static bool CanRunOnlineProgressTick(
            bool isEnabled,
            SaveData saveData,
            ISharedGameDataProvider sharedGameData)
        {
            return isEnabled
                && saveData != null
                && sharedGameData != null
                && sharedGameData.IsLoaded;
        }

        /// <summary>
        /// 새 게임 저장 데이터를 만들고 즉시 저장한 뒤 loading scene으로 이동한다.
        /// </summary>
        /// <remarks>
        /// 기존 CurrentSaveData 참조를 새 데이터로 교체하므로 새 게임 버튼에서만 호출해야 한다.
        /// </remarks>
        public void StartNewGame()
        {
            isOnlineProgressTickEnabled = false;
            // 새 게임은 기본 저장 데이터를 먼저 디스크에 기록해 이후 loading 단계가 같은 데이터를 사용하게 한다.
            CurrentSaveData = SaveService.CreateNewGameData();
            SaveService.Save(CurrentSaveData);
            SceneFlow.GoToLoading();
        }

        /// <summary>
        /// Leaves the post-settlement town state and starts a fresh trade-preparation cycle.
        /// The save transition is committed before the Preparation screen is announced.
        /// </summary>
        public bool TryBeginTradePreparationFromTown()
        {
            return TradePreparationEntryCommand.TryExecute(
                CurrentSaveData,
                SaveService,
                InGameScreenRouter);
        }

        /// <summary>
        /// 저장 데이터를 로드하고 loading scene으로 이동한다.
        /// </summary>
        /// <remarks>
        /// 저장 파일이 없거나 유효하지 않으면 ISaveService 구현체의 복구 정책에 따라 새 데이터가 반환될 수 있다.
        /// </remarks>
        public void ContinueGame()
        {
            isOnlineProgressTickEnabled = false;
            // 이어하기는 저장 데이터를 먼저 확보한 뒤 scene flow를 loading 단계로 넘긴다.
            CurrentSaveData = SaveService.Load();
            SceneFlow.GoToLoading();
        }

        /// <summary>
        /// loading 단계를 완료하고 저장 데이터 기준으로 인게임 화면 상태를 갱신한 뒤 in-game scene으로 이동한다.
        /// </summary>
        /// <remarks>
        /// CurrentSaveData가 비어 있으면 저장 서비스를 통해 복구한 뒤 LoadCompleted 이벤트를 발행한다.
        /// SharedGameData 로드 이후 Traveling이면 ApplyOfflineProgressOnLoad로 오프라인 경과를 적용한다.
        /// SettlementPending이면 RestorePendingSettlement로 runtime 정산 cache를 재구성한다.
        /// </remarks>
        public void CompleteLoadingAndEnterGame()
        {
            // 직접 Loading scene에 진입하거나 재호출돼도 복구가 끝나기 전 online tick을 차단한다.
            isOnlineProgressTickEnabled = false;

            // loading scene에 직접 진입한 경우에도 game scene이 사용할 저장 데이터를 보장한다.
            if (CurrentSaveData == null)
            {
                CurrentSaveData = SaveService.Load();
            }

            // SaveData의 ID를 해석할 공용 기준 데이터가 준비되지 않으면 InGame에 진입하지 않는다.
            if (!EnsureSharedGameDataLoaded())
            {
                return;
            }

            // 로드 전부터 존재하던 canonical pending을 cache 복구 대상으로 기억한다.
            // 이번 offline restore에서 새로 완료된 entry는 이미 ready 이벤트를 발행하므로 중복 복구하지 않는다.
            var restorePending = CurrentSaveData.pendingSettlements != null
                && CurrentSaveData.pendingSettlements.Exists(
                    pending => pending != null && pending.hasResult && !pending.claimed);

            // Traveling 이어하기는 모든 명시 entry의 오프라인 경과·완료를 먼저 반영한다.
            TradeProgressCoordinator?.ApplyOfflineProgressOnLoad(CurrentSaveData);

            // 기존 SettlementPending 재진입 시에만 세션 cache를 복구한다.
            if (restorePending)
            {
                TradeProgressCoordinator?.RestorePendingSettlements(CurrentSaveData);
            }

            // scene 전환 전에 화면 router와 load event를 갱신해 UI가 현재 trade state를 기준으로 초기화되게 한다.
            InGameScreenRouter.RefreshFromSaveData(CurrentSaveData);
            FrameworkEvents.RaiseLoadCompleted(CurrentSaveData);
            // SaveData·SharedData·offline/pending 복구와 load event 처리가 모두 끝난 세션만 online tick을 허용한다.
            isOnlineProgressTickEnabled = true;
            SceneFlow.GoToInGame();
        }

        /// <summary>
        /// 현재 저장 데이터를 기록한 뒤 애플리케이션을 종료한다.
        /// </summary>
        /// <remarks>
        /// Editor Play Mode에서는 Application.Quit이 동작하지 않으므로 Play Mode를 종료한다.
        /// CurrentSaveData가 없으면 저장을 생략하고 종료만 수행한다.
        /// </remarks>
        public void ExitGame()
        {
            // 종료 직전 런타임 변경이 유실되지 않도록 현재 메모리를 디스크에 기록한다.
            if (CurrentSaveData != null)
            {
                SaveService.Save(CurrentSaveData);
            }
#if UNITY_EDITOR
            // 빌드가 아닌 Editor Play Mode에서도 Exit 버튼으로 플레이를 끝낼 수 있게 한다.
            UnityEditor.EditorApplication.isPlaying = false;
#else
    Application.Quit();
#endif
        }


        /// <summary>
        /// 현재 저장 데이터를 저장한 뒤 title scene으로 돌아간다.
        /// </summary>
        /// <remarks>
        /// CurrentSaveData가 없으면 저장을 생략하고 scene만 전환한다.
        /// </remarks>
        public void ReturnToTitle()
        {
            isOnlineProgressTickEnabled = false;
            // title 복귀 전 runtime 변경 사항이 유실되지 않도록 현재 저장 데이터를 기록한다.
            if (CurrentSaveData != null)
            {
                SaveService.Save(CurrentSaveData);
            }

            SceneFlow.GoToTitle();
        }

        private void InitializeServices()
        {
            // 서비스 생성 순서는 의존성 방향을 따른다. 저장, 시간, 화면 router를 먼저 만들고 무역 서비스를 조립한다.
            var policyConfig = Resources.Load<InGameTimePolicyConfig>(InGameTimePolicyConfig.ResourceName);
            if (policyConfig == null)
            {
                policyConfig = ScriptableObject.CreateInstance<InGameTimePolicyConfig>();
                FrameworkLog.Warning(
                    $"InGameTimePolicyConfig was not found at Resources/{InGameTimePolicyConfig.ResourceName}. Using runtime defaults.");
            }

            GameTime = new GameTimeService(policyConfig);
            SaveService = new JsonSaveService();
            SharedGameDataService = new SharedGameDataService();
            SceneFlow = new SceneFlowService();
            DebugCommands = new FrameworkDebugCommands(GameTime);
            TradeProgressRecorder = new TradeProgressRecorder(GameTime, GameTime);
            InGameScreenRouter = new InGameScreenStateRouter();
            TradePrepareCommitStore = new FrameworkTradePrepareCommitStore(() => CurrentSaveData);
            TradeProgressCoordinator = new TradeProgressCoordinator(
                () => CurrentSaveData,
                SaveService,
                GameTime,
                TradeProgressRecorder,
                InGameScreenRouter,
                GameTime,
                () => SharedGameData,
                TradePrepareCommitStore,
                TradePrepareCommitStore);
            TradeStart = new TradeStartService(
                () => CurrentSaveData,
                SaveService,
                TradeProgressRecorder,
                InGameScreenRouter,
                ClearSettlementRuntimeCache,
                // Register every newly departed caravan so the coordinator never reuses the
                // claimed Prepare-state caravan left by the previous trade cycle.
                TradeProgressCoordinator.SetActiveCaravan,
                () => SharedGameData,
                TradeProgressCoordinator.GetOrCreateRuntimeCaravan);
            CurrentSaveData = SaveService.HasSaveData() ? SaveService.Load() : SaveService.CreateNewGameData();
            BuildingPlacement = new BuildingPlacementCommand(() => CurrentSaveData, SaveService);
            TradeProgressCoordinator.RebuildRuntimeCaravans();
            CaravanManagement = new CaravanManagementService(
                () => CurrentSaveData,
                SaveService,
                TradeProgressCoordinator.GetOrCreateRuntimeCaravan);

            // 실제 MinimumTradeCost는 Content/Progression 공급 전까지 0으로 두어 command가 안전하게 거부되게 한다.
            ConfigureRescueLoanDefinition(new RescueLoanDefinition());

            // Settlement bridge는 event 구독이 필요한 MonoBehaviour이므로 root GameObject에 component로 붙인다.
            SettlementUiBridge = gameObject.AddComponent<SettlementUiBridge>();
            SettlementUiBridge.Initialize(
                () => CurrentSaveData,
                TradeProgressCoordinator,
                InGameScreenRouter,
                autoClaimOnArrival: false);
            InGameScreenRouter.RefreshFromSaveData(CurrentSaveData);

            FrameworkLog.Info("FrameworkRoot initialized.");
        }

        /// <summary>
        /// Content/Progression이 제공한 구조 대출 정의를 command service에 주입한다.
        /// </summary>
        /// <param name="definition">안정적인 LoanId와 0보다 큰 최소 무역 비용을 가진 정의.</param>
        public void ConfigureRescueLoanDefinition(RescueLoanDefinition definition)
        {
            RescueLoan = new RescueLoanCommandService(
                SaveService,
                () => CurrentSaveData,
                definition,
                () => DateTime.UtcNow.Ticks);
        }

        private bool EnsureSharedGameDataLoaded()
        {
            if (SharedGameDataService == null)
            {
                FrameworkLog.Error("Shared game data load failed because SharedGameDataService is not initialized.");
                return false;
            }

            if (!SharedGameDataService.LoadInitialData())
            {
                FrameworkLog.Error("InGame entry blocked because shared game data validation failed.");
                return false;
            }

            SharedGameData = SharedGameDataService.CurrentData;
            if (SharedGameData == null || !SharedGameData.IsLoaded)
            {
                FrameworkLog.Error("InGame entry blocked because shared game data provider is missing after load.");
                return false;
            }

            FrameworkEvents.RaiseSharedGameDataLoaded(SharedGameData);
            return true;
        }

        private void ClearSettlementRuntimeCache()
        {
            // 새 무역 출발 시 이전 정산 결과가 UI·저장 DTO에 남지 않도록 runtime cache와 pendingSettlement를 함께 비운다.
            TradeProgressCoordinator?.ClearSettlementCache();
            TradeProgressCoordinator?.ClearPendingSettlementSave(CurrentSaveData);
            SettlementUiBridge?.ClearPendingSettlement();
        }
    }

    /// <summary>
    /// 무역 정산 이벤트를 캐시하고 settlement UI와 claim 처리를 연결하는 bridge component이다.
    /// </summary>
    /// <remarks>
    /// FrameworkRoot가 runtime에 추가하고 Initialize를 호출해야 정상 동작한다.
    /// </remarks>
    public sealed class SettlementUiBridge : MonoBehaviour
    {
        private Func<SaveData> getCurrentSaveData;
        private TradeProgressCoordinator tradeProgressCoordinator;
        private InGameScreenStateRouter inGameScreenRouter;
        private string latestNotificationCaravanId = string.Empty;
        private string latestNotificationTradeId = string.Empty;
        private JourneyResultData latestNotificationResult;
        private string presentedCaravanId = string.Empty;
        private string presentedTradeId = string.Empty;
        private JourneyResultData presentedResult;
        private bool settlementPresentationRequested;
        private bool isClaimProcessing;
        private bool autoClaimOnArrival;
        private bool frameworkEventsSubscribed;

        /// <summary>
        /// 표시 가능한 pending settlement가 준비되었을 때 발생한다.
        /// </summary>
        public event Action<string, JourneyResultData> SettlementReady;

        /// <summary>
        /// bridge가 현재 settlement UI에 표시할 결과를 보유하고 있는지 여부이다.
        /// </summary>
        public bool HasPendingSettlement
        {
            get { return presentedResult != null; }
        }

        /// <summary>
        /// settlement claim 처리가 진행 중인지 여부이다.
        /// </summary>
        public bool IsClaimProcessing
        {
            get { return isClaimProcessing; }
        }

        /// <summary>
        /// bridge가 사용할 저장 데이터 접근자와 정산/화면 router 의존성을 설정한다.
        /// </summary>
        /// <param name="getCurrentSaveData">현재 SaveData를 반환하는 접근자.</param>
        /// <param name="tradeProgressCoordinator">정산 claim과 cache 관리를 담당하는 coordinator.</param>
        /// <param name="inGameScreenRouter">정산 화면 전환 요청을 처리하는 router.</param>
        /// <remarks>
        /// FrameworkRoot.InitializeServices 이후 한 번 호출되는 것을 전제로 한다.
        /// </remarks>
        public void Initialize(
            Func<SaveData> getCurrentSaveData,
            TradeProgressCoordinator tradeProgressCoordinator,
            InGameScreenStateRouter inGameScreenRouter,
            bool autoClaimOnArrival = false)
        {
            this.getCurrentSaveData = getCurrentSaveData;
            this.tradeProgressCoordinator = tradeProgressCoordinator;
            this.inGameScreenRouter = inGameScreenRouter;
            this.autoClaimOnArrival = autoClaimOnArrival;
            SubscribeFrameworkEvents();
        }

        /// <summary>
        /// 현재 명시적으로 표시 중인 settlement의 읽기 전용 identity와 결과를 조회한다.
        /// </summary>
        /// <param name="tradeId">표시 중인 trade ID. 결과가 없으면 빈 문자열일 수 있다.</param>
        /// <param name="result">표시 중인 정산 결과. 결과가 없으면 null.</param>
        /// <returns>표시할 정산 결과가 있으면 true, 없으면 false.</returns>
        public bool TryGetPendingSettlement(out string caravanId, out string tradeId, out JourneyResultData result)
        {
            caravanId = presentedCaravanId;
            tradeId = presentedTradeId;
            result = presentedResult;
            return result != null;
        }

        public bool TryGetPendingEconomyResult(string tradeId, out EconomyM1LoopResult result)
        {
            result = null;
            return tradeProgressCoordinator != null
                && tradeProgressCoordinator.TryGetPendingEconomyResult(tradeId, out result);
        }

        public bool TryGetPendingEconomyResult(
            string caravanId,
            string tradeId,
            out EconomyM1LoopResult result)
        {
            result = null;
            return tradeProgressCoordinator != null
                && tradeProgressCoordinator.TryGetPendingEconomyResult(
                    caravanId, tradeId, out result);
        }

        public bool IsSettlementPresentationRequested => settlementPresentationRequested;

        /// <summary>
        /// 사용자가 도착한 caravan의 판매 단계를 마친 뒤 해당 정산 화면을 명시적으로 연다.
        /// 도착 이벤트 자체는 결과를 보존하기만 하며 이 API를 호출하기 전에는 Claim하거나 UI를 열지 않는다.
        /// </summary>
        public bool PresentSettlement(string caravanId, string tradeId)
        {
            if (string.IsNullOrWhiteSpace(caravanId) || string.IsNullOrWhiteSpace(tradeId))
            {
                FrameworkLog.Warning(
                    $"PresentSettlement failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: blank identity.");
                return false;
            }

            SaveData saveData = GetSaveData();
            PendingSettlementSaveData pending;
            JourneyResultData result;
            if (!SaveDataLookup.TryGetPendingSettlement(saveData, caravanId, tradeId, out pending)
                || pending == null)
            {
                FrameworkLog.Warning(
                    $"PresentSettlement failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: exact pending settlement not found.");
                return false;
            }

            if (pending.claimed
                || !string.Equals(pending.caravanId, caravanId, StringComparison.Ordinal)
                || !string.Equals(pending.tradeId, tradeId, StringComparison.Ordinal)
                || !PendingSettlementSaveDataMapper.TryToRuntime(pending, out result)
                || !IsSettlementEntryValid("PresentSettlement", caravanId, tradeId, result))
            {
                FrameworkLog.Warning(
                    $"PresentSettlement failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: pending settlement is invalid or already claimed.");
                return false;
            }

            presentedCaravanId = caravanId;
            presentedTradeId = tradeId;
            presentedResult = result;
            settlementPresentationRequested = true;
            inGameScreenRouter?.RequestScreen(InGameScreenState.Settlement);
            SettlementReady?.Invoke(presentedTradeId, presentedResult);
            return true;
        }

        /// <summary>Claims one durable pending settlement by its exact Caravan and trade identity.</summary>
        /// <returns>
        /// The coordinator outcome. Success clears only matching runtime presentation and notification state;
        /// failure preserves the displayed identity and unrelated pending settlements.
        /// </returns>
        public ClaimSettlementResult ClaimSettlement(string caravanId, string tradeId)
        {
            if (isClaimProcessing)
            {
                FrameworkLog.Warning(
                    $"ClaimSettlement failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: claim already in progress.");
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.InvalidTradeState);
            }

            if (string.IsNullOrWhiteSpace(caravanId))
            {
                FrameworkLog.Warning(
                    $"ClaimSettlement failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: blank Caravan ID.");
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.InvalidCaravanId);
            }

            if (string.IsNullOrWhiteSpace(tradeId))
            {
                FrameworkLog.Warning(
                    $"ClaimSettlement failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: blank trade ID.");
                return ClaimSettlementResult.Failure(ClaimSettlementFailureReason.InvalidTradeId);
            }

            isClaimProcessing = true;
            try
            {
                var claimResult = tradeProgressCoordinator != null
                    ? tradeProgressCoordinator.ClaimSettlement(caravanId, tradeId)
                    : ClaimSettlementResult.Failure(ClaimSettlementFailureReason.SettlementDataInvalid);
                if (claimResult.Succeeded)
                {
                    ClearClaimedIdentity(caravanId, tradeId);
                }
                else
                {
                    FrameworkLog.Warning(
                        $"ClaimSettlement failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: {claimResult.FailureReason}.");
                }

                return claimResult;
            }
            finally
            {
                isClaimProcessing = false;
            }
        }

        /// <summary>
        /// 현재 표시 커서가 가리키는 exact settlement를 claim한다.
        /// </summary>
        /// <returns>claim과 저장 데이터 초기화가 모두 성공하면 true, 검증 실패나 중복 처리 중이면 false.</returns>
        /// <remarks>
        /// 성공 시 표시 커서와 같은 identity의 알림 cache만 삭제되고 coordinator가 저장 데이터를 갱신한다.
        /// </remarks>
        public bool ClaimSettlementAndReset()
        {
            if (presentedResult == null
                || string.IsNullOrWhiteSpace(presentedCaravanId)
                || string.IsNullOrWhiteSpace(presentedTradeId))
            {
                FrameworkLog.Warning(
                    $"ClaimSettlementAndReset failed. CaravanId: {presentedCaravanId}, TradeId: {presentedTradeId}, Reason: no presented settlement cursor.");
                return false;
            }

            return ClaimSettlement(presentedCaravanId, presentedTradeId).Succeeded;
        }

        /// <summary>
        /// bridge가 보유한 알림 cache와 runtime 표시 커서를 비운다.
        /// </summary>
        public void ClearPendingSettlement()
        {
            latestNotificationCaravanId = string.Empty;
            latestNotificationTradeId = string.Empty;
            latestNotificationResult = null;
            ClearPresentedSettlement();
        }

        private void ClearPresentedSettlement()
        {
            presentedCaravanId = string.Empty;
            presentedTradeId = string.Empty;
            presentedResult = null;
            settlementPresentationRequested = false;
        }

        private void ClearClaimedIdentity(string caravanId, string tradeId)
        {
            if (string.Equals(presentedCaravanId, caravanId, StringComparison.Ordinal)
                && string.Equals(presentedTradeId, tradeId, StringComparison.Ordinal))
            {
                ClearPresentedSettlement();
            }

            if (string.Equals(latestNotificationCaravanId, caravanId, StringComparison.Ordinal)
                && string.Equals(latestNotificationTradeId, tradeId, StringComparison.Ordinal))
            {
                latestNotificationCaravanId = string.Empty;
                latestNotificationTradeId = string.Empty;
                latestNotificationResult = null;
            }
        }

        private void OnEnable()
        {
            // 정산 결과 생성 이벤트를 받아 UI 표시 cache로 전환하기 위해 활성화 시 구독한다.
            SubscribeFrameworkEvents();
        }

        private void OnDisable()
        {
            // 비활성 bridge가 stale settlement 이벤트를 받지 않도록 구독을 해제한다.
            UnsubscribeFrameworkEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFrameworkEvents();
        }

        private void SubscribeFrameworkEvents()
        {
            if (frameworkEventsSubscribed)
                return;

            FrameworkEvents.TradeSettlementReady += HandleSettlementReady;
            frameworkEventsSubscribed = true;
        }

        private void UnsubscribeFrameworkEvents()
        {
            if (!frameworkEventsSubscribed)
                return;

            FrameworkEvents.TradeSettlementReady -= HandleSettlementReady;
            frameworkEventsSubscribed = false;
        }

        private void HandleSettlementReady(string caravanId, string tradeId, JourneyResultData result)
        {
            if (!IsSettlementEntryValid("HandleSettlementReady", caravanId, tradeId, result))
            {
                return;
            }

            latestNotificationCaravanId = caravanId;
            latestNotificationTradeId = tradeId;
            latestNotificationResult = result;

            // The first notification may initialize legacy single-Caravan presentation state.
            // Once a settlement is presented, later notifications cannot redirect that cursor.
            if (presentedResult == null)
            {
                presentedCaravanId = caravanId;
                presentedTradeId = tradeId;
                presentedResult = result;
                settlementPresentationRequested = false;
            }

            // Compatibility probes may still opt into automatic Claim explicitly. Runtime
            // initialization disables it so arrival remains pending until the sale flow calls
            // PresentSettlement(caravanId, tradeId).
            if (autoClaimOnArrival)
            {
                if (ClaimSettlementAndReset())
                {
                    FrameworkLog.Info($"Settlement auto-claimed on arrival. TradeId: {tradeId}");
                    return;
                }

                FrameworkLog.Warning(
                    $"Settlement auto-claim failed; keeping pending result for recovery UI. TradeId: {tradeId}");
            }

            // A saved ready event only marks this caravan as awaiting player action. The
            // Caravan status UI opens the sell-only panel, and that flow explicitly presents
            // settlement after the player confirms or skips selling.
        }

        private bool IsSettlementEntryValid(
            string operation,
            string caravanId,
            string tradeId,
            JourneyResultData result)
        {
            if (string.IsNullOrWhiteSpace(caravanId)
                || string.IsNullOrWhiteSpace(tradeId)
                || result == null)
            {
                FrameworkLog.Warning(
                    $"{operation} failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: blank identity or null result.");
                return false;
            }

            var saveData = GetSaveData();
            PendingSettlementSaveData pending;
            if (!SaveDataLookup.TryGetPendingSettlement(saveData, caravanId, tradeId, out pending)
                || pending == null
                || pending.claimed
                || !string.Equals(pending.caravanId, caravanId, StringComparison.Ordinal)
                || !string.Equals(pending.tradeId, tradeId, StringComparison.Ordinal))
            {
                FrameworkLog.Warning(
                    $"{operation} failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: exact durable pending settlement is unavailable.");
                return false;
            }

            TradeProgressSaveData progress;
            if (!SaveDataLookup.TryGetTradeProgress(saveData, caravanId, out progress))
            {
                FrameworkLog.Warning(
                    $"{operation} failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: trade progress is missing.");
                return false;
            }

            if (progress.state != TradeProgressState.SettlementPending)
            {
                FrameworkLog.Warning(
                    $"{operation} failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: trade state is {progress.state}.");
                return false;
            }

            var activeTradeId = progress.activeTradeId ?? string.Empty;
            if (!string.Equals(tradeId, activeTradeId, StringComparison.Ordinal))
            {
                FrameworkLog.Warning(
                    $"{operation} failed. CaravanId: {caravanId}, TradeId: {tradeId}, Reason: active trade is {activeTradeId}.");
                return false;
            }

            return true;
        }

        private SaveData GetSaveData()
        {
            return getCurrentSaveData != null ? getCurrentSaveData() : null;
        }
    }
}
