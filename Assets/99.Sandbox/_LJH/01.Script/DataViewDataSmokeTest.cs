using UnityEngine;

public class DataViewDataSmokeTest : MonoBehaviour
{
    [SerializeField] private TownData town;
    [SerializeField] private RouteData route;
    [SerializeField] private TradeItemData item;

    [ContextMenu("Run ViewData Smoke Test")]
    private void RunViewDataSmokeTest()
    {
        if (town == null || route == null || item == null)
        {
            Debug.LogError("Smoke Test failed: town, route, or item is not assigned.");
            return;
        }

        if (route.FromTown == null || route.ToTown == null)
        {
            Debug.LogError("Smoke Test failed: route FromTown or ToTown is not assigned.");
            return;
        }

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
            purchasePrice = item.BaseBuyPrice,
            sellPrice = item.BaseSellPrice,
            ownedAmount = 0,
            selectedAmount = 0,
            canBuy = true,
            canSell = false,
            disabledReason = string.Empty
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
            requiredFoodQuantity = route.BaseRequiredFoodQuantity,
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
            requiredFoodQuantity = routeViewData.requiredFoodQuantity,
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
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            currentLoad = saveData.caravan.currentLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedFoodQuantity = routeViewData.requiredFoodQuantity,
            requiredFoodQuantity = routeViewData.requiredFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var successCondition = conditionEvaluator.Evaluate(successInput);

        var notEnoughMoneyInput = new TradePrepareConditionInput
        {
            isRouteSelected = true,
            isRouteUnlocked = true,
            currentTradingCurrency = 0,
            totalPurchaseCost = itemViewData.purchasePrice > 0 ? itemViewData.purchasePrice : 1,
            currentLoad = saveData.caravan.currentLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedFoodQuantity = routeViewData.requiredFoodQuantity,
            requiredFoodQuantity = routeViewData.requiredFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var notEnoughMoneyCondition = conditionEvaluator.Evaluate(notEnoughMoneyInput);

        var routeNotSelectedInput = new TradePrepareConditionInput
        {
            isRouteSelected = false,
            isRouteUnlocked = true,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            currentLoad = saveData.caravan.currentLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedFoodQuantity = routeViewData.requiredFoodQuantity,
            requiredFoodQuantity = routeViewData.requiredFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var routeNotSelectedCondition = conditionEvaluator.Evaluate(routeNotSelectedInput);

        var routeLockedInput = new TradePrepareConditionInput
        {
            isRouteSelected = true,
            isRouteUnlocked = false,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            currentLoad = saveData.caravan.currentLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedFoodQuantity = routeViewData.requiredFoodQuantity,
            requiredFoodQuantity = routeViewData.requiredFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var routeLockedCondition = conditionEvaluator.Evaluate(routeLockedInput);

        var notEnoughFoodInput = new TradePrepareConditionInput
        {
            isRouteSelected = true,
            isRouteUnlocked = true,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            currentLoad = saveData.caravan.currentLoad,
            maxLoad = saveData.caravan.maxLoad,
            loadedFoodQuantity = 0,
            requiredFoodQuantity = Mathf.Max(1, routeViewData.requiredFoodQuantity),
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var notEnoughFoodCondition = conditionEvaluator.Evaluate(notEnoughFoodInput);

        var loadExceededInput = new TradePrepareConditionInput
        {
            isRouteSelected = true,
            isRouteUnlocked = true,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            currentLoad = 120,
            maxLoad = 100,
            loadedFoodQuantity = routeViewData.requiredFoodQuantity,
            requiredFoodQuantity = routeViewData.requiredFoodQuantity,
            selectedMercenaryPower = routeViewData.requiredMercenaryPower,
            requiredMercenaryPower = routeViewData.requiredMercenaryPower
        };

        var loadExceededCondition = conditionEvaluator.Evaluate(loadExceededInput);

        var multipleWarningInput = new TradePrepareConditionInput
        {
            isRouteSelected = true,
            isRouteUnlocked = true,
            currentTradingCurrency = saveData.player.tradingCurrency,
            totalPurchaseCost = itemViewData.purchasePrice,
            currentLoad = 120,
            maxLoad = 100,
            loadedFoodQuantity = 0,
            requiredFoodQuantity = Mathf.Max(1, routeViewData.requiredFoodQuantity),
            selectedMercenaryPower = 0,
            requiredMercenaryPower = Mathf.Max(1, routeViewData.requiredMercenaryPower)
        };

        var multipleWarningCondition = conditionEvaluator.Evaluate(multipleWarningInput);

        var prepareViewData = new TradePrepareViewData
        {
            currentTownId = town.TownId,
            currentTownName = town.DisplayName,

            currentTradingCurrency = successInput.currentTradingCurrency,
            currentDevelopmentCurrency = saveData.player.developmentCurrency,

            towns = new[] { townViewData },
            routes = new[] { routeViewData },
            tradeItems = new[] { itemViewData },

            selectedRouteId = route.RouteId,

            currentLoad = successInput.currentLoad,
            maxLoad = successInput.maxLoad,

            totalPurchaseCost = successInput.totalPurchaseCost,

            loadedFoodQuantity = successInput.loadedFoodQuantity,
            requiredFoodQuantity = successInput.requiredFoodQuantity,

            selectedMercenaryPower = successInput.selectedMercenaryPower,
            requiredMercenaryPower = successInput.requiredMercenaryPower,

            startCondition = successCondition
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
            maxLoad = notEnoughMoneyInput.maxLoad,

            totalPurchaseCost = notEnoughMoneyInput.totalPurchaseCost,

            loadedFoodQuantity = notEnoughMoneyInput.loadedFoodQuantity,
            requiredFoodQuantity = notEnoughMoneyInput.requiredFoodQuantity,

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
            maxLoad = notEnoughFoodInput.maxLoad,

            totalPurchaseCost = notEnoughFoodInput.totalPurchaseCost,

            loadedFoodQuantity = notEnoughFoodInput.loadedFoodQuantity,
            requiredFoodQuantity = notEnoughFoodInput.requiredFoodQuantity,

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
            maxLoad = loadExceededInput.maxLoad,

            totalPurchaseCost = loadExceededInput.totalPurchaseCost,

            loadedFoodQuantity = loadExceededInput.loadedFoodQuantity,
            requiredFoodQuantity = loadExceededInput.requiredFoodQuantity,

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
            maxLoad = multipleWarningInput.maxLoad,

            totalPurchaseCost = multipleWarningInput.totalPurchaseCost,

            loadedFoodQuantity = multipleWarningInput.loadedFoodQuantity,
            requiredFoodQuantity = multipleWarningInput.requiredFoodQuantity,

            selectedMercenaryPower = multipleWarningInput.selectedMercenaryPower,
            requiredMercenaryPower = multipleWarningInput.requiredMercenaryPower,

            startCondition = multipleWarningCondition
        };


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

        // Not Enough Food
        Debug.Log($"Prepare Warning: {notEnoughFoodPrepareViewData.startCondition.warningMessages[0]}");

        Debug.Assert(notEnoughFoodPrepareViewData.startCondition != null, "Prepare Smoke Test failed: food condition should not be null.");
        Debug.Assert(notEnoughFoodPrepareViewData.startCondition.canStart, "Prepare Smoke Test failed: food shortage should allow start.");
        Debug.Assert(notEnoughFoodPrepareViewData.startCondition.hasWarning, "Prepare Smoke Test failed: food shortage should show warning.");
        Debug.Assert(notEnoughFoodPrepareViewData.startCondition.warningMessages.Count > 0, "Prepare Smoke Test failed: warning message is required.");

        // Load Exceeded
        Debug.Log($"Prepare Warning: {loadExceededPrepareViewData.startCondition.warningMessages[0]}");


        Debug.Assert(loadExceededPrepareViewData.startCondition != null, "Prepare Smoke Test failed: load condition should not be null.");
        Debug.Assert(loadExceededPrepareViewData.startCondition.canStart, "Prepare Smoke Test failed: load exceeded should allow start.");
        Debug.Assert(loadExceededPrepareViewData.startCondition.hasWarning, "Prepare Smoke Test failed: load exceeded should show warning.");
        Debug.Assert(loadExceededPrepareViewData.startCondition.warningMessages.Count > 0, "Prepare Smoke Test failed: warning message is required.");
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

        Debug.Log($"Result Message: [{result.messages[0].type}] {result.messages[0].messageCode} / {result.messages[0].messageText}");
        Debug.Assert(result.messages.Count > 0, "Smoke Test failed: result messages should not be empty.");
        Debug.Assert(result.failureReason == FailureReason.FoodShortage, "Smoke Test failed: failureReason mismatch.");

        Debug.Log("ViewData Smoke Test PASSED.");
    }
}
