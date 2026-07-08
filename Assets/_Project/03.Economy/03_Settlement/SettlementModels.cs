using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum SettlementEntryType
    {
        ItemPurchaseCost,
        ItemSaleRevenue,
        FoodCost,
        MercenaryCost,
        CartRepairCost,
        LostItemValue,
        EventProfit,
        EventLoss,
        LoanRepayment,
        DevelopmentCurrencyReward,
        GrowthPurchaseCost,
        Debug
    }

    [Serializable]
    public sealed class SoldItemInput
    {
        public string TradeItemId = string.Empty;
        public int Quantity;
        public long TotalBuyPrice;
        public long TotalSellPrice;
    }

    [Serializable]
    public sealed class SettlementInput
    {
        public string TradeId = string.Empty;
        public List<SoldItemInput> SoldItems = new List<SoldItemInput>();
        public long TradeMoneyBefore;
        public long FoodCost;
        public long MercenaryCost;
        public long CartRepairCost;
        public long LostItemValue;
        public long EventProfit;
        public long EventLoss;
        public long LoanRepayment;
        public long DevelopmentCurrencyReward;
    }

    [Serializable]
    public sealed class SettlementEntry
    {
        public SettlementEntryType EntryType;
        public string DisplayNameKey = string.Empty;
        public long Amount;
        public bool IsPositive;
        public string SourceId = string.Empty;
    }

    [Serializable]
    public sealed class SettlementBreakdown
    {
        public string TradeId = string.Empty;
        public long TotalRevenue;
        public long TotalExpense;
        public long GrossTradeProfit;
        public long NetProfit;
        public long TradeMoneyAfter;
        public long DevelopmentCurrencyReward;
        public List<SettlementEntry> Entries = new List<SettlementEntry>();
        public bool IsBankrupt;
        public long MinimumRecoveryMoney;
    }
}
