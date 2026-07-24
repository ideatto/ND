using System;
using System.Collections.Generic;
using System.Linq;
using ND.Economy;
using ND.Framework;

namespace ND.Framework.CargoLoading
{
    public enum MarketTradeMode
    {
        BuyAndSell = 0,
        BuyOnly = 1,
        SellOnly = 2
    }

    public sealed class MarketStockView
    {
        public TradeItemData Item;
        public int Quantity;
        public long UnitPrice;
    }

    public sealed class CargoInventoryView
    {
        public string ItemId = string.Empty;
        public TradeItemData Item;
        public int Quantity;
        public long UnitPrice;
        public float Weight;
        public int MaxStackQuantity = 1;
    }

    /// <summary>
    /// Read-only view over one market and the cargo currently stored in SaveData.
    /// Mutation methods intentionally live on MarketInventoryMutationSession.
    /// </summary>
    public sealed class MarketInventorySession
    {
        private readonly MarketInventoryMutationSession source;

        internal MarketInventorySession(MarketInventoryMutationSession source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public string MarketId => source.MarketId;
        public string CaravanId => source.CaravanId;
        public MarketTradeMode TradeMode => source.TradeMode;
        public long TradingCurrency => source.TradingCurrency;
        public IReadOnlyList<MarketStockView> Stocks => source.Stocks;
        public IReadOnlyList<CargoInventoryView> SavedCargo => source.ReadSavedCargo();
    }

    public sealed class MarketTransactionLine
    {
        public string ItemId = string.Empty;
        public int BuyQuantity;
        public int SellQuantity;
    }

    public sealed class MarketTransactionResult
    {
        public bool Success;
        public string ErrorCode = string.Empty;
        public long TradingCurrencyAfter;
        public long PurchaseCost;
        public long SaleRevenue;

        internal static MarketTransactionResult Fail(string errorCode, long currency)
        {
            return new MarketTransactionResult
            {
                Success = false,
                ErrorCode = errorCode ?? string.Empty,
                TradingCurrencyAfter = Math.Max(0L, currency)
            };
        }
    }

    /// <summary>
    /// Applies explicit buy/sell deltas atomically. Draft cancellation belongs to the UI and
    /// never calls this command, so unrelated cargo entries are preserved.
    /// </summary>
    public static class MarketTransactionCommand
    {
        public static MarketTransactionResult Execute(
            MarketInventoryMutationSession session,
            IReadOnlyList<MarketTransactionLine> lines,
            float maximumCargoWeight,
            int maximumCargoSlots = int.MaxValue)
        {
            return session == null
                ? MarketTransactionResult.Fail(MarketInventoryMutationSession.ErrorInvalidFramework, 0L)
                : session.ExecuteTransaction(lines, maximumCargoWeight, maximumCargoSlots);
        }
    }

    /// <summary>
    /// Owns market refresh and atomic buy/sell transactions.
    /// Consumers must use View for reads so query and mutation responsibilities stay explicit.
    /// </summary>
    public sealed class MarketInventoryMutationSession
    {
        public const string ErrorInvalidFramework = "INVALID_FRAMEWORK";
        public const string ErrorInvalidCaravan = "INVALID_CARAVAN";
        public const string ErrorInvalidCatalog = "INVALID_CATALOG";
        public const string ErrorInsufficientStock = "INSUFFICIENT_STOCK";
        public const string ErrorCurrency = "CURRENCY_FAILURE";
        public const string ErrorInvalidTransaction = "INVALID_MARKET_TRANSACTION";
        public const string ErrorInsufficientCargo = "INSUFFICIENT_CARGO";
        public const string ErrorCargoWeight = "CARGO_WEIGHT_EXCEEDED";
        public const string ErrorCargoSlots = "CARGO_SLOT_EXCEEDED";
        public const string ErrorSaveFailed = "SAVE_FAILED";

        private readonly SaveData saveData;
        private readonly CaravanSaveData targetCaravan;
        private readonly MarketTradeMode tradeMode;
        private readonly ISaveService saveService;
        private readonly IGameTimeProvider timeProvider;
        private readonly Dictionary<string, TradeItemData> catalogById;
        private readonly HashSet<string> stockItemIds;
        private readonly int slotCount;
        private readonly int maximumGeneratedStock;
        private readonly long refreshIntervalTicks;
        private readonly int worldSeed;

