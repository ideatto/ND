using System;
using System.Collections.Generic;

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
        // Opening TradePrepareUI never inherits the Caravan focused by Caravan Overview.
        draftStore.Reset(currentTownId);
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

    // Accepts only a Provider-approved option so UI selection cannot bypass the displayed disabled state.
    // Framework departure validation still performs the final authoritative check before saving.
    public bool SelectDepartureCaravan(string caravanId)
    {
        if (IsCommitted || string.IsNullOrWhiteSpace(caravanId)
            || CurrentViewData == null || CurrentViewData.caravanOptions == null)
        {
            return false;
        }

        for (int index = 0; index < CurrentViewData.caravanOptions.Length; index++)
        {
            TradePrepareCaravanOptionViewData option = CurrentViewData.caravanOptions[index];
            if (option != null
                && option.canSelect
                && !string.IsNullOrWhiteSpace(option.currentTownId)
                && string.Equals(option.caravanId, caravanId, StringComparison.Ordinal))
            {
                draftStore.SelectDepartureCaravan(option.caravanId);
                // Location is selected with the Caravan option so route calculation
                // does not depend on the separate S3 wagon/animal setting provider.
                draftStore.SetCurrentTown(option.currentTownId);
                return true;
            }
        }

        return false;
    }

    public bool ApplyCargoPlan(CaravanLoadSettingViewData plan)
    {
        if (IsCommitted || plan == null || string.IsNullOrWhiteSpace(plan.caravanId))
            return false;

        TradePrepareDraft currentDraft = draftStore.Current;
        if (!string.Equals(
            currentDraft.departureCaravanId,
            plan.caravanId.Trim(),
            StringComparison.Ordinal))
        {
            return false;
        }

        draftStore.ReplaceCargoPlan(plan.plannedItems);
        return true;
    }

    public bool ApplyCaravanSetting(CaravanSettingViewData setting)
    {
        if (IsCommitted || setting == null || string.IsNullOrWhiteSpace(setting.caravanId))
            return false;

        TradePrepareDraft currentDraft = draftStore.Current;
        if (!string.Equals(
            currentDraft.departureCaravanId,
            setting.caravanId.Trim(),
            StringComparison.Ordinal))
        {
            return false;
        }

        string selectedWagonInstanceId = setting.selectedWagonInstanceId?.Trim() ?? string.Empty;
        string selectedWagonId = string.Empty;
        if (!string.IsNullOrEmpty(selectedWagonInstanceId))
        {
            WagonViewData wagon = Array.Find(
                setting.wagons ?? Array.Empty<WagonViewData>(),
                value => value != null && string.Equals(
                    value.wagonInstanceId,
                    selectedWagonInstanceId,
                    StringComparison.Ordinal));
            if (wagon == null || string.IsNullOrWhiteSpace(wagon.wagonId))
                return false;
            selectedWagonId = wagon.wagonId.Trim();
        }

        var quantities = new Dictionary<string, int>(StringComparer.Ordinal);
        var visitedInstances = new HashSet<string>(StringComparer.Ordinal);
        string[] selectedAnimalInstances = setting.selectedAnimalInstanceIds ?? Array.Empty<string>();
        DraftAnimalViewData[] animals = setting.draftAnimals ?? Array.Empty<DraftAnimalViewData>();
        for (int index = 0; index < selectedAnimalInstances.Length; index++)
        {
            string instanceId = selectedAnimalInstances[index]?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(instanceId) || !visitedInstances.Add(instanceId))
                return false;

            DraftAnimalViewData animal = Array.Find(
                animals,
                value => value != null && string.Equals(
                    value.draftAnimalInstanceId,
                    instanceId,
                    StringComparison.Ordinal));
            if (animal == null || string.IsNullOrWhiteSpace(animal.draftAnimalId))
                return false;

            string contentId = animal.draftAnimalId.Trim();
            quantities[contentId] = quantities.TryGetValue(contentId, out int quantity)
                ? quantity + 1
                : 1;
        }

        var selections = new List<DraftAnimalSelectionData>();
        foreach (KeyValuePair<string, int> pair in quantities)
        {
            selections.Add(new DraftAnimalSelectionData
            {
                draftAnimalId = pair.Key,
                quantity = pair.Value
            });
        }

        int currentDurability = string.IsNullOrEmpty(selectedWagonId)
            ? 0
            : Math.Max(0, Array.Find(
                setting.wagons ?? Array.Empty<WagonViewData>(),
                value => value != null && string.Equals(
                    value.wagonInstanceId,
                    selectedWagonInstanceId,
                    StringComparison.Ordinal))?.currentDurability ?? 0);
        draftStore.ReplaceCaravanComposition(selectedWagonId, selections, currentDurability);
        return true;
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

    // Selection integration uses this only to roll back a failed provider refresh.
    public void RestoreDraft(TradePrepareDraft snapshot)
    {
        if (!IsCommitted)
            draftStore.Restore(snapshot);
    }

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
