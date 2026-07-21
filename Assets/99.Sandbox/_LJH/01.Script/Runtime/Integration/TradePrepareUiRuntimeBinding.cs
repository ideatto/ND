using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Connects the production trade-prepare runtime context to the existing preparation panels.
/// UI input is sent through the provider so the UI never edits the preparation draft directly.
/// </summary>
public sealed class TradePrepareUiRuntimeBinding : MonoBehaviour
{
    [Header("Runtime source")]
    [SerializeField] private TradePrepareRuntimeContextProvider runtimeContext;

    [Header("S1 view")]
    [SerializeField] private TownRoutePanel townRoutePanel;

    [Header("S3 wagon and draft-animal view")]
    [SerializeField] private TradePrepareUIManager uiManager;
    [SerializeField] private AnimalInventoryPanel animalPanel;

    [Header("S4 cargo view")]
    [SerializeField] private CargoLoadingPanelController cargoPanel;

    [Header("Departure warning")]
    [SerializeField] private NoticeUI departureWarning;

    private void OnEnable()
    {
        if (uiManager != null)
        {
            // The existing manager asks providers for S3 data whenever that screen is entered.
            // Supplying Runtime ViewData here avoids changing the external UI navigation code.
            uiManager.AnimalProvider = BuildAnimalEntries;
            uiManager.OwnedWagonProvider = BuildOwnedWagonEntries;
            uiManager.CargoProvider = BuildCargoConfig;
            uiManager.SummaryProvider = BuildSummaryData;

            // The demo used to consume OnDepart, but disabling it left the production button
            // with no subscriber. Forward departure to RuntimeContext so Draft is validated
            // and Framework can record Traveling before the presenter opens S7.
            uiManager.OnDepart += HandleDepartRequested;
        }

        if (runtimeContext != null)
        {
            runtimeContext.ViewDataChanged += HandleViewDataChanged;

            // The context can initialize while TradePrepareUI is inactive, so apply its latest
            // snapshot immediately when the UI is opened for the first time.
            HandleViewDataChanged(runtimeContext.CurrentViewData);
        }

        if (townRoutePanel != null)
            townRoutePanel.OnRouteSelected += HandleRouteSelected;

        if (animalPanel != null)
        {
            animalPanel.OnWagonSelected += HandleWagonSelected;
            animalPanel.OnWagonRemoved += HandleWagonRemoved;
            animalPanel.OnSelectionChanged += HandleAnimalSelectionChanged;
        }

    }

    private void OnDisable()
    {
        if (runtimeContext != null)
            runtimeContext.ViewDataChanged -= HandleViewDataChanged;

        if (townRoutePanel != null)
            townRoutePanel.OnRouteSelected -= HandleRouteSelected;

        if (animalPanel != null)
        {
            animalPanel.OnWagonSelected -= HandleWagonSelected;
            animalPanel.OnWagonRemoved -= HandleWagonRemoved;
            animalPanel.OnSelectionChanged -= HandleAnimalSelectionChanged;
        }


        if (uiManager != null)
        {
            uiManager.OnDepart -= HandleDepartRequested;
            if (uiManager.AnimalProvider == BuildAnimalEntries)
                uiManager.AnimalProvider = null;
            if (uiManager.OwnedWagonProvider == BuildOwnedWagonEntries)
                uiManager.OwnedWagonProvider = null;
            if (uiManager.CargoProvider == BuildCargoConfig)
                uiManager.CargoProvider = null;
            if (uiManager.SummaryProvider == BuildSummaryData)
                uiManager.SummaryProvider = null;
        }
    }

