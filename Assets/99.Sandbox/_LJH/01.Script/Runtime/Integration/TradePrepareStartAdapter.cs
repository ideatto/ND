using System;
using System.Collections.Generic;

public sealed class TradePrepareStartAdapter
{
    public const string ErrorNone = "";
    public const string ErrorPrepareBlocked = "PREPARE_BLOCKED";
    public const string ErrorInvalidTradeId = "INVALID_TRADE_ID";
    public const string ErrorRouteNotFound = "ROUTE_NOT_FOUND";
    public const string ErrorRouteValidationFailed = "ROUTE_VALIDATION_FAILED";
    public const string ErrorStartServiceMissing = "START_SERVICE_MISSING";
    public const string ErrorCoreDepartureBlocked = "CORE_DEPARTURE_BLOCKED";
    public const string ErrorFrameworkRecordFailed = "FRAMEWORK_RECORD_FAILED";
    public const string ErrorCommitSinkMissing = "COMMIT_SINK_MISSING";
    public const string ErrorCommitStageFailed = "COMMIT_STAGE_FAILED";

    private readonly ITradePrepareStartGateway startGateway;
    private readonly TradePrepareViewDataBuilder viewDataBuilder;
    private readonly ITradePrepareCommitSink commitSink;

    public TradePrepareStartAdapter(ND.Framework.TradeStartService tradeStartService)
        : this(CreateFrameworkGateway(tradeStartService), new TradePrepareViewDataBuilder(), null)
    {
    }

    public TradePrepareStartAdapter(
        ND.Framework.TradeStartService tradeStartService,
        TradePrepareViewDataBuilder viewDataBuilder)
        : this(CreateFrameworkGateway(tradeStartService), viewDataBuilder, null)
    {
    }

    public TradePrepareStartAdapter(
        ND.Framework.TradeStartService tradeStartService,
        TradePrepareViewDataBuilder viewDataBuilder,
        ITradePrepareCommitSink commitSink)
        : this(CreateFrameworkGateway(tradeStartService), viewDataBuilder, commitSink)
    {
    }

    public TradePrepareStartAdapter(
        ITradePrepareStartGateway startGateway,
        TradePrepareViewDataBuilder viewDataBuilder,
        ITradePrepareCommitSink commitSink)
    {
        this.startGateway = startGateway;
        this.viewDataBuilder = viewDataBuilder ?? new TradePrepareViewDataBuilder();
        this.commitSink = commitSink;
    }

