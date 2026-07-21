using System;
using System.Collections.Generic;
using System.Linq;
using ND.Framework;
using ND.Framework.CargoLoading;
using UnityEngine;

namespace ND.UI.Market
{
    [Serializable]
    public sealed class MarketTradeItemState
    {
        public string ItemId = string.Empty;
        public TradeItemData Item;
        public int MarketStock;
        public int CargoQuantity;
        public long BuyUnitPrice;
        public long SellUnitPrice;
        public int BuyDraftQuantity;
        public int SellDraftQuantity;

        public int ProjectedMarketStock => MarketStock - BuyDraftQuantity + SellDraftQuantity;
        public int ProjectedCargoQuantity => CargoQuantity + BuyDraftQuantity - SellDraftQuantity;
    }

    /// <summary>
    /// Owns only the presentation draft for one market. SaveData is mutated exclusively by
    /// MarketTransactionCommand when Commit succeeds.
    /// </summary>
    public sealed class MarketTradePanelModel
    {
        private readonly MarketInventoryMutationSession commands;
        private readonly MarketInventorySession query;
        private readonly float maximumCargoWeight;
        private readonly int maximumCargoSlots;
        private readonly List<MarketTradeItemState> items = new List<MarketTradeItemState>();

        public MarketTradePanelModel(
            MarketInventoryMutationSession commands,
            float maximumCargoWeight,
            int maximumCargoSlots = int.MaxValue)
        {
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            query = commands.View;
            this.maximumCargoWeight = Mathf.Max(0f, maximumCargoWeight);
            this.maximumCargoSlots = Math.Max(0, maximumCargoSlots);
            Refresh();
        }

        public string MarketId => query.MarketId;
        public long TradingCurrency => query.TradingCurrency;
        public IReadOnlyList<MarketTradeItemState> Items => items;
        public bool HasDraft => items.Any(item => item.BuyDraftQuantity > 0 || item.SellDraftQuantity > 0);
        public long DraftPurchaseCost => SumMoney(item => item.BuyUnitPrice, item => item.BuyDraftQuantity);
        public long DraftSaleRevenue => SumMoney(item => item.SellUnitPrice, item => item.SellDraftQuantity);
        public long ProjectedTradingCurrency => ClampMoney(
            (decimal)TradingCurrency - DraftPurchaseCost + DraftSaleRevenue);
        public float CurrentCargoWeight => SumSavedCargoWeight();
        public float ProjectedCargoWeight => AddWeightDelta(CurrentCargoWeight);
        public float MaximumCargoWeight => maximumCargoWeight;
        public int CurrentCargoSlots => SumSavedCargoSlots();
        public int ProjectedCargoSlots => AddSlotDelta(CurrentCargoSlots);
        public int MaximumCargoSlots => maximumCargoSlots;
        private bool DoesNotIncreaseOverweight =>
            ProjectedCargoWeight <= maximumCargoWeight + 0.0001f ||
            ProjectedCargoWeight <= CurrentCargoWeight + 0.0001f;
        private bool DoesNotIncreaseSlotOverflow =>
            ProjectedCargoSlots <= maximumCargoSlots ||
            ProjectedCargoSlots <= CurrentCargoSlots;
        public bool CanCommit => HasDraft
            && ProjectedTradingCurrency >= 0L
            && DoesNotIncreaseOverweight
            && DoesNotIncreaseSlotOverflow;
        public string DraftValidationError => !HasDraft
            ? MarketInventoryMutationSession.ErrorInvalidTransaction
            : ProjectedTradingCurrency < 0L
                ? MarketInventoryMutationSession.ErrorCurrency
                : !DoesNotIncreaseOverweight
                    ? MarketInventoryMutationSession.ErrorCargoWeight
                    : !DoesNotIncreaseSlotOverflow
                        ? MarketInventoryMutationSession.ErrorCargoSlots
                        : string.Empty;

