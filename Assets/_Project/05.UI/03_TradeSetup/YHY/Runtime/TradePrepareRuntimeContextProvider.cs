using System;
using System.Linq;
using UnityEngine;
using ND.Framework;
using FrameworkSaveData = ND.Framework.SaveData;

/// <summary>
/// Production-facing owner of TradePrepareBuildContext and TradePrepareFlowController.
/// It supplies official data assets and Framework SaveData without changing the existing panels.
/// </summary>
public sealed class TradePrepareRuntimeContextProvider : MonoBehaviour
{
    [Header("Official content data")]
    [SerializeField] private TownData[] towns = Array.Empty<TownData>();
    [SerializeField] private RouteData[] routes = Array.Empty<RouteData>();
    [SerializeField] private TradeItemData[] tradeItems = Array.Empty<TradeItemData>();
    [SerializeField] private WagonData[] wagons = Array.Empty<WagonData>();
    [SerializeField] private DraftAnimalData[] draftAnimals = Array.Empty<DraftAnimalData>();
    [SerializeField] private MercenaryData[] mercenaries = Array.Empty<MercenaryData>();

    [Header("Optional production settlement staging")]
    [Tooltip("Assign a MonoBehaviour implementing ITradePrepareCommitSink. Temporary/InMemory implementations are not allowed for submission.")]
    [SerializeField] private MonoBehaviour commitSinkBehaviour;

    private TradePrepareFlowController flowController;
    private TradePrepareStartAdapter startAdapter;
    private InGameScreenState currentScreenState;
    // TODO(PRODUCTION): These interfaces are currently supplied by TestCaravanSettingService in the
    // sandbox scene. Keep the interfaces, but inject Framework SaveData-backed Caravan query/command
    // services from the production composition root instead of the in-memory test component.
    private ICaravanSettingViewDataProvider caravanSettingProvider;
    private ICaravanLoadSettingViewDataProvider caravanCargoPlanProvider;
    private ITradePrepareCaravanOptionProvider caravanOptionProvider;
    private TradePrepareBuildContext buildContext;

    public TradePrepareFlowController FlowController => flowController;
    public TradePrepareViewData CurrentViewData => flowController?.CurrentViewData;
    public event Action<TradePrepareViewData> ViewDataChanged;

    private void OnEnable()
    {
        FrameworkSaveData currentSaveData = FrameworkRoot.Instance != null
            ? FrameworkRoot.Instance.CurrentSaveData
            : null;
        currentScreenState = InGameScreenStateRouter.MapFromSaveData(currentSaveData);
        FrameworkEvents.LoadCompleted += HandleLoadCompleted;
        FrameworkEvents.InGameScreenChanged += HandleScreenChanged;
        TryInitialize(currentSaveData);
        AttachSceneCaravanProviders();
    }

    private void OnDisable()
    {
        FrameworkEvents.LoadCompleted -= HandleLoadCompleted;
        FrameworkEvents.InGameScreenChanged -= HandleScreenChanged;
        DisposeFlow();
    }

    private void HandleLoadCompleted(FrameworkSaveData saveData)
    {
        // Synchronize the transition baseline as well as data because the provider may enable
        // before FrameworkRoot has loaded a Traveling or SettlementPending save.
        currentScreenState = InGameScreenStateRouter.MapFromSaveData(saveData);
        TryInitialize(saveData);
    }

    private void HandleScreenChanged(InGameScreenState state)
    {
        InGameScreenState previousState = currentScreenState;
        currentScreenState = state;

        // A deliberate transition into Preparation starts a fresh draft. Ordinary UI
        // close/reopen does not emit a state change and therefore preserves the draft.
        if (previousState == InGameScreenState.Preparation ||
            state != InGameScreenState.Preparation ||
            flowController == null)
        {
            return;
        }

        TryInitialize(FrameworkRoot.Instance != null ? FrameworkRoot.Instance.CurrentSaveData : null);
    }

    public void RefreshFromFramework()
    {
        TryInitialize(FrameworkRoot.Instance != null ? FrameworkRoot.Instance.CurrentSaveData : null);
    }

    public void SelectDestination(string townId) => flowController?.SelectDestination(townId);
    public void SelectRoute(string routeId) => flowController?.SelectRoute(routeId);
    public void SelectWagon(string wagonId)
    {
        flowController?.SelectWagon(wagonId);
        RefreshSelectedCaravanCargoPlan();
    }
    public void SetAnimalQuantity(string animalId, int quantity) => flowController?.SetAnimalQuantity(animalId, quantity);
    public void SetBuyItemQuantity(string itemId, int quantity) => flowController?.SetBuyItemQuantity(itemId, quantity);
    public void ClearCargoDraft() => flowController?.ClearCargo();
    public void SelectMercenary(string mercenaryId) => flowController?.SelectMercenary(mercenaryId);
    public void DeselectMercenary(string mercenaryId) => flowController?.DeselectMercenary(mercenaryId);

