using System;
using System.Collections.Generic;
using System.Linq;
using ND.Economy;
using ND.Framework;

namespace ND.Framework.CargoLoading
{
    /// <summary>
    /// Process-local change token for shared market stock presentation.
    /// Consumers compare revisions as well as listening to Changed, so inactive panels recover
    /// even when they were not subscribed at transaction time.
    /// </summary>
    public static class MarketInventoryChangeTracker
    {
        private static readonly Dictionary<string, int> revisions =
            new Dictionary<string, int>(StringComparer.Ordinal);
        public static event Action<string, int, bool> Changed;

        public static int GetRevision(string marketId)
        {
            string key = marketId ?? string.Empty;
            return revisions.TryGetValue(key, out int revision) ? revision : 0;
        }

        internal static int Publish(string marketId, bool stockChanged = true)
        {
            string key = marketId ?? string.Empty;
            int current = GetRevision(key);
            int next = current == int.MaxValue ? 1 : current + 1;
            revisions[key] = next;
            Changed?.Invoke(key, next, stockChanged);
            return next;
        }
    }

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
        public List<MarketSalePriceGroup> SalePriceGroups = new List<MarketSalePriceGroup>();
    }

    /// <summary>
    /// Optional exact Cargo source for a sell line. Legacy callers may omit it and retain the
    /// existing FIFO-by-item behavior; CargoSellPopup supplies it to preserve purchase groups.
    /// </summary>
    public sealed class MarketSalePriceGroup
    {
        public long PurchaseUnitPrice;
        public int Quantity;
    }

    public sealed class MarketTransactionResult
    {
        public bool Success;
        public string ErrorCode = string.Empty;
        public long TradingCurrencyAfter;
        public long PurchaseCost;
        public long SaleRevenue;
        public List<MarketTransactionItemSummary> Items = new List<MarketTransactionItemSummary>();

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

    public sealed class MarketTransactionItemSummary
    {
        public string ItemId = string.Empty;
        public int BuyQuantity;
        public int SellQuantity;
        public long PurchaseCost;
        public long SaleRevenue;
        public long BaseSellPrice;
        public long FinalUnitSellPrice;
        public List<MarketSaleModifierSnapshot> SaleModifiers = new List<MarketSaleModifierSnapshot>();
    }

    /// <summary>
    /// One commit-time sell modifier copied for receipt presentation.
    /// The transaction owns this snapshot; callers do not receive calculator collections.
    /// </summary>
    public sealed class MarketSaleModifierSnapshot
    {
        public PriceModifierType ModifierType;
        public string SourceId = string.Empty;
        public string DisplayNameKey = string.Empty;
        public PriceModifierOperation Operation;
        public float Value;
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
            int maximumCargoSlots = int.MaxValue,
            Func<MarketTransactionResult, bool> stageBeforeSave = null,
            Action rollbackStagedData = null)
        {
            return session == null
                ? MarketTransactionResult.Fail(MarketInventoryMutationSession.ErrorInvalidFramework, 0L)
                : session.ExecuteTransaction(
                    lines,
                    maximumCargoWeight,
                    maximumCargoSlots,
                    stageBeforeSave,
                    rollbackStagedData);
        }
    }

    /// <summary>
    /// Owns market refresh and atomic buy/sell transactions.
    /// Consumers must use View for reads so query and mutation responsibilities stay explicit.
    /// Preview and sale commit resolve SellPrice through one contextual calculation contract.
    /// The session snapshots destination specialty IDs, and commit captures Season, saved route
    /// distance, and lucky state once for all transaction lines.
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
        private readonly SellPriceModifierPolicy sellPriceModifierPolicy;
        private readonly string[] destinationLocalSpecialtyItemIds;
        private readonly Dictionary<string, TradeItemData> catalogById;
        private readonly HashSet<string> stockItemIds;
        private readonly int slotCount;
        private readonly int minimumGeneratedStock;
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
            int minimumGeneratedStock,
            int maximumGeneratedStock,
            double refreshIntervalSeconds,
            int worldSeed,
            SellPriceModifierPolicy sellPriceModifierPolicy,
            IReadOnlyList<string> destinationLocalSpecialtyItemIds)
        {
            this.saveData = saveData;
            this.targetCaravan = targetCaravan;
            this.tradeMode = tradeMode;
            this.saveService = saveService;
            this.timeProvider = timeProvider;
            this.sellPriceModifierPolicy = sellPriceModifierPolicy;
            this.destinationLocalSpecialtyItemIds = destinationLocalSpecialtyItemIds == null
                ? Array.Empty<string>()
                : destinationLocalSpecialtyItemIds.ToArray();
            MarketId = string.IsNullOrWhiteSpace(marketId) ? "default-market" : marketId;
            this.slotCount = Math.Max(1, slotCount);
            this.minimumGeneratedStock = Math.Max(1, minimumGeneratedStock);
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

        /// <summary>
        /// Resolves preview unit prices from the current Caravan pricing context without mutation.
        /// The same session policy and calculation boundary are used again by transaction commit.
        /// </summary>
        internal PriceCalculationResult ResolvePreviewUnitPrices(TradeItemData item)
        {
            return ResolveUnitPrices(item, CaptureSellPriceContext(), sellPriceModifierPolicy);
        }

        /// <summary>
        /// Captures the authoritative Season, saved route-distance snapshot, lucky state, and the
        /// session-owned destination specialty-ID snapshot.
        /// Missing world or trade state falls back to values that preserve legacy sell pricing.
        /// </summary>
        private SellPriceCalculationContext CaptureSellPriceContext()
        {
            string seasonId = saveData?.world != null
                ? saveData.world.currentSeasonId
                : string.Empty;
            float distanceKm = targetCaravan != null
                ? Math.Max(0f, targetCaravan.currentDistanceKm)
                : 0f;
            bool isLuckyMoneyActive = false;
            if (targetCaravan != null
                && SaveDataLookup.TryGetTradeProgress(saveData, CaravanId, out TradeProgressSaveData progress)
                && progress != null
                && !string.IsNullOrWhiteSpace(progress.activeTradeId))
            {
                isLuckyMoneyActive = WeatherLuckyMoneyStateReader.IsActive(progress.activeTradeId);
            }

            return new SellPriceCalculationContext(
                seasonId,
                distanceKm,
                isLuckyMoneyActive,
                destinationLocalSpecialtyItemIds);
        }

        /// <summary>
        /// Resolves unit prices without seasonal SellPrice filtering.
        /// Used by non-transaction preview paths that must not invent a commit-time season.
        /// </summary>
        internal static PriceCalculationResult ResolveUnitPrices(TradeItemData item)
        {
            return ResolveUnitPrices(item, null, false);
        }

        /// <summary>
        /// Resolves unit prices, optionally filtering Season SellPrice modifiers by canonical Season ID.
        /// </summary>
        /// <param name="currentSeasonId">
        /// Commit-time canonical Season ID. Ignored when <paramref name="selectSeasonalSellPrice"/> is false.
        /// Matching uses ordinal equality against modifier <c>SourceId</c>.
        /// </param>
        /// <param name="selectSeasonalSellPrice">
        /// When true, only eligible Season SellPrice modifiers for <paramref name="currentSeasonId"/> remain;
        /// non-season modifiers pass through unchanged. Arithmetic stays in <see cref="PriceCalculator"/>.
        /// </param>
        internal static PriceCalculationResult ResolveUnitPrices(
            TradeItemData item,
            string currentSeasonId,
            bool selectSeasonalSellPrice)
        {
            if (item == null)
                return new PriceCalculationResult();

            List<PriceModifierInput> modifiers = item.AffectModify
                ? LjhEconomyM1InputAdapter.ToPriceModifierInputs(item.Modifiers)
                : new List<PriceModifierInput>();
            if (selectSeasonalSellPrice)
            {
                modifiers = SeasonalSellPriceModifierSelector.SelectForSellPrice(
                    modifiers,
                    currentSeasonId);
            }
            return PriceCalculator.CalculateUnitPrices(
                item.BaseBuyPrice,
                item.BaseSellPrice,
                modifiers);
        }

        /// <summary>
        /// Resolves SellPrice through the shared contextual calculator. A null policy retains
        /// existing item modifiers and seasonal selection without policy-authored effects.
        /// </summary>
        internal static PriceCalculationResult ResolveUnitPrices(
            TradeItemData item,
            SellPriceCalculationContext context,
            SellPriceModifierPolicy policy)
        {
            return ContextualSellPriceCalculator.CalculateUnitPrices(item, context, policy);
        }

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
                    string itemId = stock?.itemId ?? string.Empty;
                    if (stock == null
                        || !stockItemIds.Contains(itemId)
                        || !catalogById.TryGetValue(itemId, out TradeItemData item))
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
                saveData, caravanId, MarketTradeMode.BuyAndSell, saveService, timeProvider, marketId,
                stockCatalog, transactionCatalog, slotCount, 1, maximumGeneratedStock,
                refreshIntervalSeconds, worldSeed,
                out session, out error);
        }

        public static bool TryOpen(
            SaveData saveData,
            ISaveService saveService,
            IGameTimeProvider timeProvider,
            string marketId,
            IEnumerable<TradeItemData> catalog,
            int slotCount,
            int minimumGeneratedStock,
            int maximumGeneratedStock,
            double refreshIntervalSeconds,
            int worldSeed,
            out MarketInventoryMutationSession session,
            out string error)
        {
            return TryOpen(
                saveData,
                saveData != null ? saveData.selectedCaravanId : string.Empty,
                MarketTradeMode.BuyAndSell,
                saveService,
                timeProvider,
                marketId,
                catalog,
                catalog,
                slotCount,
                minimumGeneratedStock,
                maximumGeneratedStock,
                refreshIntervalSeconds,
                worldSeed,
                out session,
                out error);
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
            int minimumGeneratedStock,
            int maximumGeneratedStock,
            double refreshIntervalSeconds,
            int worldSeed,
            out MarketInventoryMutationSession session,
            out string error)
        {
            return TryOpen(
                saveData, caravanId, tradeMode, saveService, timeProvider, marketId,
                stockCatalog, transactionCatalog, slotCount, minimumGeneratedStock,
                maximumGeneratedStock, refreshIntervalSeconds, worldSeed, null, null,
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
            int minimumGeneratedStock,
            int maximumGeneratedStock,
            double refreshIntervalSeconds,
            int worldSeed,
            SellPriceModifierPolicy sellPriceModifierPolicy,
            out MarketInventoryMutationSession session,
            out string error)
        {
            return TryOpen(
                saveData, caravanId, tradeMode, saveService, timeProvider, marketId,
                stockCatalog, transactionCatalog, slotCount, minimumGeneratedStock,
                maximumGeneratedStock, refreshIntervalSeconds, worldSeed,
                sellPriceModifierPolicy, null, out session, out error);
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
            int minimumGeneratedStock,
            int maximumGeneratedStock,
            double refreshIntervalSeconds,
            int worldSeed,
            SellPriceModifierPolicy sellPriceModifierPolicy,
            IReadOnlyList<string> destinationLocalSpecialtyItemIds,
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
                minimumGeneratedStock,
                maximumGeneratedStock,
                refreshIntervalSeconds,
                worldSeed,
                sellPriceModifierPolicy,
                destinationLocalSpecialtyItemIds);

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
            return TryOpen(
                saveData, caravanId, tradeMode, saveService, timeProvider, marketId,
                stockCatalog, transactionCatalog, slotCount, 1, maximumGeneratedStock,
                refreshIntervalSeconds, worldSeed, out session, out error);
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
            int maximumCargoSlots,
            Func<MarketTransactionResult, bool> stageBeforeSave = null,
            Action rollbackStagedData = null)
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

                IReadOnlyList<MarketSalePriceGroup> saleGroups =
                    line.SalePriceGroups != null
                        ? line.SalePriceGroups
                        : (IReadOnlyList<MarketSalePriceGroup>)Array.Empty<MarketSalePriceGroup>();
                if (saleGroups.Count > 0)
                {
                    if (line.BuyQuantity > 0
                        || saleGroups.Any(group => group == null || group.Quantity <= 0)
                        || saleGroups.GroupBy(
                                group => Math.Max(0L, group.PurchaseUnitPrice))
                            .Any(group => group.Count() > 1)
                        || saleGroups.Sum(group => group.Quantity) != line.SellQuantity)
                    {
                        return MarketTransactionResult.Fail(ErrorInvalidTransaction, TradingCurrency);
                    }

                    foreach (MarketSalePriceGroup group in saleGroups)
                    {
                        int available = targetCaravan.cargo
                            .Where(entry => entry?.item != null
                                && string.Equals(entry.item.itemId, line.ItemId, StringComparison.Ordinal)
                                && Math.Max(0L, entry.item.purchaseUnitPrice)
                                    == Math.Max(0L, group.PurchaseUnitPrice))
                            .Sum(entry => Math.Max(0, entry.quantity));
                        if (group.Quantity > available)
                            return MarketTransactionResult.Fail(ErrorInsufficientCargo, TradingCurrency);
                    }
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
            // Capture once so every line shares one authoritative pricing snapshot.
            SellPriceCalculationContext transactionContext = CaptureSellPriceContext();
            var sellPriceResults = new Dictionary<string, PriceCalculationResult>(StringComparer.Ordinal);
            foreach (MarketTransactionLine line in normalized.Values)
            {
                TradeItemData item = catalogById[line.ItemId];
                MarketStockSaveData stock = FindStock(line.ItemId);
                PriceCalculationResult sellPriceResult = ResolveUnitPrices(
                    item,
                    transactionContext,
                    sellPriceModifierPolicy);
                sellPriceResults[line.ItemId] = sellPriceResult;
                calculationInput.Items.Add(new ND.Economy.MarketTransactionItemInput
                {
                    ItemId = line.ItemId,
                    CargoQuantityBefore = GetCargoQuantity(line.ItemId),
                    MarketStockBefore = Math.Max(0, stock?.quantity ?? 0),
                    BuyQuantity = line.BuyQuantity,
                    SellQuantity = line.SellQuantity,
                    BuyUnitPrice = Math.Max(0L, stock?.unitPrice ?? 0L),
                    SellUnitPrice = sellPriceResult.UnitSellPrice,
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
                    MarketStockSaveData transactionStock = FindStock(itemResult.ItemId);
                    ApplyCargoDelta(
                        itemResult.ItemId,
                        itemResult.BuyQuantity - itemResult.SellQuantity,
                        itemResult.BuyQuantity > 0
                            ? Math.Max(0L, transactionStock?.unitPrice ?? 0L)
                            : 0L,
                        normalized[itemResult.ItemId].SalePriceGroups);
                    MarketStockSaveData stock = itemResult.SellQuantity > 0
                        ? GetOrCreateStock(itemResult.ItemId)
                        : FindStock(itemResult.ItemId);
                    stock.quantity = itemResult.MarketStockAfter;
                }

                saveData.player.tradingCurrency = calculation.TradingCurrencyAfter;
                successfulResult = new MarketTransactionResult
                {
                    Success = true,
                    TradingCurrencyAfter = calculation.TradingCurrencyAfter,
                    PurchaseCost = calculation.TotalPurchaseCost,
                    SaleRevenue = calculation.TotalSaleRevenue,
                    Items = calculation.Items
                        .Where(item => item != null)
                        .Select(item => CreateItemSummary(item, sellPriceResults, catalogById))
                        .ToList()
                };
                if (stageBeforeSave != null && !stageBeforeSave(successfulResult))
                {
                    rollbackStagedData?.Invoke();
                    RestoreTransactionSnapshot(currencyBefore, cargoBefore, stockBefore);
                    return MarketTransactionResult.Fail(ErrorInvalidTransaction, currencyBefore);
                }

                SaveResult saveResult = saveService.Save(saveData);
                if (saveResult == null || !saveResult.Succeeded)
                {
                    rollbackStagedData?.Invoke();
                    RestoreTransactionSnapshot(currencyBefore, cargoBefore, stockBefore);
                    return MarketTransactionResult.Fail(ErrorSaveFailed, currencyBefore);
                }
            }
            catch
            {
                rollbackStagedData?.Invoke();
                RestoreTransactionSnapshot(currencyBefore, cargoBefore, stockBefore);
                return MarketTransactionResult.Fail(ErrorInvalidTransaction, currencyBefore);
            }

            // Publish only after SaveData persistence succeeds. UI subscribers re-read the saved
            // Caravan snapshot, and failed/rolled-back transactions never emit refresh signals.
            MarketInventoryChangeTracker.Publish(MarketId);
            FrameworkEvents.RaiseCaravanCargoChanged(
                CaravanId,
                CaravanCargoChangeSource.MarketTransaction);
            FrameworkEvents.RaiseTradingCurrencyChanged(calculation.TradingCurrencyAfter);
            return successfulResult;
        }

        private static MarketTransactionItemSummary CreateItemSummary(
            ND.Economy.MarketTransactionItemResult item,
            IReadOnlyDictionary<string, PriceCalculationResult> sellPriceResults,
            IReadOnlyDictionary<string, TradeItemData> catalog)
        {
            string itemId = item.ItemId ?? string.Empty;
            sellPriceResults.TryGetValue(itemId, out PriceCalculationResult priceResult);
            catalog.TryGetValue(itemId, out TradeItemData tradeItem);
            var summary = new MarketTransactionItemSummary
            {
                ItemId = itemId,
                BuyQuantity = item.BuyQuantity,
                SellQuantity = item.SellQuantity,
                PurchaseCost = item.PurchaseCost,
                SaleRevenue = item.SaleRevenue,
                BaseSellPrice = Math.Max(0L, tradeItem?.BaseSellPrice ?? 0L),
                FinalUnitSellPrice = Math.Max(0L, priceResult?.UnitSellPrice ?? 0L)
            };
            if (priceResult?.Modifiers == null)
                return summary;

            foreach (PriceModifierBreakdown modifier in priceResult.Modifiers)
            {
                if (modifier == null
                    || (modifier.Target != PriceModifierTarget.SellPrice
                        && modifier.Target != PriceModifierTarget.Both))
                    continue;
                summary.SaleModifiers.Add(new MarketSaleModifierSnapshot
                {
                    ModifierType = modifier.ModifierType,
                    SourceId = modifier.SourceId ?? string.Empty,
                    DisplayNameKey = modifier.DisplayNameKey ?? string.Empty,
                    Operation = modifier.Operation,
                    Value = modifier.Value
                });
            }
            return summary;
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
                    // SaveData owns the persisted quantity, while the catalog owns the current
                    // item specification. Reading an old weight snapshot here makes capacity
                    // validation disagree with Cargo UI after an SO balance change.
                    Weight = item != null
                        ? Math.Max(0f, item.Weight)
                        : Math.Max(0f, entry.item.weight),
                    MaxStackQuantity = item != null
                        ? Math.Max(1, item.MaxCount)
                        : Math.Max(1, entry.item.maxCount)
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
                MarketInventorySaveData currentSnapshot = CloneInventory(inventory);
                try
                {
                    if (!ReconcileCurrentCatalog(refreshIndex))
                        return true;

                    SaveResult reconciled = saveService.Save(saveData);
                    if (reconciled != null && reconciled.Succeeded)
                        return true;
                }
                catch
                {
                    // Restore below so an unsuccessful catalog reconciliation cannot leak.
                }

                RestoreInventory(inventory, currentSnapshot);
                return false;
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

        private bool ReconcileCurrentCatalog(long refreshIndex)
        {
            inventory.stocks ??= new List<MarketStockSaveData>();
            var visibleIds = new HashSet<string>(
                inventory.stocks
                    .Where(stock => stock != null && stockItemIds.Contains(stock.itemId ?? string.Empty))
                    .Select(stock => stock.itemId),
                StringComparer.Ordinal);
            int expectedVisibleCount = Math.Min(slotCount, stockItemIds.Count);
            if (visibleIds.Count >= expectedVisibleCount)
                return false;

            List<string> missingIds = stockItemIds
                .Where(itemId => !visibleIds.Contains(itemId) && catalogById.ContainsKey(itemId))
                .OrderBy(itemId => itemId, StringComparer.Ordinal)
                .ToList();
            int addCount = Math.Min(
                expectedVisibleCount - visibleIds.Count,
                missingIds.Count);
            for (int index = 0; index < addCount; index++)
            {
                string itemId = missingIds[index];
                TradeItemData item = catalogById[itemId];
                var random = new Random(StableHash(
                    worldSeed,
                    MarketId + "\n" + itemId,
                    refreshIndex));
                inventory.stocks.Add(new MarketStockSaveData
                {
                    itemId = itemId,
                    quantity = MarketTransactionCalculator.GetEffectiveMarketStock(
                        itemId,
                        random.Next(
                            Math.Min(minimumGeneratedStock, maximumGeneratedStock),
                            maximumGeneratedStock + 1)),
                    unitPrice = ResolveUnitPrices(item).UnitBuyPrice
                });
                visibleIds.Add(itemId);
            }

            return addCount > 0;
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
                        random.Next(
                            Math.Min(minimumGeneratedStock, maximumGeneratedStock),
                            maximumGeneratedStock + 1)),
                    unitPrice = ResolveUnitPrices(item).UnitBuyPrice
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

        private void ApplyCargoDelta(
            string itemId,
            int delta,
            long purchaseUnitPrice = 0L,
            IReadOnlyList<MarketSalePriceGroup> salePriceGroups = null)
        {
            List<CargoEntrySaveData> entries = targetCaravan.cargo
                .Where(candidate => candidate?.item != null &&
                    string.Equals(candidate.item.itemId, itemId, StringComparison.Ordinal))
                .ToList();

            if (delta < 0)
            {
                if (salePriceGroups != null && salePriceGroups.Count > 0)
                {
                    foreach (MarketSalePriceGroup group in salePriceGroups)
                    {
                        int remainingInGroup = Math.Max(0, group.Quantity);
                        long normalizedGroupPrice = Math.Max(0L, group.PurchaseUnitPrice);
                        foreach (CargoEntrySaveData entry in entries.Where(entry =>
                            Math.Max(0L, entry.item.purchaseUnitPrice) == normalizedGroupPrice).ToList())
                        {
                            int removed = Math.Min(remainingInGroup, Math.Max(0, entry.quantity));
                            entry.quantity -= removed;
                            remainingInGroup -= removed;
                            if (entry.quantity <= 0)
                                targetCaravan.cargo.Remove(entry);
                            if (remainingInGroup == 0) break;
                        }
                        if (remainingInGroup > 0)
                            throw new InvalidOperationException(ErrorInsufficientCargo);
                    }
                    return;
                }

                int remainingToSell = -delta;
                foreach (CargoEntrySaveData entry in entries)
                {
                    int removed = Math.Min(remainingToSell, Math.Max(0, entry.quantity));
                    entry.quantity -= removed;
                    remainingToSell -= removed;
                    if (entry.quantity <= 0)
                        targetCaravan.cargo.Remove(entry);
                    if (remainingToSell == 0)
                        break;
                }

                if (remainingToSell > 0)
                    throw new InvalidOperationException(ErrorInsufficientCargo);
                return;
            }

            if (delta == 0)
                return;

            long normalizedPrice = Math.Max(0L, purchaseUnitPrice);
            CargoEntrySaveData matchingGroup = entries.FirstOrDefault(entry =>
                Math.Max(0L, entry.item.purchaseUnitPrice) == normalizedPrice);
            if (matchingGroup == null)
            {
                TradeItemData item = catalogById[itemId];
                matchingGroup = new CargoEntrySaveData
                {
                    item = new TradeItemSaveData
                    {
                        itemId = item.ItemId,
                        itemName = item.DisplayName,
                        weight = item.Weight,
                        basePrice = Math.Max(0L, item.BaseBuyPrice),
                        purchaseUnitPrice = normalizedPrice,
                        maxCount = item.MaxCount
                    }
                };
                targetCaravan.cargo.Add(matchingGroup);
            }

            matchingGroup.quantity = checked(matchingGroup.quantity + delta);
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
                            purchaseUnitPrice = entry.item.purchaseUnitPrice,
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
