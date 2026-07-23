using System.Collections.Generic;
using UnityEngine;

public class DataViewDataSmokeTest : MonoBehaviour
{
    [SerializeField] private TownData town;
    [SerializeField] private RouteData route;
    [SerializeField] private TradeItemData item;

    [ContextMenu("Run ViewData Smoke Test")]
    private void RunViewDataSmokeTest()
    {
        if (!ValidateRequiredAssets())
        {
            return;
        }

        // Keeps each feature contract isolated so a failure identifies the affected data path.
        AssertTradePrepareDraftBehavior();
        AssertTradePrepareFlowViewData();
        AssertTradeResultData();
        AssertCurrencyProjectionScenarios();
        AssertCaravanOverviewViewData();
        AssertSaveDataCaravanOverviewProvider(item);
        AssertCaravanSettingServices(item);

        Debug.Log("ViewData Smoke Test PASSED.");
    }

    // Verifies the scene references required by the legacy TradePrepare smoke scenarios.
    private bool ValidateRequiredAssets()
    {
        if (town == null || route == null || item == null)
        {
            Debug.LogError("Smoke Test failed: town, route, or item is not assigned.");
            return false;
        }

        if (route.FromTown == null || route.ToTown == null)
        {
            Debug.LogError("Smoke Test failed: route FromTown or ToTown is not assigned.");
            return false;
        }

        return true;
    }

    // Verifies Draft mutation, snapshot isolation, duplicate prevention, and cancellation rules.
    private void AssertTradePrepareDraftBehavior()
    {
        var prepareDraft = new TradePrepareDraft
        {
            departureCaravanId = "smoke-caravan-prepare",
            currentTownId = town.TownId,
            selectedDestinationTownId = route.ToTownId,
            selectedRouteId = route.RouteId
        };

        prepareDraft.selectedAnimals.Add(new DraftAnimalSelectionData
        {
            draftAnimalId = "smoke-animal",
            quantity = 1
        });

        var firstMercenarySelection = prepareDraft.SelectMercenary("smoke-mercenary");
        var duplicateMercenarySelection = prepareDraft.SelectMercenary("smoke-mercenary");

        var draftChangeCount = 0;
        var draftStore = new TradePrepareDraftStore();
        draftStore.DraftChanged += _ => draftChangeCount++;
        draftStore.Reset(town.TownId);
        var storeResetTownMatched = draftStore.Current.currentTownId == town.TownId;
        var storeResetHasNoCaravan = string.IsNullOrEmpty(draftStore.Current.departureCaravanId);
        draftStore.SelectDepartureCaravan("smoke-caravan-prepare");
        var storeSelectedCaravanMatched = draftStore.Current.departureCaravanId == "smoke-caravan-prepare";
        var mutableDraftSnapshot = draftStore.Current;
        mutableDraftSnapshot.selectedBuyItems.Add(new TradeItemBundle { itemId = "snapshot-only", quantity = 1 });
        var storeSnapshotIsolated = draftStore.Current.selectedBuyItems.Count == 0;

        draftStore.SelectDestination(route.ToTownId);
        draftStore.SelectRoute(route.RouteId);
        draftStore.SelectWagon("smoke-wagon-a");
        draftStore.SetAnimalQuantity("smoke-animal", 1);
        draftStore.SetAnimalQuantity("smoke-animal", 2);
        var storeAnimalSelectionUpdated = draftStore.Current.selectedAnimals.Count == 1
            && draftStore.Current.selectedAnimals[0].quantity == 2;

        draftStore.SetBuyItemQuantity(item.ItemId, 1);
        draftStore.SetBuyItemQuantity(item.ItemId, 3);
        var storeItemSelectionUpdated = draftStore.Current.selectedBuyItems.Count == 1
            && draftStore.Current.selectedBuyItems[0].quantity == 3;
        draftStore.SetBuyItemQuantity(item.ItemId, 0);
        var storeZeroQuantityRemovedItem = draftStore.Current.selectedBuyItems.Count == 0;

        draftStore.SelectMercenary("smoke-mercenary");
        draftStore.SelectMercenary("smoke-mercenary");
        var storePreventedDuplicateMercenary = draftStore.Current.SelectedMercenaryIds.Count == 1;

        draftStore.SetBuyItemQuantity(item.ItemId, 1);
        draftStore.SelectWagon("smoke-wagon-b");
        var storeClearedAnimalsAfterWagonChange = draftStore.Current.selectedAnimals.Count == 0;
        var storeClearedCargoAfterWagonChange = draftStore.Current.selectedBuyItems.Count == 0;

        draftStore.Cancel();
        var storeCancelClearedDraft = string.IsNullOrEmpty(draftStore.Current.currentTownId)
            && string.IsNullOrEmpty(draftStore.Current.departureCaravanId)
            && draftStore.Current.SelectedMercenaryIds.Count == 0;

        Debug.Assert(firstMercenarySelection, "Draft Smoke Test failed: first mercenary selection should succeed.");
        Debug.Assert(!duplicateMercenarySelection, "Draft Smoke Test failed: duplicate mercenary selection should fail.");
        Debug.Assert(prepareDraft.SelectedMercenaryIds.Count == 1, "Draft Smoke Test failed: duplicate mercenary ID should not be stored.");
        Debug.Assert(storeResetTownMatched, "Draft Store Smoke Test failed: Reset should set currentTownId.");
        Debug.Assert(storeResetHasNoCaravan, "Draft Store Smoke Test failed: opening preparation must not inherit Overview focus.");
        Debug.Assert(storeSelectedCaravanMatched, "Draft Store Smoke Test failed: SelectDepartureCaravan should set the departure Caravan inside the Draft.");
        Debug.Assert(storeSnapshotIsolated, "Draft Store Smoke Test failed: modifying a snapshot should not mutate the stored draft.");
        Debug.Assert(storeAnimalSelectionUpdated, "Draft Store Smoke Test failed: animal selection should update without duplicates.");
        Debug.Assert(storeItemSelectionUpdated, "Draft Store Smoke Test failed: item selection should update without duplicates.");
        Debug.Assert(storeZeroQuantityRemovedItem, "Draft Store Smoke Test failed: zero item quantity should remove the selection.");
        Debug.Assert(storePreventedDuplicateMercenary, "Draft Store Smoke Test failed: mercenary IDs should not be duplicated.");
        Debug.Assert(storeClearedAnimalsAfterWagonChange, "Draft Store Smoke Test failed: changing wagon should clear animal selections.");
        Debug.Assert(storeClearedCargoAfterWagonChange, "Draft Store Smoke Test failed: changing wagon should clear cargo selections.");
        Debug.Assert(storeCancelClearedDraft, "Draft Store Smoke Test failed: Cancel should clear the current draft.");
        Debug.Assert(draftChangeCount > 0, "Draft Store Smoke Test failed: draft changes should raise DraftChanged.");
    }

