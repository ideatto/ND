using System;
using System.Collections.Generic;
using System.Linq;

namespace ND.Framework
{
    public sealed class FrameworkTradePrepareCommitStore :
        global::ITradePrepareCommitSink,
        global::ITradePrepareCommitSource,
        global::ITradePrepareCommitCompletion,
        global::IExactTradePrepareCommitStore
    {
        private readonly Func<SaveData> getCurrentSaveData;

        public FrameworkTradePrepareCommitStore(Func<SaveData> getCurrentSaveData)
        {
            this.getCurrentSaveData = getCurrentSaveData;
        }

        public bool TryStage(global::TradePrepareCommitData commitData)
        {
            global::TradePrepareCommitData snapshot = commitData?.CreateSnapshot();
            SaveData saveData = getCurrentSaveData?.Invoke();
            if (snapshot == null || saveData == null
                || string.IsNullOrWhiteSpace(snapshot.caravanId)
                || string.IsNullOrWhiteSpace(snapshot.tradeId))
            {
                FrameworkLog.Warning(
                    $"Trade preparation commit stage rejected. CaravanId: {snapshot?.caravanId}, TradeId: {snapshot?.tradeId}");
                return false;
            }

            string caravanId = snapshot.caravanId.Trim();
            string tradeId = snapshot.tradeId.Trim();
            Normalize(saveData);

            TradePreparationCommitSaveData exact = FindExact(saveData, caravanId, tradeId);
            if (exact != null)
            {
                FrameworkLog.Info(
                    $"Trade preparation commit stage was idempotent. CaravanId: {caravanId}, TradeId: {tradeId}");
                return true;
            }

            TradePreparationCommitSaveData tradeOwner = saveData.tradePreparationCommits.FirstOrDefault(
                saved => IsActive(saved)
                    && string.Equals(saved.tradeId, tradeId, StringComparison.Ordinal));
            if (tradeOwner != null)
            {
                FrameworkLog.Warning(
                    $"Trade preparation commit stage rejected because the trade belongs to another Caravan. "
                    + $"CaravanId: {caravanId}, TradeId: {tradeId}, StoredCaravanId: {tradeOwner.caravanId}");
                return false;
            }

            if (saveData.tradePreparationCommits.Any(
                saved => IsActive(saved)
                    && string.Equals(saved.caravanId, caravanId, StringComparison.Ordinal)))
            {
                FrameworkLog.Warning(
                    $"Trade preparation commit stage rejected because the Caravan already owns another active trade. "
                    + $"CaravanId: {caravanId}, TradeId: {tradeId}");
                return false;
            }

            var savedCommit = new TradePreparationCommitSaveData();
            CopyToSave(snapshot, savedCommit);
            saveData.tradePreparationCommits.Add(savedCommit);
            FrameworkLog.Info(
                $"Trade preparation commit staged. CaravanId: {caravanId}, TradeId: {tradeId}, "
                + $"Items: {savedCommit.purchasedItems.Count}");
            return true;
        }

        [Obsolete("Use Rollback(caravanId, tradeId).")]
        public void Rollback(string tradeId)
        {
            if (!TryResolveUniqueTradeOwner(tradeId, out string caravanId))
            {
                FrameworkLog.Warning(
                    $"Legacy trade preparation rollback rejected because exact ownership could not be resolved. TradeId: {tradeId}");
                return;
            }

            Rollback(caravanId, tradeId);
        }

        public void Rollback(string caravanId, string tradeId)
        {
            if (TryRemove(caravanId, tradeId))
            {
                FrameworkLog.Info(
                    $"Trade preparation commit rolled back. CaravanId: {caravanId.Trim()}, TradeId: {tradeId.Trim()}");
            }
        }

        [Obsolete("Use TryGet(caravanId, tradeId, out commitData).")]
        public bool TryGet(string tradeId, out global::TradePrepareCommitData commitData)
        {
            commitData = null;
            return TryResolveUniqueTradeOwner(tradeId, out string caravanId)
                && TryGet(caravanId, tradeId, out commitData);
        }

        public bool TryGet(
            string caravanId,
            string tradeId,
            out global::TradePrepareCommitData commitData)
        {
            commitData = null;
            if (!TryNormalizeIdentity(caravanId, tradeId, out caravanId, out tradeId))
                return false;

            SaveData saveData = getCurrentSaveData?.Invoke();
            if (saveData == null)
                return false;
            Normalize(saveData);
            TradePreparationCommitSaveData saved = FindExact(saveData, caravanId, tradeId);
            if (saved == null)
            {
                FrameworkLog.Warning(
                    $"Trade preparation commit lookup failed. CaravanId: {caravanId}, TradeId: {tradeId}");
                return false;
            }

            commitData = CopyToRuntime(saved);
            return true;
        }

        [Obsolete("Use TryComplete(caravanId, tradeId, out commitData).")]
        public bool TryComplete(string tradeId, out global::TradePrepareCommitData commitData)
        {
            commitData = null;
            return TryResolveUniqueTradeOwner(tradeId, out string caravanId)
                && TryComplete(caravanId, tradeId, out commitData);
        }

        public bool TryComplete(
            string caravanId,
            string tradeId,
            out global::TradePrepareCommitData commitData)
        {
            if (!TryGet(caravanId, tradeId, out commitData))
                return false;

            if (!TryRemove(caravanId, tradeId))
            {
                commitData = null;
                return false;
            }

            FrameworkLog.Info(
                $"Trade preparation commit completed. CaravanId: {caravanId.Trim()}, TradeId: {tradeId.Trim()}");
            return true;
        }

        public bool TryRemove(string caravanId, string tradeId)
        {
            if (!TryNormalizeIdentity(caravanId, tradeId, out caravanId, out tradeId))
                return false;

            SaveData saveData = getCurrentSaveData?.Invoke();
            if (saveData == null)
                return false;
            Normalize(saveData);
            TradePreparationCommitSaveData saved = FindExact(saveData, caravanId, tradeId);
            if (saved == null)
            {
                FrameworkLog.Warning(
                    $"Trade preparation commit removal failed. CaravanId: {caravanId}, TradeId: {tradeId}");
                return false;
            }

            saveData.tradePreparationCommits.Remove(saved);
            FrameworkLog.Info(
                $"Trade preparation commit removed. CaravanId: {caravanId}, TradeId: {tradeId}");
            return true;
        }

        public static bool Normalize(SaveData saveData)
        {
            if (saveData == null)
                return false;

            bool changed = false;
            if (saveData.tradePreparationCommits == null)
            {
                saveData.tradePreparationCommits = new List<TradePreparationCommitSaveData>();
                changed = true;
            }

            var exactKeys = new HashSet<string>(StringComparer.Ordinal);
            var caravanOwners = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < saveData.tradePreparationCommits.Count;)
            {
                TradePreparationCommitSaveData saved = saveData.tradePreparationCommits[index];
                if (saved == null || !IsActive(saved)
                    || string.IsNullOrWhiteSpace(saved.caravanId)
                    || string.IsNullOrWhiteSpace(saved.tradeId))
                {
                    saveData.tradePreparationCommits.RemoveAt(index);
                    changed = true;
                    continue;
                }

                changed |= NormalizeCommit(saved);
                string key = CreateKey(saved.caravanId, saved.tradeId);
                if (!exactKeys.Add(key))
                {
                    FrameworkLog.Warning(
                        $"Duplicate trade preparation commit was removed. CaravanId: {saved.caravanId}, TradeId: {saved.tradeId}");
                    saveData.tradePreparationCommits.RemoveAt(index);
                    changed = true;
                    continue;
                }

                if (!caravanOwners.Add(saved.caravanId))
                {
                    FrameworkLog.Error(
                        $"Conflicting active trade preparation commits were preserved for inspection. "
                        + $"CaravanId: {saved.caravanId}, TradeId: {saved.tradeId}");
                }

                index++;
            }

            saveData.tradePreparationCommit ??= new TradePreparationCommitSaveData();
            changed |= NormalizeCommit(saveData.tradePreparationCommit);
            TradePreparationCommitSaveData legacy = saveData.tradePreparationCommit;
            if (IsActive(legacy))
            {
                if (string.IsNullOrWhiteSpace(legacy.caravanId))
                {
                    legacy.caravanId = ResolveLegacyCaravanId(saveData, legacy.tradeId);
                    changed |= !string.IsNullOrWhiteSpace(legacy.caravanId);
                }

                if (!string.IsNullOrWhiteSpace(legacy.caravanId)
                    && !string.IsNullOrWhiteSpace(legacy.tradeId))
                {
                    string legacyKey = CreateKey(legacy.caravanId, legacy.tradeId);
                    if (!exactKeys.Contains(legacyKey))
                    {
                        var migrated = Clone(legacy);
                        saveData.tradePreparationCommits.Add(migrated);
                        exactKeys.Add(legacyKey);
                        changed = true;
                        FrameworkLog.Info(
                            $"Legacy trade preparation commit migrated. CaravanId: {migrated.caravanId}, "
                            + $"TradeId: {migrated.tradeId}");
                    }
                    else
                    {
                        TradePreparationCommitSaveData existing =
                            FindExact(saveData, legacy.caravanId, legacy.tradeId);
                        if (existing != null && !HasSamePayload(existing, legacy))
                        {
                            FrameworkLog.Warning(
                                $"Conflicting legacy trade preparation commit was ignored. "
                                + $"CaravanId: {legacy.caravanId}, TradeId: {legacy.tradeId}");
                        }
                    }

                    saveData.tradePreparationCommit = new TradePreparationCommitSaveData();
                    changed = true;
                }
                else
                {
                    FrameworkLog.Warning(
                        $"Legacy trade preparation commit could not be migrated because exact ownership is unavailable. "
                        + $"CaravanId: {legacy.caravanId}, TradeId: {legacy.tradeId}");
                }
            }

            return changed;
        }