        private MarketInventorySaveData inventory;

        private MarketInventoryMutationSession(
            SaveData saveData,
            CaravanSaveData targetCaravan,
            MarketTradeMode tradeMode,
            ISaveService saveService,
            IGameTimeProvider timeProvider,
            string marketId,
            IEnumerable<TradeItemData> stockCatalog,
            IEnumerable<TradeItemData> transactionCatalog,
            int slotCount,
            int maximumGeneratedStock,
            double refreshIntervalSeconds,
            int worldSeed)
        {
            this.saveData = saveData;
            this.targetCaravan = targetCaravan;
            this.tradeMode = tradeMode;
            this.saveService = saveService;
            this.timeProvider = timeProvider;
            MarketId = string.IsNullOrWhiteSpace(marketId) ? "default-market" : marketId;
            this.slotCount = Math.Max(1, slotCount);
            this.maximumGeneratedStock = Math.Max(1, maximumGeneratedStock);
            refreshIntervalTicks = TimeSpan.FromSeconds(Math.Max(1d, refreshIntervalSeconds)).Ticks;
            this.worldSeed = worldSeed;
            TradeItemData[] stockItems = (stockCatalog ?? Enumerable.Empty<TradeItemData>())
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ItemId))
                .ToArray();
            catalogById = BuildCatalog(stockItems.Concat(
                transactionCatalog ?? Enumerable.Empty<TradeItemData>()));
            stockItemIds = new HashSet<string>(
                stockItems.Select(item => item.ItemId),
                StringComparer.Ordinal);
            View = new MarketInventorySession(this);
        }

        public MarketInventorySession View { get; }

        public string CaravanId => targetCaravan.caravanId ?? string.Empty;

        public MarketTradeMode TradeMode => tradeMode;

        internal string MarketId { get; }

        internal long TradingCurrency =>
            saveData.player != null ? Math.Max(0L, saveData.player.tradingCurrency) : 0L;