        public bool SetBuyDraft(string itemId, int quantity)
        {
            MarketTradeItemState item = Find(itemId);
            if (item == null || quantity < 0 || quantity > item.MarketStock)
                return false;

            item.BuyDraftQuantity = quantity;
            if (quantity > 0)
                item.SellDraftQuantity = 0;
            return true;
        }

        public bool SetSellDraft(string itemId, int quantity)
        {
            MarketTradeItemState item = Find(itemId);
            if (item == null || quantity < 0 || quantity > item.CargoQuantity)
                return false;

            item.SellDraftQuantity = quantity;
            if (quantity > 0)
                item.BuyDraftQuantity = 0;
            return true;
        }

        public void CancelDraft()
        {
            foreach (MarketTradeItemState item in items)
            {
                item.BuyDraftQuantity = 0;
                item.SellDraftQuantity = 0;
            }
        }

        public MarketTransactionResult Commit()
        {
            List<MarketTransactionLine> lines = items
                .Where(item => item.BuyDraftQuantity > 0 || item.SellDraftQuantity > 0)
                .Select(item => new MarketTransactionLine
                {
                    ItemId = item.ItemId,
                    BuyQuantity = item.BuyDraftQuantity,
                    SellQuantity = item.SellDraftQuantity
                })
                .ToList();

            MarketTransactionResult result = MarketTransactionCommand.Execute(
                commands,
                lines,
                maximumCargoWeight,
                maximumCargoSlots);
            if (result.Success)
                Refresh();
            return result;
        }

        public void Refresh()
        {
            Dictionary<string, MarketStockView> stocks = query.Stocks
                .Where(stock => stock?.Item != null && !string.IsNullOrWhiteSpace(stock.Item.ItemId))
                .GroupBy(stock => stock.Item.ItemId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            Dictionary<string, CargoInventoryView> cargo = query.SavedCargo
                .Where(entry => entry?.Item != null && !string.IsNullOrWhiteSpace(entry.ItemId))
                .GroupBy(entry => entry.ItemId, StringComparer.Ordinal)
                .ToDictionary(
                    group => group.Key,
                    group => new CargoInventoryView
                    {
                        ItemId = group.Key,
                        Item = group.First().Item,
                        Quantity = group.Sum(entry => entry.Quantity),
                        UnitPrice = group.First().UnitPrice,
                        Weight = group.First().Weight,
                        MaxStackQuantity = Math.Max(1, group.First().MaxStackQuantity)
                    },
                    StringComparer.Ordinal);

            items.Clear();
            foreach (string itemId in stocks.Keys.Union(cargo.Keys, StringComparer.Ordinal).OrderBy(id => id))
            {
                stocks.TryGetValue(itemId, out MarketStockView stock);
                cargo.TryGetValue(itemId, out CargoInventoryView held);
                TradeItemData definition = stock?.Item ?? held?.Item;
                items.Add(new MarketTradeItemState
                {
                    ItemId = itemId,
                    Item = definition,
                    MarketStock = stock?.Quantity ?? 0,
                    CargoQuantity = held?.Quantity ?? 0,
                    BuyUnitPrice = stock?.UnitPrice ?? 0L,
                    SellUnitPrice = definition?.BaseSellPrice ?? 0L
                });
            }
        }

        private MarketTradeItemState Find(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId)
                ? null
                : items.FirstOrDefault(item => string.Equals(item.ItemId, itemId, StringComparison.Ordinal));
        }

        private long SumMoney(
            Func<MarketTradeItemState, long> getUnitPrice,
            Func<MarketTradeItemState, int> getQuantity)
        {
            decimal total = 0m;
            foreach (MarketTradeItemState item in items)
                total += (decimal)Math.Max(0L, getUnitPrice(item)) * Math.Max(0, getQuantity(item));
            return ClampMoney(total);
        }

        private float SumSavedCargoWeight()
        {
            double total = 0d;
            foreach (CargoInventoryView cargo in query.SavedCargo)
            {
                if (cargo != null)
                    total += Math.Max(0f, cargo.Weight) * Math.Max(0, cargo.Quantity);
            }
            return total >= float.MaxValue ? float.PositiveInfinity : (float)total;
        }