        public static void Clear(SaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.tradePreparationCommits = new List<TradePreparationCommitSaveData>();
            saveData.tradePreparationCommit = new TradePreparationCommitSaveData();
        }

        public static bool HasActiveCommit(SaveData saveData, string caravanId, string tradeId = null)
        {
            if (saveData?.tradePreparationCommits == null || string.IsNullOrWhiteSpace(caravanId))
                return false;

            caravanId = caravanId.Trim();
            string normalizedTradeId = tradeId?.Trim();
            return saveData.tradePreparationCommits.Any(
                saved => IsActive(saved)
                    && string.Equals(saved.caravanId, caravanId, StringComparison.Ordinal)
                    && (string.IsNullOrWhiteSpace(normalizedTradeId)
                        || string.Equals(saved.tradeId, normalizedTradeId, StringComparison.Ordinal)));
        }

        private bool TryResolveUniqueTradeOwner(string tradeId, out string caravanId)
        {
            caravanId = string.Empty;
            if (string.IsNullOrWhiteSpace(tradeId))
                return false;

            tradeId = tradeId.Trim();
            SaveData saveData = getCurrentSaveData?.Invoke();
            if (saveData == null)
                return false;
            Normalize(saveData);
            TradePreparationCommitSaveData match = null;
            foreach (TradePreparationCommitSaveData candidate in saveData.tradePreparationCommits)
            {
                if (!IsActive(candidate)
                    || !string.Equals(candidate.tradeId, tradeId, StringComparison.Ordinal))
                    continue;

                if (match != null)
                    return false;
                match = candidate;
            }

            if (match == null)
                return false;

            caravanId = match.caravanId;
            return true;
        }