    private void HandleDepartRequested(TradePrepareUIManager.DepartData departure)
    {
        if (runtimeContext == null)
        {
            Debug.LogError(
                "[TradePrepare] Departure was requested without a RuntimeContext.",
                this);
            return;
        }

        // DepartData belongs to the legacy panel flow. RuntimeContext's Draft is authoritative
        // because every production selection was already sent to it through provider commands.
        // A new ID is created only at confirmation so retries cannot reuse a failed trade record.
        string tradeId = Guid.NewGuid().ToString("N");
        TradePrepareStartResult result = runtimeContext.TryStartTrade(tradeId);
        if (result == null || !result.succeeded)
        {
            // Runtime validation remains authoritative; the binding only converts its result
            // into a user-facing message and forwards it to the temporary notice view.
            if (departureWarning != null)
                departureWarning.Show(BuildDepartureWarning(result));

            Debug.LogError(
                $"[TradePrepare] Trade start failed: {result?.errorCode ?? "NULL_RESULT"} - " +
                $"{result?.errorMessage ?? "RuntimeContext returned no result."}",
                this);

            // A failed request must remain on S6 and must not continue into any success path.
            return;
        }

        // Do not activate S7 here. A successful start changes Framework SaveData to Traveling;
        // FrameworkTradeScreenPresenter observes that state and becomes the sole screen router.
    }

    private static string BuildDepartureWarning(TradePrepareStartResult result)
    {
        if (result == null)
            return "출발 결과를 확인할 수 없습니다.";

        DepartureValidationResult validation = result.departureValidation;
        if (validation != null && validation.reasons != null && validation.reasons.Count > 0)
        {
            var messages = new List<string>();
            for (int index = 0; index < validation.reasons.Count; index++)
                messages.Add(GetDepartureReasonMessage(validation.reasons[index]));

            return string.Join("\n", messages);
        }

        // Prepare validation and Framework recording failures do not always contain Core enums,
        // so preserve the detailed message already produced by TradePrepareStartAdapter.
        return string.IsNullOrWhiteSpace(result.errorMessage)
            ? "출발 조건을 만족하지 못했습니다."
            : result.errorMessage;
    }

    private static string GetDepartureReasonMessage(DepartureBlockReason reason)
    {
        // Core reasons stay language-neutral; this UI boundary owns their Korean presentation.
        switch (reason)
        {
            case DepartureBlockReason.NoWagon:
                return "이동 수단을 선택해 주세요.";
            case DepartureBlockReason.NotEnoughAnimals:
                return "견인 동물이 부족합니다.";
            case DepartureBlockReason.TooManyAnimals:
                return "견인 동물이 너무 많습니다.";
            case DepartureBlockReason.Overloaded:
                return "최대 적재 중량을 초과했습니다.";
            case DepartureBlockReason.NoCargo:
                return "적재된 무역품이 없습니다.";
            case DepartureBlockReason.BrokenWagon:
                return "이동 수단의 내구도가 부족합니다.";
            case DepartureBlockReason.SlotExceeded:
                return "사용 가능한 적재 슬롯을 초과했습니다.";
            case DepartureBlockReason.MixedAnimalType:
                return "서로 다른 종류의 견인 동물을 함께 사용할 수 없습니다.";
            case DepartureBlockReason.NotInPrepare:
                return "현재는 새로운 무역을 출발할 수 없는 상태입니다.";
            default:
                return "출발 조건을 만족하지 못했습니다.";
        }
    }

    private void HandleViewDataChanged(TradePrepareViewData viewData)
    {
        if (townRoutePanel != null && viewData != null)
            townRoutePanel.Populate(viewData);
    }

    private void HandleRouteSelected(string destinationTownId, string routeId, float distance)
    {
        if (runtimeContext == null || !CanSelectRoute(runtimeContext.CurrentViewData, routeId))
            return;

        // Provider commands update the draft and rebuild ViewData; the panel only supplies IDs.
        runtimeContext.SelectDestination(destinationTownId);
        runtimeContext.SelectRoute(routeId);
    }

