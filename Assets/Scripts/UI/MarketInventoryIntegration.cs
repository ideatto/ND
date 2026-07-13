#if ND_MARKET_SAVE_SCHEMA_VNEXT
using System;
using System.Collections.Generic;
using System.Linq;
using ND.Economy;
using ND.Framework;

namespace ND.Framework.CargoLoading
{
    public sealed class MarketStockView
    {
        public TradeItemData Item;
        public int Quantity;
        public long UnitPrice;
    }

    public sealed class CargoPurchaseRequest
    {
        public TradeItemData Item;
        public int Quantity;
        public long UnitPrice;
    }

    public sealed class CargoIntegrationResult
    {
        public bool Success;
        public string ErrorCode = string.Empty;
        public long TradingCurrencyAfter;

        public static CargoIntegrationResult Ok(long tradingCurrency)
        {
            return new CargoIntegrationResult
            {
                Success = true,
                TradingCurrencyAfter = Math.Max(0L, tradingCurrency)
            };
        }

        public static CargoIntegrationResult Fail(string errorCode, long tradingCurrency)
        {
            return new CargoIntegrationResult
            {
                Success = false,
                ErrorCode = errorCode ?? string.Empty,
                TradingCurrencyAfter = Math.Max(0L, tradingCurrency)
            };
        }
    }

    /// <summary>
    /// Temporary vertical-slice implementation for the proposed market-save schema.
    /// The complete file is excluded unless ND_MARKET_SAVE_SCHEMA_VNEXT is defined.
    /// </summary>
    public sealed class MarketInventorySession
    {
        public const string ErrorInvalidFramework = "INVALID_FRAMEWORK";
        public const string ErrorInvalidCatalog = "INVALID_CATALOG";
        public const string ErrorInvalidCargo = "INVALID_CARGO";
        public const string ErrorInsufficientStock = "INSUFFICIENT_STOCK";
        public const string ErrorCurrency = "CURRENCY_FAILURE";

        private readonly SaveData saveData;
        private readonly ISaveService saveService;
        private readonly IGameTimeProvider timeProvider;
        private readonly Dictionary<string, TradeItemData> catalogById;
        private readonly int slotCount;
        private readonly int maximumGeneratedStock;
        private readonly long refreshIntervalTicks;
        private readonly int worldSeed;

        private MarketInventorySaveData inventory;

        private MarketInventorySession(
            SaveData saveData,
            ISaveService saveService,
            IGameTimeProvider timeProvider,
            string marketId,
            IEnumerable<TradeItemData> catalog,
            int slotCount,
            int maximumGeneratedStock,
            double refreshIntervalSeconds,
            int worldSeed)
        {
            this.saveData = saveData;
            this.saveService = saveService;
            this.timeProvider = timeProvider;
            MarketId = string.IsNullOrWhiteSpace(marketId) ? "default-market" : marketId;
            this.slotCount = Math.Max(1, slotCount);
            this.maximumGeneratedStock = Math.Max(1, maximumGeneratedStock);
            refreshIntervalTicks = TimeSpan.FromSeconds(Math.Max(1d, refreshIntervalSeconds)).Ticks;
            this.worldSeed = worldSeed;
            catalogById = BuildCatalog(catalog);
        }

        public string MarketId { get; }

        public bool IsCommitted =>
            saveData.world.marketPurchasePreparation != null
            && saveData.world.marketPurchasePreparation.isCommitted
            && saveData.world.marketPurchasePreparation.marketId == MarketId;

        public long TradingCurrency =>
            saveData.player != null ? Math.Max(0L, saveData.player.tradingCurrency) : 0L;

        public IReadOnlyList<MarketStockView> Stocks
        {
            get
            {
                var result = new List<MarketStockView>();
                if (inventory == null || inventory.stocks == null)
                {
                    return result;
                }

                foreach (MarketStockSaveData stock in inventory.stocks)
                {
                    if (stock == null || !catalogById.TryGetValue(stock.itemId ?? string.Empty, out TradeItemData item))
                    {
                        continue;
                    }

                    result.Add(new MarketStockView
                    {
                        Item = item,
                        Quantity = Math.Max(0, stock.quantity),
                        UnitPrice = Math.Max(0L, stock.unitPrice)
                    });
                }

                return result;
            }
        }