        internal IReadOnlyList<MarketStockView> Stocks
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
                        Quantity = MarketTransactionCalculator.GetEffectiveMarketStock(
                            item.ItemId,
                            stock.quantity),
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
            out MarketInventoryMutationSession session,
            out string error)
        {
            return TryOpen(
                saveData, saveData != null ? saveData.selectedCaravanId : string.Empty,
                saveService, timeProvider, marketId,
                catalog, catalog, slotCount, maximumGeneratedStock,
                refreshIntervalSeconds, worldSeed, out session, out error);
        }

        public static bool TryOpen(
            SaveData saveData,
            ISaveService saveService,
            IGameTimeProvider timeProvider,
            string marketId,
            IEnumerable<TradeItemData> stockCatalog,
            IEnumerable<TradeItemData> transactionCatalog,
            int slotCount,
            int maximumGeneratedStock,
            double refreshIntervalSeconds,
            int worldSeed,
            out MarketInventoryMutationSession session,
            out string error)
        {
            return TryOpen(
                saveData, saveData != null ? saveData.selectedCaravanId : string.Empty,
                saveService, timeProvider, marketId, stockCatalog, transactionCatalog,
                slotCount, maximumGeneratedStock, refreshIntervalSeconds, worldSeed,
                out session, out error);
        }

        public static bool TryOpen(
            SaveData saveData,
            string caravanId,
            ISaveService saveService,
            IGameTimeProvider timeProvider,
            string marketId,
            IEnumerable<TradeItemData> stockCatalog,
            IEnumerable<TradeItemData> transactionCatalog,
            int slotCount,
            int maximumGeneratedStock,
            double refreshIntervalSeconds,
            int worldSeed,
            out MarketInventoryMutationSession session,
            out string error)
        {
            return TryOpen(
                saveData, caravanId, MarketTradeMode.BuyAndSell,
                saveService, timeProvider, marketId, stockCatalog, transactionCatalog,
                slotCount, maximumGeneratedStock, refreshIntervalSeconds, worldSeed,
                out session, out error);
        }

        public static bool TryOpen(
            SaveData saveData,
            string caravanId,
            MarketTradeMode tradeMode,
            ISaveService saveService,
            IGameTimeProvider timeProvider,
            string marketId,
            IEnumerable<TradeItemData> stockCatalog,
            IEnumerable<TradeItemData> transactionCatalog,
            int slotCount,
            int maximumGeneratedStock,
            double refreshIntervalSeconds,
            int worldSeed,
            out MarketInventoryMutationSession session,
            out string error)
        {
            session = null;
            error = string.Empty;

            if (saveData == null || saveService == null || timeProvider == null)
            {
                error = ErrorInvalidFramework;
                return false;
            }

            CaravanSaveData targetCaravan;
            if (!SaveDataLookup.TryGetCaravan(saveData, caravanId, out targetCaravan))
            {
                error = ErrorInvalidCaravan;
                return false;
            }

            var created = new MarketInventoryMutationSession(
                saveData,
                targetCaravan,
                tradeMode,
                saveService,
                timeProvider,
                marketId,
                stockCatalog,
                transactionCatalog,
                slotCount,
                maximumGeneratedStock,
                refreshIntervalSeconds,
                worldSeed);

            if (created.stockItemIds.Count == 0 || created.catalogById.Count == 0)
            {
                error = ErrorInvalidCatalog;
                return false;
            }

            created.EnsureSaveContainers();
            if (!created.TryRestoreStoredDraftAnimalFoodToCargo())
            {
                error = ErrorSaveFailed;
                return false;
            }
            if (!created.TryResolveOrRefreshInventory())
            {
                error = ErrorSaveFailed;
                return false;
            }

            session = created;
            return true;
        }

        /// <summary>
        /// Travel represents draft-animal food through CaravanSaveData.foodAmount instead of a
        /// Cargo entry. Once the caravan is back in Town, restore the remaining whole units to
        /// their trade-item form so the next Market screen can display, reuse, or sell them.
        /// </summary>
        private bool TryRestoreStoredDraftAnimalFoodToCargo()
        {
            int storedQuantity = Math.Max(0, targetCaravan.foodAmount);
            if (storedQuantity == 0)
                return true;

            TradeItemData foodItem = catalogById.Values.FirstOrDefault(
                item => item != null && item.Category == TradeItemCategory.DraftAnimalsFood);
            if (foodItem == null)
                return true;

            int foodBefore = targetCaravan.foodAmount;
            List<CargoEntrySaveData> cargoBefore = CloneCargo(targetCaravan.cargo);
            try
            {
                ApplyCargoDelta(foodItem.ItemId, storedQuantity);
                targetCaravan.foodAmount = 0;
                SaveResult saveResult = saveService.Save(saveData);
                if (saveResult != null && saveResult.Succeeded)
                    return true;
            }
            catch
            {
                // Restore below so callers see the same state regardless of failure source.
            }

            targetCaravan.foodAmount = foodBefore;
            targetCaravan.cargo = cargoBefore;
            return false;
        }

        internal MarketTransactionResult ExecuteTransaction(
            IReadOnlyList<MarketTransactionLine> lines,
            float maximumCargoWeight,
            int maximumCargoSlots)
        {
            if (lines == null || float.IsNaN(maximumCargoWeight) || maximumCargoWeight < 0f ||
                maximumCargoSlots < 0)
            {
                return MarketTransactionResult.Fail(ErrorInvalidTransaction, TradingCurrency);
            }

            var normalized = new Dictionary<string, MarketTransactionLine>(StringComparer.Ordinal);
            foreach (MarketTransactionLine line in lines)
            {
                if (line == null
                    || string.IsNullOrWhiteSpace(line.ItemId)
                    || line.BuyQuantity < 0
                    || line.SellQuantity < 0
                    || (line.BuyQuantity > 0 && line.SellQuantity > 0)
                    || (line.BuyQuantity == 0 && line.SellQuantity == 0)
                    || normalized.ContainsKey(line.ItemId)
                    || !catalogById.ContainsKey(line.ItemId))
                {
                    return MarketTransactionResult.Fail(ErrorInvalidTransaction, TradingCurrency);
                }

                if ((tradeMode == MarketTradeMode.BuyOnly && line.SellQuantity > 0)
                    || (tradeMode == MarketTradeMode.SellOnly && line.BuyQuantity > 0))
                {
                    return MarketTransactionResult.Fail(ErrorInvalidTransaction, TradingCurrency);
                }

                normalized.Add(line.ItemId, line);
            }

            if (normalized.Count == 0)
            {
                return MarketTransactionResult.Fail(ErrorInvalidTransaction, TradingCurrency);
            }

            var calculationInput = new ND.Economy.MarketTransactionInput
            {
                TradingCurrencyBefore = TradingCurrency,
                CurrentCargoWeight = CalculateCurrentCargoWeight(),
                MaximumCargoWeight = maximumCargoWeight,
                CurrentCargoSlots = CalculateCurrentCargoSlots(),
                MaximumCargoSlots = maximumCargoSlots
            };
            foreach (MarketTransactionLine line in normalized.Values)
            {
                TradeItemData item = catalogById[line.ItemId];
                MarketStockSaveData stock = FindStock(line.ItemId);
                calculationInput.Items.Add(new ND.Economy.MarketTransactionItemInput
                {
                    ItemId = line.ItemId,
                    CargoQuantityBefore = GetCargoQuantity(line.ItemId),
                    MarketStockBefore = Math.Max(0, stock?.quantity ?? 0),
                    BuyQuantity = line.BuyQuantity,
                    SellQuantity = line.SellQuantity,
                    BuyUnitPrice = Math.Max(0L, stock?.unitPrice ?? 0L),
                    SellUnitPrice = Math.Max(0L, item.BaseSellPrice),
                    UnitWeight = Math.Max(0f, item.Weight),
                    MaxStackQuantity = Math.Max(1, item.MaxCount)
                });
            }

            ND.Economy.MarketTransactionResult calculation =
                ND.Economy.MarketTransactionCalculator.CalculateMarketTransaction(calculationInput);
            if (!calculation.Success)
            {
                return MarketTransactionResult.Fail(
                    MapCalculationFailure(calculation.FailureReason),
                    TradingCurrency);
            }

            long currencyBefore = saveData.player.tradingCurrency;
            List<CargoEntrySaveData> cargoBefore = CloneCargo(targetCaravan.cargo);
            var stockBefore = inventory.stocks
                .Where(stock => stock != null)
                .ToDictionary(stock => stock.itemId ?? string.Empty, stock => stock.quantity, StringComparer.Ordinal);

            MarketTransactionResult successfulResult;
            try
            {
                foreach (ND.Economy.MarketTransactionItemResult itemResult in calculation.Items)
                {
                    ApplyCargoDelta(itemResult.ItemId, itemResult.BuyQuantity - itemResult.SellQuantity);
                    MarketStockSaveData stock = itemResult.SellQuantity > 0
                        ? GetOrCreateStock(itemResult.ItemId)
                        : FindStock(itemResult.ItemId);
                    stock.quantity = itemResult.MarketStockAfter;
                }

                saveData.player.tradingCurrency = calculation.TradingCurrencyAfter;
                SaveResult saveResult = saveService.Save(saveData);
                if (saveResult == null || !saveResult.Succeeded)
                {
                    RestoreTransactionSnapshot(currencyBefore, cargoBefore, stockBefore);
                    return MarketTransactionResult.Fail(ErrorSaveFailed, currencyBefore);
                }

                successfulResult = new MarketTransactionResult
                {
                    Success = true,
                    TradingCurrencyAfter = calculation.TradingCurrencyAfter,
                    PurchaseCost = calculation.TotalPurchaseCost,
                    SaleRevenue = calculation.TotalSaleRevenue
                };
            }
            catch
            {
                RestoreTransactionSnapshot(currencyBefore, cargoBefore, stockBefore);
                return MarketTransactionResult.Fail(ErrorInvalidTransaction, currencyBefore);
            }

            // Publish only after SaveData persistence succeeds. UI subscribers re-read the saved
            // Caravan snapshot, and failed/rolled-back transactions never emit refresh signals.
            FrameworkEvents.RaiseCaravanCargoChanged(CaravanId);
            FrameworkEvents.RaiseTradingCurrencyChanged(calculation.TradingCurrencyAfter);
            return successfulResult;
        }

        internal IReadOnlyList<CargoInventoryView> ReadSavedCargo()
        {
            var result = new List<CargoInventoryView>();
            if (targetCaravan.cargo == null)
            {
                return result;
            }

            foreach (CargoEntrySaveData entry in targetCaravan.cargo)
            {
                if (entry == null || entry.item == null || entry.quantity <= 0)
                {
                    continue;
                }

                string itemId = entry.item.itemId ?? string.Empty;
                catalogById.TryGetValue(itemId, out TradeItemData item);

                result.Add(new CargoInventoryView
                {
                    ItemId = itemId,
                    Item = item,
                    Quantity = entry.quantity,
                    UnitPrice = Math.Max(0L, entry.item.basePrice),
                    Weight = Math.Max(0f, entry.item.weight),
                    MaxStackQuantity = Math.Max(1, entry.item.maxCount)
                });
            }

            return result;
        }

        private bool TryResolveOrRefreshInventory()
        {
            DateTime now = timeProvider.CurrentUtc;
            long refreshIndex = Math.Max(0L, now.Ticks / refreshIntervalTicks);
            inventory = saveData.world.marketInventories.FirstOrDefault(
                candidate => candidate != null && candidate.marketId == MarketId);

            if (inventory != null
                && inventory.refreshIndex == refreshIndex
                && inventory.stocks != null
                && inventory.stocks.Count > 0)
            {
                return true;
            }

            MarketInventorySaveData previousInventory = inventory;
            MarketInventorySaveData snapshot = CloneInventory(previousInventory);
            if (inventory == null)
            {
                inventory = new MarketInventorySaveData { marketId = MarketId };
                saveData.world.marketInventories.Add(inventory);
            }

            try
            {
                GenerateInventory(refreshIndex);
                SaveResult result = saveService.Save(saveData);
                if (result != null && result.Succeeded)
                {
                    return true;
                }
            }
            catch
            {
                // Restore below so an unsuccessful open cannot expose unsaved inventory.
            }

            if (previousInventory == null)
            {
                saveData.world.marketInventories.Remove(inventory);
                inventory = null;
            }
            else
            {
                RestoreInventory(previousInventory, snapshot);
                inventory = previousInventory;
            }

            return false;
        }

        private static MarketInventorySaveData CloneInventory(MarketInventorySaveData source)
        {
            if (source == null)
            {
                return null;
            }

            return new MarketInventorySaveData
            {
                marketId = source.marketId,
                refreshIndex = source.refreshIndex,
                nextRefreshUtcTicks = source.nextRefreshUtcTicks,
                seed = source.seed,
                stocks = (source.stocks ?? new List<MarketStockSaveData>())
                    .Where(stock => stock != null)
                    .Select(stock => new MarketStockSaveData
                    {
                        itemId = stock.itemId,
                        quantity = stock.quantity,
                        unitPrice = stock.unitPrice
                    })
                    .ToList()
            };
        }

        private static void RestoreInventory(
            MarketInventorySaveData destination,
            MarketInventorySaveData snapshot)
        {
            destination.marketId = snapshot.marketId;
            destination.refreshIndex = snapshot.refreshIndex;
            destination.nextRefreshUtcTicks = snapshot.nextRefreshUtcTicks;
            destination.seed = snapshot.seed;
            destination.stocks = snapshot.stocks;
        }

        private void GenerateInventory(long refreshIndex)
        {
            int seed = StableHash(worldSeed, MarketId, refreshIndex);
            var random = new Random(seed);
            List<TradeItemData> candidates = stockItemIds
                .Where(catalogById.ContainsKey)
                .Select(itemId => catalogById[itemId])
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
                    quantity = MarketTransactionCalculator.GetEffectiveMarketStock(
                        item.ItemId,
                        random.Next(1, maximumGeneratedStock + 1)),
                    unitPrice = Math.Max(0L, item.BaseBuyPrice)
                })
                .ToList();
        }

        private int GetCargoQuantity(string itemId)
        {
            return targetCaravan.cargo
                .Where(entry => entry?.item != null && entry.item.itemId == itemId)
                .Sum(entry => Math.Max(0, entry.quantity));
        }

        private float CalculateCurrentCargoWeight()
        {
            double result = 0d;
            foreach (CargoEntrySaveData entry in targetCaravan.cargo)
            {
                if (entry?.item != null && entry.quantity > 0)
                {
                    result += Math.Max(0f, entry.item.weight) * entry.quantity;
                }
            }

            return result >= float.MaxValue ? float.PositiveInfinity : (float)result;
        }

        private int CalculateCurrentCargoSlots()
        {
            int total = 0;
            foreach (IGrouping<string, CargoEntrySaveData> group in targetCaravan.cargo
                .Where(entry => entry?.item != null && entry.quantity > 0)
                .GroupBy(entry => entry.item.itemId ?? string.Empty, StringComparer.Ordinal))
            {
                int quantity = group.Sum(entry => Math.Max(0, entry.quantity));
                int maxStack = Math.Max(1, group.First().item.maxCount);
                total = checked(total + (quantity - 1) / maxStack + 1);
            }
            return total;
        }

        private static string MapCalculationFailure(
            ND.Economy.MarketTransactionFailureReason failureReason)
        {
            switch (failureReason)
            {
                case ND.Economy.MarketTransactionFailureReason.InsufficientCurrency:
                    return ErrorCurrency;
                case ND.Economy.MarketTransactionFailureReason.InsufficientCargo:
                    return ErrorInsufficientCargo;
                case ND.Economy.MarketTransactionFailureReason.InsufficientStock:
                    return ErrorInsufficientStock;
                case ND.Economy.MarketTransactionFailureReason.CargoWeightExceeded:
                    return ErrorCargoWeight;
                case ND.Economy.MarketTransactionFailureReason.CargoSlotExceeded:
                    return ErrorCargoSlots;
                default:
                    return ErrorInvalidTransaction;
            }
        }

        private void ApplyCargoDelta(string itemId, int delta)
        {
            List<CargoEntrySaveData> entries = targetCaravan.cargo
                .Where(candidate => candidate?.item != null && candidate.item.itemId == itemId)
                .ToList();
            CargoEntrySaveData entry = entries.FirstOrDefault();
            int quantityAfter = checked(entries.Sum(candidate => Math.Max(0, candidate.quantity)) + delta);
            if (quantityAfter < 0)
            {
                throw new InvalidOperationException(ErrorInsufficientCargo);
            }

            if (quantityAfter == 0)
            {
                foreach (CargoEntrySaveData existing in entries)
                {
                    targetCaravan.cargo.Remove(existing);
                }

                return;
            }

            if (entry == null)
            {
                TradeItemData item = catalogById[itemId];
                MarketStockSaveData stock = FindStock(itemId);
                entry = new CargoEntrySaveData
                {
                    item = new TradeItemSaveData
                    {
                        itemId = item.ItemId,
                        itemName = item.DisplayName,
                        weight = item.Weight,
                        basePrice = Math.Max(0L, stock?.unitPrice ?? item.BaseBuyPrice),
                        maxCount = item.MaxCount
                    }
                };
                targetCaravan.cargo.Add(entry);
            }

            foreach (CargoEntrySaveData duplicate in entries.Skip(1))
            {
                targetCaravan.cargo.Remove(duplicate);
            }

            entry.quantity = quantityAfter;
        }

        private MarketStockSaveData GetOrCreateStock(string itemId)
        {
            MarketStockSaveData stock = FindStock(itemId);
            if (stock != null)
            {
                return stock;
            }

            TradeItemData item = catalogById[itemId];
            stock = new MarketStockSaveData
            {
                itemId = itemId,
                quantity = 0,
                unitPrice = Math.Max(0L, item.BaseBuyPrice)
            };
            inventory.stocks.Add(stock);
            return stock;
        }

        private static List<CargoEntrySaveData> CloneCargo(IEnumerable<CargoEntrySaveData> source)
        {
            return source
                .Where(entry => entry != null)
                .Select(entry => new CargoEntrySaveData
                {
                    quantity = entry.quantity,
                    item = entry.item == null
                        ? null
                        : new TradeItemSaveData
                        {
                            itemId = entry.item.itemId,
                            itemName = entry.item.itemName,
                            weight = entry.item.weight,
                            basePrice = entry.item.basePrice,
                            maxCount = entry.item.maxCount
                        }
                })
                .ToList();
        }

        private void RestoreTransactionSnapshot(
            long currencyBefore,
            List<CargoEntrySaveData> cargoBefore,
            IReadOnlyDictionary<string, int> stockBefore)
        {
            saveData.player.tradingCurrency = currencyBefore;
            targetCaravan.cargo = cargoBefore;
            inventory.stocks.RemoveAll(stock =>
                stock != null && !stockBefore.ContainsKey(stock.itemId ?? string.Empty));
            foreach (MarketStockSaveData stock in inventory.stocks.Where(stock => stock != null))
            {
                if (stockBefore.TryGetValue(stock.itemId ?? string.Empty, out int quantity))
                {
                    stock.quantity = quantity;
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
            targetCaravan.cargo ??= new List<CargoEntrySaveData>();
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