    /// <summary>Runs projected departure checks without mutating SaveData or invoking start.</summary>
    public TradePrepareStartResult ValidateDeparture(
        TradePrepareDraft draft,
        TradePrepareBuildContext context)
    {
        draft = draft ?? new TradePrepareDraft();
        context = context ?? new TradePrepareBuildContext();
        TradePrepareViewData viewData = viewDataBuilder.Build(draft, context);

        if (!TradePrepareCaravanFactory.TryCreateDeparture(
                draft, context, out CaravanData caravan,
                out string caravanErrorCode, out string caravanErrorMessage))
        {
            return CreateFailure(caravanErrorCode, caravanErrorMessage, string.Empty,
                viewData.startCondition, null);
        }

        DepartureValidationResult departure = CaravanValidator.Validate(caravan);
        if (departure == null || !departure.canDepart)
        {
            return CreateFailure(ErrorCoreDepartureBlocked,
                CreateCoreDepartureBlockedMessage(departure), string.Empty,
                viewData.startCondition, departure);
        }

        if (viewData.startCondition == null || !viewData.startCondition.canStart)
        {
            return CreateFailure(ErrorPrepareBlocked,
                viewData.startCondition != null
                    ? viewData.startCondition.disabledReason
                    : "Trade preparation validation failed.",
                string.Empty, viewData.startCondition, departure);
        }

        RouteData route = TradePrepareCaravanFactory.ResolveSelectedRoute(draft, context);
        if (route == null)
        {
            return CreateFailure(ErrorRouteNotFound, "Selected route could not be resolved.",
                string.Empty, viewData.startCondition, departure);
        }

        if (!TryValidateRouteForStart(draft, context, route, out string routeValidationMessage))
        {
            return CreateFailure(ErrorRouteValidationFailed, routeValidationMessage, string.Empty,
                viewData.startCondition, departure);
        }

        return new TradePrepareStartResult
        {
            succeeded = true,
            errorCode = ErrorNone,
            errorMessage = string.Empty,
            prepareCondition = viewData.startCondition,
            departureValidation = departure
        };
    }
    public TradePrepareStartResult TryStartTrade(
        TradePrepareDraft draft,
        TradePrepareBuildContext context,
        string tradeId,
        bool saveImmediately = true)
    {
        draft = draft ?? new TradePrepareDraft();
        context = context ?? new TradePrepareBuildContext();
        TradePrepareViewData viewData = viewDataBuilder.Build(draft, context);
        if (viewData.startCondition == null || !viewData.startCondition.canStart)
        {
            return CreateFailure(
                ErrorPrepareBlocked,
                viewData.startCondition != null ? viewData.startCondition.disabledReason : "Trade preparation validation failed.",
                tradeId,
                viewData.startCondition,
                null);
        }

        if (string.IsNullOrWhiteSpace(tradeId))
        {
            return CreateFailure(
                ErrorInvalidTradeId,
                "Trade ID is required.",
                tradeId,
                viewData.startCondition,
                null);
        }

        RouteData route = TradePrepareCaravanFactory.ResolveSelectedRoute(draft, context);
        if (route == null)
        {
            return CreateFailure(
                ErrorRouteNotFound,
                "Selected route could not be resolved.",
                tradeId,
                viewData.startCondition,
                null);
        }

        if(!TryValidateRouteForStart(draft, context, route, out string routeValidationMessage))
        {
            return CreateFailure(ErrorRouteValidationFailed, routeValidationMessage, tradeId, viewData.startCondition, null);
        }

        if (startGateway == null)
        {
            return CreateFailure(
                ErrorStartServiceMissing,
                "Trade start service is not connected.",
                tradeId,
                viewData.startCondition,
                null);
        }

        if (!TradePrepareCaravanFactory.TryCreateDeparture(
            draft,
            context,
            out CaravanData caravan,
            out string caravanErrorCode,
            out string caravanErrorMessage))
        {
            return CreateFailure(
                caravanErrorCode,
                caravanErrorMessage,
                tradeId,
                viewData.startCondition,
                null);
        }

        TradePrepareCommitData commitData = CreateCommitData(
            draft,
            viewData,
            tradeId.Trim(),
            route.RouteId,
            caravan.caravanId);
        if ((commitData.mercenaryCost > 0L || commitData.purchasedItems.Length > 0) && commitSink == null)
        {
            return CreateFailure(
                ErrorCommitSinkMissing,
                "Purchased cargo and preparation costs cannot be preserved because the settlement commit sink is not connected.",
                tradeId,
                viewData.startCondition,
                null,
                commitData);
        }

        bool staged = commitSink == null || commitSink.TryStage(commitData.CreateSnapshot());
        if (!staged)
        {
            return CreateFailure(
                ErrorCommitStageFailed,
                "Settlement cost data could not be staged.",
                tradeId,
                viewData.startCondition,
                null,
                commitData);
        }

        TradePrepareGatewayResult gatewayResult;
        try
        {
            gatewayResult = startGateway.TryStartTrade(
                caravan,
                route.Distance,
                tradeId.Trim(),
                route.RouteId,
                saveImmediately);
        }
        catch
        {
            commitSink?.Rollback(tradeId.Trim());
            throw;
        }

        DepartureValidationResult departure = gatewayResult != null
            ? gatewayResult.departureValidation
            : null;

        if (departure != null && !departure.canDepart && HasDepartureBlockReasons(departure))
        {
            commitSink?.Rollback(tradeId.Trim());
            return CreateFailure(
                ErrorCoreDepartureBlocked,
                CreateCoreDepartureBlockedMessage(departure),
                tradeId,
                viewData.startCondition,
                departure,
                commitData);
        }

        if (gatewayResult == null || !gatewayResult.recordSucceeded)
        {
            commitSink?.Rollback(tradeId.Trim());
            return CreateFailure(
                ErrorFrameworkRecordFailed,
                "The start gateway failed to record the started trade.",
                tradeId,
                viewData.startCondition,
                departure,
                commitData);
        }

        if (departure == null || !departure.canDepart)
        {
            commitSink?.Rollback(tradeId.Trim());
            return CreateFailure(
                ErrorCoreDepartureBlocked,
                CreateCoreDepartureBlockedMessage(departure),
                tradeId,
                viewData.startCondition,
                departure,
                commitData);
        }

        return new TradePrepareStartResult
        {
            succeeded = true,
            errorCode = ErrorNone,
            errorMessage = string.Empty,
            tradeId = tradeId.Trim(),
            commitData = commitData.CreateSnapshot(),
            prepareCondition = viewData.startCondition,
            departureValidation = departure
        };
    }

