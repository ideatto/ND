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
        public string ItemId = string.Empty;
        public int Quantity;
        public int TotalBuyPrice;
        public int TotalSellPrice;
    }

    [Serializable]
    public sealed class SettlementInput
    {
        public string TradeId = string.Empty;
        public List<SoldItemInput> SoldItems = new List<SoldItemInput>();
        public int TradeMoneyBefore;
        public int FoodCost;
        public int MercenaryCost;
        public int CartRepairCost;
        public int LostItemValue;
        public int EventProfit;
        public int EventLoss;
        public int LoanRepayment;
        public int DevelopmentCurrencyReward;
    }

    [Serializable]
    public sealed class SettlementEntry
    {
        public SettlementEntryType EntryType;
        public string DisplayNameKey = string.Empty;
        public int Amount;
        public bool IsPositive;
        public string SourceId = string.Empty;
    }

    [Serializable]
    public sealed class SettlementBreakdown
    {
        public string TradeId = string.Empty;
        public int TotalRevenue;
        public int TotalExpense;
        public int GrossTradeProfit;
        public int NetProfit;
        public int TradeMoneyAfter;
        public int DevelopmentCurrencyReward;
        public List<SettlementEntry> Entries = new List<SettlementEntry>();
        public bool IsBankrupt;
        public int MinimumRecoveryMoney;
    }
}