    private static bool CanSelectRoute(TradePrepareViewData viewData, string routeId)
    {
        if (viewData == null || viewData.routes == null || string.IsNullOrWhiteSpace(routeId))
            return false;

        foreach (RouteViewData route in viewData.routes)
        {
            if (route != null &&
                string.Equals(route.routeId, routeId, StringComparison.Ordinal) &&
                route.isUnlocked &&
                route.canSelect)
            {
                return true;
            }
        }

        return false;
    }

    private List<AnimalInventoryPanel.AnimalEntry> BuildAnimalEntries()
    {
        var result = new List<AnimalInventoryPanel.AnimalEntry>();
        TradePrepareViewData viewData = runtimeContext != null ? runtimeContext.CurrentViewData : null;
        if (viewData == null || viewData.draftAnimals == null)
            return result;

        foreach (DraftAnimalViewData animal in viewData.draftAnimals)
        {
            if (animal != null)
                result.Add(new AnimalInventoryPanel.AnimalEntry(animal));
        }

        return result;
    }

    private List<TransportSelectPanel.TransportEntry> BuildOwnedWagonEntries()
    {
        var result = new List<TransportSelectPanel.TransportEntry>();
        TradePrepareViewData viewData = runtimeContext != null ? runtimeContext.CurrentViewData : null;
        if (viewData == null || viewData.wagons == null)
            return result;

        foreach (WagonViewData wagon in viewData.wagons)
        {
            // S3 selects a travel method, so animal wagons, mounts, and walking are all valid entries.
            if (wagon != null)
                result.Add(new TransportSelectPanel.TransportEntry(wagon));
        }

        return result;
    }

    private TradePrepareUIManager.CargoConfig BuildCargoConfig()
    {
        TradePrepareViewData viewData = runtimeContext != null ? runtimeContext.CurrentViewData : null;
        TradeItemData[] availableItems = runtimeContext != null
            ? runtimeContext.GetAvailableTradeItems()
            : Array.Empty<TradeItemData>();

        var items = new List<TradeItemData>();
        var stocks = new List<int>();
        if (viewData != null && viewData.tradeItems != null)
        {
            foreach (TradeItemViewData itemView in viewData.tradeItems)
            {
                if (itemView == null)
                    continue;

                TradeItemData item = Array.Find(
                    availableItems,
                    candidate => candidate != null &&
                        string.Equals(candidate.ItemId, itemView.itemId, StringComparison.Ordinal));
                if (item == null)
                    continue;

                items.Add(item);
                stocks.Add(Mathf.Max(0, itemView.contentQuantityLimit));
            }
        }

        return new TradePrepareUIManager.CargoConfig
        {
            automaticCargoLoading = true,
            gold = viewData != null ? viewData.currentTradingCurrency : 0L,
            maxLoad = viewData != null ? viewData.maxLoad : 0f,
            requiredFood = viewData != null ? viewData.requiredDraftAnimalFoodQuantity : 0,
            shopItems = items.ToArray(),
            stocks = stocks.ToArray(),
            selectedItems = viewData != null ? viewData.tradeItems : Array.Empty<TradeItemViewData>()
        };
    }