    public void SetCaravanCargoPlanProvider(ICaravanLoadSettingViewDataProvider provider)
    {
        caravanCargoPlanProvider = provider;
        RefreshSelectedCaravanCargoPlan();
    }

    public void SetCaravanSettingProvider(ICaravanSettingViewDataProvider provider)
    {
        caravanSettingProvider = provider;
        RefreshSelectedCaravanSetting();
    }

    public void SetCaravanOptionProvider(ITradePrepareCaravanOptionProvider provider)
    {
        caravanOptionProvider = provider;
        if (flowController == null)
            return;

        buildContext.caravanOptions = GetLatestCaravanOptions();
        flowController.UpdateBuildContext(buildContext);
    }

    public bool TrySelectOnlyAvailableDepartureCaravan()
    {
        return TryGetOnlyAvailableDepartureCaravanId(out string caravanId)
            && SelectDepartureCaravan(caravanId);
    }

    public bool TryGetOnlyAvailableDepartureCaravanId(out string caravanId)
    {
        caravanId = string.Empty;
        TradePrepareCaravanOptionViewData[] options = CurrentViewData?.caravanOptions;
        if (options == null)
            return false;

        int selectableCount = 0;
        for (int index = 0; index < options.Length; index++)
        {
            TradePrepareCaravanOptionViewData option = options[index];
            if (option == null || !option.canSelect || string.IsNullOrWhiteSpace(option.caravanId))
                continue;

            caravanId = option.caravanId;
            selectableCount++;
        }

        if (selectableCount == 1)
            return true;

        caravanId = string.Empty;
        return false;
    }

