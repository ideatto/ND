using System;
using System.Collections.Generic;

namespace ND.Framework
{
    public sealed class CaravanMarketCatalogItem
    {
        public SharedTradeItemDefinition Definition { get; }
        public int Stock { get; }
        public long BuyUnitPrice { get; }

        internal CaravanMarketCatalogItem(
            SharedTradeItemDefinition definition,
            int stock,
            long buyUnitPrice)
        {
            Definition = definition;
            Stock = Math.Max(0, stock);
            BuyUnitPrice = Math.Max(0L, buyUnitPrice);
        }
    }

    public sealed class CaravanMarketCatalogSnapshot
    {
        private readonly Dictionary<string, CaravanMarketCatalogItem> itemsById;

        public string CaravanId { get; }
        public string TownId { get; }
        public string MarketId { get; }
        public IReadOnlyList<CaravanMarketCatalogItem> Items { get; }

        internal CaravanMarketCatalogSnapshot(
            string caravanId,
            string townId,
            string marketId,
            IReadOnlyList<CaravanMarketCatalogItem> items)
        {
            CaravanId = caravanId;
            TownId = townId;
            MarketId = marketId;
            Items = items;
            itemsById = new Dictionary<string, CaravanMarketCatalogItem>(StringComparer.Ordinal);
            for (int index = 0; index < items.Count; index++)
            {
                CaravanMarketCatalogItem item = items[index];
                if (item?.Definition != null && !string.IsNullOrEmpty(item.Definition.Id))
                    itemsById[item.Definition.Id] = item;
            }
        }

        public bool TryGetItem(string itemId, out CaravanMarketCatalogItem item)
        {
            return itemsById.TryGetValue(NormalizeId(itemId), out item);
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }

    /// <summary>
    /// Resolves a Caravan's authoritative town market through SharedGameData and overlays the
    /// current SaveData market inventory. Player.currentTownId is intentionally not consulted.
    /// </summary>
    public sealed class CaravanMarketCatalogService
    {
        public bool TryResolve(
            SaveData saveData,
            ISharedGameDataProvider sharedGameData,
            string caravanId,
            out CaravanMarketCatalogSnapshot snapshot)
        {
            snapshot = null;
            string normalizedCaravanId = NormalizeId(caravanId);
            if (saveData == null
                || sharedGameData == null
                || !sharedGameData.IsLoaded
                || !SaveDataLookup.TryGetCaravan(saveData, normalizedCaravanId, out CaravanSaveData caravan)
                || string.IsNullOrEmpty(NormalizeId(caravan.currentTownId))
                || !sharedGameData.TryGetTown(caravan.currentTownId, out SharedTownDefinition town)
                || town == null
                || string.IsNullOrEmpty(NormalizeId(town.MarketId))
                || !sharedGameData.TryGetMarket(town.MarketId, out SharedMarketDefinition market)
                || market == null)
            {
                return false;
            }

            MarketInventorySaveData inventory = FindInventory(saveData, market.Id);
            var itemIds = new List<string>();
            AddUniqueIds(itemIds, market.TradeItemIds);
            AddUnlockedSpecialtyIds(
                itemIds,
                market.LocalSpecialtyItemIds,
                saveData.world?.unlockedTownSpecialties,
                caravan.currentTownId);

            var items = new List<CaravanMarketCatalogItem>(itemIds.Count);
            for (int index = 0; index < itemIds.Count; index++)
            {
                string itemId = itemIds[index];
                if (!sharedGameData.TryGetTradeItem(itemId, out SharedTradeItemDefinition definition)
                    || definition == null)
                {
                    continue;
                }

                MarketStockSaveData stock = FindStock(inventory, itemId);
                items.Add(new CaravanMarketCatalogItem(
                    definition,
                    stock != null ? stock.quantity : 0,
                    stock != null ? stock.unitPrice : definition.BaseBuyPrice));
            }

            snapshot = new CaravanMarketCatalogSnapshot(
                normalizedCaravanId,
                caravan.currentTownId,
                market.Id,
                items.ToArray());
            return true;
        }

        private static MarketInventorySaveData FindInventory(SaveData saveData, string marketId)
        {
            if (saveData?.world?.marketInventories == null)
                return null;

            for (int index = 0; index < saveData.world.marketInventories.Count; index++)
            {
                MarketInventorySaveData candidate = saveData.world.marketInventories[index];
                if (candidate != null
                    && string.Equals(candidate.marketId, marketId, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static MarketStockSaveData FindStock(MarketInventorySaveData inventory, string itemId)
        {
            if (inventory?.stocks == null)
                return null;

            for (int index = 0; index < inventory.stocks.Count; index++)
            {
                MarketStockSaveData candidate = inventory.stocks[index];
                if (candidate != null
                    && string.Equals(candidate.itemId, itemId, StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static void AddUniqueIds(List<string> destination, IReadOnlyList<string> source)
        {
            if (source == null)
                return;

            for (int index = 0; index < source.Count; index++)
            {
                string itemId = NormalizeId(source[index]);
                if (!string.IsNullOrEmpty(itemId) && !destination.Contains(itemId))
                    destination.Add(itemId);
            }
        }

        private static void AddUnlockedSpecialtyIds(
            List<string> destination,
            IReadOnlyList<string> specialtyItemIds,
            IReadOnlyList<TownSpecialtyUnlockSaveData> unlocks,
            string townId)
        {
            if (specialtyItemIds == null || unlocks == null)
                return;

            for (int index = 0; index < specialtyItemIds.Count; index++)
            {
                string itemId = NormalizeId(specialtyItemIds[index]);
                if (string.IsNullOrEmpty(itemId) || destination.Contains(itemId))
                    continue;

                for (int unlockIndex = 0; unlockIndex < unlocks.Count; unlockIndex++)
                {
                    TownSpecialtyUnlockSaveData unlock = unlocks[unlockIndex];
                    if (unlock != null
                        && string.Equals(unlock.townId, townId, StringComparison.Ordinal)
                        && string.Equals(unlock.itemId, itemId, StringComparison.Ordinal))
                    {
                        destination.Add(itemId);
                        break;
                    }
                }
            }
        }

        private static string NormalizeId(string value) => value?.Trim() ?? string.Empty;
    }
}
