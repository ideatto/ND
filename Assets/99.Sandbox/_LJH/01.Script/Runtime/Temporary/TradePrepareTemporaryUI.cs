using System;
using UnityEngine;

// Replaceable IMGUI shell for exercising the trade preparation data cycle.
// It owns no business rules: every edit goes through TradePrepareFlowController.
public sealed class TradePrepareTemporaryUI : MonoBehaviour
{
    [Header("Temporary UI Data Sources")]
    [SerializeField] private TownData[] towns = new TownData[0];
    [SerializeField] private RouteData[] routes = new RouteData[0];
    [SerializeField] private TradeItemData[] tradeItems = new TradeItemData[0];
    [SerializeField] private WagonData[] wagons = new WagonData[0];
    [SerializeField] private DraftAnimalData[] draftAnimals = new DraftAnimalData[0];
    [SerializeField] private MercenaryData[] mercenaries = new MercenaryData[0];

    [Header("Temporary Window")]
    [SerializeField] private Rect windowRect = new Rect(20f, 20f, 560f, 760f);
    [SerializeField] private bool visible = true;

    private TradePrepareFlowController flowController;
    private InMemoryTradePrepareCommitSink commitSink;
    private TemporaryTradeSettlementService settlementService;
    private TradePrepareStartAdapter startAdapter;
    private Vector2 scrollPosition;
    private string activeTradeId = string.Empty;
    private string statusMessage = string.Empty;
    private long temporaryCurrency;
    private bool hasTemporaryCurrency;
    private int windowId;

    public TradePrepareFlowController FlowController => flowController;

    public void ConfigureData(
        TownData[] townData,
        RouteData[] routeData,
        TradeItemData[] itemData,
        WagonData[] wagonData,
        DraftAnimalData[] animalData,
        MercenaryData[] mercenaryData)
    {
        towns = townData ?? new TownData[0];
        routes = routeData ?? new RouteData[0];
        tradeItems = itemData ?? new TradeItemData[0];
        wagons = wagonData ?? new WagonData[0];
        draftAnimals = animalData ?? new DraftAnimalData[0];
        mercenaries = mercenaryData ?? new MercenaryData[0];
    }

    private void Awake()
    {
        windowId = GetHashCode();
        InitializeCycle();
    }

    private void OnDestroy()
    {
        flowController?.Dispose();
    }

    private void OnGUI()
    {
        if (!visible)
        {
            return;
        }

        windowRect = GUILayout.Window(windowId, windowRect, DrawWindow, "Trade Prepare - Temporary UI");
    }

    private void InitializeCycle()
    {
        flowController?.Dispose();

        ND.Framework.SaveData saveData = ND.Framework.FrameworkRoot.Instance != null
            ? ND.Framework.FrameworkRoot.Instance.CurrentSaveData
            : null;

        var context = new TradePrepareBuildContext
        {
            // Read-only input. This temporary UI never creates or mutates SaveData.
            saveData = saveData,
            towns = towns ?? new TownData[0],
            routes = routes ?? new RouteData[0],
            tradeItems = tradeItems ?? new TradeItemData[0],
            wagons = wagons ?? new WagonData[0],
            draftAnimals = draftAnimals ?? new DraftAnimalData[0],
            mercenaries = mercenaries ?? new MercenaryData[0]
        };

        commitSink = new InMemoryTradePrepareCommitSink();
        settlementService = new TemporaryTradeSettlementService(commitSink);
        startAdapter = new TradePrepareStartAdapter(
            new TemporaryTradePrepareStartGateway(),
            new TradePrepareViewDataBuilder(),
            commitSink);
        flowController = new TradePrepareFlowController(context);

        string currentTownId = saveData != null && saveData.player != null
            ? saveData.player.currentTownId
            : FirstTownId();
        flowController.Initialize(currentTownId);

        temporaryCurrency = flowController.CurrentViewData != null
            ? flowController.CurrentViewData.currentTradingCurrency
            : 0L;
        hasTemporaryCurrency = true;
        activeTradeId = string.Empty;
        statusMessage = saveData != null
            ? "Temporary cycle ready. SaveData is read-only."
            : "Framework SaveData is unavailable. Start a game before testing selections.";
    }

    private void DrawWindow(int windowId)
    {
        if (flowController == null)
        {
            if (GUILayout.Button("Initialize"))
            {
                InitializeCycle();
            }

            GUI.DragWindow();
            return;
        }

        TradePrepareViewData viewData = flowController.CurrentViewData;
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        DrawSummary(viewData);
        DrawRoutes(viewData);
        DrawWagons(viewData);
        DrawAnimals(viewData);
        DrawCargo(viewData);
        DrawDraftAnimalFoodSummary(viewData);
        DrawMercenaries(viewData);
        DrawValidation(viewData);
        DrawCycleActions(viewData);

        GUILayout.EndScrollView();
        GUI.DragWindow(new Rect(0f, 0f, windowRect.width, 24f));
    }