    public bool CanSelectDepartureCaravan(string caravanId)
    {
        TradePrepareCaravanOptionViewData[] options = CurrentViewData?.caravanOptions;
        if (options == null || string.IsNullOrWhiteSpace(caravanId))
            return false;

        for (int index = 0; index < options.Length; index++)
        {
            TradePrepareCaravanOptionViewData option = options[index];
            if (option != null && option.canSelect
                && string.Equals(option.caravanId, caravanId.Trim(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public bool SelectDepartureCaravan(string caravanId)
    {
        if (flowController == null || !flowController.SelectDepartureCaravan(caravanId))
            return false;

        if (!RefreshSelectedCaravanSetting())
            return false;

        RefreshSelectedCaravanCargoPlan();
        return true;
    }

    public bool RefreshCaravanSetting(string caravanId = null)
    {
        if (flowController == null || caravanSettingProvider == null)
            return false;

        string selectedCaravanId = flowController.CurrentDraft?.departureCaravanId;
        string requestedCaravanId = string.IsNullOrWhiteSpace(caravanId)
            ? selectedCaravanId
            : caravanId.Trim();
        if (string.IsNullOrWhiteSpace(selectedCaravanId)
            || !string.Equals(selectedCaravanId, requestedCaravanId, StringComparison.Ordinal))
        {
            return false;
        }

        CaravanSettingViewData setting = caravanSettingProvider.GetSetting(selectedCaravanId);
        return setting != null && flowController.ApplyCaravanSetting(setting);
    }

    public bool RefreshCaravanCargoPlan(string caravanId = null)
    {
        if (flowController == null || caravanCargoPlanProvider == null)
            return false;

        string selectedCaravanId = flowController.CurrentDraft?.departureCaravanId;
        string requestedCaravanId = string.IsNullOrWhiteSpace(caravanId)
            ? selectedCaravanId
            : caravanId.Trim();
        if (string.IsNullOrWhiteSpace(selectedCaravanId)
            || !string.Equals(selectedCaravanId, requestedCaravanId, StringComparison.Ordinal))
        {
            return false;
        }

        CaravanLoadSettingViewData plan = caravanCargoPlanProvider.GetLoadSetting(selectedCaravanId);
        return plan != null && flowController.ApplyCargoPlan(plan);
    }

    public TradeItemData[] GetAvailableTradeItems()
    {
        string currentTownId = CurrentViewData != null
            ? CurrentViewData.currentTownId
            : string.Empty;
        TownData currentTown = TradePrepareViewDataBuilder.FindTown(towns, currentTownId);
        return TradePrepareViewDataBuilder.MergeUnique(
            tradeItems ?? Array.Empty<TradeItemData>(),
            currentTown != null && currentTown.Market != null
                ? currentTown.Market.TradeItems
                : null,
            item => item != null ? item.ItemId : string.Empty);
    }

    public MarketData[] GetAvailableMarkets()
    {
        return (towns ?? Array.Empty<TownData>())
            .Where(town => town != null && town.Market != null)
            .Select(town => town.Market)
            .GroupBy(market => market.MarketId ?? string.Empty, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    public void RefreshFromCurrentSaveData()
    {
        flowController?.Refresh();
    }

    public TradePrepareStartResult TryStartTrade(string tradeId, bool saveImmediately = true)
    {
        if (flowController == null)
        {
            return new TradePrepareStartResult
            {
                succeeded = false,
                errorCode = TradePrepareStartAdapter.ErrorStartServiceMissing,
                errorMessage = "Trade prepare runtime context is not initialized.",
                tradeId = tradeId ?? string.Empty
            };
        }

        // Re-read both provider-owned snapshots at the commit boundary. S3/S4 can be edited
        // independently after entering TradePrepare, and transient panel state must never be
        // allowed to overwrite the authoritative saved Caravan composition.
        RefreshSelectedCaravanSetting();
        RefreshSelectedCaravanCargoPlan();
        return flowController.TryStartTrade(startAdapter, tradeId, saveImmediately);
    }

    private void TryInitialize(FrameworkSaveData saveData)
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || saveData == null)
            return;

        DisposeFlow();
        buildContext = new TradePrepareBuildContext
        {
            saveData = saveData,
            caravanOptions = GetLatestCaravanOptions(),
            towns = towns ?? Array.Empty<TownData>(),
            routes = routes ?? Array.Empty<RouteData>(),
            tradeItems = tradeItems ?? Array.Empty<TradeItemData>(),
            wagons = wagons ?? Array.Empty<WagonData>(),
            draftAnimals = draftAnimals ?? Array.Empty<DraftAnimalData>(),
            mercenaries = mercenaries ?? Array.Empty<MercenaryData>()
        };

        ITradePrepareCommitSink commitSink = commitSinkBehaviour as ITradePrepareCommitSink;
        if (commitSink == null)
            commitSink = root.TradePrepareCommitStore;
        startAdapter = new TradePrepareStartAdapter(root.TradeStart, new TradePrepareViewDataBuilder(), commitSink);
        flowController = new TradePrepareFlowController(buildContext);
        flowController.ViewDataChanged += HandleViewDataChanged;
        string currentTownId = saveData.player != null ? saveData.player.currentTownId : string.Empty;
        flowController.Initialize(currentTownId);
        ViewDataChanged?.Invoke(flowController.CurrentViewData);
    }

    private void RefreshSelectedCaravanCargoPlan()
    {
        RefreshCaravanCargoPlan();
    }

    private bool RefreshSelectedCaravanSetting()
    {
        return RefreshCaravanSetting();
    }

    private TradePrepareCaravanOptionViewData[] GetLatestCaravanOptions()
    {
        return caravanOptionProvider?.GetOptions()
            ?? Array.Empty<TradePrepareCaravanOptionViewData>();
    }

    private void HandleViewDataChanged(TradePrepareViewData viewData)
    {
        ViewDataChanged?.Invoke(viewData);
    }

    private void DisposeFlow()
    {
        if (flowController == null)
            return;
        flowController.ViewDataChanged -= HandleViewDataChanged;
        flowController.Dispose();
        flowController = null;
        startAdapter = null;
        buildContext = null;
    }

    private void AttachSceneCaravanProviders()
    {
        // The TradePrepare feature prefab can be instantiated after the InGame scene binding's
        // OnEnable. Register from this side as well so prefab creation order cannot omit S0/S3/S4
        // provider injection and leave the selected Caravan without its saved composition.
        CaravanOverviewEditBinding binding = FindFirstObjectByType<CaravanOverviewEditBinding>(
            FindObjectsInactive.Include);
        binding?.AttachTradePrepareRuntimeContext(this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        WarnSandboxAssets(towns);
        WarnSandboxAssets(routes);
        WarnSandboxAssets(tradeItems);
        WarnSandboxAssets(wagons);
        WarnSandboxAssets(draftAnimals);
        WarnSandboxAssets(mercenaries);
    }

    private void WarnSandboxAssets(UnityEngine.Object[] assets)
    {
        if (assets == null) return;
        foreach (UnityEngine.Object asset in assets)
        {
            if (asset == null) continue;
            string path = UnityEditor.AssetDatabase.GetAssetPath(asset).Replace('\\', '/');
            if (path.Contains("/99.Sandbox/"))
                Debug.LogError($"[TradePrepareRuntimeContext] Submission data must not reference Sandbox asset: {path}", this);
        }
    }
#endif
}
