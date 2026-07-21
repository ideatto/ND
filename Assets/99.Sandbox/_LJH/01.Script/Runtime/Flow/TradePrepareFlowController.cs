using System;

public sealed class TradePrepareFlowController : IDisposable
{
    private readonly TradePrepareDraftStore draftStore;
    private readonly TradePrepareViewDataBuilder viewDataBuilder;
    private TradePrepareBuildContext buildContext;

    public TradePrepareDraft CurrentDraft => draftStore.Current;
    public TradePrepareViewData CurrentViewData { get; private set; }
    public bool IsCommitted { get; private set; }

    public event Action<TradePrepareViewData> ViewDataChanged;

    public TradePrepareFlowController(TradePrepareBuildContext buildContext)
        : this(buildContext, new TradePrepareDraftStore(), new TradePrepareViewDataBuilder())
    {
    }

    public TradePrepareFlowController(
        TradePrepareBuildContext buildContext,
        TradePrepareDraftStore draftStore,
        TradePrepareViewDataBuilder viewDataBuilder)
    {
        this.buildContext = buildContext ?? new TradePrepareBuildContext();
        this.draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
        this.viewDataBuilder = viewDataBuilder ?? throw new ArgumentNullException(nameof(viewDataBuilder));
        this.draftStore.DraftChanged += HandleDraftChanged;
        Rebuild(this.draftStore.Current);
    }

    public void Initialize(string currentTownId)
    {
        InitializeForCaravan(string.Empty, currentTownId);
    }

    // Begins destination, route, and mercenary preparation for the Caravan selected in the overview.
    // Legacy callers may continue using Initialize until the scene routes an explicit Caravan ID.
    public void InitializeForCaravan(string caravanId, string currentTownId)
    {
        IsCommitted = false;
        draftStore.ResetForCaravan(caravanId, currentTownId);
    }

    // Call this only after settlement claim succeeds. It releases the committed
    // preparation session and starts a fresh draft at the town supplied by the UI.
    public void ResetAfterSettlement(string currentTownId)
    {
        Initialize(currentTownId);
    }

    public void UpdateBuildContext(TradePrepareBuildContext context)
    {
        if (IsCommitted) return;
        buildContext = context ?? new TradePrepareBuildContext();
        Rebuild(draftStore.Current);
    }

    public void Refresh()
    {
        if (IsCommitted) return;
        Rebuild(draftStore.Current);
    }

    public TradePrepareStartResult TryStartTrade(
        TradePrepareStartAdapter startAdapter,
        string tradeId,
        bool saveImmediately = true)
    {
        if (IsCommitted)
        {
            return new TradePrepareStartResult
            {
                succeeded = false,
                errorCode = "DRAFT_ALREADY_COMMITTED",
                errorMessage = "Trade preparation draft was already committed.",
                tradeId = tradeId ?? string.Empty,
                prepareCondition = CurrentViewData != null ? CurrentViewData.startCondition : null
            };
        }

        if (startAdapter == null)
        {
            return new TradePrepareStartResult
            {
                succeeded = false,
                errorCode = TradePrepareStartAdapter.ErrorStartServiceMissing,
                errorMessage = "Trade start adapter is not connected.",
                tradeId = tradeId ?? string.Empty,
                prepareCondition = CurrentViewData != null ? CurrentViewData.startCondition : null
            };
        }

        TradePrepareStartResult result = startAdapter.TryStartTrade(
            draftStore.Current,
            buildContext,
            tradeId,
            saveImmediately);
        if (result.succeeded)
        {
            IsCommitted = true;
        }

        return result;
    }

    public void SelectDestination(string townId) { if (!IsCommitted) draftStore.SelectDestination(townId); }
    public void SelectRoute(string routeId) { if (!IsCommitted) draftStore.SelectRoute(routeId); }
    public void SelectWagon(string wagonId) { if (!IsCommitted) draftStore.SelectWagon(wagonId); }
    public void SetAnimalQuantity(string animalId, int quantity) { if (!IsCommitted) draftStore.SetAnimalQuantity(animalId, quantity); }
    public void SetBuyItemQuantity(string itemId, int quantity) { if (!IsCommitted) draftStore.SetBuyItemQuantity(itemId, quantity); }
    public void ClearCargo() { if (!IsCommitted) draftStore.ClearCargo(); }
    public void SelectMercenary(string mercenaryId) { if (!IsCommitted) draftStore.SelectMercenary(mercenaryId); }
    public void DeselectMercenary(string mercenaryId) { if (!IsCommitted) draftStore.DeselectMercenary(mercenaryId); }
    public void ClearMercenaries() { if (!IsCommitted) draftStore.ClearMercenaries(); }
    public void Cancel() { if (!IsCommitted) draftStore.Cancel(); }

    public void Dispose()
    {
        draftStore.DraftChanged -= HandleDraftChanged;
    }

    private void HandleDraftChanged(TradePrepareDraft draft)
    {
        Rebuild(draft);
    }

    private void Rebuild(TradePrepareDraft draft)
    {
        CurrentViewData = viewDataBuilder.Build(draft, buildContext);
        ViewDataChanged?.Invoke(CurrentViewData);
    }
}
