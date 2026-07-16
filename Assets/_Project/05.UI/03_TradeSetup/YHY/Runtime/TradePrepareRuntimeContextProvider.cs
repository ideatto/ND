using System;
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

    public TradePrepareFlowController FlowController => flowController;
    public TradePrepareViewData CurrentViewData => flowController?.CurrentViewData;
    public event Action<TradePrepareViewData> ViewDataChanged;

    private void OnEnable()
    {
        FrameworkEvents.LoadCompleted += HandleLoadCompleted;
        TryInitialize(FrameworkRoot.Instance != null ? FrameworkRoot.Instance.CurrentSaveData : null);
    }

    private void OnDisable()
    {
        FrameworkEvents.LoadCompleted -= HandleLoadCompleted;
        DisposeFlow();
    }

    private void HandleLoadCompleted(FrameworkSaveData saveData)
    {
        TryInitialize(saveData);
    }

    public void RefreshFromFramework()
    {
        TryInitialize(FrameworkRoot.Instance != null ? FrameworkRoot.Instance.CurrentSaveData : null);
    }

    public void SelectDestination(string townId) => flowController?.SelectDestination(townId);
    public void SelectRoute(string routeId) => flowController?.SelectRoute(routeId);
    public void SelectWagon(string wagonId) => flowController?.SelectWagon(wagonId);
    public void SetAnimalQuantity(string animalId, int quantity) => flowController?.SetAnimalQuantity(animalId, quantity);
    public void SetBuyItemQuantity(string itemId, int quantity) => flowController?.SetBuyItemQuantity(itemId, quantity);
    public void SelectMercenary(string mercenaryId) => flowController?.SelectMercenary(mercenaryId);
    public void DeselectMercenary(string mercenaryId) => flowController?.DeselectMercenary(mercenaryId);

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

        return flowController.TryStartTrade(startAdapter, tradeId, saveImmediately);
    }

    private void TryInitialize(FrameworkSaveData saveData)
    {
        FrameworkRoot root = FrameworkRoot.Instance;
        if (root == null || saveData == null)
            return;

        DisposeFlow();
        TradePrepareBuildContext context = new TradePrepareBuildContext
        {
            saveData = saveData,
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
        flowController = new TradePrepareFlowController(context);
        flowController.ViewDataChanged += HandleViewDataChanged;
        string currentTownId = saveData.player != null ? saveData.player.currentTownId : string.Empty;
        flowController.Initialize(currentTownId);
        ViewDataChanged?.Invoke(flowController.CurrentViewData);
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