        public static bool TryOpen(
            SaveData saveData,
            ISaveService saveService,
            IGameTimeProvider timeProvider,
            string marketId,
            IEnumerable<TradeItemData> catalog,
            int slotCount,
            int maximumGeneratedStock,
            double refreshIntervalSeconds,
            int worldSeed,
            out MarketInventorySession session,
            out string error)
        {
            session = null;
            error = string.Empty;

            if (saveData == null || saveService == null || timeProvider == null)
            {
                error = ErrorInvalidFramework;
                return false;
            }

            var created = new MarketInventorySession(
                saveData,
                saveService,
                timeProvider,
                marketId,
                catalog,
                slotCount,
                maximumGeneratedStock,
                refreshIntervalSeconds,
                worldSeed);

            if (created.catalogById.Count == 0)
            {
                error = ErrorInvalidCatalog;
                return false;
            }

            created.EnsureSaveContainers();
            created.ResolveOrRefreshInventory();
            session = created;
            return true;
        }

        public CargoIntegrationResult PersistDraft(IReadOnlyList<CargoPurchaseRequest> requests)
        {
            if (!TryValidateRequests(requests, false, out long totalCost, out string error))
            {
                return CargoIntegrationResult.Fail(error, TradingCurrency);
            }

            WriteCargo(requests);
            MarketPurchasePreparationSaveData preparation = saveData.world.marketPurchasePreparation;
            preparation.marketId = MarketId;
            preparation.isCommitted = false;
            preparation.totalCost = totalCost;
            preparation.cargoHash = ComputeCargoHash(requests);
            saveService.Save(saveData);
            return CargoIntegrationResult.Ok(TradingCurrency);
        }

        public CargoIntegrationResult Commit(IReadOnlyList<CargoPurchaseRequest> requests)
        {
            if (!TryValidateRequests(requests, true, out long totalCost, out string error))
            {
                return CargoIntegrationResult.Fail(error, TradingCurrency);
            }

            int cargoHash = ComputeCargoHash(requests);
            MarketPurchasePreparationSaveData preparation = saveData.world.marketPurchasePreparation;
            if (preparation.isCommitted
                && preparation.marketId == MarketId
                && preparation.cargoHash == cargoHash)
            {
                return CargoIntegrationResult.Ok(TradingCurrency);
            }

            var currency = new CurrencyState
            {
                TradeMoney = TradingCurrency,
                DevelopmentCurrency = Math.Max(0L, saveData.player.developmentCurrency)
            };

            CurrencyApplyResult currencyResult = CurrencyWallet.ApplyTradePurchase(currency, totalCost);
            if (currencyResult == null || !currencyResult.Success || currencyResult.After == null)
            {
                return CargoIntegrationResult.Fail(
                    currencyResult != null ? currencyResult.ErrorCode : ErrorCurrency,
                    TradingCurrency);
            }

            foreach (CargoPurchaseRequest request in requests)
            {
                MarketStockSaveData stock = FindStock(request.Item.ItemId);
                stock.quantity -= request.Quantity;
            }

            saveData.player.tradingCurrency = currencyResult.After.TradeMoney;
            saveData.player.developmentCurrency = currencyResult.After.DevelopmentCurrency;
            WriteCargo(requests);
            preparation.marketId = MarketId;
            preparation.isCommitted = true;
            preparation.totalCost = totalCost;
            preparation.cargoHash = cargoHash;
            saveService.Save(saveData);
            return CargoIntegrationResult.Ok(saveData.player.tradingCurrency);
        }

        public CargoIntegrationResult ReopenCommittedAsDraft(IReadOnlyList<CargoPurchaseRequest> requests)
        {
            MarketPurchasePreparationSaveData preparation = saveData.world.marketPurchasePreparation;
            if (!preparation.isCommitted || preparation.marketId != MarketId)
            {
                return PersistDraft(requests);
            }

            var currency = new CurrencyState
            {
                TradeMoney = TradingCurrency,
                DevelopmentCurrency = Math.Max(0L, saveData.player.developmentCurrency)
            };

            CurrencyApplyResult refundResult = CurrencyWallet.ApplyTradeRefund(currency, preparation.totalCost);
            if (refundResult == null || !refundResult.Success || refundResult.After == null)
            {
                return CargoIntegrationResult.Fail(
                    refundResult != null ? refundResult.ErrorCode : ErrorCurrency,
                    TradingCurrency);
            }

            RestoreStocks(requests);
            saveData.player.tradingCurrency = refundResult.After.TradeMoney;
            saveData.player.developmentCurrency = refundResult.After.DevelopmentCurrency;
            preparation.isCommitted = false;
            preparation.totalCost = CalculateTotalCost(requests);
            preparation.cargoHash = ComputeCargoHash(requests);
            WriteCargo(requests);
            saveService.Save(saveData);
            return CargoIntegrationResult.Ok(saveData.player.tradingCurrency);
        }