    private static bool TryValidateRouteForStart(
    TradePrepareDraft draft,
    TradePrepareBuildContext context,
    RouteData route,
    out string errorMessage)
    {
        errorMessage = string.Empty;

        if (draft == null ||
            context == null ||
            route == null)
        {
            errorMessage = "Route validation input is missing.";
            return false;
        }

        string caravanId =
            draft.departureCaravanId?.Trim() ?? string.Empty;

        string currentTownId =
            draft.currentTownId?.Trim() ?? string.Empty;

        string destinationTownId =
            draft.selectedDestinationTownId?.Trim()
            ?? string.Empty;

        // 선택 Caravan이 최신 SaveData에도 존재하는지 확인하고,
        // 저장된 실제 위치와 Draft 위치를 비교한다.
        if (!string.IsNullOrEmpty(caravanId))
        {
            if (!ND.Framework.SaveDataLookup.TryGetCaravan(
                context.saveData,
                caravanId,
                out ND.Framework.CaravanSaveData savedCaravan))
            {
                errorMessage =
                    "The selected departure Caravan does not exist.";
                return false;
            }

            if (!string.Equals(
                savedCaravan.currentTownId,
                currentTownId,
                StringComparison.Ordinal))
            {
                errorMessage =
                    "The Caravan location changed. Refresh the preparation screen.";
                return false;
            }
        }

        TownData currentTown =
            TradePrepareViewDataBuilder.FindTown(
                context.towns,
                currentTownId);

        if (currentTown == null)
        {
            errorMessage =
                "The departure town does not exist.";
            return false;
        }

        TownData destinationTown =
            TradePrepareViewDataBuilder.FindTown(
                context.towns,
                destinationTownId);

        if (destinationTown == null)
        {
            errorMessage =
                "The destination town does not exist.";
            return false;
        }

        if (!IsTownUnlocked(currentTown, context.saveData))
        {
            errorMessage =
                "The departure town is locked.";
            return false;
        }

        if (!IsTownUnlocked(destinationTown, context.saveData))
        {
            errorMessage =
                "The destination town is locked.";
            return false;
        }

        if (!IsRouteUnlocked(route, context.saveData))
        {
            errorMessage =
                "The selected route is locked.";
            return false;
        }

        if (!string.Equals(
            route.FromTownId,
            currentTownId,
            StringComparison.Ordinal))
        {
            errorMessage =
                "The selected route does not depart from the current town.";
            return false;
        }

        if (!string.Equals(
            route.ToTownId,
            destinationTownId,
            StringComparison.Ordinal))
        {
            errorMessage =
                "The selected route does not lead to the selected destination.";
            return false;
        }

        if (!ContainsRoute(
            currentTown.AvailableRoutes,
            route.RouteId))
        {
            errorMessage =
                "The selected route is not registered in the departure town.";
            return false;
        }

        return true;
    }

    private static bool IsTownUnlocked(
    TownData town,
    ND.Framework.SaveData saveData)
    {
        if (town == null)
        {
            return false;
        }

        return town.UnlockedByDefault ||
            ContainsId(
                saveData?.world?.unlockedTownIds,
                town.TownId);
    }

    private static bool IsRouteUnlocked(
        RouteData route,
        ND.Framework.SaveData saveData)
    {
        if (route == null)
        {
            return false;
        }

        return route.UnlockedByDefault ||
            ContainsId(
                saveData?.world?.unlockedRouteIds,
                route.RouteId);
    }

