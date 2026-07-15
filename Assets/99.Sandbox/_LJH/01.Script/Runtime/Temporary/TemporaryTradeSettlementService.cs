[System.Serializable]
public sealed class TemporaryTradeSettlementResult
{
    public bool succeeded;
    public string errorCode;
    public string errorMessage;
    public string tradeId;
    public long currencyBefore;
    public long purchaseCost;
    public long foodCost;
    public long mercenaryCost;
    public long sellRevenue;
    public long currencyAfter;
}

// Preview-only settlement. It proves that preparation costs are charged once
// at claim time; it does not replace Economy calculations or mutate SaveData.
public sealed class TemporaryTradeSettlementService
{
    public const string ErrorNone = "";
    public const string ErrorTradeNotFound = "TRADE_NOT_FOUND";
    public const string ErrorInsufficientCurrency = "INSUFFICIENT_CURRENCY";

    private readonly ITradePrepareCommitSource commitSource;
    private readonly ITradePrepareCommitCompletion commitCompletion;

    public TemporaryTradeSettlementService(
        ITradePrepareCommitSource commitSource,
        ITradePrepareCommitCompletion commitCompletion)
    {
        this.commitSource = commitSource;
        this.commitCompletion = commitCompletion;
    }

    public TemporaryTradeSettlementService(InMemoryTradePrepareCommitSink commitStore)
        : this(commitStore, commitStore)
    {
    }

    public TemporaryTradeSettlementResult TryClaim(string tradeId, long currentCurrency)
    {
        long normalizedCurrency = currentCurrency > 0L ? currentCurrency : 0L;
        if (commitSource == null
            || !commitSource.TryGet(tradeId, out TradePrepareCommitData commitData))
        {
            return new TemporaryTradeSettlementResult
            {
                succeeded = false,
                errorCode = ErrorTradeNotFound,
                errorMessage = "No staged trade data exists, or this trade was already claimed.",
                tradeId = tradeId ?? string.Empty,
                currencyBefore = normalizedCurrency,
                currencyAfter = normalizedCurrency
            };
        }

        if (normalizedCurrency < commitData.TotalCost)
        {
            return new TemporaryTradeSettlementResult
            {
                succeeded = false,
                errorCode = ErrorInsufficientCurrency,
                errorMessage = "Current currency is lower than the staged preparation cost.",
                tradeId = commitData.tradeId,
                currencyBefore = normalizedCurrency,
                purchaseCost = commitData.purchaseCost,
                foodCost = commitData.foodCost,
                mercenaryCost = commitData.mercenaryCost,
                sellRevenue = commitData.estimatedSellRevenue,
                currencyAfter = normalizedCurrency
            };
        }

        if (commitCompletion == null
            || !commitCompletion.TryComplete(tradeId, out commitData))
        {
            return new TemporaryTradeSettlementResult
            {
                succeeded = false,
                errorCode = ErrorTradeNotFound,
                errorMessage = "The staged trade was removed before settlement could complete.",
                tradeId = tradeId ?? string.Empty,
                currencyBefore = normalizedCurrency,
                currencyAfter = normalizedCurrency
            };
        }

        long currencyAfterCosts = normalizedCurrency - commitData.TotalCost;
        long finalCurrency = AddClamped(currencyAfterCosts, commitData.estimatedSellRevenue);

        return new TemporaryTradeSettlementResult
        {
            succeeded = true,
            errorCode = ErrorNone,
            errorMessage = string.Empty,
            tradeId = commitData.tradeId,
            currencyBefore = normalizedCurrency,
            purchaseCost = commitData.purchaseCost,
            foodCost = commitData.foodCost,
            mercenaryCost = commitData.mercenaryCost,
            sellRevenue = commitData.estimatedSellRevenue,
            currencyAfter = finalCurrency
        };
    }

    private static long AddClamped(long left, long right)
    {
        left = left > 0L ? left : 0L;
        right = right > 0L ? right : 0L;
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }
}