        public CargoIntegrationResult CancelPreparation(IReadOnlyList<CargoPurchaseRequest> requests)
        {
            MarketPurchasePreparationSaveData preparation = saveData.world.marketPurchasePreparation;
            if (preparation.isCommitted && preparation.marketId == MarketId)
            {
                var currency = new CurrencyState
                {
                    TradeMoney = TradingCurrency,
                    DevelopmentCurrency = Math.Max(0L, saveData.player.developmentCurrency)
                };

                CurrencyApplyResult refundResult = CurrencyWallet.ApplyTradeRefund(currency, preparation.totalCost);
                if (refundResult == null || !refundResult.Success || refundResult.After == null)
                {
                    return CargoIntegrationResult.Fail(
                        refundResult != null ? refundResult.ErrorCode : ErrorCurrency,
                        TradingCurrency);
                }

                RestoreStocks(requests);
                saveData.player.tradingCurrency = refundResult.After.TradeMoney;
                saveData.player.developmentCurrency = refundResult.After.DevelopmentCurrency;
            }

            saveData.caravan.cargo.Clear();
            preparation.marketId = string.Empty;
            preparation.isCommitted = false;
            preparation.totalCost = 0L;
            preparation.cargoHash = 0;
            saveService.Save(saveData);
            return CargoIntegrationResult.Ok(TradingCurrency);
        }

        public IReadOnlyList<CargoPurchaseRequest> RestoreSavedCargo()
        {
            var result = new List<CargoPurchaseRequest>();
            if (saveData.caravan == null || saveData.caravan.cargo == null)
            {
                return result;
            }

            foreach (CargoEntrySaveData entry in saveData.caravan.cargo)
            {
                if (entry == null || entry.item == null || entry.quantity <= 0)
                {
                    continue;
                }

                if (!catalogById.TryGetValue(entry.item.itemId ?? string.Empty, out TradeItemData item))
                {
                    continue;
                }

                result.Add(new CargoPurchaseRequest
                {
                    Item = item,
                    Quantity = entry.quantity,
                    UnitPrice = Math.Max(0L, entry.item.basePrice)
                });
            }

            return result;
        }

        private void ResolveOrRefreshInventory()
        {
            DateTime now = timeProvider.CurrentUtc;
            long refreshIndex = Math.Max(0L, now.Ticks / refreshIntervalTicks);
            inventory = saveData.world.marketInventories.FirstOrDefault(
                candidate => candidate != null && candidate.marketId == MarketId);

            bool hasActivePreparation = saveData.world.marketPurchasePreparation != null
                && saveData.world.marketPurchasePreparation.marketId == MarketId
                && saveData.caravan?.cargo != null
                && saveData.caravan.cargo.Count > 0;

            if (inventory != null
                && (inventory.refreshIndex == refreshIndex || hasActivePreparation)
                && inventory.stocks != null
                && inventory.stocks.Count > 0)
            {
                return;
            }

            if (inventory == null)
            {
                inventory = new MarketInventorySaveData { marketId = MarketId };
                saveData.world.marketInventories.Add(inventory);
            }

            GenerateInventory(refreshIndex);
            saveService.Save(saveData);
        }

        private void GenerateInventory(long refreshIndex)
        {
            int seed = StableHash(worldSeed, MarketId, refreshIndex);
            var random = new Random(seed);
            List<TradeItemData> candidates = catalogById.Values
                .OrderBy(item => item.ItemId, StringComparer.Ordinal)
                .ToList();

            for (int index = candidates.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                TradeItemData temporary = candidates[index];
                candidates[index] = candidates[swapIndex];
                candidates[swapIndex] = temporary;
            }

            int count = Math.Min(slotCount, candidates.Count);
            List<TradeItemData> selected = candidates.Take(count).ToList();
            TradeItemData requiredFood = candidates.FirstOrDefault(IsFood);
            if (requiredFood != null && selected.All(item => !IsFood(item)))
            {
                selected[selected.Count - 1] = requiredFood;
            }

            inventory.marketId = MarketId;
            inventory.refreshIndex = refreshIndex;
            inventory.nextRefreshUtcTicks = checked((refreshIndex + 1L) * refreshIntervalTicks);
            inventory.seed = seed;
            inventory.stocks = selected
                .Distinct()
                .Select(item => new MarketStockSaveData
                {
                    itemId = item.ItemId,
                    quantity = random.Next(1, maximumGeneratedStock + 1),
                    unitPrice = Math.Max(0L, item.BaseBuyPrice)
                })
                .ToList();
        }

