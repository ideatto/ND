using System;

namespace ND.Economy
{
    public static class SettlementCalculator
    {
        public static SettlementBreakdown Calculate(SettlementInput input, int minimumRecoveryMoney = 0)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            SettlementBreakdown result = new SettlementBreakdown
            {
                TradeId = input.TradeId,
                DevelopmentCurrencyReward = Math.Max(0, input.DevelopmentCurrencyReward),
                MinimumRecoveryMoney = Math.Max(0, minimumRecoveryMoney)
            };

            int itemPurchaseCost = 0;
            int itemSaleRevenue = 0;

            if (input.SoldItems != null)
            {
                for (int i = 0; i < input.SoldItems.Count; i++)
                {
                    SoldItemInput soldItem = input.SoldItems[i];
                    if (soldItem == null)
                    {
                        continue;
                    }

                    itemPurchaseCost += Math.Max(0, soldItem.TotalBuyPrice);
                    itemSaleRevenue += Math.Max(0, soldItem.TotalSellPrice);
                }
            }

            int foodCost = Math.Max(0, input.FoodCost);
            int mercenaryCost = Math.Max(0, input.MercenaryCost);
            int cartRepairCost = Math.Max(0, input.CartRepairCost);
            int lostItemValue = Math.Max(0, input.LostItemValue);
            int eventProfit = Math.Max(0, input.EventProfit);
            int eventLoss = Math.Max(0, input.EventLoss);
            int loanRepayment = Math.Max(0, input.LoanRepayment);

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
            int amount,
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
            int amount,
            bool isPositive,
            string sourceId)
        {
            result.Entries.Add(new SettlementEntry
            {
                EntryType = entryType,
                DisplayNameKey = displayNameKey,
                Amount = Math.Max(0, amount),
                IsPositive = isPositive,
                SourceId = sourceId
            });
        }
    }
}
