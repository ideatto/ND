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
            selectedCaravanId = "smoke-caravan-prepare",
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
        draftStore.ResetForCaravan("smoke-caravan-prepare", town.TownId);
        var storeResetTownMatched = draftStore.Current.currentTownId == town.TownId;
        var storeResetCaravanMatched = draftStore.Current.selectedCaravanId == "smoke-caravan-prepare";
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
            && string.IsNullOrEmpty(draftStore.Current.selectedCaravanId)
            && draftStore.Current.SelectedMercenaryIds.Count == 0;

        Debug.Assert(firstMercenarySelection, "Draft Smoke Test failed: first mercenary selection should succeed.");
        Debug.Assert(!duplicateMercenarySelection, "Draft Smoke Test failed: duplicate mercenary selection should fail.");
        Debug.Assert(prepareDraft.SelectedMercenaryIds.Count == 1, "Draft Smoke Test failed: duplicate mercenary ID should not be stored.");
        Debug.Assert(storeResetTownMatched, "Draft Store Smoke Test failed: Reset should set currentTownId.");
        Debug.Assert(storeResetCaravanMatched, "Draft Store Smoke Test failed: ResetForCaravan should preserve selectedCaravanId.");
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

        var prepareViewData = new TradePrepareViewData
        {
            selectedCaravanId = "smoke-caravan-prepare",
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
        Debug.Assert(prepareViewData.selectedCaravanId == "smoke-caravan-prepare", "Prepare Smoke Test failed: selectedCaravanId mismatch.");
        Debug.Assert(prepareViewData.selectedRouteId == route.RouteId, "Prepare Smoke Test failed: selectedRouteId mismatch.");
        Debug.Assert(prepareViewData.startCondition != null, "Prepare Smoke Test failed: startCondition should not be null.");
        Debug.Assert(prepareViewData.startCondition.canStart == successCondition.canStart, "Prepare Smoke Test failed: success condition mismatch.");

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
        Debug.Assert(
            overview.selectedCaravanId == TestCaravanOverviewViewDataProvider.PrepareCaravanId,
            "Caravan Overview Provider Smoke Test failed: the default selected Caravan was not preserved.");

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
        AssertSelectedCaravanExists(overview);
        AssertTravelingCaravanContents(travelingBlock);
        AssertCaravanSettingViewData(prepareCaravan, travelingCaravan);
        AssertCaravanOverviewProviderSelection();
        AssertCaravanOverviewProviderSnapshotIsolation(provider);
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

    // Verifies that the temporary fixture supports valid selection previews and rejects unknown IDs.
    private static void AssertCaravanOverviewProviderSelection()
    {
        ICaravanOverviewViewDataProvider travelingProvider =
            new TestCaravanOverviewViewDataProvider(TestCaravanOverviewViewDataProvider.TravelingCaravanId);
        CaravanOverviewViewData travelingOverview = travelingProvider.GetOverview();
        AssertSelectedCaravanExists(travelingOverview);
        Debug.Assert(
            travelingOverview.selectedCaravanId == TestCaravanOverviewViewDataProvider.TravelingCaravanId,
            "Caravan Overview Provider Smoke Test failed: a valid traveling selection was not preserved.");

        ICaravanOverviewViewDataProvider invalidProvider =
            new TestCaravanOverviewViewDataProvider("unknown-caravan");
        Debug.Assert(
            string.IsNullOrEmpty(invalidProvider.GetOverview().selectedCaravanId),
            "Caravan Overview Provider Smoke Test failed: an unknown Caravan ID must not become selected.");
    }

    // Verifies that mutable ViewData returned to UI code cannot corrupt the next Provider snapshot.
    private static void AssertCaravanOverviewProviderSnapshotIsolation(
        ICaravanOverviewViewDataProvider provider)
    {
        CaravanOverviewViewData mutableOverview = provider.GetOverview();
        CaravanBlockViewData mutablePrepareBlock = FindCaravanBlock(mutableOverview.caravans, 0);
        CaravanBlockViewData mutableTravelingBlock = FindCaravanBlock(mutableOverview.caravans, 1);

        mutableOverview.selectedCaravanId = string.Empty;
        mutablePrepareBlock.displayName = "Mutated by UI";
        mutableTravelingBlock.animalIcons[0].quantity = 999;

        CaravanOverviewViewData freshOverview = provider.GetOverview();
        CaravanBlockViewData freshPrepareBlock = FindCaravanBlock(freshOverview.caravans, 0);
        CaravanBlockViewData freshTravelingBlock = FindCaravanBlock(freshOverview.caravans, 1);

        Debug.Assert(
            freshOverview.selectedCaravanId == TestCaravanOverviewViewDataProvider.PrepareCaravanId
                && freshPrepareBlock.displayName == "Preparation Caravan"
                && freshTravelingBlock.animalIcons[0].quantity == 2,
            "Caravan Overview Provider Smoke Test failed: UI mutations leaked into a later Provider snapshot.");
    }

    // Keeps collection defaults safe for UI binding before a provider supplies actual slots.
    private static void AssertDefaultCaravanCollections()
    {
        var overview = new CaravanOverviewViewData();
        var block = new CaravanBlockViewData();

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
            string.IsNullOrEmpty(block.lockedReason),
            "Caravan Overview Smoke Test failed: an unlocked occupied slot must not have a locked reason.");

        if (runtimeCaravan.state == JourneyState.Prepare)
        {
            Debug.Assert(
                block.canBeginTradePreparation && string.IsNullOrEmpty(block.tradePreparationBlockedReason),
                "Caravan Overview Smoke Test failed: a Preparation Caravan should allow the route flow.");
        }
        else
        {
            Debug.Assert(
                !block.canBeginTradePreparation && !string.IsNullOrEmpty(block.tradePreparationBlockedReason),
                "Caravan Overview Smoke Test failed: a non-Preparation Caravan should explain why trade preparation is blocked.");
        }
    }

    // Verifies that an unlocked empty slot can represent a future Caravan creation target.
    private static void AssertAvailableEmptyCaravanBlock(CaravanBlockViewData block)
    {
        AssertEmptyCaravanData(block);
        Debug.Assert(
            block.slotState == CaravanSlotState.Empty,
            "Caravan Overview Smoke Test failed: an available creation slot must use the Empty state.");
        Debug.Assert(
            string.IsNullOrEmpty(block.lockedReason),
            "Caravan Overview Smoke Test failed: an available empty slot must not have a locked reason.");
    }

    // Verifies that a locked empty slot provides a user-facing explanation.
    private static void AssertLockedEmptyCaravanBlock(CaravanBlockViewData block)
    {
        AssertEmptyCaravanData(block);
        Debug.Assert(
            block.slotState == CaravanSlotState.Locked,
            "Caravan Overview Smoke Test failed: an unavailable slot must use the Locked state.");
        Debug.Assert(
            !string.IsNullOrEmpty(block.lockedReason),
            "Caravan Overview Smoke Test failed: a locked empty slot must provide lockedReason.");
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
        Debug.Assert(
            !block.canBeginTradePreparation && !string.IsNullOrEmpty(block.tradePreparationBlockedReason),
            "Caravan Overview Smoke Test failed: an empty or locked slot must explain why trade preparation is blocked.");
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

    // Verifies that overview selection resolves to exactly one occupied Caravan block.
    private static void AssertSelectedCaravanExists(CaravanOverviewViewData overview)
    {
        var matchCount = 0;

        for (var index = 0; index < overview.caravans.Length; index++)
        {
            CaravanBlockViewData block = overview.caravans[index];
            if (block != null
                && block.slotState == CaravanSlotState.Occupied
                && block.caravanId == overview.selectedCaravanId)
            {
                matchCount++;
            }
        }

        Debug.Assert(
            matchCount == 1,
            "Caravan Overview Smoke Test failed: selectedCaravanId must resolve to one occupied block.");
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
            selectedWagonId = "smoke-wagon-medium",
            wagons = new[] { new WagonViewData { wagonId = "smoke-wagon-medium" } },
            draftAnimals = new[]
            {
                new DraftAnimalViewData
                {
                    draftAnimalId = "smoke-horse",
                    selectedAmount = 2
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
                && prepareSetting.wagons.Length == 1
                && prepareSetting.draftAnimals.Length == 1,
            "Caravan Setting Smoke Test failed: Preparation settings should be editable and populated.");
        Debug.Assert(
            !travelingSetting.canEdit && !string.IsNullOrEmpty(travelingSetting.editBlockedReason),
            "Caravan Setting Smoke Test failed: Traveling settings should be read-only with a reason.");
        Debug.Assert(
            prepareLoad.canEdit
                && string.IsNullOrEmpty(prepareLoad.editBlockedReason)
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
                && defaultSetting.draftAnimals.Length == 0,
            "Caravan Setting Smoke Test failed: setting collections must default to empty arrays.");
        Debug.Assert(
            defaultLoad.availableItems != null
                && defaultLoad.availableItems.Length == 0
                && defaultLoad.plannedItems != null
                && defaultLoad.plannedItems.Length == 0,
            "Caravan Setting Smoke Test failed: load collections must default to empty arrays.");
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
