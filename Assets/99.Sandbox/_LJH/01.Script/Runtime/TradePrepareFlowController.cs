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
        IsCommitted = false;
        draftStore.Reset(currentTownId);
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
    public void AddWaypoint(string townId) { if (!IsCommitted) draftStore.AddWaypoint(townId); }
    public void RemoveWaypoint(string townId) { if (!IsCommitted) draftStore.RemoveWaypoint(townId); }
    public void ClearWaypoints() { if (!IsCommitted) draftStore.ClearWaypoints(); }
    public void SelectRoute(string routeId) { if (!IsCommitted) draftStore.SelectRoute(routeId); }
    public void SelectWagon(string wagonId) { if (!IsCommitted) draftStore.SelectWagon(wagonId); }
    public void SetAnimalQuantity(string animalId, int quantity) { if (!IsCommitted) draftStore.SetAnimalQuantity(animalId, quantity); }
    public void SetBuyItemQuantity(string itemId, int quantity) { if (!IsCommitted) draftStore.SetBuyItemQuantity(itemId, quantity); }
    public void SetSellItemQuantity(string itemId, int quantity) { if (!IsCommitted) draftStore.SetSellItemQuantity(itemId, quantity); }
    public void ClearCargo() { if (!IsCommitted) draftStore.ClearCargo(); }
    public void SetFoodQuantity(int quantity) { if (!IsCommitted) draftStore.SetFoodQuantity(quantity); }
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
