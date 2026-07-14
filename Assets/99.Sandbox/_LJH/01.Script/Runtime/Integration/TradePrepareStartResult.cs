[System.Serializable]
public sealed class TradePrepareStartResult
{
    public bool succeeded;
    public string errorCode;
    public string errorMessage;
    public string tradeId;
    public TradePrepareCommitData commitData;
    public TradePrepareConditionResult prepareCondition;
    public DepartureValidationResult departureValidation;
}