        private static TradePreparationCommitSaveData FindExact(
            SaveData saveData,
            string caravanId,
            string tradeId)
        {
            if (saveData?.tradePreparationCommits == null)
                return null;

            return saveData.tradePreparationCommits.FirstOrDefault(
                saved => IsActive(saved)
                    && string.Equals(saved.caravanId, caravanId, StringComparison.Ordinal)
                    && string.Equals(saved.tradeId, tradeId, StringComparison.Ordinal));
        }

        private static bool TryNormalizeIdentity(
            string caravanId,
            string tradeId,
            out string normalizedCaravanId,
            out string normalizedTradeId)
        {
            normalizedCaravanId = caravanId?.Trim() ?? string.Empty;
            normalizedTradeId = tradeId?.Trim() ?? string.Empty;
            return normalizedCaravanId.Length > 0 && normalizedTradeId.Length > 0;
        }

        private static string ResolveLegacyCaravanId(SaveData saveData, string tradeId)
        {
            if (saveData?.tradeProgressEntries == null || string.IsNullOrWhiteSpace(tradeId))
                return string.Empty;

            string owner = string.Empty;
            foreach (TradeProgressSaveData progress in saveData.tradeProgressEntries)
            {
                if (progress == null
                    || !string.Equals(progress.activeTradeId, tradeId.Trim(), StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(progress.caravanId))
                    continue;

                if (owner.Length > 0 && !string.Equals(owner, progress.caravanId, StringComparison.Ordinal))
                    return string.Empty;
                owner = progress.caravanId.Trim();
            }

            return owner;
        }

        private static bool NormalizeCommit(TradePreparationCommitSaveData saved)
        {
            if (saved == null)
                return false;

            bool changed = false;
            changed |= NormalizeString(ref saved.caravanId);
            changed |= NormalizeString(ref saved.tradeId);
            changed |= NormalizeString(ref saved.currentTownId);
            changed |= NormalizeString(ref saved.destinationTownId);
            changed |= NormalizeString(ref saved.routeId);
            changed |= NormalizeString(ref saved.wagonId);
            if (saved.animals == null)
            {
                saved.animals = new List<TradePreparationAnimalSaveData>();
                changed = true;
            }
            if (saved.purchasedItems == null)
            {
                saved.purchasedItems = new List<TradePreparationItemSaveData>();
                changed = true;
            }
            if (saved.mercenaryIds == null)
            {
                saved.mercenaryIds = new List<string>();
                changed = true;
            }
            return changed;
        }

        private static bool NormalizeString(ref string value)
        {
            string normalized = value?.Trim() ?? string.Empty;
            if (string.Equals(value, normalized, StringComparison.Ordinal))
                return false;
            value = normalized;
            return true;
        }

        private static bool IsActive(TradePreparationCommitSaveData saved)
        {
            return saved != null && saved.hasCommit;
        }

        private static string CreateKey(string caravanId, string tradeId)
        {
            return caravanId + "\n" + tradeId;
        }

        private static TradePreparationCommitSaveData Clone(TradePreparationCommitSaveData source)
        {
            var clone = new TradePreparationCommitSaveData();
            CopyToSave(CopyToRuntime(source), clone);
            return clone;
        }

        private static bool HasSamePayload(
            TradePreparationCommitSaveData left,
            TradePreparationCommitSaveData right)
        {
            if (left == null || right == null)
                return left == right;

            return string.Equals(left.currentTownId, right.currentTownId, StringComparison.Ordinal)
                && string.Equals(left.destinationTownId, right.destinationTownId, StringComparison.Ordinal)
                && string.Equals(left.routeId, right.routeId, StringComparison.Ordinal)
                && string.Equals(left.wagonId, right.wagonId, StringComparison.Ordinal)
                && left.purchaseCost == right.purchaseCost
                && left.foodCost == right.foodCost
                && left.mercenaryCost == right.mercenaryCost
                && left.estimatedSellRevenue == right.estimatedSellRevenue;
        }

        private static void CopyToSave(global::TradePrepareCommitData source, TradePreparationCommitSaveData destination)
        {
            destination.hasCommit = true;
            destination.caravanId = source.caravanId.Trim();
            destination.tradeId = source.tradeId.Trim();
            destination.currentTownId = source.currentTownId ?? string.Empty;
            destination.destinationTownId = source.selectedDestinationTownId ?? string.Empty;
            destination.routeId = source.routeId ?? string.Empty;
            destination.wagonId = source.selectedWagonId ?? string.Empty;
            destination.purchaseCost = Math.Max(0L, source.purchaseCost);
            destination.foodCost = Math.Max(0L, source.foodCost);
            destination.mercenaryCost = Math.Max(0L, source.mercenaryCost);
            destination.estimatedSellRevenue = Math.Max(0L, source.estimatedSellRevenue);
            destination.animals = new List<TradePreparationAnimalSaveData>();
            destination.purchasedItems = new List<TradePreparationItemSaveData>();
            destination.mercenaryIds = source.selectedMercenaryIds != null
                ? new List<string>(source.selectedMercenaryIds)
                : new List<string>();

            if (source.selectedAnimals != null)
            {
                foreach (global::DraftAnimalSelectionData animal in source.selectedAnimals)
                {
                    if (animal == null) continue;
                    destination.animals.Add(new TradePreparationAnimalSaveData
                    {
                        animalId = animal.draftAnimalId ?? string.Empty,
                        quantity = Math.Max(0, animal.quantity)
                    });
                }
            }

            if (source.purchasedItems != null)
            {
                foreach (global::TradeItemBundle item in source.purchasedItems)
                {
                    if (item == null) continue;
                    destination.purchasedItems.Add(new TradePreparationItemSaveData
                    {
                        itemId = item.itemId ?? string.Empty,
                        quantity = Math.Max(0, item.quantity),
                        purchaseUnitPrice = Math.Max(0L, item.purchaseUnitPrice),
                        sellUnitPrice = Math.Max(0L, item.sellUnitPrice)
                    });
                }
            }
        }

        private static global::TradePrepareCommitData CopyToRuntime(TradePreparationCommitSaveData source)
        {
            var animals = new global::DraftAnimalSelectionData[source.animals?.Count ?? 0];
            for (int index = 0; index < animals.Length; index++)
            {
                TradePreparationAnimalSaveData animal = source.animals[index];
                animals[index] = animal == null ? null : new global::DraftAnimalSelectionData
                {
                    draftAnimalId = animal.animalId ?? string.Empty,
                    quantity = Math.Max(0, animal.quantity)
                };
            }

            var items = new global::TradeItemBundle[source.purchasedItems?.Count ?? 0];
            for (int index = 0; index < items.Length; index++)
            {
                TradePreparationItemSaveData item = source.purchasedItems[index];
                items[index] = item == null ? null : new global::TradeItemBundle
                {
                    itemId = item.itemId ?? string.Empty,
                    quantity = Math.Max(0, item.quantity),
                    purchaseUnitPrice = Math.Max(0L, item.purchaseUnitPrice),
                    sellUnitPrice = Math.Max(0L, item.sellUnitPrice)
                };
            }

            return new global::TradePrepareCommitData
            {
                caravanId = source.caravanId ?? string.Empty,
                tradeId = source.tradeId ?? string.Empty,
                currentTownId = source.currentTownId ?? string.Empty,
                selectedDestinationTownId = source.destinationTownId ?? string.Empty,
                routeId = source.routeId ?? string.Empty,
                selectedWagonId = source.wagonId ?? string.Empty,
                selectedAnimals = animals,
                purchaseCost = Math.Max(0L, source.purchaseCost),
                foodCost = Math.Max(0L, source.foodCost),
                mercenaryCost = Math.Max(0L, source.mercenaryCost),
                estimatedSellRevenue = Math.Max(0L, source.estimatedSellRevenue),
                purchasedItems = items,
                selectedMercenaryIds = source.mercenaryIds?.ToArray() ?? Array.Empty<string>()
            };
        }
    }
}