        private bool TryValidateRequests(
            IReadOnlyList<CargoPurchaseRequest> requests,
            bool validateStock,
            out long totalCost,
            out string error)
        {
            totalCost = 0L;
            error = string.Empty;
            if (requests == null)
            {
                error = ErrorInvalidCargo;
                return false;
            }

            try
            {
                foreach (CargoPurchaseRequest request in requests)
                {
                    if (request == null || request.Item == null || request.Quantity <= 0)
                    {
                        error = ErrorInvalidCargo;
                        return false;
                    }

                    MarketStockSaveData stock = FindStock(request.Item.ItemId);
                    if (stock == null || (validateStock && stock.quantity < request.Quantity))
                    {
                        error = ErrorInsufficientStock;
                        return false;
                    }

                    totalCost = checked(totalCost + checked(Math.Max(0L, request.UnitPrice) * request.Quantity));
                }
            }
            catch (OverflowException)
            {
                error = ErrorInvalidCargo;
                return false;
            }

            return true;
        }

        private void WriteCargo(IReadOnlyList<CargoPurchaseRequest> requests)
        {
            saveData.caravan.cargo.Clear();
            foreach (CargoPurchaseRequest request in requests)
            {
                saveData.caravan.cargo.Add(new CargoEntrySaveData
                {
                    quantity = request.Quantity,
                    item = new TradeItemSaveData
                    {
                        itemId = request.Item.ItemId,
                        itemName = request.Item.DisplayName,
                        weight = request.Item.Weight,
                        basePrice = Math.Max(0L, request.UnitPrice),
                        maxCount = request.Item.MaxCount
                    }
                });
            }
        }

        private void RestoreStocks(IReadOnlyList<CargoPurchaseRequest> requests)
        {
            foreach (CargoPurchaseRequest request in requests)
            {
                MarketStockSaveData stock = FindStock(request.Item.ItemId);
                if (stock != null)
                {
                    stock.quantity = checked(stock.quantity + request.Quantity);
                }
            }
        }

        private MarketStockSaveData FindStock(string itemId)
        {
            return inventory?.stocks?.FirstOrDefault(
                candidate => candidate != null && candidate.itemId == itemId);
        }

        private void EnsureSaveContainers()
        {
            saveData.player ??= new PlayerSaveData();
            saveData.caravan ??= new CaravanSaveData();
            saveData.caravan.cargo ??= new List<CargoEntrySaveData>();
            saveData.world ??= new WorldSaveData();
            saveData.world.marketInventories ??= new List<MarketInventorySaveData>();
            saveData.world.marketPurchasePreparation ??= new MarketPurchasePreparationSaveData();
        }

        private static Dictionary<string, TradeItemData> BuildCatalog(IEnumerable<TradeItemData> source)
        {
            var result = new Dictionary<string, TradeItemData>(StringComparer.Ordinal);
            if (source == null)
            {
                return result;
            }

            foreach (TradeItemData item in source)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.ItemId) || result.ContainsKey(item.ItemId))
                {
                    continue;
                }

                result.Add(item.ItemId, item);
            }

            return result;
        }

        private static long CalculateTotalCost(IReadOnlyList<CargoPurchaseRequest> requests)
        {
            long result = 0L;
            if (requests == null)
            {
                return result;
            }

            foreach (CargoPurchaseRequest request in requests)
            {
                if (request != null && request.Quantity > 0)
                {
                    result = checked(result + checked(Math.Max(0L, request.UnitPrice) * request.Quantity));
                }
            }

            return result;
        }

        private static int ComputeCargoHash(IReadOnlyList<CargoPurchaseRequest> requests)
        {
            unchecked
            {
                uint hash = 2166136261u;
                if (requests == null)
                {
                    return (int)hash;
                }

                foreach (CargoPurchaseRequest request in requests
                             .Where(value => value?.Item != null)
                             .OrderBy(value => value.Item.ItemId, StringComparer.Ordinal))
                {
                    hash = HashString(hash, request.Item.ItemId);
                    hash = HashInt64(hash, request.Quantity);
                    hash = HashInt64(hash, request.UnitPrice);
                }

                return (int)hash;
            }
        }

        private static int StableHash(int seed, string marketId, long refreshIndex)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = HashInt64(hash, seed);
                hash = HashString(hash, marketId);
                hash = HashInt64(hash, refreshIndex);
                return (int)(hash & 0x7fffffff);
            }
        }

        private static uint HashString(uint hash, string value)
        {
            unchecked
            {
                foreach (char character in value ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static uint HashInt64(uint hash, long value)
        {
            unchecked
            {
                for (int offset = 0; offset < 8; offset++)
                {
                    hash ^= (byte)(value >> (offset * 8));
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static bool IsFood(TradeItemData item)
        {
            return item != null
                && (item.Category == TradeItemCategory.Food
                    || item.Category == TradeItemCategory.DraftAnimalsFood);
        }
    }
}
#endif