        private float AddWeightDelta(float currentWeight)
        {
            double total = currentWeight;
            foreach (MarketTradeItemState item in items)
            {
                float unitWeight = item.Item != null ? Math.Max(0f, item.Item.Weight) : 0f;
                total += unitWeight * (item.BuyDraftQuantity - item.SellDraftQuantity);
            }
            if (total <= 0d) return 0f;
            return total >= float.MaxValue ? float.PositiveInfinity : (float)total;
        }

        private int SumSavedCargoSlots()
        {
            int total = 0;
            foreach (IGrouping<string, CargoInventoryView> group in query.SavedCargo
                .Where(cargo => cargo != null && cargo.Quantity > 0)
                .GroupBy(cargo => cargo.ItemId ?? string.Empty, StringComparer.Ordinal))
            {
                int quantity = group.Sum(cargo => Math.Max(0, cargo.Quantity));
                int maxStack = Math.Max(1, group.First().MaxStackQuantity);
                total = checked(total + (quantity - 1) / maxStack + 1);
            }
            return total;
        }

        private int AddSlotDelta(int currentSlots)
        {
            int total = currentSlots;
            foreach (MarketTradeItemState item in items)
            {
                int maxStack = Math.Max(1, item.Item?.MaxCount ?? 1);
                total = checked(total
                    - RequiredSlots(item.CargoQuantity, maxStack)
                    + RequiredSlots(item.ProjectedCargoQuantity, maxStack));
            }
            return Math.Max(0, total);
        }

        private static int RequiredSlots(int quantity, int maxStack)
        {
            return quantity <= 0 ? 0 : checked((quantity - 1) / maxStack + 1);
        }

        private static long ClampMoney(decimal value)
        {
            if (value >= long.MaxValue) return long.MaxValue;
            if (value <= long.MinValue) return long.MinValue;
            return (long)value;
        }
    }

    /// <summary>
    /// Scene-facing entry point for a MarketData-backed trade panel. Visual controls subscribe
    /// to StateChanged and call the draft methods; this component does not depend on Cargo UI.
    /// </summary>
    public sealed class MarketTradePanelController : MonoBehaviour
    {
        public const string ErrorNotInTown = "MARKET_NOT_IN_TOWN";
        public const string ErrorCurrentTownMissing = "MARKET_CURRENT_TOWN_MISSING";
        public const string ErrorTownMarketMismatch = "MARKET_TOWN_MISMATCH";
        public const string ErrorMarketDataMissing = "MARKET_DATA_MISSING";

        [SerializeField] private MarketData marketData;
        [SerializeField] private MarketData[] marketCatalog = Array.Empty<MarketData>();
        [SerializeField] private bool overrideMaximumCargoWeight;
        [SerializeField, Min(0f)] private float maximumCargoWeight = 100f;
        [SerializeField] private int worldSeed = 20260721;

        private MarketTradePanelModel model;

        public event Action<IReadOnlyList<MarketTradeItemState>> StateChanged;
        public event Action<MarketTransactionResult> TransactionCompleted;
        public event Action<string> ErrorChanged;

        public MarketTradePanelModel Model => model;
        public bool IsOpen => model != null;
        public string LastErrorCode { get; private set; } = string.Empty;

        private void OnEnable()
        {
            FrameworkEvents.InGameScreenChanged += HandleScreenChanged;
        }

        private void OnDisable()
        {
            FrameworkEvents.InGameScreenChanged -= HandleScreenChanged;
        }

        public void Configure(MarketData data, float cargoWeightLimit)
        {
            marketData = data;
            overrideMaximumCargoWeight = true;
            maximumCargoWeight = Mathf.Max(0f, cargoWeightLimit);
        }

        public void ConfigureCatalog(IEnumerable<MarketData> markets)
        {
            marketCatalog = markets?.Where(value => value != null).ToArray()
                ?? Array.Empty<MarketData>();
        }

