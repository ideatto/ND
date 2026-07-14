[System.Serializable]
public sealed class TradePrepareCommitData
{
    public string tradeId;
    public string routeId;
    public long mercenaryCost;
    public string[] selectedMercenaryIds = new string[0];

    public TradePrepareCommitData CreateSnapshot()
    {
        return new TradePrepareCommitData
        {
            tradeId = tradeId ?? string.Empty,
            routeId = routeId ?? string.Empty,
            mercenaryCost = mercenaryCost > 0L ? mercenaryCost : 0L,
            selectedMercenaryIds = selectedMercenaryIds != null
                ? (string[])selectedMercenaryIds.Clone()
                : new string[0]
        };
    }
}

// Framework/Integration owns the implementation. UI & Data only sends a snapshot
// and never creates or mutates Framework SaveData through this contract.
public interface ITradePrepareCommitSink
{
    // Stage must attach the snapshot to Framework-owned state so the existing
    // TradeStartService save includes it. Return false without partial mutation.
    bool TryStage(TradePrepareCommitData commitData);

    // Restore the Framework-owned state if departure fails after staging.
    void Rollback(string tradeId);
}
