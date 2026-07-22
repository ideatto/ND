using System;

public sealed class TradePrepareStartAdapter
{
    public const string ErrorNone = "";
    public const string ErrorPrepareBlocked = "PREPARE_BLOCKED";
    public const string ErrorInvalidTradeId = "INVALID_TRADE_ID";
    public const string ErrorRouteNotFound = "ROUTE_NOT_FOUND";
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
            purchaseCost = 0L,
            foodCost = 0L,
            mercenaryCost = viewData.mercenaryCost > 0L ? viewData.mercenaryCost : 0L,
            // Market sale revenue is committed immediately by MarketTransactionCommand.
            // Departure/arrival settlement must never credit the carried cargo automatically.
            estimatedSellRevenue = 0L,
            purchasedItems = new TradeItemBundle[0],
            selectedMercenaryIds = mercenaryIds
        };
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
