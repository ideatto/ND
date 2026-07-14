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

    private readonly ND.Framework.TradeStartService tradeStartService;
    private readonly TradePrepareViewDataBuilder viewDataBuilder;
    private readonly ITradePrepareCommitSink commitSink;

    public TradePrepareStartAdapter(ND.Framework.TradeStartService tradeStartService)
        : this(tradeStartService, new TradePrepareViewDataBuilder(), null)
    {
    }

    public TradePrepareStartAdapter(
        ND.Framework.TradeStartService tradeStartService,
        TradePrepareViewDataBuilder viewDataBuilder)
        : this(tradeStartService, viewDataBuilder, null)
    {
    }

    public TradePrepareStartAdapter(
        ND.Framework.TradeStartService tradeStartService,
        TradePrepareViewDataBuilder viewDataBuilder,
        ITradePrepareCommitSink commitSink)
    {
        this.tradeStartService = tradeStartService;
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

        if (tradeStartService == null)
        {
            return CreateFailure(
                ErrorStartServiceMissing,
                "Trade start service is not connected.",
                tradeId,
                viewData.startCondition,
                null);
        }

        TradePrepareCommitData commitData = CreateCommitData(draft, viewData, tradeId.Trim(), route.RouteId);
        if (commitData.mercenaryCost > 0L && commitSink == null)
        {
            return CreateFailure(
                ErrorCommitSinkMissing,
                "Selected mercenary cost cannot be committed because the settlement commit sink is not connected.",
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

        CaravanData caravan = TradePrepareCaravanFactory.Create(draft, context);
        DepartureValidationResult departure;
        try
        {
            departure = tradeStartService.TryStartTrade(
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

        if (departure == null || !departure.canDepart)
        {
            commitSink?.Rollback(tradeId.Trim());
            return CreateFailure(
                ErrorCoreDepartureBlocked,
                "Core departure validation blocked trade start.",
                tradeId,
                viewData.startCondition,
                departure,
                commitData);
        }

        if (!tradeStartService.LastRecordSucceeded)
        {
            commitSink?.Rollback(tradeId.Trim());
            return CreateFailure(
                ErrorFrameworkRecordFailed,
                "Framework failed to record the started trade.",
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

    private static TradePrepareCommitData CreateCommitData(
        TradePrepareDraft draft,
        TradePrepareViewData viewData,
        string tradeId,
        string routeId)
    {
        var mercenaryIds = new string[draft.SelectedMercenaryIds.Count];
        for (int index = 0; index < draft.SelectedMercenaryIds.Count; index++)
        {
            mercenaryIds[index] = draft.SelectedMercenaryIds[index];
        }

        return new TradePrepareCommitData
        {
            tradeId = tradeId,
            routeId = routeId,
            mercenaryCost = viewData.mercenaryCost > 0L ? viewData.mercenaryCost : 0L,
            selectedMercenaryIds = mercenaryIds
        };
    }
}
