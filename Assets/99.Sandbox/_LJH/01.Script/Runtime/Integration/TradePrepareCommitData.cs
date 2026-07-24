[System.Serializable]
public sealed class TradePrepareCommitData
{
    // Identifies the Caravan that owns this departure snapshot.
    // Framework assigns this ID; UI only forwards it from the selected overview slot.
    public string caravanId;

    public string tradeId;
    public string currentTownId;
    public string selectedDestinationTownId;
    public string routeId;
    public string selectedWagonId;
    public DraftAnimalSelectionData[] selectedAnimals = new DraftAnimalSelectionData[0];
    // Departure-time previews for the temporary settlement only. Production
    // settlement may replace these with event-adjusted Economy results.
    public long purchaseCost;
    public long foodCost;
    public long mercenaryCost;
    public long estimatedSellRevenue;
    public TradeItemBundle[] purchasedItems = new TradeItemBundle[0];
    public string[] selectedMercenaryIds = new string[0];

    public long TotalCost => AddClamped(AddClamped(purchaseCost, foodCost), mercenaryCost);

    public TradePrepareCommitData CreateSnapshot()
    {
        return new TradePrepareCommitData
        {
            caravanId = caravanId ?? string.Empty,
            tradeId = tradeId ?? string.Empty,
            currentTownId = currentTownId ?? string.Empty,
            selectedDestinationTownId = selectedDestinationTownId ?? string.Empty,
            routeId = routeId ?? string.Empty,
            selectedWagonId = selectedWagonId ?? string.Empty,
            selectedAnimals = CloneSelectedAnimals(selectedAnimals),
            purchaseCost = NormalizeMoney(purchaseCost),
            foodCost = NormalizeMoney(foodCost),
            mercenaryCost = mercenaryCost > 0L ? mercenaryCost : 0L,
            estimatedSellRevenue = NormalizeMoney(estimatedSellRevenue),
            purchasedItems = ClonePurchasedItems(purchasedItems),
            selectedMercenaryIds = selectedMercenaryIds != null
                ? (string[])selectedMercenaryIds.Clone()
                : new string[0]
        };
    }

    private static DraftAnimalSelectionData[] CloneSelectedAnimals(
        DraftAnimalSelectionData[] source)
    {
        if (source == null || source.Length == 0)
        {
            return new DraftAnimalSelectionData[0];
        }

        var result = new DraftAnimalSelectionData[source.Length];
        for (int index = 0; index < source.Length; index++)
        {
            DraftAnimalSelectionData animal = source[index];
            result[index] = animal == null ? null : new DraftAnimalSelectionData
            {
                draftAnimalId = animal.draftAnimalId ?? string.Empty,
                quantity = animal.quantity > 0 ? animal.quantity : 0
            };
        }

        return result;
    }

    private static long NormalizeMoney(long value)
    {
        return value > 0L ? value : 0L;
    }

    private static TradeItemBundle[] ClonePurchasedItems(TradeItemBundle[] source)
    {
        if (source == null || source.Length == 0)
        {
            return new TradeItemBundle[0];
        }

        var result = new TradeItemBundle[source.Length];
        for (int index = 0; index < source.Length; index++)
        {
            TradeItemBundle item = source[index];
            result[index] = item == null ? null : new TradeItemBundle
            {
                itemId = item.itemId ?? string.Empty,
                quantity = item.quantity > 0 ? item.quantity : 0,
                purchaseUnitPrice = NormalizeMoney(item.purchaseUnitPrice),
                sellUnitPrice = NormalizeMoney(item.sellUnitPrice)
            };
        }

        return result;
    }

    private static long AddClamped(long left, long right)
    {
        left = NormalizeMoney(left);
        right = NormalizeMoney(right);
        return left > long.MaxValue - right ? long.MaxValue : left + right;
    }
}

// Framework/Integration owns the production implementation. UI & Data only sends
// a snapshot and never creates or mutates Framework SaveData through this contract.
public interface ITradePrepareCommitSink
{
    // Stage must attach the snapshot to Framework-owned state so the existing
    // TradeStartService save includes it. Return false without partial mutation.
    bool TryStage(TradePrepareCommitData commitData);

    // Restore the Framework-owned state if departure fails after staging.
    void Rollback(string tradeId);
}

// Read access is separated from staging so settlement can consume Framework-owned
// persisted data without depending on a concrete storage implementation.
public interface ITradePrepareCommitSource
{
    bool TryGet(string tradeId, out TradePrepareCommitData commitData);
}

// Completing a settlement returns the committed snapshot and removes it atomically
// so the same trade cannot be claimed twice.
public interface ITradePrepareCommitCompletion
{
    bool TryComplete(string tradeId, out TradePrepareCommitData commitData);
}