    private TradeSummaryPanel.SummaryData BuildSummaryData()
    {
        TradePrepareViewData viewData = runtimeContext != null ? runtimeContext.CurrentViewData : null;
        if (viewData == null)
            return default;

        RouteViewData selectedRoute = null;
        if (viewData.routes != null)
        {
            selectedRoute = Array.Find(
                viewData.routes,
                route => route != null &&
                    string.Equals(route.routeId, viewData.selectedRouteId, StringComparison.Ordinal));
        }

        string fromTown = !string.IsNullOrWhiteSpace(viewData.currentTownName)
            ? viewData.currentTownName
            : viewData.currentTownId;
        string toTown = selectedRoute != null && !string.IsNullOrWhiteSpace(selectedRoute.toTownName)
            ? selectedRoute.toTownName
            : selectedRoute != null ? selectedRoute.toTownId : string.Empty;

        return new TradeSummaryPanel.SummaryData
        {
            fromTown = string.IsNullOrWhiteSpace(fromTown) ? "-" : fromTown,
            toTown = string.IsNullOrWhiteSpace(toTown) ? "-" : toTown,
            viaText = "없음",
            expectedRisk = selectedRoute != null ? Mathf.RoundToInt(selectedRoute.riskLevel) : 0,
            mercenaryPower = Mathf.Max(0, viewData.selectedMercenaryPower),
            expectedFood = Mathf.Max(0, viewData.requiredDraftAnimalFoodQuantity),
            loadedFood = Mathf.Max(0, viewData.loadedDraftAnimalFoodQuantity),
            prepareCost = Math.Max(0L, viewData.totalPreparationCost),
            expectedProfit = viewData.estimatedNetProfit,
            durationSeconds = Mathf.Max(0f, viewData.finalExpectedTravelTime)
        };
    }

    private void HandleWagonSelected(TransportSelectPanel.TransportEntry wagon)
    {
        if (runtimeContext == null || !CanSelectWagon(runtimeContext.CurrentViewData, wagon.id))
            return;

        // Selecting a different wagon clears dependent animal and cargo choices in DraftStore.
        runtimeContext.SelectWagon(wagon.id);
    }

    private void HandleWagonRemoved()
    {
        if (runtimeContext != null)
            runtimeContext.SelectWagon(string.Empty);
    }

    private void HandleAnimalSelectionChanged(
        IReadOnlyList<AnimalInventoryPanel.AnimalPick> picks,
        bool isValid)
    {
        if (runtimeContext == null || runtimeContext.FlowController == null)
            return;

        var desiredQuantities = new Dictionary<string, int>(StringComparer.Ordinal);
        if (picks != null)
        {
            foreach (AnimalInventoryPanel.AnimalPick pick in picks)
            {
                if (!string.IsNullOrWhiteSpace(pick.animalId))
                    desiredQuantities[pick.animalId] = Mathf.Max(0, pick.count);
            }
        }

        TradePrepareDraft draft = runtimeContext.FlowController.CurrentDraft;
        TradePrepareViewData viewData = runtimeContext.CurrentViewData;
        if (draft == null || viewData == null || viewData.draftAnimals == null)
            return;

        var currentQuantities = new Dictionary<string, int>(StringComparer.Ordinal);
        if (draft.selectedAnimals != null)
        {
            foreach (DraftAnimalSelectionData selected in draft.selectedAnimals)
            {
                if (selected != null && !string.IsNullOrWhiteSpace(selected.draftAnimalId))
                    currentQuantities[selected.draftAnimalId] = Mathf.Max(0, selected.quantity);
            }
        }

        // Send zero for removed picks as well; otherwise an animal removed in S3 would remain in Draft.
        foreach (DraftAnimalViewData animal in viewData.draftAnimals)
        {
            if (animal == null || string.IsNullOrWhiteSpace(animal.draftAnimalId))
                continue;

            int desired = desiredQuantities.TryGetValue(animal.draftAnimalId, out int value) ? value : 0;
            int current = currentQuantities.TryGetValue(animal.draftAnimalId, out int oldValue) ? oldValue : 0;
            if (desired != current)
                runtimeContext.SetAnimalQuantity(animal.draftAnimalId, desired);
        }
    }

    private static bool CanSelectWagon(TradePrepareViewData viewData, string wagonId)
    {
        if (viewData == null || viewData.wagons == null || string.IsNullOrWhiteSpace(wagonId))
            return false;

        foreach (WagonViewData wagon in viewData.wagons)
        {
            if (wagon != null &&
                string.Equals(wagon.wagonId, wagonId, StringComparison.Ordinal) &&
                wagon.canSelect &&
                (wagon.wagonType == WagonType.None || wagon.isOwned))
            {
                return true;
            }
        }

        return false;
    }
}
