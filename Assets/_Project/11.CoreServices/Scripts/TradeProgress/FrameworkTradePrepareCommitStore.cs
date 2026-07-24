using System;
using System.Collections.Generic;
using System.Linq;

namespace ND.Framework
{
    public sealed class FrameworkTradePrepareCommitStore :
        global::ITradePrepareCommitSink,
        global::ITradePrepareCommitSource,
        global::ITradePrepareCommitCompletion
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
            if (snapshot == null || saveData == null || string.IsNullOrWhiteSpace(snapshot.tradeId))
                return false;

            Normalize(saveData);
            TradePreparationCommitSaveData saved = saveData.tradePreparationCommit;
            if (saved.hasCommit && !string.Equals(saved.tradeId, snapshot.tradeId, StringComparison.Ordinal))
                return false;

            CopyToSave(snapshot, saved);
            FrameworkLog.Info($"Trade preparation commit staged. TradeId: {saved.tradeId}, Items: {saved.purchasedItems.Count}");
            return true;
        }

        public void Rollback(string tradeId)
        {
            SaveData saveData = getCurrentSaveData?.Invoke();
            if (saveData?.tradePreparationCommit == null || string.IsNullOrWhiteSpace(tradeId))
                return;

            if (saveData.tradePreparationCommit.hasCommit &&
                string.Equals(saveData.tradePreparationCommit.tradeId, tradeId.Trim(), StringComparison.Ordinal))
            {
                Clear(saveData);
                FrameworkLog.Info($"Trade preparation commit rolled back. TradeId: {tradeId.Trim()}");
            }
        }

        public bool TryGet(string tradeId, out global::TradePrepareCommitData commitData)
        {
            commitData = null;
            TradePreparationCommitSaveData saved = getCurrentSaveData?.Invoke()?.tradePreparationCommit;
            if (saved == null || !saved.hasCommit || string.IsNullOrWhiteSpace(tradeId) ||
                !string.Equals(saved.tradeId, tradeId.Trim(), StringComparison.Ordinal))
                return false;

            commitData = CopyToRuntime(saved);
            return true;
        }

        public bool TryComplete(string tradeId, out global::TradePrepareCommitData commitData)
        {
            if (!TryGet(tradeId, out commitData))
                return false;

            Clear(getCurrentSaveData?.Invoke());
            FrameworkLog.Info($"Trade preparation commit completed. TradeId: {tradeId.Trim()}");
            return true;
        }

        public static void Normalize(SaveData saveData)
        {
            if (saveData == null)
                return;

            saveData.tradePreparationCommit ??= new TradePreparationCommitSaveData();
            TradePreparationCommitSaveData saved = saveData.tradePreparationCommit;
            saved.tradeId ??= string.Empty;
            saved.currentTownId ??= string.Empty;
            saved.destinationTownId ??= string.Empty;
            saved.routeId ??= string.Empty;
            saved.wagonId ??= string.Empty;
            saved.animals ??= new List<TradePreparationAnimalSaveData>();
            saved.purchasedItems ??= new List<TradePreparationItemSaveData>();
            saved.mercenaryIds ??= new List<string>();
        }

        public static void Clear(SaveData saveData)
        {
            if (saveData != null)
                saveData.tradePreparationCommit = new TradePreparationCommitSaveData();
        }

        private static void CopyToSave(global::TradePrepareCommitData source, TradePreparationCommitSaveData destination)
        {
            destination.hasCommit = true;
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
