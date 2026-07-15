using System;
using System.Collections.Generic;

// Temporary storage for the hand-off between departure and settlement.
// Replace this implementation with a Framework-owned sink without changing
// TradePrepareStartAdapter or the UI binding.
public sealed class InMemoryTradePrepareCommitSink :
    ITradePrepareCommitSink,
    ITradePrepareCommitSource,
    ITradePrepareCommitCompletion
{
    private readonly Dictionary<string, TradePrepareCommitData> stagedByTradeId =
        new Dictionary<string, TradePrepareCommitData>(StringComparer.Ordinal);

    public int Count => stagedByTradeId.Count;

    public bool TryStage(TradePrepareCommitData commitData)
    {
        if (commitData == null || string.IsNullOrWhiteSpace(commitData.tradeId))
        {
            return false;
        }

        string tradeId = commitData.tradeId.Trim();
        if (stagedByTradeId.ContainsKey(tradeId))
        {
            return false;
        }

        TradePrepareCommitData snapshot = commitData.CreateSnapshot();
        snapshot.tradeId = tradeId;
        stagedByTradeId.Add(tradeId, snapshot);
        return true;
    }

    public void Rollback(string tradeId)
    {
        if (!string.IsNullOrWhiteSpace(tradeId))
        {
            stagedByTradeId.Remove(tradeId.Trim());
        }
    }

    public bool TryGet(string tradeId, out TradePrepareCommitData commitData)
    {
        commitData = null;
        if (string.IsNullOrWhiteSpace(tradeId)
            || !stagedByTradeId.TryGetValue(tradeId.Trim(), out TradePrepareCommitData stored))
        {
            return false;
        }

        commitData = stored.CreateSnapshot();
        return true;
    }

    public bool TryComplete(string tradeId, out TradePrepareCommitData commitData)
    {
        if (!TryGet(tradeId, out commitData))
        {
            return false;
        }

        stagedByTradeId.Remove(tradeId.Trim());
        return true;
    }

    public void Clear()
    {
        stagedByTradeId.Clear();
    }
}
