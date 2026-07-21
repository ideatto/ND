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

    public sealed class CargoInventoryView
    {
        public string ItemId = string.Empty;
        public TradeItemData Item;
        public int Quantity;
        public long UnitPrice;
        public float Weight;
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
        public bool IsCommitted => source.IsCommitted;
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
            float maximumCargoWeight)
        {
            return session == null
                ? MarketTransactionResult.Fail(MarketInventoryMutationSession.ErrorInvalidFramework, 0L)
                : session.ExecuteTransaction(lines, maximumCargoWeight);
        }
    }

    /// <summary>
    /// Owns market refresh, draft persistence, purchase commit, refund, and cancellation.
    /// Consumers must use View for reads so query and mutation responsibilities stay explicit.
    /// </summary>
    public sealed class MarketInventoryMutationSession
    {
        public const string ErrorInvalidFramework = "INVALID_FRAMEWORK";
        public const string ErrorInvalidCatalog = "INVALID_CATALOG";
        public const string ErrorInvalidCargo = "INVALID_CARGO";
        public const string ErrorInsufficientStock = "INSUFFICIENT_STOCK";
        public const string ErrorCurrency = "CURRENCY_FAILURE";
        public const string ErrorInvalidTransaction = "INVALID_MARKET_TRANSACTION";
        public const string ErrorInsufficientCargo = "INSUFFICIENT_CARGO";
        public const string ErrorCargoWeight = "CARGO_WEIGHT_EXCEEDED";
        public const string ErrorSaveFailed = "SAVE_FAILED";

        private readonly SaveData saveData;
        private readonly ISaveService saveService;
        private readonly IGameTimeProvider timeProvider;
        private readonly Dictionary<string, TradeItemData> catalogById;
        private readonly int slotCount;
        private readonly int maximumGeneratedStock;
        private readonly long refreshIntervalTicks;
        private readonly int worldSeed;

        private MarketInventorySaveData inventory;

        private MarketInventoryMutationSession(
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
            View = new MarketInventorySession(this);
        }

        public MarketInventorySession View { get; }

        internal string MarketId { get; }

        internal bool IsCommitted =>
            saveData.world.marketPurchasePreparation != null
            && saveData.world.marketPurchasePreparation.isCommitted
            && saveData.world.marketPurchasePreparation.marketId == MarketId;

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

            var created = new MarketInventoryMutationSession(
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

        internal MarketTransactionResult ExecuteTransaction(
            IReadOnlyList<MarketTransactionLine> lines,
            float maximumCargoWeight)
        {
            if (lines == null || float.IsNaN(maximumCargoWeight) || maximumCargoWeight < 0f)
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

                normalized.Add(line.ItemId, line);
            }

            if (normalized.Count == 0)
            {
                return MarketTransactionResult.Fail(ErrorInvalidTransaction, TradingCurrency);
            }

            long purchaseCost = 0L;
            long saleRevenue = 0L;
            try
            {
                foreach (MarketTransactionLine line in normalized.Values)
                {
                    TradeItemData item = catalogById[line.ItemId];
                    MarketStockSaveData stock = FindStock(line.ItemId);
                    int cargoQuantity = GetCargoQuantity(line.ItemId);
                    if (line.BuyQuantity > 0 && (stock == null || stock.quantity < line.BuyQuantity))
                    {
                        return MarketTransactionResult.Fail(ErrorInsufficientStock, TradingCurrency);
                    }

                    if (line.SellQuantity > cargoQuantity)
                    {
                        return MarketTransactionResult.Fail(ErrorInsufficientCargo, TradingCurrency);
                    }

                    purchaseCost = checked(purchaseCost + checked((stock?.unitPrice ?? 0L) * line.BuyQuantity));
                    saleRevenue = checked(saleRevenue + checked(Math.Max(0L, item.BaseSellPrice) * line.SellQuantity));
                }

                long currencyAfter = checked(checked(TradingCurrency - purchaseCost) + saleRevenue);
                if (currencyAfter < 0L)
                {
                    return MarketTransactionResult.Fail(ErrorCurrency, TradingCurrency);
                }

                float weightAfter = CalculateCargoWeightAfter(normalized);
                if (!float.IsPositiveInfinity(maximumCargoWeight)
                    && weightAfter > maximumCargoWeight + 0.0001f)
                {
                    return MarketTransactionResult.Fail(ErrorCargoWeight, TradingCurrency);
                }

                long currencyBefore = saveData.player.tradingCurrency;
                List<CargoEntrySaveData> cargoBefore = CloneCargo(saveData.caravan.cargo);
                var stockBefore = inventory.stocks
                    .Where(stock => stock != null)
                    .ToDictionary(stock => stock.itemId ?? string.Empty, stock => stock.quantity, StringComparer.Ordinal);

                try
                {
                    foreach (MarketTransactionLine line in normalized.Values)
                    {
                        ApplyCargoDelta(line.ItemId, line.BuyQuantity - line.SellQuantity);
                        MarketStockSaveData stock = line.SellQuantity > 0
                            ? GetOrCreateStock(line.ItemId)
                            : FindStock(line.ItemId);
                        stock.quantity = checked(stock.quantity - line.BuyQuantity + line.SellQuantity);
                    }

                    saveData.player.tradingCurrency = currencyAfter;
                    SaveResult saveResult = saveService.Save(saveData);
                    if (saveResult == null || !saveResult.Succeeded)
                    {
                        RestoreTransactionSnapshot(currencyBefore, cargoBefore, stockBefore);
                        return MarketTransactionResult.Fail(ErrorSaveFailed, currencyBefore);
                    }

                    return new MarketTransactionResult
                    {
                        Success = true,
                        TradingCurrencyAfter = currencyAfter,
                        PurchaseCost = purchaseCost,
                        SaleRevenue = saleRevenue
                    };
                }
                catch
                {
                    RestoreTransactionSnapshot(currencyBefore, cargoBefore, stockBefore);
                    return MarketTransactionResult.Fail(ErrorInvalidTransaction, currencyBefore);
                }
            }
            catch (OverflowException)
            {
                return MarketTransactionResult.Fail(ErrorInvalidTransaction, TradingCurrency);
            }
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

        internal IReadOnlyList<CargoInventoryView> ReadSavedCargo()
        {
            var result = new List<CargoInventoryView>();
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

                string itemId = entry.item.itemId ?? string.Empty;
                catalogById.TryGetValue(itemId, out TradeItemData item);

                result.Add(new CargoInventoryView
                {
                    ItemId = itemId,
                    Item = item,
                    Quantity = entry.quantity,
                    UnitPrice = Math.Max(0L, entry.item.basePrice),
                    Weight = Math.Max(0f, entry.item.weight)
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

        private int GetCargoQuantity(string itemId)
        {
            return saveData.caravan.cargo
                .Where(entry => entry?.item != null && entry.item.itemId == itemId)
                .Sum(entry => Math.Max(0, entry.quantity));
        }

        private float CalculateCargoWeightAfter(
            IReadOnlyDictionary<string, MarketTransactionLine> normalized)
        {
            double result = 0d;
            foreach (IGrouping<string, CargoEntrySaveData> group in saveData.caravan.cargo
                         .Where(entry => entry?.item != null && entry.quantity > 0)
                         .GroupBy(entry => entry.item.itemId ?? string.Empty, StringComparer.Ordinal))
            {
                int quantity = group.Sum(entry => entry.quantity);
                if (normalized.TryGetValue(group.Key, out MarketTransactionLine line))
                {
                    quantity = checked(quantity + line.BuyQuantity - line.SellQuantity);
                }

                result += Math.Max(0f, group.First().item.weight) * Math.Max(0, quantity);
            }

            foreach (MarketTransactionLine line in normalized.Values)
            {
                if (line.BuyQuantity <= 0 || GetCargoQuantity(line.ItemId) > 0)
                {
                    continue;
                }

                result += Math.Max(0f, catalogById[line.ItemId].Weight) * line.BuyQuantity;
            }

            return result >= float.MaxValue ? float.PositiveInfinity : (float)result;
        }

        private void ApplyCargoDelta(string itemId, int delta)
        {
            List<CargoEntrySaveData> entries = saveData.caravan.cargo
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
                    saveData.caravan.cargo.Remove(existing);
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
                saveData.caravan.cargo.Add(entry);
            }

            foreach (CargoEntrySaveData duplicate in entries.Skip(1))
            {
                saveData.caravan.cargo.Remove(duplicate);
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
            saveData.caravan.cargo = cargoBefore;
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
