public interface ITradePrepareStartGateway
{
    TradePrepareGatewayResult TryStartTrade(
        CaravanData caravan,
        float distance,
        string tradeId,
        string routeId,
        bool saveImmediately);
}


public sealed class TradePrepareGatewayResult
{
    public DepartureValidationResult departureValidation;
    public bool recordSucceeded;
}

public sealed class FrameworkTradePrepareStartGateway : ITradePrepareStartGateway
{
    private readonly ND.Framework.TradeStartService tradeStartService;

    public FrameworkTradePrepareStartGateway(ND.Framework.TradeStartService tradeStartService)
    {
        this.tradeStartService = tradeStartService;
    }

    public TradePrepareGatewayResult TryStartTrade(
        CaravanData caravan,
        float distance,
        string tradeId,
        string routeId,
        bool saveImmediately)
    {
        if (tradeStartService == null)
        {
            return new TradePrepareGatewayResult
            {
                departureValidation = new DepartureValidationResult { canDepart = false },
                recordSucceeded = false
            };
        }

        DepartureValidationResult validation = tradeStartService.TryStartTrade(
            caravan,
            distance,
            tradeId,
            routeId,
            saveImmediately);

        return new TradePrepareGatewayResult
        {
            departureValidation = validation,
            recordSucceeded = tradeStartService.LastRecordSucceeded
        };
    }
}

// Temporary runtime implementation. It validates the same CaravanData without
// mutating Framework state, saving, moving scenes, or starting a real journey.
public sealed class TemporaryTradePrepareStartGateway : ITradePrepareStartGateway
{
    public TradePrepareGatewayResult TryStartTrade(
        CaravanData caravan,
        float distance,
        string tradeId,
        string routeId,
        bool saveImmediately)
    {
        DepartureValidationResult validation = CaravanValidator.Validate(caravan);
        return new TradePrepareGatewayResult
        {
            departureValidation = validation,
            recordSucceeded = validation != null && validation.canDepart
        };
    }
}
