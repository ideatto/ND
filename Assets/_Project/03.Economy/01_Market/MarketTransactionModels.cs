using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum MarketTransactionFailureReason
    {
        None,
        InvalidInput,
        DuplicateItem,
        InsufficientCurrency,
        InsufficientCargo,
        InsufficientStock,
        CargoWeightExceeded,
        CargoSlotExceeded,
        Overflow
    }

    [Serializable]
    public sealed class MarketTransactionItemInput
    {
        public string ItemId = string.Empty;
        public int CargoQuantityBefore;
        public int MarketStockBefore;
        public int BuyQuantity;
        public int SellQuantity;
        public long BuyUnitPrice;
        public long SellUnitPrice;
        public float UnitWeight;
        public int MaxStackQuantity = 1;
    }

    [Serializable]
    public sealed class MarketTransactionInput
    {
        public long TradingCurrencyBefore;
        public float CurrentCargoWeight;
        public float MaximumCargoWeight;
        public int CurrentCargoSlots;
        public int MaximumCargoSlots = int.MaxValue;
        public List<MarketTransactionItemInput> Items = new List<MarketTransactionItemInput>();
    }

    [Serializable]
    public sealed class MarketTransactionItemResult
    {
        public string ItemId = string.Empty;
        public int BuyQuantity;
        public int SellQuantity;
        public int CargoQuantityBefore;
        public int CargoQuantityAfter;
        public int MarketStockBefore;
        public int MarketStockAfter;
        public long PurchaseCost;
        public long SaleRevenue;
        public float CargoWeightDelta;
    }

    [Serializable]
    public sealed class MarketTransactionResult
    {
        public bool Success;
        public MarketTransactionFailureReason FailureReason;
        public string FailedItemId = string.Empty;
        public long TradingCurrencyBefore;
        public long TradingCurrencyAfter;
        public long TotalPurchaseCost;
        public long TotalSaleRevenue;
        public long NetTradeMoneyChange;
        public float CargoWeightBefore;
        public float CargoWeightAfter;
        public int CargoSlotsBefore;
        public int CargoSlotsAfter;
        public List<MarketTransactionItemResult> Items = new List<MarketTransactionItemResult>();
    }
}
