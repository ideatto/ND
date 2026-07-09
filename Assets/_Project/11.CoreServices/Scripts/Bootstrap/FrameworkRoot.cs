using System;
using UnityEngine;

namespace ND.Framework
{
    public sealed class FrameworkRoot : MonoBehaviour
    {
        private const string RootObjectName = "FrameworkRoot";

        public static FrameworkRoot Instance { get; private set; }

        public GameTimeService GameTime { get; private set; }
        public ISaveService SaveService { get; private set; }
        public SceneFlowService SceneFlow { get; private set; }
        public FrameworkDebugCommands DebugCommands { get; private set; }
        public TradeProgressRecorder TradeProgressRecorder { get; private set; }
        public TradeStartService TradeStart { get; private set; }
        public TradeProgressCoordinator TradeProgressCoordinator { get; private set; }
        public InGameScreenStateRouter InGameScreenRouter { get; private set; }
        public SettlementUiBridge SettlementUiBridge { get; private set; }
        public SaveData CurrentSaveData { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRootExists()
        {
            if (Instance != null)
            {
                return;
            }

            var rootObject = new GameObject(RootObjectName);
            rootObject.AddComponent<FrameworkRoot>();
        }

        private void Awake()
        {
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

        public void StartNewGame()
        {
            CurrentSaveData = SaveService.CreateNewGameData();
            SaveService.Save(CurrentSaveData);
            SceneFlow.GoToLoading();
        }

        public void ContinueGame()
        {
            CurrentSaveData = SaveService.Load();
            SceneFlow.GoToLoading();
        }

        public void CompleteLoadingAndEnterGame()
        {
            if (CurrentSaveData == null)
            {
                CurrentSaveData = SaveService.Load();
            }

            InGameScreenRouter.RefreshFromSaveData(CurrentSaveData);
            FrameworkEvents.RaiseLoadCompleted(CurrentSaveData);
            SceneFlow.GoToInGame();
        }

        public void ReturnToTitle()
        {
            if (CurrentSaveData != null)
            {
                SaveService.Save(CurrentSaveData);
            }

            SceneFlow.GoToTitle();
        }

        private void InitializeServices()
        {
            GameTime = new GameTimeService();
            SaveService = new JsonSaveService();
            SceneFlow = new SceneFlowService();
            DebugCommands = new FrameworkDebugCommands(GameTime);
            TradeProgressRecorder = new TradeProgressRecorder(GameTime);
            InGameScreenRouter = new InGameScreenStateRouter();
            TradeProgressCoordinator = new TradeProgressCoordinator(
                () => CurrentSaveData,
                SaveService,
                GameTime,
                TradeProgressRecorder,
                InGameScreenRouter);
            TradeStart = new TradeStartService(
                () => CurrentSaveData,
                SaveService,
                TradeProgressRecorder,
                InGameScreenRouter,
                ClearSettlementRuntimeCache);
            CurrentSaveData = SaveService.HasSaveData() ? SaveService.Load() : SaveService.CreateNewGameData();
            SettlementUiBridge = gameObject.AddComponent<SettlementUiBridge>();
            SettlementUiBridge.Initialize(
                () => CurrentSaveData,
                TradeProgressCoordinator,
                InGameScreenRouter);
            InGameScreenRouter.RefreshFromSaveData(CurrentSaveData);

            FrameworkLog.Info("FrameworkRoot initialized.");
        }

        private void ClearSettlementRuntimeCache()
        {
            TradeProgressCoordinator?.ClearSettlementCache();
            SettlementUiBridge?.ClearPendingSettlement();
        }
    }

    public sealed class SettlementUiBridge : MonoBehaviour
    {
        private Func<SaveData> getCurrentSaveData;
        private TradeProgressCoordinator tradeProgressCoordinator;
        private InGameScreenStateRouter inGameScreenRouter;
        private string pendingTradeId = string.Empty;
        private JourneyResultData pendingResult;
        private bool isClaimProcessing;

        public event Action<string, JourneyResultData> SettlementReady;

        public bool HasPendingSettlement
        {
            get { return pendingResult != null; }
        }

        public bool IsClaimProcessing
        {
            get { return isClaimProcessing; }
        }

        public void Initialize(
            Func<SaveData> getCurrentSaveData,
            TradeProgressCoordinator tradeProgressCoordinator,
            InGameScreenStateRouter inGameScreenRouter)
        {
            this.getCurrentSaveData = getCurrentSaveData;
            this.tradeProgressCoordinator = tradeProgressCoordinator;
            this.inGameScreenRouter = inGameScreenRouter;
        }

        public bool TryGetPendingSettlement(out string tradeId, out JourneyResultData result)
        {
            tradeId = pendingTradeId;
            result = pendingResult;
            return result != null;
        }

        public bool ClaimSettlementAndReset()
        {
            if (isClaimProcessing)
            {
                FrameworkLog.Warning("Settlement claim ignored because a claim is already being processed.");
                return false;
            }

            if (!IsPendingSettlementValid())
            {
                return false;
            }

            isClaimProcessing = true;
            try
            {
                var claimed = tradeProgressCoordinator != null
                    && tradeProgressCoordinator.ClaimSettlementAndReset();
                if (claimed)
                {
                    ClearPendingSettlement();
                }

                return claimed;
            }
            finally
            {
                isClaimProcessing = false;
            }
        }

        public void ClearPendingSettlement()
        {
            pendingTradeId = string.Empty;
            pendingResult = null;
        }

        private void OnEnable()
        {
            FrameworkEvents.TradeSettlementReady += HandleSettlementReady;
        }

        private void OnDisable()
        {
            FrameworkEvents.TradeSettlementReady -= HandleSettlementReady;
        }

        private void HandleSettlementReady(string tradeId, JourneyResultData result)
        {
            if (!IsSettlementEntryValid(tradeId, result))
            {
                return;
            }

            pendingTradeId = tradeId ?? string.Empty;
            pendingResult = result;
            inGameScreenRouter?.RequestScreen(InGameScreenState.Settlement);
            SettlementReady?.Invoke(pendingTradeId, pendingResult);
        }

        private bool IsSettlementEntryValid(string tradeId, JourneyResultData result)
        {
            if (result == null)
            {
                FrameworkLog.Warning("Settlement screen entry blocked because settlement result is null.");
                return false;
            }

            var saveData = GetSaveData();
            if (saveData == null || saveData.tradeProgress == null)
            {
                FrameworkLog.Warning("Settlement screen entry blocked because trade progress save data is missing.");
                return false;
            }

            if (saveData.tradeProgress.state != TradeProgressState.SettlementPending)
            {
                FrameworkLog.Warning($"Settlement screen entry blocked because trade state is {saveData.tradeProgress.state}.");
                return false;
            }

            var activeTradeId = saveData.tradeProgress.activeTradeId ?? string.Empty;
            if (string.IsNullOrEmpty(tradeId) || tradeId != activeTradeId)
            {
                FrameworkLog.Warning(
                    $"Settlement screen entry blocked because event trade ID does not match active trade ID. Event: {tradeId}, Active: {activeTradeId}");
                return false;
            }

            return true;
        }

        private bool IsPendingSettlementValid()
        {
            if (pendingResult == null)
            {
                FrameworkLog.Warning("Settlement claim blocked because bridge has no pending settlement result.");
                return false;
            }

            var saveData = GetSaveData();
            if (saveData == null || saveData.tradeProgress == null)
            {
                FrameworkLog.Warning("Settlement claim blocked because trade progress save data is missing.");
                return false;
            }

            if (saveData.tradeProgress.state != TradeProgressState.SettlementPending)
            {
                FrameworkLog.Warning($"Settlement claim blocked because trade state is {saveData.tradeProgress.state}.");
                return false;
            }

            var activeTradeId = saveData.tradeProgress.activeTradeId ?? string.Empty;
            if (pendingTradeId != activeTradeId)
            {
                FrameworkLog.Warning(
                    $"Settlement claim blocked because bridge trade ID does not match active trade ID. Bridge: {pendingTradeId}, Active: {activeTradeId}");
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