    // Preserves the existing TradePrepare condition and ViewData projection coverage.
    private void AssertTradePrepareFlowViewData()
    {
        var saveData = new SaveData();
        saveData.player.currentTownId = town.TownId;

        if (town.UnlockedByDefault)
            saveData.world.unlockedTownIds.Add(town.TownId);

        if (route.UnlockedByDefault)
            saveData.world.unlockedRouteIds.Add(route.RouteId);

        var townViewData = new TownViewData
        {
            townId = town.TownId,
            displayName = town.DisplayName,
            icon = town.Icon,
            description = town.Description,
            isUnlocked = saveData.world.unlockedTownIds.Contains(town.TownId),
            isCurrentTown = saveData.player.currentTownId == town.TownId,
            canSelect = saveData.world.unlockedTownIds.Contains(town.TownId),
            disabledReason = string.Empty
        };

        var itemViewData = new TradeItemViewData
        {
            itemId = item.ItemId,
            displayName = item.DisplayName,
            icon = item.Icon,
            description = item.Description,
            rarity = item.Rarity,
            category = item.Category,
            purchasePrice = item.BaseBuyPrice,
            sellPrice = item.BaseSellPrice,
            ownedAmount = 0,
            selectedBuyAmount = 0,
            selectedSellAmount = 0,
            contentQuantityLimit = item.MaxCount,
            hasAuthoritativeStock = false,
            unitWeight = item.Weight,
            selectedWeight = 0f,
            canBuy = true,
            canSell = false,
            buyDisabledReason = string.Empty,
            sellDisabledReason = "No owned item."
        };

        var cargoItemViewData = new CargoItemViewData
        {
            itemId = item.ItemId,
            displayName = item.DisplayName,
            icon = item.Icon,
            quantity = 1,
            unitWeight = item.Weight,
            totalWeight = item.Weight,
            purchaseUnitPrice = item.BaseBuyPrice,
            totalPurchasePrice = item.BaseBuyPrice
        };

        var routeViewData = new RouteViewData
        {
            routeId = route.RouteId,
            displayName = route.DisplayName,
            fromTownId = route.FromTownId,
            fromTownName = route.FromTownName,
            toTownId = route.ToTownId,
            toTownName = route.ToTownName,
            distance = route.Distance,
            estimatedTime = route.DefaultElapsedTime,
            requiredDraftAnimalFoodQuantity = route.BaseRequiredDraftAnimalFoodQuantity,
            requiredMercenaryPower = route.BaseRequiredMercenaryPower,
            riskLevel = route.BaseRiskLevel,
            isUnlocked = saveData.world.unlockedRouteIds.Contains(route.RouteId),
            canSelect = saveData.world.unlockedRouteIds.Contains(route.RouteId),
            disabledReason = string.Empty
        };

        var lockedRouteViewData = new RouteViewData
        {
            routeId = routeViewData.routeId,
            displayName = routeViewData.displayName,
            fromTownId = routeViewData.fromTownId,
            fromTownName = routeViewData.fromTownName,
            toTownId = routeViewData.toTownId,
            toTownName = routeViewData.toTownName,
            distance = routeViewData.distance,
            estimatedTime = routeViewData.estimatedTime,
            requiredDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower,
            riskLevel = routeViewData.riskLevel,
            isUnlocked = false,
            canSelect = false,
            disabledReason = "Route is not unlocked yet."
        };

        var conditionEvaluator = new TradePrepareConditionEvaluator();

        var successInput = new TradePrepareConditionInput
        {
            isDepartureCaravanSelected = true,
            isRouteSelected = true,
            isRouteUnlocked = true,
            hasCargo = true,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            totalPreparationCost = itemViewData.purchasePrice,
            currentLoad = saveData.caravan.currentLoad,
            overloadLimit = saveData.caravan.maxLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var successCondition = conditionEvaluator.Evaluate(successInput);

        var notEnoughMoneyInput = new TradePrepareConditionInput
        {
            isDepartureCaravanSelected = true,
            isRouteSelected = true,
            isRouteUnlocked = true,
            hasCargo = true,
            currentTradingCurrency = 0,
            totalPurchaseCost = itemViewData.purchasePrice > 0 ? itemViewData.purchasePrice : 1,
            totalPreparationCost = itemViewData.purchasePrice > 0 ? itemViewData.purchasePrice : 1,
            currentLoad = saveData.caravan.currentLoad,
            overloadLimit = saveData.caravan.maxLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var notEnoughMoneyCondition = conditionEvaluator.Evaluate(notEnoughMoneyInput);

        var routeNotSelectedInput = new TradePrepareConditionInput
        {
            isDepartureCaravanSelected = true,
            isRouteSelected = false,
            isRouteUnlocked = true,
            hasCargo = true,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            totalPreparationCost = itemViewData.purchasePrice,
            currentLoad = saveData.caravan.currentLoad,
            overloadLimit = saveData.caravan.maxLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var routeNotSelectedCondition = conditionEvaluator.Evaluate(routeNotSelectedInput);

        var routeLockedInput = new TradePrepareConditionInput
        {
            isDepartureCaravanSelected = true,
            isRouteSelected = true,
            isRouteUnlocked = false,
            hasCargo = true,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            totalPreparationCost = itemViewData.purchasePrice,
            currentLoad = saveData.caravan.currentLoad,
            overloadLimit = saveData.caravan.maxLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var routeLockedCondition = conditionEvaluator.Evaluate(routeLockedInput);

        var notEnoughFoodInput = new TradePrepareConditionInput
        {
            isDepartureCaravanSelected = true,
            isRouteSelected = true,
            isRouteUnlocked = true,
            hasCargo = true,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            totalPreparationCost = itemViewData.purchasePrice,
            currentLoad = saveData.caravan.currentLoad,
            overloadLimit = saveData.caravan.maxLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedDraftAnimalFoodQuantity = 0,
            requiredDraftAnimalFoodQuantity = Mathf.Max(1, routeViewData.requiredDraftAnimalFoodQuantity),
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var notEnoughFoodCondition = conditionEvaluator.Evaluate(notEnoughFoodInput);

        var loadExceededInput = new TradePrepareConditionInput
        {
            isDepartureCaravanSelected = true,
            isRouteSelected = true,
            isRouteUnlocked = true,
            hasCargo = true,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            totalPreparationCost = itemViewData.purchasePrice,
            currentLoad = 120,
            overloadLimit = 80,
            maxLoad = 100,
            loadedDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var loadExceededCondition = conditionEvaluator.Evaluate(loadExceededInput);

        var multipleWarningInput = new TradePrepareConditionInput
        {
            isDepartureCaravanSelected = true,
            isRouteSelected = true,
            isRouteUnlocked = true,
            hasCargo = true,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            totalPreparationCost = itemViewData.purchasePrice,
            currentLoad = 90,
            overloadLimit = 80,
            maxLoad = 100,
            loadedDraftAnimalFoodQuantity = 0,
            requiredDraftAnimalFoodQuantity = Mathf.Max(1, routeViewData.requiredDraftAnimalFoodQuantity),
            selectedMercenaryPower = 0,
            requiredMercenaryPower = Mathf.Max(1, routeViewData.requiredMercenaryPower)
        };

        var multipleWarningCondition = conditionEvaluator.Evaluate(multipleWarningInput);

        var noCargoInput = new TradePrepareConditionInput
        {
            isDepartureCaravanSelected = true,
            isRouteSelected = true,
            isRouteUnlocked = true,
            hasCargo = false,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = 0,
            totalPreparationCost = 0,
            currentLoad = 0,
            overloadLimit = saveData.caravan.maxLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var noCargoCondition = conditionEvaluator.Evaluate(noCargoInput);

        var mixedDraftAnimalInput = new TradePrepareConditionInput
        {
            isDepartureCaravanSelected = true,
            isRouteSelected = true,
            isRouteUnlocked = true,
            isWagonRequired = true,
            isWagonSelected = true,
            isSelectedWagonOwned = true,
            currentWagonDurability = 100,
            selectedWagonType = WagonType.WagonWithAnimals,
            selectedDraftAnimalCount = 2,
            minRequiredDraftAnimalCount = 1,
            maxAllowedDraftAnimalCount = 2,
            selectedDraftAnimalTypes = new[] { DraftAnimalType.Donkey, DraftAnimalType.Horse },
            eligibleDraftAnimalTypes = new[] { DraftAnimalType.Donkey, DraftAnimalType.Horse },
            hasCargo = true,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            totalPreparationCost = itemViewData.purchasePrice,
            currentLoad = saveData.caravan.currentLoad,
            overloadLimit = saveData.caravan.maxLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var mixedDraftAnimalCondition = conditionEvaluator.Evaluate(mixedDraftAnimalInput);

        var inventorySlotExceededInput = new TradePrepareConditionInput
        {
            isDepartureCaravanSelected = true,
            isRouteSelected = true,
            isRouteUnlocked = true,
            hasCargo = true,
            usedInventorySlotCount = 2,
            maxInventorySlotCount = 1,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            totalPreparationCost = itemViewData.purchasePrice,
            currentLoad = saveData.caravan.currentLoad,
            overloadLimit = saveData.caravan.maxLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = routeViewData.requiredDraftAnimalFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var inventorySlotExceededCondition = conditionEvaluator.Evaluate(inventorySlotExceededInput);

        // Opening TradePrepareUI starts with no departure Caravan even when Overview has UI focus.
        var caravanNotSelectedCondition = conditionEvaluator.Evaluate(new TradePrepareConditionInput
        {
            isDepartureCaravanSelectionRequired = true,
            isDepartureCaravanSelected = false,
            isRouteSelected = true,
            isRouteUnlocked = true
        });

        var prepareViewData = new TradePrepareViewData
        {
            departureCaravanId = "smoke-caravan-prepare",
            currentTownId = town.TownId,
            currentTownName = town.DisplayName,

            currentTradingCurrency = successInput.currentTradingCurrency,
            currentDevelopmentCurrency = saveData.player.developmentCurrency,

            towns = new[] { townViewData },
            routes = new[] { routeViewData },
            tradeItems = new[] { itemViewData },
            loadedItems = new[] { cargoItemViewData },

            selectedRouteId = route.RouteId,

            currentLoad = successInput.currentLoad,
            overloadLimit = successInput.overloadLimit,
            maxLoad = successInput.maxLoad,
            usedInventorySlotCount = successInput.usedInventorySlotCount,
            maxInventorySlotCount = successInput.maxInventorySlotCount,

            totalPurchaseCost = successInput.totalPurchaseCost,
            totalPreparationCost = successInput.totalPreparationCost,

            loadedDraftAnimalFoodQuantity = successInput.loadedDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = successInput.requiredDraftAnimalFoodQuantity,

            selectedMercenaryPower = successInput.selectedMercenaryPower,
            requiredMercenaryPower = successInput.requiredMercenaryPower,

            startCondition = successCondition,
            baseExpectedTravelTime = routeViewData.estimatedTime,
            finalExpectedTravelTime = routeViewData.estimatedTime
        };

        var notEnoughMoneyPrepareViewData = new TradePrepareViewData
        {
            currentTownId = town.TownId,
            currentTownName = town.DisplayName,

            currentTradingCurrency = notEnoughMoneyInput.currentTradingCurrency,
            currentDevelopmentCurrency = saveData.player.developmentCurrency,

            towns = new[] { townViewData },
            routes = new[] { routeViewData },
            tradeItems = new[] { itemViewData },

            selectedRouteId = route.RouteId,

            currentLoad = notEnoughMoneyInput.currentLoad,
            overloadLimit = notEnoughMoneyInput.overloadLimit,
            maxLoad = notEnoughMoneyInput.maxLoad,

            totalPurchaseCost = notEnoughMoneyInput.totalPurchaseCost,
            totalPreparationCost = notEnoughMoneyInput.totalPreparationCost,

            loadedDraftAnimalFoodQuantity = notEnoughMoneyInput.loadedDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = notEnoughMoneyInput.requiredDraftAnimalFoodQuantity,

            selectedMercenaryPower = notEnoughMoneyInput.selectedMercenaryPower,
            requiredMercenaryPower = notEnoughMoneyInput.requiredMercenaryPower,

            startCondition = notEnoughMoneyCondition
        };

        var notEnoughFoodPrepareViewData = new TradePrepareViewData
        {
            currentTownId = town.TownId,
            currentTownName = town.DisplayName,

            currentTradingCurrency = notEnoughFoodInput.currentTradingCurrency,
            currentDevelopmentCurrency = saveData.player.developmentCurrency,

            towns = new[] { townViewData },
            routes = new[] { routeViewData },
            tradeItems = new[] { itemViewData },

            selectedRouteId = route.RouteId,

            currentLoad = notEnoughFoodInput.currentLoad,
            overloadLimit = notEnoughFoodInput.overloadLimit,
            maxLoad = notEnoughFoodInput.maxLoad,

            totalPurchaseCost = notEnoughFoodInput.totalPurchaseCost,
            totalPreparationCost = notEnoughFoodInput.totalPreparationCost,

            loadedDraftAnimalFoodQuantity = notEnoughFoodInput.loadedDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = notEnoughFoodInput.requiredDraftAnimalFoodQuantity,

            selectedMercenaryPower = notEnoughFoodInput.selectedMercenaryPower,
            requiredMercenaryPower = notEnoughFoodInput.requiredMercenaryPower,

            startCondition = notEnoughFoodCondition
        };

        var loadExceededPrepareViewData = new TradePrepareViewData
        {
            currentTownId = town.TownId,
            currentTownName = town.DisplayName,

            currentTradingCurrency = loadExceededInput.currentTradingCurrency,
            currentDevelopmentCurrency = saveData.player.developmentCurrency,

            towns = new[] { townViewData },
            routes = new[] { routeViewData },
            tradeItems = new[] { itemViewData },

            selectedRouteId = route.RouteId,

            currentLoad = loadExceededInput.currentLoad,
            overloadLimit = loadExceededInput.overloadLimit,
            maxLoad = loadExceededInput.maxLoad,

            totalPurchaseCost = loadExceededInput.totalPurchaseCost,
            totalPreparationCost = loadExceededInput.totalPreparationCost,

            loadedDraftAnimalFoodQuantity = loadExceededInput.loadedDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = loadExceededInput.requiredDraftAnimalFoodQuantity,

            selectedMercenaryPower = loadExceededInput.selectedMercenaryPower,
            requiredMercenaryPower = loadExceededInput.requiredMercenaryPower,

            startCondition = loadExceededCondition
        };

        var multipleWarningPrepareViewData = new TradePrepareViewData
        {
            currentTownId = town.TownId,
            currentTownName = town.DisplayName,

            currentTradingCurrency = multipleWarningInput.currentTradingCurrency,
            currentDevelopmentCurrency = saveData.player.developmentCurrency,

            towns = new[] { townViewData },
            routes = new[] { routeViewData },
            tradeItems = new[] { itemViewData },

            selectedRouteId = route.RouteId,

            currentLoad = multipleWarningInput.currentLoad,
            overloadLimit = multipleWarningInput.overloadLimit,
            maxLoad = multipleWarningInput.maxLoad,

            totalPurchaseCost = multipleWarningInput.totalPurchaseCost,
            totalPreparationCost = multipleWarningInput.totalPreparationCost,

            loadedDraftAnimalFoodQuantity = multipleWarningInput.loadedDraftAnimalFoodQuantity,
            requiredDraftAnimalFoodQuantity = multipleWarningInput.requiredDraftAnimalFoodQuantity,

            selectedMercenaryPower = multipleWarningInput.selectedMercenaryPower,
            requiredMercenaryPower = multipleWarningInput.requiredMercenaryPower,

            startCondition = multipleWarningCondition
        };


        Debug.Log($"Town: {townViewData.displayName} / Unlocked: {townViewData.isUnlocked} / Current: {townViewData.isCurrentTown} / CanSelect: {townViewData.canSelect}");
        Debug.Assert(townViewData.townId == town.TownId, "Smoke Test failed: townId mismatch.");
        Debug.Assert(townViewData.isCurrentTown, "Smoke Test failed: current town should be true.");

        Debug.Log($"Item: {itemViewData.displayName} / Buy: {itemViewData.purchasePrice} / Sell: {itemViewData.sellPrice}");
        Debug.Assert(itemViewData.itemId == item.ItemId, "Smoke Test failed: itemId mismatch.");
        Debug.Assert(itemViewData.purchasePrice >= 0 && itemViewData.sellPrice >= 0, "Smoke Test failed: item prices should not be negative.");

        Debug.Log($"Route: {routeViewData.displayName} / From: {routeViewData.fromTownName} / To: {routeViewData.toTownName} / CanSelect: {routeViewData.canSelect}");
        Debug.Assert(routeViewData.routeId == route.RouteId, "Smoke Test failed: routeId mismatch.");
        Debug.Assert(routeViewData.canSelect == route.UnlockedByDefault, "Smoke Test failed: route select state mismatch.");

        Debug.Log($"Locked Route: {lockedRouteViewData.displayName} / CanSelect: {lockedRouteViewData.canSelect} / Reason: {lockedRouteViewData.disabledReason}");
        Debug.Assert(!lockedRouteViewData.canSelect, "Smoke Test failed: locked route should not be selectable.");
        Debug.Assert(!string.IsNullOrEmpty(lockedRouteViewData.disabledReason), "Smoke Test failed: locked route needs disabledReason.");

        // Success
        Debug.Log($"Prepare: {prepareViewData.currentTownName} / Money: {prepareViewData.currentTradingCurrency} / CanStart: {prepareViewData.startCondition.canStart}");

        Debug.Assert(prepareViewData.towns.Length > 0, "Prepare Smoke Test failed: towns should not be empty.");
        Debug.Assert(prepareViewData.routes.Length > 0, "Prepare Smoke Test failed: routes should not be empty.");
        Debug.Assert(prepareViewData.tradeItems.Length > 0, "Prepare Smoke Test failed: tradeItems should not be empty.");
        Debug.Assert(prepareViewData.loadedItems.Length > 0, "Prepare Smoke Test failed: loadedItems should not be empty.");
        Debug.Assert(prepareViewData.loadedItems[0].quantity > 0, "Prepare Smoke Test failed: loaded item quantity should be positive.");
        Debug.Assert(prepareViewData.departureCaravanId == "smoke-caravan-prepare", "Prepare Smoke Test failed: departureCaravanId mismatch.");
        Debug.Assert(prepareViewData.selectedRouteId == route.RouteId, "Prepare Smoke Test failed: selectedRouteId mismatch.");
        Debug.Assert(prepareViewData.startCondition != null, "Prepare Smoke Test failed: startCondition should not be null.");
        Debug.Assert(prepareViewData.startCondition.canStart == successCondition.canStart, "Prepare Smoke Test failed: success condition mismatch.");

        // Caravan Not Selected
        Debug.Assert(caravanNotSelectedCondition != null, "Prepare Smoke Test failed: Caravan selection condition should not be null.");
        Debug.Assert(!caravanNotSelectedCondition.canStart, "Prepare Smoke Test failed: departure must be blocked until TradePrepareUI selects a Caravan.");
        Debug.Assert(!string.IsNullOrEmpty(caravanNotSelectedCondition.disabledReason), "Prepare Smoke Test failed: missing Caravan selection needs a disabled reason.");

        // Not Enough Money
        Debug.Log($"Prepare Fail: {notEnoughMoneyPrepareViewData.startCondition.disabledReason}");

        Debug.Assert(notEnoughMoneyPrepareViewData.startCondition != null, "Prepare Smoke Test failed: money condition should not be null.");
        Debug.Assert(!notEnoughMoneyPrepareViewData.startCondition.canStart, "Prepare Smoke Test failed: not enough money case should not start.");
        Debug.Assert(!string.IsNullOrEmpty(notEnoughMoneyPrepareViewData.startCondition.disabledReason), "Prepare Smoke Test failed: disabled reason is required.");

        // Route Not Selected
        Debug.Log($"Prepare Fail: {routeNotSelectedCondition.disabledReason}");

        Debug.Assert(routeNotSelectedCondition != null, "Prepare Smoke Test failed: route not selected condition should not be null.");
        Debug.Assert(!routeNotSelectedCondition.canStart, "Prepare Smoke Test failed: route not selected case should not start.");
        Debug.Assert(!string.IsNullOrEmpty(routeNotSelectedCondition.disabledReason), "Prepare Smoke Test failed: route not selected disabled reason is required.");

        // Route Locked
        Debug.Log($"Prepare Fail: {routeLockedCondition.disabledReason}");

        Debug.Assert(routeLockedCondition != null, "Prepare Smoke Test failed: route locked condition should not be null.");
        Debug.Assert(!routeLockedCondition.canStart, "Prepare Smoke Test failed: route locked case should not start.");
        Debug.Assert(!string.IsNullOrEmpty(routeLockedCondition.disabledReason), "Prepare Smoke Test failed: route locked disabled reason is required.");

        // No Cargo
        Debug.Log($"Prepare Fail: {noCargoCondition.disabledReason}");

        Debug.Assert(noCargoCondition != null, "Prepare Smoke Test failed: no cargo condition should not be null.");
        Debug.Assert(!noCargoCondition.canStart, "Prepare Smoke Test failed: no cargo case should not start.");
        Debug.Assert(!string.IsNullOrEmpty(noCargoCondition.disabledReason), "Prepare Smoke Test failed: no cargo disabled reason is required.");

        // Mixed Draft Animal Type
        Debug.Log($"Prepare Fail: {mixedDraftAnimalCondition.disabledReason}");

        Debug.Assert(mixedDraftAnimalCondition != null, "Prepare Smoke Test failed: mixed animal condition should not be null.");
        Debug.Assert(!mixedDraftAnimalCondition.canStart, "Prepare Smoke Test failed: mixed animal types should not start.");
        Debug.Assert(!string.IsNullOrEmpty(mixedDraftAnimalCondition.disabledReason), "Prepare Smoke Test failed: mixed animal disabled reason is required.");

        // Inventory Slot Exceeded
        Debug.Log($"Prepare Fail: {inventorySlotExceededCondition.disabledReason}");

        Debug.Assert(inventorySlotExceededCondition != null, "Prepare Smoke Test failed: inventory slot condition should not be null.");
        Debug.Assert(!inventorySlotExceededCondition.canStart, "Prepare Smoke Test failed: inventory slot exceeded case should not start.");
        Debug.Assert(!string.IsNullOrEmpty(inventorySlotExceededCondition.disabledReason), "Prepare Smoke Test failed: inventory slot disabled reason is required.");

        // Not Enough Food
        Debug.Log($"Prepare Warning: {notEnoughFoodPrepareViewData.startCondition.warningMessages[0]}");

        Debug.Assert(notEnoughFoodPrepareViewData.startCondition != null, "Prepare Smoke Test failed: food condition should not be null.");
        Debug.Assert(notEnoughFoodPrepareViewData.startCondition.canStart, "Prepare Smoke Test failed: food shortage should allow start.");
        Debug.Assert(notEnoughFoodPrepareViewData.startCondition.hasWarning, "Prepare Smoke Test failed: food shortage should show warning.");
        Debug.Assert(notEnoughFoodPrepareViewData.startCondition.warningMessages.Count > 0, "Prepare Smoke Test failed: warning message is required.");

        // Load Exceeded
        Debug.Log($"Prepare Fail: {loadExceededPrepareViewData.startCondition.disabledReason}");


        Debug.Assert(loadExceededPrepareViewData.startCondition != null, "Prepare Smoke Test failed: load condition should not be null.");
        Debug.Assert(!loadExceededPrepareViewData.startCondition.canStart, "Prepare Smoke Test failed: max load exceeded should not start.");
        Debug.Assert(!string.IsNullOrEmpty(loadExceededPrepareViewData.startCondition.disabledReason), "Prepare Smoke Test failed: max load exceeded disabled reason is required.");
        Debug.Assert(loadExceededPrepareViewData.currentLoad > loadExceededPrepareViewData.maxLoad, "Prepare Smoke Test failed: currentLoad should exceed maxLoad.");

        // Multiple Warnings
        Debug.Log($"Prepare Warning Count: {multipleWarningPrepareViewData.startCondition.warningMessages.Count}");

        for (var index = 0; index < multipleWarningPrepareViewData.startCondition.warningMessages.Count; index++)
        {
            Debug.Log($"Prepare Warning : {multipleWarningPrepareViewData.startCondition.warningMessages[index]}");
        }

        Debug.Assert(multipleWarningPrepareViewData.startCondition != null, "Prepare Smoke Test failed: multiple warning condition should not be null.");
        Debug.Assert(multipleWarningPrepareViewData.startCondition.canStart, "Prepare Smoke Test failed: multiple warning case should allow start.");
        Debug.Assert(multipleWarningPrepareViewData.startCondition.hasWarning, "Prepare Smoke Test failed: multiple warning case should show warning.");
        Debug.Assert(multipleWarningPrepareViewData.startCondition.warningMessages.Count >= 3, "Prepare Smoke Test failed: multiple warning case should contain all warning messages.");

    }

    // Verifies that a failed trade result preserves its route, failure reason, and display message.
    private void AssertTradeResultData()
    {
        var result = new TradeResultData
        {
            isSuccess = false,
            tradeId = "trade_test_001",
            routeId = route.RouteId,
            fromTownId = route.FromTownId,
            toTownId = route.ToTownId,
            failureReason = FailureReason.FoodShortage,
            messages =
            {
                new TradeResultMessageData
                {
                    type = TradeResultMessageType.Error,
                    messageCode = "FOOD_SHORTAGE",
                    messageText = "Trade failed because food was insufficient."
                }
            }
        };

        Debug.Log($"Result Message: [{result.messages[0].type}] {result.messages[0].messageCode} / {result.messages[0].messageText}");
        Debug.Assert(result.routeId == route.RouteId, "Trade Result Smoke Test failed: routeId mismatch.");
        Debug.Assert(result.messages.Count > 0, "Trade Result Smoke Test failed: result messages should not be empty.");
        Debug.Assert(result.failureReason == FailureReason.FoodShortage, "Trade Result Smoke Test failed: failureReason mismatch.");
    }

    // Verifies the overview contract for occupied, available-empty, and locked-empty slots.
    private static void AssertCaravanOverviewViewData()
    {
        AssertDefaultCaravanCollections();

        // The UI consumes the contract instead of constructing slot data or reading SaveData directly.
        ICaravanOverviewViewDataProvider provider = new TestCaravanOverviewViewDataProvider();
        CaravanOverviewViewData overview = provider.GetOverview();

        Debug.Assert(
            overview != null
                && overview.caravans != null
                && overview.caravans.Length == TestCaravanOverviewViewDataProvider.SlotCount,
            "Caravan Overview Provider Smoke Test failed: the Provider must return all four non-null slot entries.");

        CaravanBlockViewData prepareBlock = FindCaravanBlock(overview.caravans, 0);
        CaravanBlockViewData travelingBlock = FindCaravanBlock(overview.caravans, 1);
        CaravanBlockViewData availableEmptyBlock = FindCaravanBlock(overview.caravans, 2);
        CaravanBlockViewData lockedEmptyBlock = FindCaravanBlock(overview.caravans, 3);

        Debug.Assert(
            prepareBlock != null
                && travelingBlock != null
                && availableEmptyBlock != null
                && lockedEmptyBlock != null,
            "Caravan Overview Provider Smoke Test failed: slot indices 0 through 3 must all be present.");

        // Runtime Caravan IDs are copied into ViewData; the UI does not create or derive them.
        var prepareCaravan = new CaravanData
        {
            caravanId = TestCaravanOverviewViewDataProvider.PrepareCaravanId,
            state = JourneyState.Prepare
        };
        var travelingCaravan = new CaravanData
        {
            caravanId = TestCaravanOverviewViewDataProvider.TravelingCaravanId,
            state = JourneyState.Traveling
        };

        AssertOccupiedCaravanBlock(prepareBlock, prepareCaravan);
        AssertOccupiedCaravanBlock(travelingBlock, travelingCaravan);
        AssertAvailableEmptyCaravanBlock(availableEmptyBlock);
        AssertLockedEmptyCaravanBlock(lockedEmptyBlock);
        AssertUniqueCaravanSlotIndices(overview.caravans);
        AssertTravelingCaravanContents(travelingBlock);
        AssertCaravanSettingViewData(prepareCaravan, travelingCaravan);
        AssertCaravanOverviewProviderSnapshotIsolation(provider);
        AssertTradePrepareCaravanSelectionContract();
    }

    private static void AssertSaveDataCaravanOverviewProvider(TradeItemData catalogItem)
    {
        var providerObject = new GameObject("SaveDataCaravanOverviewProviderSmokeTest");
        try
        {
            var provider = providerObject.AddComponent<SaveDataCaravanOverviewProviderBehaviour>();
            provider.SetSaveDataForTests(null);
            CaravanOverviewViewData unloadedOverview = provider.GetOverview();
            Debug.Assert(
                unloadedOverview != null
                    && unloadedOverview.caravans.Length == SaveDataCaravanOverviewProviderBehaviour.SlotCount
                    && FindCaravanBlock(unloadedOverview.caravans, 0).slotState == CaravanSlotState.Unknown,
                "SaveData Caravan Overview Smoke Test failed: an unloaded Framework exposed fixture data.");

            var saveData = new ND.Framework.SaveData();
            saveData.caravans.Clear();
            var caravan = new ND.Framework.CaravanSaveData
            {
                caravanId = "saved-overview-caravan",
                state = JourneyState.Prepare
            };
            caravan.cargo.Add(new ND.Framework.CargoEntrySaveData
            {
                item = new ND.Framework.TradeItemSaveData
                {
                    itemId = catalogItem.ItemId,
                    itemName = catalogItem.DisplayName,
                    weight = catalogItem.Weight,
                    basePrice = catalogItem.BaseBuyPrice,
                    maxCount = catalogItem.MaxCount
                },
                quantity = 4
            });
            saveData.caravans.Add(caravan);
            saveData.selectedCaravanId = caravan.caravanId;
            provider.SetSaveDataForTests(saveData);

            CaravanBlockViewData savedBlock = FindCaravanBlock(provider.GetOverview().caravans, 0);
            Debug.Assert(
                savedBlock != null
                    && savedBlock.slotState == CaravanSlotState.Occupied
                    && savedBlock.caravanId == caravan.caravanId
                    && savedBlock.cargoIcons.Length == 1
                    && savedBlock.cargoIcons[0].itemId == catalogItem.ItemId
                    && savedBlock.cargoIcons[0].quantity == 4,
                "SaveData Caravan Overview Smoke Test failed: saved Cargo was not projected.");

            caravan.cargo.Clear();
            CaravanBlockViewData soldBlock = FindCaravanBlock(provider.GetOverview().caravans, 0);
            Debug.Assert(
                soldBlock != null && soldBlock.cargoIcons.Length == 0,
                "SaveData Caravan Overview Smoke Test failed: sold Cargo remained in the Overview.");
        }
        finally
        {
            Object.DestroyImmediate(providerObject);
        }
    }

    // Finds a fixed slot without assuming that a production Provider must return the array in slot order.
    private static CaravanBlockViewData FindCaravanBlock(CaravanBlockViewData[] blocks, int slotIndex)
    {
        if (blocks == null)
        {
            return null;
        }

        for (var index = 0; index < blocks.Length; index++)
        {
            CaravanBlockViewData block = blocks[index];
            if (block != null && block.slotIndex == slotIndex)
            {
                return block;
            }
        }

        return null;
    }

    // Verifies that mutable ViewData returned to UI code cannot corrupt the next Provider snapshot.
    private static void AssertCaravanOverviewProviderSnapshotIsolation(
        ICaravanOverviewViewDataProvider provider)
    {
        CaravanOverviewViewData mutableOverview = provider.GetOverview();
        CaravanBlockViewData mutablePrepareBlock = FindCaravanBlock(mutableOverview.caravans, 0);
        CaravanBlockViewData mutableTravelingBlock = FindCaravanBlock(mutableOverview.caravans, 1);

        mutablePrepareBlock.displayName = "Mutated by UI";
        mutableTravelingBlock.animalIcons[0].quantity = 999;

        CaravanOverviewViewData freshOverview = provider.GetOverview();
        CaravanBlockViewData freshPrepareBlock = FindCaravanBlock(freshOverview.caravans, 0);
        CaravanBlockViewData freshTravelingBlock = FindCaravanBlock(freshOverview.caravans, 1);

        Debug.Assert(
            freshPrepareBlock.displayName == "Preparation Caravan"
                && freshTravelingBlock.animalIcons[0].quantity == 2,
            "Caravan Overview Provider Smoke Test failed: UI mutations leaked into a later Provider snapshot.");
    }

    // Keeps collection defaults safe for UI binding before a provider supplies actual slots.
    private static void AssertDefaultCaravanCollections()
    {
        var overview = new CaravanOverviewViewData();
        var block = new CaravanBlockViewData();
        var prepare = new TradePrepareViewData();

        Debug.Assert(
            overview.caravans != null && overview.caravans.Length == 0,
            "Caravan Overview Smoke Test failed: the default slot array should be empty, not null.");
        Debug.Assert(
            block.animalIcons != null && block.animalIcons.Length == 0,
            "Caravan Overview Smoke Test failed: the default animal array should be empty, not null.");
        Debug.Assert(
            block.cargoIcons != null && block.cargoIcons.Length == 0,
            "Caravan Overview Smoke Test failed: the default cargo array should be empty, not null.");
        Debug.Assert(
            block.slotState == CaravanSlotState.Unknown,
            "Caravan Overview Smoke Test failed: an uninitialized slot must remain Unknown.");
        Debug.Assert(
            prepare.caravanOptions != null && prepare.caravanOptions.Length == 0,
            "Trade Prepare Smoke Test failed: the default Caravan option array should be empty, not null.");
    }

    // Verifies that an occupied slot preserves the Framework-assigned runtime identity and state.
    private static void AssertOccupiedCaravanBlock(
        CaravanBlockViewData block,
        CaravanData runtimeCaravan)
    {
        Debug.Assert(
            block != null && runtimeCaravan != null,
            "Caravan Overview Smoke Test failed: occupied slot inputs must not be null.");
        Debug.Assert(
            block.slotState == CaravanSlotState.Occupied,
            "Caravan Overview Smoke Test failed: an assigned Caravan must use the Occupied slot state.");
        Debug.Assert(
            !string.IsNullOrEmpty(runtimeCaravan.caravanId)
                && block.caravanId == runtimeCaravan.caravanId,
            "Caravan Overview Smoke Test failed: runtime caravanId was not preserved in ViewData.");
        Debug.Assert(
            block.state == runtimeCaravan.state,
            "Caravan Overview Smoke Test failed: runtime JourneyState was not preserved in ViewData.");
        Debug.Assert(
            string.IsNullOrEmpty(block.unlockHintText),
            "Caravan Overview Smoke Test failed: an occupied slot must not expose an unlock hint.");

    }

    // Verifies that an unlocked empty slot can represent a future Caravan creation target.
    private static void AssertAvailableEmptyCaravanBlock(CaravanBlockViewData block)
    {
        AssertEmptyCaravanData(block);
        Debug.Assert(
            block.slotState == CaravanSlotState.Empty,
            "Caravan Overview Smoke Test failed: an available creation slot must use the Empty state.");
        Debug.Assert(
            string.IsNullOrEmpty(block.unlockHintText),
            "Caravan Overview Smoke Test failed: an available empty slot must not expose an unlock hint.");
    }

    // Verifies that a locked empty slot provides a user-facing explanation.
    private static void AssertLockedEmptyCaravanBlock(CaravanBlockViewData block)
    {
        AssertEmptyCaravanData(block);
        Debug.Assert(
            block.slotState == CaravanSlotState.Locked,
            "Caravan Overview Smoke Test failed: an unavailable slot must use the Locked state.");
        Debug.Assert(
            !string.IsNullOrEmpty(block.unlockHintText),
            "Caravan Overview Smoke Test failed: a locked empty slot must explain how it can be unlocked.");
    }

    // Applies the shared rule that an empty slot carries no Caravan-specific configuration.
    private static void AssertEmptyCaravanData(CaravanBlockViewData block)
    {
        Debug.Assert(
            block != null
                && (block.slotState == CaravanSlotState.Empty
                    || block.slotState == CaravanSlotState.Locked),
            "Caravan Overview Smoke Test failed: a slot without a Caravan must be Empty or Locked.");
        Debug.Assert(
            string.IsNullOrEmpty(block.caravanId)
                && string.IsNullOrEmpty(block.displayName)
                && string.IsNullOrEmpty(block.wagonContentId),
            "Caravan Overview Smoke Test failed: an empty slot must not contain Caravan identity or wagon data.");
        Debug.Assert(
            block.animalIcons != null && block.animalIcons.Length == 0,
            "Caravan Overview Smoke Test failed: an empty slot must not contain assigned animals.");
        Debug.Assert(
            block.cargoIcons != null && block.cargoIcons.Length == 0,
            "Caravan Overview Smoke Test failed: an empty slot must not contain cargo summaries.");
    }

    // Detects ambiguous UI routing caused by two blocks claiming the same fixed slot.
    private static void AssertUniqueCaravanSlotIndices(CaravanBlockViewData[] blocks)
    {
        Debug.Assert(
            blocks != null,
            "Caravan Overview Smoke Test failed: the slot array must not be null.");

        for (var firstIndex = 0; firstIndex < blocks.Length; firstIndex++)
        {
            Debug.Assert(
                blocks[firstIndex] != null,
                "Caravan Overview Smoke Test failed: a slot entry must not be null.");

            for (var secondIndex = firstIndex + 1; secondIndex < blocks.Length; secondIndex++)
            {
                Debug.Assert(
                    blocks[secondIndex] != null
                        && blocks[firstIndex].slotIndex != blocks[secondIndex].slotIndex,
                    "Caravan Overview Smoke Test failed: slotIndex values must be unique.");
            }
        }
    }

    // Verifies that departure eligibility belongs to TradePrepare options, not Overview blocks.
    private static void AssertTradePrepareCaravanSelectionContract()
    {
        ITradePrepareCaravanOptionProvider provider =
            new TestTradePrepareCaravanOptionProvider();
        TradePrepareCaravanOptionViewData[] options = provider.GetOptions();

        var context = new TradePrepareBuildContext
        {
            caravanOptions = options
        };
        var controller = new TradePrepareFlowController(context);
        controller.Initialize("smoke-town");

        bool blockedOptionRejected = !controller.SelectDepartureCaravan(
            TestTradePrepareCaravanOptionProvider.TravelingCaravanId);
        bool unknownOptionRejected = !controller.SelectDepartureCaravan("unknown-caravan");
        bool selectableOptionAccepted = controller.SelectDepartureCaravan(
            TestTradePrepareCaravanOptionProvider.PrepareCaravanId);
        TradePrepareViewData prepare = controller.CurrentViewData;

        Debug.Assert(
            prepare.caravanOptions.Length == TestTradePrepareCaravanOptionProvider.MaxOptionCount
                && prepare.caravanOptions[0].canSelect
                && !prepare.caravanOptions[1].canSelect
                && !string.IsNullOrEmpty(prepare.caravanOptions[1].disabledReason),
            "Trade Prepare Smoke Test failed: all four departure options must provide Provider-owned availability data.");
        Debug.Assert(
            blockedOptionRejected
                && unknownOptionRejected
                && selectableOptionAccepted
                && prepare.departureCaravanId == prepare.caravanOptions[0].caravanId,
            "Trade Prepare Smoke Test failed: only a selectable TradePrepare option may enter the Draft.");

        bool departureCreated = TradePrepareCaravanFactory.TryCreateDeparture(
            controller.CurrentDraft,
            context,
            out CaravanData departureCaravan,
            out string createErrorCode,
            out string createErrorMessage);
        bool missingDepartureRejected = !TradePrepareCaravanFactory.TryCreateDeparture(
            new TradePrepareDraft(),
            context,
            out _,
            out string missingErrorCode,
            out _);
        bool unavailableDepartureRejected = !TradePrepareCaravanFactory.TryCreateDeparture(
            new TradePrepareDraft
            {
                departureCaravanId = TestTradePrepareCaravanOptionProvider.TravelingCaravanId
            },
            context,
            out _,
            out string unavailableErrorCode,
            out _);
        bool unknownDepartureRejected = !TradePrepareCaravanFactory.TryCreateDeparture(
            new TradePrepareDraft
            {
                departureCaravanId = "unknown-caravan"
            },
            context,
            out _,
            out string unknownErrorCode,
            out _);
        var duplicateContext = new TradePrepareBuildContext
        {
            caravanOptions = new[]
            {
                options[0],
                options[0]
            }
        };
        bool duplicateDepartureRejected = !TradePrepareCaravanFactory.TryCreateDeparture(
            new TradePrepareDraft
            {
                departureCaravanId = TestTradePrepareCaravanOptionProvider.PrepareCaravanId
            },
            duplicateContext,
            out _,
            out string duplicateErrorCode,
            out _);

        Debug.Assert(
            departureCreated
                && departureCaravan != null
                && departureCaravan.caravanId == TestTradePrepareCaravanOptionProvider.PrepareCaravanId
                && string.IsNullOrEmpty(createErrorCode)
                && string.IsNullOrEmpty(createErrorMessage),
            "Trade Prepare Smoke Test failed: Factory did not preserve the validated departure Caravan ID.");
        Debug.Assert(
            missingDepartureRejected
                && missingErrorCode == TradePrepareCaravanFactory.ErrorDepartureCaravanRequired
                && unavailableDepartureRejected
                && unavailableErrorCode == TradePrepareCaravanFactory.ErrorDepartureCaravanUnavailable
                && unknownDepartureRejected
                && unknownErrorCode == TradePrepareCaravanFactory.ErrorDepartureCaravanNotFound
                && duplicateDepartureRejected
                && duplicateErrorCode == TradePrepareCaravanFactory.ErrorDepartureCaravanDuplicate,
            "Trade Prepare Smoke Test failed: Factory did not reject an invalid departure Caravan before gateway use.");

        // Provider snapshots and ViewData projections must not expose a mutable shared option instance.
        options[0].displayName = "Mutated by UI";
        TradePrepareCaravanOptionViewData[] freshOptions = provider.GetOptions();
        Debug.Assert(
            freshOptions[0].displayName == "Preparation Caravan"
                && prepare.caravanOptions[0].displayName == "Preparation Caravan",
            "Trade Prepare Smoke Test failed: UI mutations leaked into Provider or ViewData option snapshots.");

        controller.Dispose();
    }

    // Verifies icon lookup IDs and quantities for a populated traveling Caravan block.
    private static void AssertTravelingCaravanContents(CaravanBlockViewData block)
    {
        Debug.Assert(
            block.wagonContentId == "test-wagon-medium",
            "Caravan Overview Smoke Test failed: wagon content ID was not preserved.");
        Debug.Assert(
            block.animalIcons.Length == 1
                && block.animalIcons[0].animalContentId == "test-horse"
                && block.animalIcons[0].quantity == 2,
            "Caravan Overview Smoke Test failed: animal icon data was not preserved.");
        Debug.Assert(
            block.cargoIcons.Length == 1
                && block.cargoIcons[0].itemId == "test-grain"
                && block.cargoIcons[0].quantity == 10,
            "Caravan Overview Smoke Test failed: cargo icon data was not preserved.");
    }

    // Verifies that extracted S3/S4 contracts remain editable only for Preparation Caravans.
    private static void AssertCaravanSettingViewData(
        CaravanData prepareCaravan,
        CaravanData travelingCaravan)
    {
        var prepareSetting = new CaravanSettingViewData
        {
            caravanId = prepareCaravan.caravanId,
            caravanDisplayName = "Preparation Caravan",
            state = prepareCaravan.state,
            canEdit = true,
            selectedWagonInstanceId = "smoke-wagon-instance-01",
            selectedAnimalInstanceIds = new[]
            {
                "smoke-horse-instance-01",
                "smoke-horse-instance-02"
            },
            wagons = new[]
            {
                new WagonViewData
                {
                    wagonId = "smoke-wagon-medium",
                    wagonInstanceId = "smoke-wagon-instance-01",
                    wagonType = WagonType.WagonWithAnimals,
                    minRequireAnimals = 1,
                    maxPullAnimals = 2,
                    isOwned = true,
                    canSelect = true
                }
            },
            draftAnimals = new[]
            {
                new DraftAnimalViewData
                {
                    draftAnimalId = "smoke-horse",
                    draftAnimalInstanceId = "smoke-horse-instance-01",
                    selectedAmount = 1,
                    isEligibleForSelectedWagon = true,
                    canSelect = true
                },
                new DraftAnimalViewData
                {
                    draftAnimalId = "smoke-horse",
                    draftAnimalInstanceId = "smoke-horse-instance-02",
                    selectedAmount = 1,
                    isEligibleForSelectedWagon = true,
                    canSelect = true
                }
            }
        };
        var travelingSetting = new CaravanSettingViewData
        {
            caravanId = travelingCaravan.caravanId,
            caravanDisplayName = "Traveling Caravan",
            state = travelingCaravan.state,
            canEdit = false,
            editBlockedReason = "Caravan settings cannot be changed while the Caravan is traveling."
        };
        var prepareLoad = new CaravanLoadSettingViewData
        {
            caravanId = prepareCaravan.caravanId,
            caravanDisplayName = "Preparation Caravan",
            currentTownId = "smoke-base-town",
            state = prepareCaravan.state,
            canEdit = true,
            availableItems = new[] { new TradeItemViewData { itemId = "smoke-grain" } },
            plannedItems = new[]
            {
                new CargoItemViewData
                {
                    itemId = "smoke-grain",
                    quantity = 10,
                    totalPurchasePrice = 100
                }
            },
            currentLoad = 10f,
            maxLoad = 100f,
            usedInventorySlotCount = 1,
            maxInventorySlotCount = 5,
            totalPlannedPurchaseCost = 100,
            estimatedCurrencyAfterPurchase = 900
        };
        var travelingLoad = new CaravanLoadSettingViewData
        {
            caravanId = travelingCaravan.caravanId,
            caravanDisplayName = "Traveling Caravan",
            state = travelingCaravan.state,
            canEdit = false,
            editBlockedReason = "Cargo cannot be changed while the Caravan is traveling."
        };

        Debug.Assert(
            prepareSetting.canEdit
                && string.IsNullOrEmpty(prepareSetting.editBlockedReason)
                && prepareSetting.selectedWagonInstanceId == "smoke-wagon-instance-01"
                && prepareSetting.selectedAnimalInstanceIds.Length == 2
                && prepareSetting.wagons.Length == 1
                && prepareSetting.wagons[0].wagonId == "smoke-wagon-medium"
                && prepareSetting.wagons[0].wagonInstanceId == "smoke-wagon-instance-01"
                && prepareSetting.draftAnimals.Length == 2
                && prepareSetting.draftAnimals[0].draftAnimalId == "smoke-horse"
                && prepareSetting.draftAnimals[0].draftAnimalInstanceId == "smoke-horse-instance-01",
            "Caravan Setting Smoke Test failed: content and owned-instance identities must remain distinct.");
        Debug.Assert(
            !travelingSetting.canEdit && !string.IsNullOrEmpty(travelingSetting.editBlockedReason),
            "Caravan Setting Smoke Test failed: Traveling settings should be read-only with a reason.");
        Debug.Assert(
            prepareLoad.canEdit
                && string.IsNullOrEmpty(prepareLoad.editBlockedReason)
                && prepareLoad.currentTownId == "smoke-base-town"
                && prepareLoad.plannedItems.Length == 1
                && prepareLoad.totalPlannedPurchaseCost == 100,
            "Caravan Setting Smoke Test failed: Preparation cargo should expose an editable plan.");
        Debug.Assert(
            !travelingLoad.canEdit && !string.IsNullOrEmpty(travelingLoad.editBlockedReason),
            "Caravan Setting Smoke Test failed: Traveling cargo should be read-only with a reason.");

        var defaultSetting = new CaravanSettingViewData();
        var defaultLoad = new CaravanLoadSettingViewData();
        Debug.Assert(
            defaultSetting.wagons != null
                && defaultSetting.wagons.Length == 0
                && defaultSetting.draftAnimals != null
                && defaultSetting.draftAnimals.Length == 0
                && defaultSetting.selectedAnimalInstanceIds != null
                && defaultSetting.selectedAnimalInstanceIds.Length == 0,
            "Caravan Setting Smoke Test failed: setting collections must default to empty arrays.");
        Debug.Assert(
            defaultLoad.availableItems != null
                && defaultLoad.availableItems.Length == 0
                && defaultLoad.plannedItems != null
                && defaultLoad.plannedItems.Length == 0,
            "Caravan Setting Smoke Test failed: load collections must default to empty arrays.");

        // The detached S3 adapter must preserve instance IDs in its UI-owned Draft snapshot.
        var panelObject = new GameObject("CaravanSettingPanelSmokeTest");
        try
        {
            AnimalInventoryPanel panel = panelObject.AddComponent<AnimalInventoryPanel>();
            bool populated = panel.Populate(prepareSetting);
            bool draftCreated = panel.TryCreateSettingDraft(prepareCaravan.caravanId, out CaravanSettingDraft draft);
            Debug.Assert(
                populated
                    && panel.CanConfirmCaravanSetting()
                    && draftCreated
                    && draft != null
                    && draft.caravanId == prepareCaravan.caravanId
                    && draft.selectedWagonInstanceId == "smoke-wagon-instance-01"
                    && draft.SelectedAnimalInstanceIds.Count == 2
                    && draft.SelectedAnimalInstanceIds[0] == "smoke-horse-instance-01"
                    && draft.SelectedAnimalInstanceIds[1] == "smoke-horse-instance-02",
                "Caravan Setting Smoke Test failed: S3 must return the selected owned-instance IDs.");

            var walkingSetting = new CaravanSettingViewData
            {
                caravanId = prepareCaravan.caravanId,
                state = JourneyState.Prepare,
                canEdit = true,
                wagons = prepareSetting.wagons,
                draftAnimals = prepareSetting.draftAnimals
            };
            bool walkingPopulated = panel.Populate(walkingSetting);
            bool walkingDraftCreated = panel.TryCreateSettingDraft(
                prepareCaravan.caravanId,
                out CaravanSettingDraft walkingDraft);
            Debug.Assert(
                walkingPopulated
                    && panel.CanConfirmCaravanSetting()
                    && walkingDraftCreated
                    && walkingDraft != null
                    && string.IsNullOrEmpty(walkingDraft.selectedWagonInstanceId)
                    && walkingDraft.SelectedAnimalInstanceIds.Count == 0,
                "Caravan Setting Smoke Test failed: a wagon-free walking configuration must remain valid.");
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
        }
    }

    // Verifies the replaceable S3 Provider and Command boundary without touching Framework SaveData.
    private static void AssertCaravanSettingServices(TradeItemData catalogItem)
    {
        var serviceObject = new GameObject("CaravanSettingServiceSmokeTest");
        try
        {
            TestCaravanSettingService service = serviceObject.AddComponent<TestCaravanSettingService>();
            service.SetCargoCatalogForTests(catalogItem);
            ICaravanSettingViewDataProvider provider = service;
            ICaravanSettingCommand command = service;

            CaravanSettingViewData initial = provider.GetSetting(TestCaravanSettingService.PrepareCaravanId);
            Debug.Assert(
                initial != null
                    && initial.canEdit
                    && initial.selectedWagonInstanceId == TestCaravanSettingService.WagonInstanceId
                    && initial.selectedAnimalInstanceIds.Length == 2,
                "Caravan Setting Service Smoke Test failed: Provider did not return the editable S3 snapshot.");

            // Mutating one UI snapshot must not leak into the Provider's next authoritative snapshot.
            initial.selectedAnimalInstanceIds[0] = "ui-mutated-instance";
            CaravanSettingViewData isolated = provider.GetSetting(TestCaravanSettingService.PrepareCaravanId);
            Debug.Assert(
                isolated.selectedAnimalInstanceIds[0] == TestCaravanSettingService.FirstAnimalInstanceId,
                "Caravan Setting Service Smoke Test failed: Provider snapshots are not isolated.");

            var walkingDraft = new CaravanSettingDraft
            {
                caravanId = TestCaravanSettingService.PrepareCaravanId
            };
            CaravanSettingCommandResult walkingResult = command.Execute(walkingDraft);
            CaravanSettingViewData walkingSnapshot = provider.GetSetting(TestCaravanSettingService.PrepareCaravanId);
            Debug.Assert(
                walkingResult != null
                    && walkingResult.succeeded
                    && string.IsNullOrEmpty(walkingSnapshot.selectedWagonInstanceId)
                    && walkingSnapshot.selectedAnimalInstanceIds.Length == 0,
                "Caravan Setting Service Smoke Test failed: a valid walking Draft was not committed in memory.");

            var invalidTravelingDraft = new CaravanSettingDraft
            {
                caravanId = TestCaravanSettingService.TravelingCaravanId,
                selectedWagonInstanceId = TestCaravanSettingService.WagonInstanceId
            };
            invalidTravelingDraft.SelectAnimal(TestCaravanSettingService.FirstAnimalInstanceId);
            CaravanSettingCommandResult travelingResult = command.Execute(invalidTravelingDraft);
            Debug.Assert(
                travelingResult != null
                    && !travelingResult.succeeded
                    && travelingResult.errorCode == CaravanSettingFailureCodes.CaravanNotEditable,
                "Caravan Setting Service Smoke Test failed: a Traveling Caravan edit was not rejected.");

            var invalidAssetDraft = new CaravanSettingDraft
            {
                caravanId = TestCaravanSettingService.PrepareCaravanId,
                selectedWagonInstanceId = "unknown-wagon-instance"
            };
            CaravanSettingCommandResult invalidAssetResult = command.Execute(invalidAssetDraft);
            Debug.Assert(
                invalidAssetResult != null
                    && !invalidAssetResult.succeeded
                    && invalidAssetResult.errorCode == CaravanSettingFailureCodes.AssetNotOwned,
                "Caravan Setting Service Smoke Test failed: an unknown owned-instance ID was not rejected.");

            Debug.Assert(
                provider.GetSetting("missing-caravan") == null,
                "Caravan Setting Service Smoke Test failed: an unknown caravanId must not produce S3 data.");

            ICaravanLoadSettingViewDataProvider loadProvider = service;
            ICaravanLoadSettingCommand loadCommand = service;
            CaravanLoadSettingViewData initialLoad = loadProvider.GetLoadSetting(
                TestCaravanSettingService.PrepareCaravanId);
            Debug.Assert(
                initialLoad != null
                    && initialLoad.canEdit
                    && initialLoad.plannedItems.Length == 0
                    && initialLoad.maxLoad == 0f
                    && initialLoad.maxInventorySlotCount == 0,
                "Caravan Load Setting Smoke Test failed: S4 capacity did not follow the walking S3 setting.");

            var wagonDraft = new CaravanSettingDraft
            {
                caravanId = TestCaravanSettingService.PrepareCaravanId,
                selectedWagonInstanceId = TestCaravanSettingService.WagonInstanceId
            };
            wagonDraft.SelectAnimal(TestCaravanSettingService.FirstAnimalInstanceId);
            CaravanSettingCommandResult wagonResult = command.Execute(wagonDraft);
            CaravanLoadSettingViewData wagonLoad = loadProvider.GetLoadSetting(
                TestCaravanSettingService.PrepareCaravanId);
            Debug.Assert(
                wagonResult != null
                    && wagonResult.succeeded
                    && wagonLoad.maxLoad == TestCaravanSettingService.WagonMaxLoad
                    && wagonLoad.maxInventorySlotCount == TestCaravanSettingService.WagonInventorySlotCount,
                "Caravan Load Setting Smoke Test failed: S4 capacity did not follow the committed S3 wagon.");

            var loadDraft = new CaravanLoadSettingDraft
            {
                caravanId = TestCaravanSettingService.PrepareCaravanId,
                items = new List<CaravanLoadItemDraft>
                {
                    new CaravanLoadItemDraft { itemId = catalogItem.ItemId, quantity = 3 }
                }
            };
            CaravanLoadSettingCommandResult loadResult = loadCommand.Execute(loadDraft);
            CaravanLoadSettingViewData committedLoad = loadProvider.GetLoadSetting(
                TestCaravanSettingService.PrepareCaravanId);
            Debug.Assert(
                loadResult != null
                    && loadResult.succeeded
                    && committedLoad.plannedItems.Length == 1
                    && committedLoad.plannedItems[0].itemId == catalogItem.ItemId
                    && committedLoad.plannedItems[0].quantity == 3
                    && committedLoad.plannedItems[0].purchaseUnitPrice == catalogItem.BaseBuyPrice,
                "Caravan Load Setting Smoke Test failed: a valid S4 Draft was not committed in memory.");

            var planContext = new TradePrepareBuildContext
            {
                tradeItems = new[] { catalogItem },
                caravanOptions = new[]
                {
                    new TradePrepareCaravanOptionViewData
                    {
                        caravanId = TestCaravanSettingService.PrepareCaravanId,
                        state = JourneyState.Prepare,
                        canSelect = true
                    }
                }
            };
            using (var planController = new TradePrepareFlowController(planContext))
            {
                planController.Initialize("test-town");
                bool caravanSelected = planController.SelectDepartureCaravan(
                    TestCaravanSettingService.PrepareCaravanId);
                bool settingApplied = planController.ApplyCaravanSetting(
                    provider.GetSetting(TestCaravanSettingService.PrepareCaravanId));
                bool planApplied = planController.ApplyCargoPlan(committedLoad);
                TradePrepareDraft departureDraft = planController.CurrentDraft;
                bool mismatchedPlanRejected = !planController.ApplyCargoPlan(
                    new CaravanLoadSettingViewData
                    {
                        caravanId = TestCaravanSettingService.TravelingCaravanId,
                        plannedItems = System.Array.Empty<CargoItemViewData>()
                    });
                Debug.Assert(
                    caravanSelected
                        && settingApplied
                        && planApplied
                        && mismatchedPlanRejected
                        && departureDraft.hasAuthoritativeCaravanComposition
                        && departureDraft.selectedWagonCurrentDurability > 0
                        && departureDraft.hasAuthoritativeCargoPlan
                        && departureDraft.selectedWagonId == TestCaravanSettingService.WagonContentId
                        && departureDraft.selectedAnimals.Count == 1
                        && departureDraft.selectedAnimals[0].draftAnimalId == TestCaravanSettingService.AnimalContentId
                        && departureDraft.selectedAnimals[0].quantity == 2
                        && departureDraft.selectedBuyItems.Count == 1
                        && departureDraft.selectedBuyItems[0].itemId == catalogItem.ItemId
                        && departureDraft.selectedBuyItems[0].quantity == 3,
                    "Caravan Setting Smoke Test failed: S3/S4 plans did not hydrate the matching departure Draft.");
            }

            CaravanSettingCommandResult occupiedWalkingResult = command.Execute(walkingDraft);
            CaravanSettingViewData retainedWagon = provider.GetSetting(TestCaravanSettingService.PrepareCaravanId);
            Debug.Assert(
                occupiedWalkingResult != null
                    && !occupiedWalkingResult.succeeded
                    && occupiedWalkingResult.errorCode == CaravanSettingFailureCodes.CargoCapacityExceeded
                    && retainedWagon.selectedWagonInstanceId == TestCaravanSettingService.WagonInstanceId,
                "Caravan Setting Service Smoke Test failed: S3 allowed a capacity reduction below planned cargo.");

            // Mutating one S4 snapshot must not leak into the next Provider result.
            committedLoad.plannedItems[0].quantity = 99;
            CaravanLoadSettingViewData isolatedLoad = loadProvider.GetLoadSetting(
                TestCaravanSettingService.PrepareCaravanId);
            Debug.Assert(
                isolatedLoad.plannedItems[0].quantity == 3,
                "Caravan Load Setting Smoke Test failed: Provider snapshots are not isolated.");

            var travelingLoadDraft = new CaravanLoadSettingDraft
            {
                caravanId = TestCaravanSettingService.TravelingCaravanId,
                items = new List<CaravanLoadItemDraft>
                {
                    new CaravanLoadItemDraft { itemId = catalogItem.ItemId, quantity = 1 }
                }
            };
            CaravanLoadSettingCommandResult travelingLoadResult = loadCommand.Execute(travelingLoadDraft);
            Debug.Assert(
                travelingLoadResult != null
                    && !travelingLoadResult.succeeded
                    && travelingLoadResult.errorCode == CaravanLoadSettingFailureCodes.CaravanNotEditable,
                "Caravan Load Setting Smoke Test failed: a Traveling Caravan S4 edit was not rejected.");

            Debug.Assert(
                loadProvider.GetLoadSetting("missing-caravan") == null,
                "Caravan Load Setting Smoke Test failed: an unknown caravanId must not produce S4 data.");

            var frameworkSaveData = new ND.Framework.SaveData();
            frameworkSaveData.caravans.Clear();
            var savedCaravan = new ND.Framework.CaravanSaveData
            {
                caravanId = TestCaravanSettingService.PrepareCaravanId,
                state = JourneyState.Prepare
            };
            savedCaravan.cargo.Add(new ND.Framework.CargoEntrySaveData
            {
                item = new ND.Framework.TradeItemSaveData
                {
                    itemId = catalogItem.ItemId,
                    itemName = catalogItem.DisplayName,
                    weight = catalogItem.Weight,
                    basePrice = catalogItem.BaseBuyPrice,
                    maxCount = catalogItem.MaxCount
                },
                quantity = 5
            });
            frameworkSaveData.caravans.Add(savedCaravan);
            frameworkSaveData.selectedCaravanId = savedCaravan.caravanId;
            service.SetSaveDataForTests(frameworkSaveData);

            CaravanLoadSettingViewData savedCargoLoad = loadProvider.GetLoadSetting(savedCaravan.caravanId);
            Debug.Assert(
                savedCargoLoad != null
                    && savedCargoLoad.plannedItems.Length == 1
                    && savedCargoLoad.plannedItems[0].quantity == 5,
                "Caravan Load Setting Smoke Test failed: Framework SaveData cargo was not authoritative.");

            var frameworkDraft = new CaravanLoadSettingDraft
            {
                caravanId = savedCaravan.caravanId,
                items = new List<CaravanLoadItemDraft>
                {
                    new CaravanLoadItemDraft { itemId = catalogItem.ItemId, quantity = 3 }
                }
            };
            CaravanLoadSettingCommandResult frameworkDraftResult = loadCommand.Execute(frameworkDraft);
            CaravanLoadSettingViewData activeDraftLoad = loadProvider.GetLoadSetting(savedCaravan.caravanId);
            Debug.Assert(
                frameworkDraftResult != null
                    && frameworkDraftResult.succeeded
                    && activeDraftLoad.plannedItems.Length == 1
                    && activeDraftLoad.plannedItems[0].quantity == 3,
                "Caravan Load Setting Smoke Test failed: an active uncommitted cargo draft was not preserved.");

            // Simulate a successful sell-only transaction. Once authoritative SaveData changes,
            // the old in-memory draft must not repopulate the UI.
            savedCaravan.cargo.Clear();
            CaravanLoadSettingViewData soldCargoLoad = loadProvider.GetLoadSetting(savedCaravan.caravanId);
            Debug.Assert(
                soldCargoLoad != null && soldCargoLoad.plannedItems.Length == 0,
                "Caravan Load Setting Smoke Test failed: sold SaveData cargo was restored from plannedCargo.");
        }
        finally
        {
            Object.DestroyImmediate(serviceObject);
        }
    }

    // Groups the currency projection boundaries separately from other ViewData contracts.
    private static void AssertCurrencyProjectionScenarios()
    {
        AssertCurrencyProjection(2000L, 600L, 300L, 1400L, 1100L, true, true);
        AssertCurrencyProjection(2000L, 2100L, 0L, 0L, 0L, false, false);
        AssertCurrencyProjection(2000L, 1700L, 500L, 300L, 0L, true, false);
        AssertCurrencyProjection(2000L, 2000L, 0L, 0L, 0L, true, true);
    }

    private static void AssertCurrencyProjection(
        long currentCurrency,
        long purchaseCost,
        long hireCost,
        long expectedAfterPurchase,
        long expectedAfterHire,
        bool expectedCanPurchase,
        bool expectedCanHire)
    {
        TradePrepareViewDataBuilder.CalculateCurrencyProjection(
            currentCurrency,
            purchaseCost,
            hireCost,
            out long estimatedAfterPurchase,
            out long estimatedAfterHire,
            out bool canPurchase,
            out bool canHire);

        Debug.Assert(
            estimatedAfterPurchase == expectedAfterPurchase,
            $"Currency Projection Smoke Test failed: after purchase {estimatedAfterPurchase}, expected {expectedAfterPurchase}.");
        Debug.Assert(
            estimatedAfterHire == expectedAfterHire,
            $"Currency Projection Smoke Test failed: after hire {estimatedAfterHire}, expected {expectedAfterHire}.");
        Debug.Assert(
            canPurchase == expectedCanPurchase,
            $"Currency Projection Smoke Test failed: canPurchase {canPurchase}, expected {expectedCanPurchase}.");
        Debug.Assert(
            canHire == expectedCanHire,
            $"Currency Projection Smoke Test failed: canHire {canHire}, expected {expectedCanHire}.");
    }
}