    private void DrawSummary(TradePrepareViewData viewData)
    {
        GUILayout.Label("Current town: " + Safe(viewData != null ? viewData.currentTownName : null));
        GUILayout.Label("Framework currency (read-only): " + (viewData != null ? viewData.currentTradingCurrency : 0L));
        GUILayout.Label("Temporary settlement currency: " + (hasTemporaryCurrency ? temporaryCurrency : 0L));
        if (viewData != null)
        {
            GUILayout.Label($"Load: {viewData.currentLoad:0.##} / {viewData.maxLoad:0.##}  Slots: {viewData.usedInventorySlotCount} / {viewData.maxInventorySlotCount}");
            GUILayout.Label($"Purchase: {viewData.totalPurchaseCost}  Mercenary: {viewData.mercenaryCost}  Expected revenue: {viewData.estimatedSellRevenue}");
        }

        GUILayout.Space(6f);
    }

    private void DrawRoutes(TradePrepareViewData viewData)
    {
        GUILayout.Label("Routes");
        RouteViewData[] values = viewData != null ? viewData.routes : null;
        for (int index = 0; values != null && index < values.Length; index++)
        {
            RouteViewData route = values[index];
            if (route == null) continue;

            bool selected = string.Equals(viewData.selectedRouteId, route.routeId, StringComparison.Ordinal);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = !flowController.IsCommitted && route.canSelect;
            if (GUILayout.Button((selected ? "[Selected] " : string.Empty) + route.displayName))
            {
                flowController.SelectDestination(route.toTownId);
                flowController.SelectRoute(route.routeId);
            }
            GUI.enabled = oldEnabled;
        }
        GUILayout.Space(6f);
    }

    private void DrawWagons(TradePrepareViewData viewData)
    {
        GUILayout.Label("Wagons");
        WagonViewData[] values = viewData != null ? viewData.wagons : null;
        for (int index = 0; values != null && index < values.Length; index++)
        {
            WagonViewData wagon = values[index];
            if (wagon == null) continue;

            bool selected = string.Equals(viewData.selectedWagonId, wagon.wagonId, StringComparison.Ordinal);
            bool oldEnabled = GUI.enabled;
            GUI.enabled = !flowController.IsCommitted && wagon.canSelect;
            if (GUILayout.Button((selected ? "[Selected] " : string.Empty) + wagon.displayName
                + $"  Durability {wagon.currentDurability}/{wagon.maxDurability}"))
            {
                flowController.SelectWagon(wagon.wagonId);
            }
            GUI.enabled = oldEnabled;
        }
        GUILayout.Space(6f);
    }

