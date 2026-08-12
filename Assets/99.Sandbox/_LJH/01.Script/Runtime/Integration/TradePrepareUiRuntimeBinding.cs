using System;
using System.Collections.Generic;
using System.Linq;
using ND.Framework.CargoLoading;
using ND.UI.Market;
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
    [SerializeField] private MarketTradePanelController marketTradePanel;

    [Header("Departure warning")]
    [SerializeField] private NoticeUI departureWarning;

    [Header("Travel presentation")]
    [SerializeField] private FrameworkTradeScreenPresenter tradeScreenPresenter;

    private string acknowledgedFoodShortageKey = string.Empty;
    private bool isPurchasingCargo;

    private void OnEnable()
    {
        ND.Framework.FrameworkEvents.SharedGameDataLoaded += HandleFrameworkDataReady;
        ND.Framework.FrameworkEvents.LoadCompleted += HandleFrameworkDataReady;
        MarketInventoryChangeTracker.Changed += HandleMarketInventoryChanged;
        EnsureMarketTradePanel();
        if (marketTradePanel != null)
        {
            marketTradePanel.ErrorChanged += HandleMarketErrorChanged;
        }

        if (uiManager != null)
        {
            // The existing manager asks providers for S3 data whenever that screen is entered.
            // Supplying Runtime ViewData here avoids changing the external UI navigation code.
            uiManager.AnimalProvider = BuildAnimalEntries;
            uiManager.OwnedWagonProvider = BuildOwnedWagonEntries;
            uiManager.CargoProvider = BuildCargoConfig;
            uiManager.DetachedCargoProvider = BuildDetachedCargoConfig;
            uiManager.SummaryProvider = BuildSummaryData;
            uiManager.CaravanOptionsProvider = BuildCaravanOptions;
            uiManager.DepartureCaravanSelector = SelectDepartureCaravan;
            uiManager.ClearMercenarySelection = ClearMercenarySelection;
            uiManager.MercenaryOptionsProvider = BuildMercenaryOptions;
            uiManager.ExpectedRiskProvider = GetSelectedRouteRisk;
            uiManager.MercenarySelector = SelectMercenary;
            uiManager.RefreshPreparationDraft = RefreshPreparationDraft;

            // The demo used to consume OnDepart, but disabling it left the production button
            // with no subscriber. Forward departure to RuntimeContext so Draft is validated
            // and Framework can record Traveling before the presenter opens S7.
            uiManager.OnDepart += HandleDepartRequested;
            uiManager.OnCaravanCompositionConfirmed += HandleCaravanCompositionConfirmed;
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

        if (cargoPanel != null)
        {
            cargoPanel.LoadChanged += HandleCargoLoadChanged;
            cargoPanel.CargoConfirmed += HandleCargoConfirmed;
            cargoPanel.TryCommitCargoTransaction = TryCommitCargoTransaction;
            cargoPanel.CanCommitCargoTransaction = CanCommitCargoTransaction;
            cargoPanel.ProjectedCurrencyAfterCargoTransaction = GetProjectedCurrency;
            cargoPanel.CancelCargoTransactionDraft = CancelCargoTransactionDraft;
        }

    }

    private void OnDisable()
    {
        ND.Framework.FrameworkEvents.SharedGameDataLoaded -= HandleFrameworkDataReady;
        ND.Framework.FrameworkEvents.LoadCompleted -= HandleFrameworkDataReady;
        MarketInventoryChangeTracker.Changed -= HandleMarketInventoryChanged;
        if (marketTradePanel != null)
        {
            marketTradePanel.ErrorChanged -= HandleMarketErrorChanged;
        }

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

        if (cargoPanel != null)
        {
            cargoPanel.LoadChanged -= HandleCargoLoadChanged;
            cargoPanel.CargoConfirmed -= HandleCargoConfirmed;
            if (cargoPanel.TryCommitCargoTransaction == TryCommitCargoTransaction)
                cargoPanel.TryCommitCargoTransaction = null;
            if (cargoPanel.CanCommitCargoTransaction == CanCommitCargoTransaction)
                cargoPanel.CanCommitCargoTransaction = null;
            if (cargoPanel.ProjectedCurrencyAfterCargoTransaction == GetProjectedCurrency)
                cargoPanel.ProjectedCurrencyAfterCargoTransaction = null;
            if (cargoPanel.CancelCargoTransactionDraft == CancelCargoTransactionDraft)
                cargoPanel.CancelCargoTransactionDraft = null;
        }


        if (uiManager != null)
        {
            uiManager.OnDepart -= HandleDepartRequested;
            uiManager.OnCaravanCompositionConfirmed -= HandleCaravanCompositionConfirmed;
            if (uiManager.AnimalProvider == BuildAnimalEntries)
                uiManager.AnimalProvider = null;
            if (uiManager.OwnedWagonProvider == BuildOwnedWagonEntries)
                uiManager.OwnedWagonProvider = null;
            if (uiManager.CargoProvider == BuildCargoConfig)
                uiManager.CargoProvider = null;
            if (uiManager.DetachedCargoProvider == BuildDetachedCargoConfig)
                uiManager.DetachedCargoProvider = null;
            if (uiManager.SummaryProvider == BuildSummaryData)
                uiManager.SummaryProvider = null;
            if (uiManager.CaravanOptionsProvider == BuildCaravanOptions)
                uiManager.CaravanOptionsProvider = null;
            if (uiManager.DepartureCaravanSelector == SelectDepartureCaravan)
                uiManager.DepartureCaravanSelector = null;
            if (uiManager.ClearMercenarySelection == ClearMercenarySelection)
                uiManager.ClearMercenarySelection = null;
            if (uiManager.MercenaryOptionsProvider == BuildMercenaryOptions)
                uiManager.MercenaryOptionsProvider = null;
            if (uiManager.ExpectedRiskProvider == GetSelectedRouteRisk)
                uiManager.ExpectedRiskProvider = null;
            if (uiManager.MercenarySelector == SelectMercenary)
                uiManager.MercenarySelector = null;
            if (uiManager.RefreshPreparationDraft == RefreshPreparationDraft)
                uiManager.RefreshPreparationDraft = null;
        }
    }

    private void ClearMercenarySelection()
    {
        TradePrepareViewData viewData = runtimeContext != null ? runtimeContext.CurrentViewData : null;
        MercenaryViewData[] options = viewData != null ? viewData.mercenaries : null;
        if (options == null)
            return;

        string[] selectedIds = options
            .Where(option => option != null && option.isSelected)
            .Select(option => option.mercenaryId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToArray();
        for (int index = 0; index < selectedIds.Length; index++)
            runtimeContext.DeselectMercenary(selectedIds[index]);
    }

    private MercenaryViewData[] BuildMercenaryOptions()
    {
        TradePrepareViewData viewData = runtimeContext != null ? runtimeContext.CurrentViewData : null;
        return viewData?.mercenaries ?? Array.Empty<MercenaryViewData>();
    }

    private float GetSelectedRouteRisk()
    {
        TradePrepareViewData viewData = runtimeContext != null ? runtimeContext.CurrentViewData : null;
        if (viewData == null)
            return 0f;
        return Mathf.Clamp01(viewData.eventOccurrenceProbability) * 100f;
    }

    private bool SelectMercenary(string mercenaryId)
    {
        ClearMercenarySelection();

        // An empty selection is the supported "hire nobody" choice.
        if (string.IsNullOrWhiteSpace(mercenaryId))
            return true;

        TradePrepareViewData viewData = runtimeContext != null ? runtimeContext.CurrentViewData : null;
        MercenaryViewData[] options = viewData != null ? viewData.mercenaries : null;
        MercenaryViewData match = options?.FirstOrDefault(option =>
            option != null
            && option.canHire
            && string.Equals(option.mercenaryId, mercenaryId, StringComparison.Ordinal));
        if (match == null || string.IsNullOrWhiteSpace(match.mercenaryId))
            return false;

        runtimeContext.SelectMercenary(match.mercenaryId);
        return true;
    }

    private void HandleDepartRequested(TradePrepareUIManager.DepartData departure)
    {
        if (runtimeContext == null)
        {
            Debug.LogError("[TradePrepare] Departure was requested without a RuntimeContext.", this);
            return;
        }

        // Departure consumes only authoritative Saved Cargo. Any successful Market purchase
        // refreshes that baseline before this command can run.
        RefreshPreparationDraft();

        TradePrepareViewData viewData = runtimeContext.CurrentViewData;
        int loadedFood = viewData != null ? Mathf.Max(0, viewData.loadedDraftAnimalFoodQuantity) : 0;
        int requiredFood = viewData != null ? Mathf.Max(0, viewData.requiredDraftAnimalFoodQuantity) : 0;
        string shortageKey = viewData == null
            ? string.Empty
            : $"{viewData.departureCaravanId}|{viewData.selectedRouteId}|{loadedFood}|{requiredFood}";
        if (loadedFood < requiredFood
            && !string.Equals(acknowledgedFoodShortageKey, shortageKey, StringComparison.Ordinal))
        {
            acknowledgedFoodShortageKey = shortageKey;
            int shortage = requiredFood - loadedFood;
            departureWarning?.Show(
                $"견인 동물 먹이가 {shortage}개 부족합니다.\n" +
                "그래도 출발하려면 출발 버튼을 다시 눌러주세요.");
            return;
        }

        TradePrepareStartResult preflight = runtimeContext.ValidateDeparture();
        if (preflight == null || !preflight.succeeded)
        {
            departureWarning?.Show(BuildDepartureWarning(preflight));
            Debug.LogWarning(
                $"[TradePrepare] Departure blocked: {preflight?.errorCode ?? "NULL_RESULT"} - " +
                $"{preflight?.errorMessage ?? "RuntimeContext returned no result."}",
                this);
            return;
        }

        string tradeId = Guid.NewGuid().ToString("N");
        TradePrepareStartResult result = runtimeContext.TryStartTrade(tradeId);
        if (result == null || !result.succeeded)
        {
            departureWarning?.Show(BuildDepartureWarning(result));
            Debug.LogError(
                $"[TradePrepare] Trade start failed: {result?.errorCode ?? "NULL_RESULT"} - " +
                $"{result?.errorMessage ?? "RuntimeContext returned no result."}",
                this);
            return;
        }

        if (tradeScreenPresenter == null)
        {
            tradeScreenPresenter = FindAnyObjectByType<FrameworkTradeScreenPresenter>(
                FindObjectsInactive.Include);
        }

        // The global screen state can already be Traveling for another Caravan, so S7 needs
        // the successful command identity instead of waiting for a duplicate state event.
        tradeScreenPresenter?.OpenTravelingScreen(
            viewData.departureCaravanId,
            string.IsNullOrWhiteSpace(result.tradeId) ? tradeId : result.tradeId);
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

        uiManager?.RefreshCaravanOptionsIfVisible();

        // A detached S3 panel owns a Caravan-specific instance snapshot. Replacing it with the
        // aggregate TradePrepare inventory would turn assigned/returned instances into x0 entries.
        if (animalPanel != null
            && animalPanel.gameObject.activeInHierarchy
            && (uiManager == null || !uiManager.IsDetachedCaravanEditOpen))
        {
            animalPanel.RefreshAnimalAvailability(BuildAnimalEntries());
        }
    }

    private void HandleRouteSelected(string destinationTownId, string routeId, float distance)
    {
        if (runtimeContext == null || !CanSelectRoute(runtimeContext.CurrentViewData, destinationTownId, routeId))
            return;

        // Provider commands update the draft and rebuild ViewData; the panel only supplies IDs.
        runtimeContext.SelectDestination(destinationTownId);
        runtimeContext.SelectRoute(routeId);
    }

    private static bool CanSelectRoute(TradePrepareViewData viewData, string destinationTownId, string routeId)
    {
        if(viewData == null || viewData.towns == null || viewData.routes == null ||
            string.IsNullOrWhiteSpace(viewData.currentTownId) || string.IsNullOrWhiteSpace(destinationTownId) || string.IsNullOrWhiteSpace(routeId))
        {
            return false;
        }

        TownViewData destinationTown = null;

        foreach(TownViewData town in viewData.towns)
        {
            if(town != null && string.Equals(town.townId, destinationTownId, StringComparison.Ordinal))
            {
                destinationTown = town;
                break;
            }
        }

        if(destinationTown == null || !destinationTown.isUnlocked || !destinationTown.canSelect)
        {
            return false;
        }

        foreach (RouteViewData route in viewData.routes)
        {
            if(route == null)
            {
                continue;
            }

            bool isValidRoute = string.Equals(route.routeId, routeId, StringComparison.Ordinal) &&
                string.Equals(route.fromTownId, viewData.currentTownId, StringComparison.Ordinal) &&
                string.Equals(route.toTownId, destinationTownId, StringComparison.Ordinal) &&
                route.isUnlocked && route.canSelect;

            if (isValidRoute)
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

    private void RefreshPreparationDraft()
    {
        runtimeContext?.RefreshCaravanSetting();
        runtimeContext?.RefreshCaravanCargoPlan();
    }

    private TradePrepareCaravanOptionViewData[] BuildCaravanOptions()
    {
        TradePrepareCaravanOptionViewData[] options = runtimeContext?.CurrentViewData?.caravanOptions;
        return options ?? Array.Empty<TradePrepareCaravanOptionViewData>();
    }
    private bool SelectDepartureCaravan(string caravanId)
    {
        // Provider selection restores authoritative Saved Cargo. Uncommitted purchase UI state
        // is never projected into departure validation.
        return runtimeContext != null && runtimeContext.SelectDepartureCaravan(caravanId);
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
        if (TryOpenPreparationMarket())
        {
            MarketTradePanelModel marketModel = marketTradePanel.Model;
            MarketTradeItemState[] marketItems = marketModel.Items
                .Where(item => item != null && item.Item != null)
                .ToArray();

            TradePrepareViewData cargoViewData = runtimeContext?.CurrentViewData;
            return new TradePrepareUIManager.CargoConfig
            {
                automaticCargoLoading = false,
                restoreOwnedCargo = true,
                caravanId = marketModel.CaravanId,
                marketId = marketModel.MarketId,
                gold = marketModel.TradingCurrency,
                maxLoad = marketModel.MaximumCargoWeight,
                requiredFood = cargoViewData?.requiredDraftAnimalFoodQuantity ?? 0,
                // Shop and saved Cargo have separate catalogs. Only actual Market products render above.
                shopItems = marketItems.Select(item => item.Item).ToArray(),
                stocks = marketItems.Select(item => Math.Max(0, item.MarketStock)).ToArray(),
                buyUnitPrices = marketItems.Select(item => Math.Max(0L, item.BuyUnitPrice)).ToArray(),
                selectedItems = BuildOwnedCargoSelection(cargoViewData?.loadedItems)
            };
        }

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
            restoreOwnedCargo = true,
            gold = ReadCurrentTradingCurrency(),
            maxLoad = viewData != null ? viewData.maxLoad : 0f,
            requiredFood = viewData != null ? viewData.requiredDraftAnimalFoodQuantity : 0,
            shopItems = items.ToArray(),
            stocks = stocks.ToArray(),
            buyUnitPrices = items.Select(item => item != null ? Math.Max(0L, item.BaseBuyPrice) : 0L).ToArray(),
            selectedItems = viewData != null
                ? BuildOwnedCargoSelection(viewData.loadedItems)
                : Array.Empty<TradeItemViewData>()
        };
    }

    private void EnsureMarketTradePanel()
    {
        if (marketTradePanel == null)
            marketTradePanel = GetComponentInChildren<MarketTradePanelController>(true);
        if (marketTradePanel == null)
            marketTradePanel = gameObject.AddComponent<MarketTradePanelController>();

        if (runtimeContext != null)
            marketTradePanel.ConfigureCatalog(runtimeContext.GetAvailableMarkets());
    }

    private bool TryOpenPreparationMarket()
    {
        if (marketTradePanel == null || !IsFrameworkMarketReady())
            return false;

        TradePrepareViewData viewData = runtimeContext?.CurrentViewData;
        if (viewData == null
            || string.IsNullOrWhiteSpace(viewData.departureCaravanId)
            || string.IsNullOrWhiteSpace(viewData.currentTownId))
        {
            return false;
        }

        ND.Framework.FrameworkRoot root = ND.Framework.FrameworkRoot.Instance;
        if (root?.SharedGameData == null
            || !root.SharedGameData.TryGetTown(
                viewData.currentTownId,
                out ND.Framework.SharedTownDefinition town))
        {
            return false;
        }

        MarketData market = runtimeContext.GetAvailableMarkets()
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(candidate.MarketId, town.MarketId, StringComparison.Ordinal));
        if (market == null)
            return false;

        marketTradePanel.Configure(
            market,
            Mathf.Max(0f, viewData.maxLoad),
            Math.Max(0, viewData.maxInventorySlotCount));

        // The Runtime draft owns the selected Caravan for this preparation session. Passing its ID
        // explicitly prevents an older SaveData.selectedCaravanId from opening another Cargo that
        // happens to use a wagon with the same capacity.
        return marketTradePanel.OpenForCaravanPreparation(
            viewData.departureCaravanId,
            market);
    }

    private TradePrepareUIManager.CargoConfig BuildDetachedCargoConfig(
        CaravanLoadSettingViewData viewData)
    {
        if (viewData == null
            || string.IsNullOrWhiteSpace(viewData.caravanId)
            || string.IsNullOrWhiteSpace(viewData.currentTownId)
            || marketTradePanel == null
            || runtimeContext == null)
        {
            return default;
        }

        ND.Framework.FrameworkRoot root = ND.Framework.FrameworkRoot.Instance;
        if (root?.SharedGameData == null
            || !root.SharedGameData.TryGetTown(
                viewData.currentTownId,
                out ND.Framework.SharedTownDefinition town))
        {
            return default;
        }

        MarketData market = runtimeContext.GetAvailableMarkets()
            .FirstOrDefault(candidate => candidate != null
                && string.Equals(candidate.MarketId, town.MarketId, StringComparison.Ordinal));
        marketTradePanel.Configure(
            market,
            Mathf.Max(0f, viewData.maxLoad),
            Math.Max(0, viewData.maxInventorySlotCount));
        if (!marketTradePanel.OpenForCaravanPreparation(viewData.caravanId, market))
            return default;

        MarketTradePanelModel marketModel = marketTradePanel.Model;
        MarketTradeItemState[] marketItems = marketModel.Items
            .Where(item => item != null && item.Item != null)
            .ToArray();
        return new TradePrepareUIManager.CargoConfig
        {
            automaticCargoLoading = false,
            restoreOwnedCargo = true,
            caravanId = marketModel.CaravanId,
            marketId = marketModel.MarketId,
            gold = marketModel.TradingCurrency,
            maxLoad = marketModel.MaximumCargoWeight,
            requiredFood = 0,
            shopItems = marketItems.Select(item => item.Item).ToArray(),
            stocks = marketItems.Select(item => Math.Max(0, item.MarketStock)).ToArray(),
            buyUnitPrices = marketItems.Select(item => Math.Max(0L, item.BuyUnitPrice)).ToArray(),
            selectedItems = BuildOwnedCargoSelection(viewData.plannedItems)
        };
    }

    private void HandleMarketInventoryChanged(string marketId, int _, bool stockChanged)
    {
        if (!stockChanged)
            return;

        // The panel controller refreshes only its market stock/price state. Rebuilding the
        // entire Cargo view here would discard the current UI working draft.
        if (marketTradePanel?.Model != null
            && string.Equals(
                marketTradePanel.Model.MarketId,
                marketId,
                StringComparison.Ordinal))
        {
            marketTradePanel.RefreshIfMarketInventoryChanged();
        }
    }



    private void HandleFrameworkDataReady(ND.Framework.ISharedGameDataProvider _)
    {
        RefreshCargoMarketAfterFrameworkLoad();
    }

    private void HandleFrameworkDataReady(ND.Framework.SaveData _)
    {
        RefreshCargoMarketAfterFrameworkLoad();
    }

    private void RefreshCargoMarketAfterFrameworkLoad()
    {
        EnsureMarketTradePanel();
        if (runtimeContext != null)
            marketTradePanel.ConfigureCatalog(runtimeContext.GetAvailableMarkets());
        uiManager?.RefreshCargoIfVisible();
    }

    private static bool IsFrameworkMarketReady()
    {
        ND.Framework.FrameworkRoot root = ND.Framework.FrameworkRoot.Instance;
        return root != null
            && root.CurrentSaveData != null
            && root.SaveService != null
            && root.GameTime != null
            && root.SharedGameData != null
            && root.SharedGameData.IsLoaded;
    }

    private void HandleCargoLoadChanged(CargoLoadingPanelController.CargoChangeSnapshot snapshot)
    {
        if (snapshot == null
            || string.IsNullOrWhiteSpace(snapshot.caravanId)
            || string.IsNullOrWhiteSpace(snapshot.marketId)
            || marketTradePanel == null)
            return;

        MarketTradePanelModel model = marketTradePanel.Model;
        if (model == null
            || !string.Equals(model.CaravanId, snapshot.caravanId, StringComparison.Ordinal)
            || !string.Equals(model.MarketId, snapshot.marketId, StringComparison.Ordinal))
        {
            if (uiManager != null && uiManager.IsDetachedCaravanCargoEditOpen)
                return;
            if (!TryOpenPreparationMarket())
                return;
            model = marketTradePanel.Model;
        }

        if (model == null
            || !string.Equals(model.CaravanId, snapshot.caravanId, StringComparison.Ordinal)
            || !string.Equals(model.MarketId, snapshot.marketId, StringComparison.Ordinal))
        {
            Debug.LogWarning($"[TradePrepare Market] Ignored stale Cargo snapshot for {snapshot.marketId}/{snapshot.caravanId}.", this);
            return;
        }

        List<MarketTransactionLine> lines = CargoMarketTransactionDeltaBuilder.Build(
            snapshot.items,
            model.Items,
            allowSell: false);
        marketTradePanel.CancelDraft();
        foreach (MarketTransactionLine line in lines)
        {
            marketTradePanel.SetBuyDraft(line.ItemId, line.BuyQuantity);
            marketTradePanel.SetSellDraft(line.ItemId, line.SellQuantity);
        }

        // UI Working Draft only. Saved Cargo changes exclusively in TryCommitCargoTransaction.
        cargoPanel?.SetCargoTransactionError(
            model.HasDraft && !model.CanCommit ? model.DraftValidationError : string.Empty);
    }

    private void HandleCaravanCompositionConfirmed()
    {
        RecordActivity(ND.Framework.CaravanActivityLogType.TransportConfirmed);
    }

    private void HandleCargoConfirmed(string caravanId)
    {
        RecordActivity(
            ND.Framework.CaravanActivityLogType.CargoConfirmed,
            caravanId);
    }

    private void RecordActivity(
        ND.Framework.CaravanActivityLogType eventType,
        string requestedCaravanId = null)
    {
        ND.Framework.FrameworkRoot root = ND.Framework.FrameworkRoot.Instance;
        string caravanId = string.IsNullOrWhiteSpace(requestedCaravanId)
            ? runtimeContext?.CurrentViewData?.departureCaravanId
            : requestedCaravanId;
        if (root == null || string.IsNullOrWhiteSpace(caravanId))
        {
            return;
        }

        ND.Framework.CaravanActivityLog.TryAddAndSave(
            root.CurrentSaveData,
            root.SaveService,
            eventType,
            caravanId);
    }

    private bool CanCommitCargoTransaction()
    {
        MarketTradePanelModel model =
        marketTradePanel != null ? marketTradePanel.Model : null;

        // Can... 검사는 UI 가능 여부만 반환한다.
        // 실제 실패 Notice는 TryCommitCargoTransaction에서만 표시한다.
        return model != null
            && (!model.HasDraft || model.CanCommit);
    }

    private long GetProjectedCurrency()
    {
        if (marketTradePanel?.Model != null)
            return Math.Max(0L, marketTradePanel.Model.ProjectedTradingCurrency);

        // S4 can be saved before the normal Market panel creates a transaction model. In that
        // path the Runtime Draft already projects the selected Caravan plan against authoritative
        // tradingCurrency, so Mercenary must use the same post-cargo budget instead of showing 0.
        return Math.Max(0L, runtimeContext?.CurrentViewData?.estimatedCurrencyAfterPurchase ?? 0L);
    }

    private bool TryCommitCargoTransaction()
    {
        if (isPurchasingCargo)
            return false;

        MarketTradePanelModel model = marketTradePanel != null ? marketTradePanel.Model : null;
        if (model == null)
        {
            cargoPanel?.SetCargoTransactionError(MarketInventoryMutationSession.ErrorInvalidFramework);
            departureWarning?.Show(GetCargoNoticeMessage(MarketInventoryMutationSession.ErrorInvalidFramework));
            return false;
        }

        // No new item was selected. Closing the independent Cargo UI does not require a save.
        if (!model.HasDraft)
            return true;

        if (!model.CanCommit)
        {
            string errorCode = model.DraftValidationError;
            cargoPanel?.SetCargoTransactionError(errorCode);
            departureWarning?.Show(GetCargoNoticeMessage(errorCode));
            return false;
        }

        isPurchasingCargo = true;
        try
        {
            MarketTransactionResult result = marketTradePanel.Commit();
            if (result == null || !result.Success)
            {
                string errorCode = result?.ErrorCode
                    ?? MarketInventoryMutationSession.ErrorInvalidTransaction;
                cargoPanel?.SetCargoTransactionError(errorCode);
                departureWarning?.Show(GetCargoNoticeMessage(errorCode));
                Debug.LogError($"[TradePrepare Market] Cargo purchase failed: {errorCode}", this);
                return false;
            }

            runtimeContext?.RecordPurchaseDelta(model.CaravanId, result);

            // Market commit is authoritative for currency, stock, and Cargo SaveData.
            // Rebuild every downstream view from Saved Cargo instead of reusing the UI snapshot.
            CaravanCargoDraftStore.Clear(model.MarketId, model.CaravanId);
            runtimeContext?.ClearCargoDraft();
            if (runtimeContext != null)
            {
                // Refresh the selected Caravan's Cargo provider before rebuilding presentation.
                // RefreshFromCurrentSaveData alone only rebuilds the existing Flow Draft.
                runtimeContext.RefreshCaravanCargoPlan(model.CaravanId);
                runtimeContext.RefreshFromCurrentSaveData();
            }
            Debug.Log(
                $"[TradePrepare Market] Cargo purchased. Cost={result.PurchaseCost}, " +
                $"Currency={result.TradingCurrencyAfter}",
                this);
            return true;
        }
        finally
        {
            isPurchasingCargo = false;
        }
    }

    private static string GetCargoNoticeMessage(string errorCode)
    {
        switch (errorCode)
        {
            case MarketInventoryMutationSession.ErrorInsufficientStock:
                return "상점 재고가 부족합니다.";
            case MarketInventoryMutationSession.ErrorCurrency:
                return "소지 골드가 부족합니다.";
            case MarketInventoryMutationSession.ErrorInsufficientCargo:
                return "판매할 화물 수량이 부족합니다.";
            case MarketInventoryMutationSession.ErrorCargoWeight:
                return "카라반의 최대 적재 무게를 초과했습니다.";
            case MarketInventoryMutationSession.ErrorCargoSlots:
                return "카라반의 화물 슬롯이 부족합니다.";
            case MarketInventoryMutationSession.ErrorInvalidCaravan:
                return "선택한 카라반을 찾을 수 없습니다.";
            case MarketInventoryMutationSession.ErrorInvalidCatalog:
                return "현재 상점의 상품 정보를 불러올 수 없습니다.";
            case MarketInventoryMutationSession.ErrorSaveFailed:
                return "거래 내용을 저장하지 못했습니다.";
            case MarketInventoryMutationSession.ErrorInvalidFramework:
                return "저장 데이터 또는 거래 서비스가 준비되지 않았습니다.";
            default:
                return "현재 화물 적재 요청을 처리할 수 없습니다.";
        }
    }

    private void HandleMarketErrorChanged(string errorCode)
    {
        cargoPanel?.SetCargoTransactionError(errorCode);
    }

    private void CancelCargoTransactionDraft()
    {
        if (isPurchasingCargo)
            return;

        // Closing a Cargo edit ends the whole UI purchase session. A later opening rebuilds
        // Saved Cargo and capacity from the requested Caravan ID.
        marketTradePanel?.Close();
    }








    private static long ReadCurrentTradingCurrency()
    {
        ND.Framework.SaveData saveData = ND.Framework.FrameworkRoot.Instance?.CurrentSaveData;
        return saveData?.player != null ? Math.Max(0L, saveData.player.tradingCurrency) : 0L;
    }

    private static TradeItemViewData CreateCargoViewData(MarketTradeItemState item)
    {
        return new TradeItemViewData
        {
            itemId = item.ItemId,
            displayName = item.Item.DisplayName,
            icon = item.Item.Icon,
            purchasePrice = item.BuyUnitPrice,
            sellPrice = item.SellUnitPrice,
            ownedAmount = Math.Max(0, item.CargoQuantity),
            contentQuantityLimit = Math.Max(0, item.MarketStock),
            hasAuthoritativeStock = true,
            unitWeight = Math.Max(0f, item.Item.Weight),
            canBuy = item.MarketStock > 0,
            canSell = item.CargoQuantity > 0
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
        TownViewData destinationTown = null;
        if (selectedRoute != null && viewData.towns != null)
        {
            destinationTown = Array.Find(
                viewData.towns,
                town => town != null &&
                    string.Equals(town.townId, selectedRoute.toTownId, StringComparison.Ordinal));
        }

        return new TradeSummaryPanel.SummaryData
        {
            fromTown = string.IsNullOrWhiteSpace(fromTown) ? "-" : fromTown,
            toTown = string.IsNullOrWhiteSpace(toTown) ? "-" : toTown,
            destinationSprite = destinationTown != null ? destinationTown.icon : null,
            expectedRisk = Mathf.RoundToInt(
                Mathf.Clamp01(viewData.eventOccurrenceProbability) * 100f),
            mercenaryPower = Mathf.Max(0, viewData.selectedMercenaryPower),
            expectedFood = Mathf.Max(0, viewData.requiredDraftAnimalFoodQuantity),
            loadedFood = Mathf.Max(0, viewData.loadedDraftAnimalFoodQuantity),
            prepareCost = Math.Max(0L, viewData.totalPreparationCost),
            // Summary displays whole seconds. Preserve any positive sub-second test route as 1 second
            // instead of making a valid calculation look like a missing 00:00:00 value.
            durationSeconds = viewData.finalExpectedTravelTime > 0f
                ? Mathf.Ceil(viewData.finalExpectedTravelTime)
                : 0f
        };
    }

    private void HandleWagonSelected(TransportSelectPanel.TransportEntry wagon)
    {
        if (uiManager != null && uiManager.IsDetachedCaravanEditOpen)
            return;

        if (runtimeContext == null || !CanSelectWagon(runtimeContext.CurrentViewData, wagon.id))
            return;

        // Selecting a different wagon clears dependent animal and cargo choices in DraftStore.
        runtimeContext.SelectWagon(wagon.id);
        animalPanel?.RefreshAnimalAvailability(BuildAnimalEntries());
    }

    private void HandleWagonRemoved()
    {
        if (uiManager != null && uiManager.IsDetachedCaravanEditOpen)
            return;

        if (runtimeContext != null)
            runtimeContext.SelectWagon(string.Empty);
    }

    private void HandleAnimalSelectionChanged(
        IReadOnlyList<AnimalInventoryPanel.AnimalPick> picks,
        bool isValid)
    {
        if (uiManager != null && uiManager.IsDetachedCaravanEditOpen)
            return;

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


    private static TradeItemViewData[] BuildOwnedCargoSelection(
        IReadOnlyList<CargoItemViewData> loadedItems)
    {
        var result = new List<TradeItemViewData>();
        foreach (CargoItemViewData item in loadedItems ?? Array.Empty<CargoItemViewData>())
        {
            if (item == null || item.quantity <= 0 || string.IsNullOrWhiteSpace(item.itemId))
                continue;

            string itemId = item.itemId.Trim();

            result.Add(new TradeItemViewData
            {
                itemId = itemId,
                displayName = item.displayName ?? string.Empty,
                icon = item.icon,
                category = item.category,
                ownedAmount = item.quantity,
                unitWeight = item.unitWeight,
                purchasePrice = item.purchaseUnitPrice,
                sellPrice = item.estimatedSellUnitPrice
            });
        }

        return result.ToArray();
    }



}