    private static bool ContainsId(
        IList<string> ids,
        string expectedId)
    {
        if (ids == null ||
            string.IsNullOrWhiteSpace(expectedId))
        {
            return false;
        }

        foreach (string id in ids)
        {
            if (string.Equals(
                id,
                expectedId,
                StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsRoute(
        RouteData[] routes,
        string routeId)
    {
        if (routes == null ||
            string.IsNullOrWhiteSpace(routeId))
        {
            return false;
        }

        foreach (RouteData availableRoute in routes)
        {
            if (availableRoute != null &&
                string.Equals(
                    availableRoute.RouteId,
                    routeId,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static ITradePrepareStartGateway CreateFrameworkGateway(
        ND.Framework.TradeStartService tradeStartService)
    {
        return tradeStartService != null
            ? new FrameworkTradePrepareStartGateway(tradeStartService)
            : null;
    }

    private static TradePrepareStartResult CreateFailure(
        string errorCode,
        string errorMessage,
        string tradeId,
        TradePrepareConditionResult prepareCondition,
        DepartureValidationResult departureValidation,
        TradePrepareCommitData commitData = null)
    {
        return new TradePrepareStartResult
        {
            succeeded = false,
            errorCode = errorCode,
            errorMessage = errorMessage ?? string.Empty,
            tradeId = tradeId ?? string.Empty,
            commitData = commitData != null ? commitData.CreateSnapshot() : null,
            prepareCondition = prepareCondition,
            departureValidation = departureValidation
        };
    }

    private static string CreateCoreDepartureBlockedMessage(DepartureValidationResult departureValidation)
    {
        if (departureValidation == null
            || departureValidation.reasons == null
            || departureValidation.reasons.Count == 0)
        {
            return "Core departure validation blocked trade start without a detailed reason.";
        }

        return "Core departure validation blocked trade start: "
            + string.Join(", ", departureValidation.reasons.ConvertAll(reason => reason.ToString()).ToArray())
            + ".";
    }

    private static bool HasDepartureBlockReasons(DepartureValidationResult departureValidation)
    {
        return departureValidation != null
            && departureValidation.reasons != null
            && departureValidation.reasons.Count > 0;
    }

    private static TradePrepareCommitData CreateCommitData(
        TradePrepareDraft draft,
        TradePrepareViewData viewData,
        string tradeId,
        string routeId,
        string departureCaravanId)
    {
        var mercenaryIds = new string[draft.SelectedMercenaryIds.Count];
        for (int index = 0; index < draft.SelectedMercenaryIds.Count; index++)
        {
            mercenaryIds[index] = draft.SelectedMercenaryIds[index];
        }

        return new TradePrepareCommitData
        {
            // Keeps the departure snapshot scoped to the Caravan selected inside TradePrepareUI.
            caravanId = departureCaravanId,
            tradeId = tradeId,
            currentTownId = draft.currentTownId,
            selectedDestinationTownId = draft.selectedDestinationTownId,
            routeId = routeId,
            selectedWagonId = draft.selectedWagonId,
            selectedAnimals = CreateSelectedAnimalSnapshots(draft),
            // Cargo and draft-animal food are committed by the Market transaction before
            // departure. The journey commit records only non-market preparation costs; carrying
            // these values forward would charge the selected Caravan's purchase a second time.
            // Purchase money was already applied by Market. Keep the amount and item lines in
            // the persisted preparation snapshot for receipt reconstruction only.
            purchaseCost = viewData.totalPurchaseCost > 0L ? viewData.totalPurchaseCost : 0L,
            foodCost = 0L,
            mercenaryCost = viewData.mercenaryCost > 0L ? viewData.mercenaryCost : 0L,
            // Arrival sales are also Market-owned and must not be projected into departure.
            estimatedSellRevenue = 0L,
            purchasedItems = CreatePurchasedItemSnapshots(draft),
            selectedMercenaryIds = mercenaryIds
        };
    }

    private static TradeItemBundle[] CreatePurchasedItemSnapshots(TradePrepareDraft draft)
    {
        if (draft == null || draft.selectedBuyItems == null)
        {
            return new TradeItemBundle[0];
        }

        var result = new TradeItemBundle[draft.selectedBuyItems.Count];
        for (int index = 0; index < draft.selectedBuyItems.Count; index++)
        {
            TradeItemBundle item = draft.selectedBuyItems[index];
            result[index] = item == null ? null : new TradeItemBundle
            {
                itemId = item.itemId ?? string.Empty,
                quantity = Math.Max(0, item.quantity),
                purchaseUnitPrice = Math.Max(0L, item.purchaseUnitPrice),
                sellUnitPrice = Math.Max(0L, item.sellUnitPrice)
            };
        }

        return result;
    }

    private static DraftAnimalSelectionData[] CreateSelectedAnimalSnapshots(
        TradePrepareDraft draft)
    {
        if (draft == null || draft.selectedAnimals == null)
        {
            return new DraftAnimalSelectionData[0];
        }

        var result = new DraftAnimalSelectionData[draft.selectedAnimals.Count];
        for (int index = 0; index < draft.selectedAnimals.Count; index++)
        {
            DraftAnimalSelectionData selected = draft.selectedAnimals[index];
            result[index] = selected == null ? null : new DraftAnimalSelectionData
            {
                draftAnimalId = selected.draftAnimalId ?? string.Empty,
                quantity = selected.quantity > 0 ? selected.quantity : 0
            };
        }

        return result;
    }

}
