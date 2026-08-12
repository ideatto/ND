using System;
using System.Collections.Generic;

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

// Runtime-only purchase receipt for one TradePrepare session. Cargo remains owned by
// Framework SaveData; this store keeps only purchases paid during the current session.
public sealed class TradePreparePurchaseDeltaStore
{
    private sealed class PurchaseDelta
    {
        public long purchaseCost;
        public readonly List<TradeItemBundle> purchasedItems = new List<TradeItemBundle>();
    }

    private readonly Dictionary<string, PurchaseDelta> deltasByCaravanId =
        new Dictionary<string, PurchaseDelta>(StringComparer.Ordinal);

    public void RecordPurchase(string caravanId, long purchaseCost, IEnumerable<TradeItemBundle> purchasedItems)
    {
        string key = NormalizeId(caravanId);
        if (string.IsNullOrEmpty(key) || purchaseCost < 0L)
            return;

        if (!deltasByCaravanId.TryGetValue(key, out PurchaseDelta delta))
        {
            delta = new PurchaseDelta();
            deltasByCaravanId.Add(key, delta);
        }

        delta.purchaseCost = AddClamped(delta.purchaseCost, purchaseCost);
        if (purchasedItems == null)
            return;

        foreach (TradeItemBundle item in purchasedItems)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.itemId) || item.quantity <= 0)
                continue;

            string itemId = item.itemId.Trim();
            long unitPrice = NormalizeMoney(item.purchaseUnitPrice);
            TradeItemBundle existing = delta.purchasedItems.Find(candidate =>
                candidate != null
                && string.Equals(candidate.itemId, itemId, StringComparison.Ordinal)
                && candidate.purchaseUnitPrice == unitPrice);
            if (existing != null)
            {
                existing.quantity = existing.quantity > int.MaxValue - item.quantity
                    ? int.MaxValue
                    : existing.quantity + item.quantity;
                continue;
            }

            delta.purchasedItems.Add(new TradeItemBundle
            {
                itemId = itemId,
                quantity = item.quantity,
                purchaseUnitPrice = unitPrice,
                sellUnitPrice = NormalizeMoney(item.sellUnitPrice)
            });
        }
    }

    public bool TryGet(string caravanId, out long purchaseCost, out TradeItemBundle[] purchasedItems)
    {
        purchaseCost = 0L;
        purchasedItems = new TradeItemBundle[0];
        string key = NormalizeId(caravanId);
        if (string.IsNullOrEmpty(key) || !deltasByCaravanId.TryGetValue(key, out PurchaseDelta delta))
            return false;

        purchaseCost = delta.purchaseCost;
        purchasedItems = ClonePurchasedItems(delta.purchasedItems);
        return true;
    }

    public void Clear(string caravanId)
    {
        string key = NormalizeId(caravanId);
        if (!string.IsNullOrEmpty(key))
            deltasByCaravanId.Remove(key);
    }

    public void ClearAll()
    {
        deltasByCaravanId.Clear();
    }

    private static TradeItemBundle[] ClonePurchasedItems(IReadOnlyList<TradeItemBundle> source)
    {
        if (source == null || source.Count == 0)
            return new TradeItemBundle[0];

        var result = new TradeItemBundle[source.Count];
        for (int index = 0; index < source.Count; index++)
        {
            TradeItemBundle item = source[index];
            result[index] = item == null ? null : new TradeItemBundle
            {
                itemId = item.itemId ?? string.Empty,
                quantity = Math.Max(0, item.quantity),
                purchaseUnitPrice = NormalizeMoney(item.purchaseUnitPrice),
                sellUnitPrice = NormalizeMoney(item.sellUnitPrice)
            };
        }

        return result;
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static long NormalizeMoney(long value)
    {
        return value > 0L ? value : 0L;
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

// Framework uses the exact Caravan and trade identity for durable multi-Caravan
// lifecycle operations. The legacy trade-only contracts remain for temporary callers.
public interface IExactTradePrepareCommitStore
{
    void Rollback(string caravanId, string tradeId);
    bool TryGet(string caravanId, string tradeId, out TradePrepareCommitData commitData);
    bool TryComplete(string caravanId, string tradeId, out TradePrepareCommitData commitData);
    bool TryRemove(string caravanId, string tradeId);
}