        public bool Open()
        {
            FrameworkRoot root = FrameworkRoot.Instance;
            if (root == null || root.CurrentSaveData == null || root.SaveService == null || root.GameTime == null)
                return FailOpen(MarketInventoryMutationSession.ErrorInvalidFramework);
            bool hasMarketCatalog = marketCatalog != null && marketCatalog.Any(value => value != null);
            if (hasMarketCatalog && !TryResolveCurrentTownMarket(
                    root.CurrentSaveData,
                    root.SharedGameData,
                    marketCatalog,
                    out marketData,
                    out string resolveError))
            {
                return FailOpen(resolveError);
            }
            if (marketData == null || string.IsNullOrWhiteSpace(marketData.MarketId))
                return FailOpen(MarketInventoryMutationSession.ErrorInvalidCatalog);

            string accessError = ValidateTownMarketAccess(
                root.CurrentSaveData,
                root.SharedGameData,
                marketData.MarketId);
            if (!string.IsNullOrEmpty(accessError))
                return FailOpen(accessError);

            if (model != null && string.Equals(model.MarketId, marketData.MarketId, StringComparison.Ordinal))
            {
                // Repeated button input must not recreate the session and discard its draft.
                SetError(string.Empty);
                RaiseStateChanged();
                return true;
            }

            // A different current town requires a new market session. Any uncommitted draft
            // belongs to the previous town and is discarded before replacing that session.
            model?.CancelDraft();
            model = null;

            TradeItemData[] catalog = marketData.TradeItems
                .Concat(marketData.LocalSpecialtyItems)
                .Where(item => item != null)
                .GroupBy(item => item.ItemId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
            TradeItemData[] transactionCatalog = hasMarketCatalog
                ? marketCatalog
                    .Where(market => market != null)
                    .SelectMany(market => market.TradeItems.Concat(market.LocalSpecialtyItems))
                    .Concat(catalog)
                    .Where(item => item != null)
                    .GroupBy(item => item.ItemId, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .ToArray()
                : catalog;

            bool opened = MarketInventoryMutationSession.TryOpen(
                root.CurrentSaveData,
                root.SaveService,
                root.GameTime,
                marketData.MarketId,
                catalog,
                transactionCatalog,
                Mathf.Max(1, catalog.Length),
                Mathf.Max(1, marketData.ItemMaxQuantity),
                Mathf.Max(1f, marketData.ItemRenewalCycle),
                CombineSeed(worldSeed, marketData.MarketId),
                out MarketInventoryMutationSession commands,
                out string error);
            if (!opened)
                return FailOpen(error);

            float cargoWeightLimit = overrideMaximumCargoWeight
                ? maximumCargoWeight
                : ResolveMaximumCargoWeight(root.CurrentSaveData);
            int cargoSlotLimit = ResolveMaximumCargoSlots(root.CurrentSaveData);
            model = new MarketTradePanelModel(commands, cargoWeightLimit, cargoSlotLimit);
            SetError(string.Empty);
            RaiseStateChanged();
            return true;
        }

        public bool SetBuyDraft(string itemId, int quantity)
        {
            bool changed = model != null && model.SetBuyDraft(itemId, quantity);
            if (changed)
            {
                SetError(string.Empty);
                RaiseStateChanged();
            }
            return changed;
        }

        public bool SetSellDraft(string itemId, int quantity)
        {
            bool changed = model != null && model.SetSellDraft(itemId, quantity);
            if (changed)
            {
                SetError(string.Empty);
                RaiseStateChanged();
            }
            return changed;
        }

        public void CancelDraft()
        {
            model?.CancelDraft();
            SetError(string.Empty);
            RaiseStateChanged();
        }

        public void Close()
        {
            model?.CancelDraft();
            model = null;
            SetError(string.Empty);
            RaiseStateChanged();
        }

        private void HandleScreenChanged(InGameScreenState state)
        {
            if (state != InGameScreenState.Town && state != InGameScreenState.Market && model != null)
                Close();
        }

        public MarketTransactionResult Commit()
        {
            MarketTransactionResult result;
            FrameworkRoot root = FrameworkRoot.Instance;
            string accessError = model == null
                ? MarketInventoryMutationSession.ErrorInvalidFramework
                : ValidateTownMarketAccess(root?.CurrentSaveData, root?.SharedGameData, model.MarketId);
            if (!string.IsNullOrEmpty(accessError))
            {
                result = MarketTransactionResult.Fail(accessError, model?.TradingCurrency ?? 0L);
            }
            else
            {
                result = model.Commit();
            }
            SetError(result.Success ? string.Empty : result.ErrorCode);
            RaiseStateChanged();
            TransactionCompleted?.Invoke(result);
            return result;
        }

        public static string ValidateTownMarketAccess(
            ND.Framework.SaveData saveData,
            ISharedGameDataProvider sharedGameData,
            string marketId)
        {
            if (saveData == null || sharedGameData == null || !sharedGameData.IsLoaded)
                return MarketInventoryMutationSession.ErrorInvalidFramework;
            if (InGameScreenStateRouter.MapFromSaveData(saveData) != InGameScreenState.Town)
                return ErrorNotInTown;

            string currentTownId = saveData.player?.currentTownId;
            if (string.IsNullOrWhiteSpace(currentTownId) ||
                !sharedGameData.TryGetTown(currentTownId, out SharedTownDefinition town))
            {
                return ErrorCurrentTownMissing;
            }

            return string.IsNullOrWhiteSpace(marketId) ||
                !string.Equals(town.MarketId, marketId, StringComparison.Ordinal)
                ? ErrorTownMarketMismatch
                : string.Empty;
        }

        public static bool TryResolveCurrentTownMarket(
            ND.Framework.SaveData saveData,
            ISharedGameDataProvider sharedGameData,
            IEnumerable<MarketData> catalog,
            out MarketData resolved,
            out string error)
        {
            resolved = null;
            error = string.Empty;
            if (saveData == null || sharedGameData == null || !sharedGameData.IsLoaded)
            {
                error = MarketInventoryMutationSession.ErrorInvalidFramework;
                return false;
            }

            string currentTownId = saveData.player?.currentTownId;
            if (string.IsNullOrWhiteSpace(currentTownId) ||
                !sharedGameData.TryGetTown(currentTownId, out SharedTownDefinition town))
            {
                error = ErrorCurrentTownMissing;
                return false;
            }

            resolved = catalog?.FirstOrDefault(candidate =>
                candidate != null &&
                string.Equals(candidate.MarketId, town.MarketId, StringComparison.Ordinal));
            if (resolved != null)
                return true;

            error = ErrorMarketDataMissing;
            return false;
        }

        public static float ResolveMaximumCargoWeight(ND.Framework.SaveData saveData)
        {
            if (saveData?.caravan == null)
                return 0f;

            CaravanData runtimeCaravan = CaravanSaveDataMapper.ToRuntime(saveData.caravan);
            return Mathf.Max(0f, CaravanCalculator.GetMaxLoad(runtimeCaravan));
        }

        public static int ResolveMaximumCargoSlots(ND.Framework.SaveData saveData)
        {
            if (saveData?.caravan == null)
                return 0;

            CaravanData runtimeCaravan = CaravanSaveDataMapper.ToRuntime(saveData.caravan);
            return Math.Max(0, CaravanCalculator.GetMaxSlots(runtimeCaravan));
        }

        private bool FailOpen(string error)
        {
            model = null;
            SetError(error);
            Debug.LogError($"[Market Panel] Open failed: {error}", this);
            return false;
        }

        private void SetError(string error)
        {
            string normalized = error ?? string.Empty;
            if (string.Equals(LastErrorCode, normalized, StringComparison.Ordinal))
                return;

            LastErrorCode = normalized;
            ErrorChanged?.Invoke(LastErrorCode);
        }

        private void RaiseStateChanged()
        {
            StateChanged?.Invoke(model?.Items ?? Array.Empty<MarketTradeItemState>());
        }

        private static int CombineSeed(int seed, string value)
        {
            unchecked
            {
                int hash = seed;
                foreach (char character in value ?? string.Empty)
                    hash = hash * 31 + character;
                return hash;
            }
        }
    }
}
