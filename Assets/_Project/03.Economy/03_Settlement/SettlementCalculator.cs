using System;

namespace ND.Economy
{
    public static class SettlementCalculator
    {
        public static SettlementBreakdown Calculate(SettlementInput input, long minimumRecoveryMoney = 0L)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            SettlementBreakdown result = new SettlementBreakdown
            {
                TradeId = input.TradeId,
                DevelopmentCurrencyReward = Math.Max(0L, input.DevelopmentCurrencyReward),
                MinimumRecoveryMoney = Math.Max(0L, minimumRecoveryMoney)
            };

            long itemPurchaseCost = 0L;
            long itemSaleRevenue = 0L;

            if (input.SoldItems != null)
            {
                for (int i = 0; i < input.SoldItems.Count; i++)
                {
                    SoldItemInput soldItem = input.SoldItems[i];
                    if (soldItem == null)
                    {
                        continue;
                    }

                    itemPurchaseCost += Math.Max(0L, soldItem.TotalBuyPrice);
                    itemSaleRevenue += Math.Max(0L, soldItem.TotalSellPrice);
                }
            }

            long foodCost = Math.Max(0L, input.FoodCost);
            long mercenaryCost = Math.Max(0L, input.MercenaryCost);
            long cartRepairCost = Math.Max(0L, input.CartRepairCost);
            long lostItemValue = Math.Max(0L, input.LostItemValue);
            long eventProfit = Math.Max(0L, input.EventProfit);
            long eventLoss = Math.Max(0L, input.EventLoss);
            long loanRepayment = Math.Max(0L, input.LoanRepayment);

            result.TotalRevenue = itemSaleRevenue + eventProfit;
            result.TotalExpense = itemPurchaseCost
                                  + foodCost
                                  + mercenaryCost
                                  + cartRepairCost
                                  + lostItemValue
                                  + eventLoss
                                  + loanRepayment;
            result.GrossTradeProfit = itemSaleRevenue - itemPurchaseCost;
            result.NetProfit = result.TotalRevenue - result.TotalExpense;
            result.TradeMoneyAfter = input.TradeMoneyBefore + result.NetProfit;
            result.IsBankrupt = result.TradeMoneyAfter < result.MinimumRecoveryMoney;

            AddEntry(result, SettlementEntryType.ItemPurchaseCost, "settlement.item_purchase_cost", itemPurchaseCost, false, "system");
            AddEntry(result, SettlementEntryType.ItemSaleRevenue, "settlement.item_sale_revenue", itemSaleRevenue, true, "system");
            AddEntry(result, SettlementEntryType.FoodCost, "settlement.food_cost", foodCost, false, "food");
            AddEntry(result, SettlementEntryType.MercenaryCost, "settlement.mercenary_cost", mercenaryCost, false, "mercenary");
            AddEntry(result, SettlementEntryType.DevelopmentCurrencyReward, "settlement.development_currency_reward", result.DevelopmentCurrencyReward, true, "developmentCurrency");

            AddOptionalEntry(result, SettlementEntryType.CartRepairCost, "settlement.cart_repair_cost", cartRepairCost, false, "cart");
            AddOptionalEntry(result, SettlementEntryType.LostItemValue, "settlement.lost_item_value", lostItemValue, false, "system");
            AddOptionalEntry(result, SettlementEntryType.EventProfit, "settlement.event_profit", eventProfit, true, "event");
            AddOptionalEntry(result, SettlementEntryType.EventLoss, "settlement.event_loss", eventLoss, false, "event");
            AddOptionalEntry(result, SettlementEntryType.LoanRepayment, "settlement.loan_repayment", loanRepayment, false, "loan");

            return result;
        }

        private static void AddOptionalEntry(
            SettlementBreakdown result,
            SettlementEntryType entryType,
            string displayNameKey,
            long amount,
            bool isPositive,
            string sourceId)
        {
            if (amount <= 0)
            {
                return;
            }

            AddEntry(result, entryType, displayNameKey, amount, isPositive, sourceId);
        }

        private static void AddEntry(
            SettlementBreakdown result,
            SettlementEntryType entryType,
            string displayNameKey,
            long amount,
            bool isPositive,
            string sourceId)
        {
            result.Entries.Add(new SettlementEntry
            {
                EntryType = entryType,
                DisplayNameKey = displayNameKey,
                Amount = Math.Max(0L, amount),
                IsPositive = isPositive,
                SourceId = sourceId
            });
        }
    }
}