    private void DrawAnimals(TradePrepareViewData viewData)
    {
        GUILayout.Label("Draft animals");
        DraftAnimalViewData[] values = viewData != null ? viewData.draftAnimals : null;
        for (int index = 0; values != null && index < values.Length; index++)
        {
            DraftAnimalViewData animal = values[index];
            if (animal == null) continue;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{animal.displayName}  {animal.selectedAmount}/{animal.ownedAmount}");
            bool oldEnabled = GUI.enabled;
            GUI.enabled = !flowController.IsCommitted && animal.selectedAmount > 0;
            if (GUILayout.Button("-", GUILayout.Width(36f)))
                flowController.SetAnimalQuantity(animal.draftAnimalId, animal.selectedAmount - 1);
            GUI.enabled = !flowController.IsCommitted && animal.canSelect
                && animal.selectedAmount < animal.maxSelectableAmount;
            if (GUILayout.Button("+", GUILayout.Width(36f)))
                flowController.SetAnimalQuantity(animal.draftAnimalId, animal.selectedAmount + 1);
            GUI.enabled = oldEnabled;
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(6f);
    }

    private void DrawCargo(TradePrepareViewData viewData)
    {
        GUILayout.Label("Cargo to buy (all purchased items are sold at the destination)");
        TradeItemViewData[] values = viewData != null ? viewData.tradeItems : null;
        for (int index = 0; values != null && index < values.Length; index++)
        {
            TradeItemViewData item = values[index];
            if (item == null) continue;

            GUILayout.BeginHorizontal();
            GUILayout.Label($"{item.displayName}  Buy {item.selectedBuyAmount}  Price {item.purchasePrice}");
            bool oldEnabled = GUI.enabled;
            GUI.enabled = !flowController.IsCommitted && item.selectedBuyAmount > 0;
            if (GUILayout.Button("-", GUILayout.Width(36f)))
                flowController.SetBuyItemQuantity(item.itemId, item.selectedBuyAmount - 1);
            GUI.enabled = !flowController.IsCommitted && item.canBuy;
            if (GUILayout.Button("+", GUILayout.Width(36f)))
                flowController.SetBuyItemQuantity(item.itemId, item.selectedBuyAmount + 1);
            GUI.enabled = oldEnabled;
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(6f);
    }

    private void DrawDraftAnimalFoodSummary(TradePrepareViewData viewData)
    {
        int quantity = viewData != null ? viewData.loadedDraftAnimalFoodQuantity : 0;
        int required = viewData != null ? viewData.requiredDraftAnimalFoodQuantity : 0;
        GUILayout.Label($"Draft animal food: {quantity} / required {required} (buy it from the cargo list)");
        GUILayout.Space(6f);
    }

    private void DrawMercenaries(TradePrepareViewData viewData)
    {
        GUILayout.Label("Mercenaries (unique selection)");
        MercenaryViewData[] values = viewData != null ? viewData.mercenaries : null;
        for (int index = 0; values != null && index < values.Length; index++)
        {
            MercenaryViewData mercenary = values[index];
            if (mercenary == null) continue;

            bool oldEnabled = GUI.enabled;
            GUI.enabled = !flowController.IsCommitted && (mercenary.isSelected || mercenary.canHire);
            if (GUILayout.Button((mercenary.isSelected ? "[Hired] " : string.Empty)
                + $"{mercenary.displayName}  Power {mercenary.combatCapability}  Cost {mercenary.baseBuyPrice}"))
            {
                if (mercenary.isSelected) flowController.DeselectMercenary(mercenary.mercenaryId);
                else flowController.SelectMercenary(mercenary.mercenaryId);
            }
            GUI.enabled = oldEnabled;
        }
        GUILayout.Space(6f);
    }

    private void DrawValidation(TradePrepareViewData viewData)
    {
        TradePrepareConditionResult condition = viewData != null ? viewData.startCondition : null;
        GUILayout.Label("Validation: " + (condition != null && condition.canStart ? "Can start" : "Blocked"));
        if (condition != null && !condition.canStart)
            GUILayout.Label("Reason: " + condition.disabledReason);
        if (condition != null && condition.warningMessages != null)
        {
            for (int index = 0; index < condition.warningMessages.Count; index++)
                GUILayout.Label("Warning: " + condition.warningMessages[index]);
        }
        GUILayout.Space(6f);
    }

    private void DrawCycleActions(TradePrepareViewData viewData)
    {
        bool oldEnabled = GUI.enabled;
        GUI.enabled = !flowController.IsCommitted
            && viewData != null
            && viewData.startCondition != null
            && viewData.startCondition.canStart;
        if (GUILayout.Button("Temporary Start"))
        {
            activeTradeId = "temporary-" + Guid.NewGuid().ToString("N");
            TradePrepareStartResult result = flowController.TryStartTrade(
                startAdapter,
                activeTradeId,
                false);
            statusMessage = result.succeeded
                ? "Departure staged. Currency has not changed."
                : result.errorCode + ": " + result.errorMessage;
        }

        GUI.enabled = flowController.IsCommitted && !string.IsNullOrEmpty(activeTradeId);
        if (GUILayout.Button("Temporary Settlement Claim"))
        {
            TradePrepareDraft committedDraft = flowController.CurrentDraft;
            string settlementTownId = ResolveSettlementTownId(committedDraft, viewData);
            TemporaryTradeSettlementResult result = settlementService.TryClaim(activeTradeId, temporaryCurrency);
            if (result.succeeded)
            {
                temporaryCurrency = result.currencyAfter;
                activeTradeId = string.Empty;
                flowController.ResetAfterSettlement(settlementTownId);
                statusMessage = $"Claimed once: {result.currencyBefore} -> {result.currencyAfter}";
            }
            else
            {
                statusMessage = result.errorCode + ": " + result.errorMessage;
            }
        }

        GUI.enabled = true;
        if (GUILayout.Button("Reset Temporary Cycle"))
        {
            InitializeCycle();
        }
        GUI.enabled = oldEnabled;

        if (!string.IsNullOrEmpty(statusMessage))
            GUILayout.Label(statusMessage);
    }

    private static string ResolveSettlementTownId(
        TradePrepareDraft draft,
        TradePrepareViewData viewData)
    {
        if (draft != null && !string.IsNullOrEmpty(draft.selectedDestinationTownId))
        {
            return draft.selectedDestinationTownId;
        }

        if (viewData != null && viewData.routes != null)
        {
            for (int index = 0; index < viewData.routes.Length; index++)
            {
                RouteViewData route = viewData.routes[index];
                if (route != null && string.Equals(route.routeId, viewData.selectedRouteId, StringComparison.Ordinal))
                {
                    return route.toTownId ?? string.Empty;
                }
            }
        }

        return viewData != null ? viewData.currentTownId ?? string.Empty : string.Empty;
    }

    private string FirstTownId()
    {
        for (int index = 0; towns != null && index < towns.Length; index++)
        {
            if (towns[index] != null && !string.IsNullOrWhiteSpace(towns[index].TownId))
                return towns[index].TownId;
        }

        return string.Empty;
    }

    private static string Safe(string value)
    {
        return string.IsNullOrEmpty(value) ? "(none)" : value;
    }
}
