using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum InvestmentQuestFailureReason
    {
        None = 0,
        InvalidInput,
        InvalidDefinition,
        AlreadyCompleted,
        CaravanUnavailable,
        InsufficientTradingCurrency,
        InventoryCorrupted,
        InsufficientItems,
        ArithmeticOverflow
    }

    [Serializable]
    public sealed class InvestmentItemCost
    {
        public string ItemId = string.Empty;
        public int Quantity;
    }

    [Serializable]
    public sealed class InvestmentQuestDefinition
    {
        public string QuestId = string.Empty;
        public long TradingCurrencyCost;
        public List<InvestmentItemCost> ItemCosts =
            new List<InvestmentItemCost>();
        public List<string> UnlockTownIds = new List<string>();
        public List<string> UnlockRouteIds = new List<string>();
    }

    [Serializable]
    public sealed class InvestmentInventoryEntry
    {
        public string ItemId = string.Empty;
        public int Quantity;
    }

    [Serializable]
    public sealed class InvestmentQuestInput
    {
        public string RequestedQuestId = string.Empty;
        public string CaravanId = string.Empty;
        public bool CanSubmitCaravanAssets;
        public bool IsAlreadyCompleted;
        public long TradingCurrency;
        public InvestmentQuestDefinition Definition;

        /// <summary>
        /// Inventory of the explicitly selected caravan only.
        /// Home inventory and other caravans must not be merged here.
        /// </summary>
        public List<InvestmentInventoryEntry> CaravanInventory =
            new List<InvestmentInventoryEntry>();
    }

    [Serializable]
    public sealed class InvestmentItemDelta
    {
        public string ItemId = string.Empty;
        public int RequiredQuantity;
        public int QuantityBefore;
        public int QuantityAfter;
        public int MissingQuantity;
    }

    [Serializable]
    public sealed class InvestmentQuestResult
    {
        public bool Success;
        public InvestmentQuestFailureReason FailureReason;
        public string QuestId = string.Empty;
        public string CaravanId = string.Empty;
        public long TradingCurrencyBefore;
        public long TradingCurrencyCost;
        public long TradingCurrencyAfter;
        public List<InvestmentItemDelta> Items =
            new List<InvestmentItemDelta>();
        public List<string> UnlockTownIds = new List<string>();
        public List<string> UnlockRouteIds = new List<string>();
    }

    public static class InvestmentQuestPolicyCalculator
    {
        public static InvestmentQuestResult Evaluate(InvestmentQuestInput input)
        {
            var result = new InvestmentQuestResult
            {
                QuestId = input != null
                    ? input.RequestedQuestId ?? string.Empty
                    : string.Empty,
                CaravanId = input != null
                    ? input.CaravanId ?? string.Empty
                    : string.Empty,
                TradingCurrencyBefore = input != null
                    ? input.TradingCurrency
                    : 0,
                TradingCurrencyAfter = input != null
                    ? input.TradingCurrency
                    : 0
            };

            if (input == null ||
                string.IsNullOrWhiteSpace(input.RequestedQuestId) ||
                string.IsNullOrWhiteSpace(input.CaravanId) ||
                input.TradingCurrency < 0 ||
                input.Definition == null)
            {
                return Fail(result, InvestmentQuestFailureReason.InvalidInput);
            }

            InvestmentQuestDefinition definition = input.Definition;
            List<string> townUnlocks;
            List<string> routeUnlocks;
            if (!IsValidDefinition(
                definition,
                input.RequestedQuestId,
                out townUnlocks,
                out routeUnlocks))
            {
                return Fail(result, InvestmentQuestFailureReason.InvalidDefinition);
            }
            result.UnlockTownIds.AddRange(townUnlocks);
            result.UnlockRouteIds.AddRange(routeUnlocks);
            if (input.IsAlreadyCompleted)
                return Fail(result, InvestmentQuestFailureReason.AlreadyCompleted);
            if (!input.CanSubmitCaravanAssets)
                return Fail(result, InvestmentQuestFailureReason.CaravanUnavailable);

            result.TradingCurrencyCost = definition.TradingCurrencyCost;
            if (input.TradingCurrency < definition.TradingCurrencyCost)
            {
                return Fail(
                    result,
                    InvestmentQuestFailureReason.InsufficientTradingCurrency);
            }

            Dictionary<string, int> inventory;
            if (!TryAggregateInventory(input.CaravanInventory, out inventory))
                return Fail(result, InvestmentQuestFailureReason.InventoryCorrupted);

            Dictionary<string, int> requirements;
            if (!TryAggregateRequirements(definition.ItemCosts, out requirements))
                return Fail(result, InvestmentQuestFailureReason.InvalidDefinition);

            foreach (KeyValuePair<string, int> requirement in requirements)
            {
                int before;
                inventory.TryGetValue(requirement.Key, out before);
                int missing = Math.Max(0, requirement.Value - before);
                result.Items.Add(new InvestmentItemDelta
                {
                    ItemId = requirement.Key,
                    RequiredQuantity = requirement.Value,
                    QuantityBefore = before,
                    QuantityAfter = Math.Max(0, before - requirement.Value),
                    MissingQuantity = missing
                });
            }
            result.Items.Sort((left, right) =>
                string.CompareOrdinal(left.ItemId, right.ItemId));

            for (int i = 0; i < result.Items.Count; i++)
            {
                if (result.Items[i].MissingQuantity > 0)
                    return Fail(result, InvestmentQuestFailureReason.InsufficientItems);
            }

            result.Success = true;
            result.FailureReason = InvestmentQuestFailureReason.None;
            result.TradingCurrencyAfter =
                input.TradingCurrency - definition.TradingCurrencyCost;
            return result;
        }

        private static bool IsValidDefinition(
            InvestmentQuestDefinition definition,
            string requestedQuestId,
            out List<string> townCopy,
            out List<string> routeCopy)
        {
            townCopy = new List<string>();
            routeCopy = new List<string>();
            if (string.IsNullOrWhiteSpace(definition.QuestId) ||
                !string.Equals(
                    requestedQuestId,
                    definition.QuestId,
                    StringComparison.Ordinal) ||
                definition.TradingCurrencyCost < 0 ||
                definition.ItemCosts == null ||
                definition.UnlockTownIds == null ||
                definition.UnlockRouteIds == null)
            {
                return false;
            }

            return TryCopyUniqueIds(definition.UnlockTownIds, townCopy) &&
                   TryCopyUniqueIds(definition.UnlockRouteIds, routeCopy) &&
                   (definition.TradingCurrencyCost > 0 ||
                    definition.ItemCosts.Count > 0) &&
                   (townCopy.Count > 0 || routeCopy.Count > 0);
        }

        private static bool TryCopyUniqueIds(
            List<string> source,
            List<string> destination)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < source.Count; i++)
            {
                string id = source[i];
                if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                    return false;
                destination.Add(id);
            }
            return true;
        }

        private static bool TryAggregateInventory(
            List<InvestmentInventoryEntry> source,
            out Dictionary<string, int> totals)
        {
            totals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (source == null)
                return false;

            try
            {
                checked
                {
                    for (int i = 0; i < source.Count; i++)
                    {
                        InvestmentInventoryEntry entry = source[i];
                        if (entry == null ||
                            string.IsNullOrWhiteSpace(entry.ItemId) ||
                            entry.Quantity < 0)
                        {
                            return false;
                        }
                        int current;
                        totals.TryGetValue(entry.ItemId, out current);
                        totals[entry.ItemId] = current + entry.Quantity;
                    }
                }
            }
            catch (OverflowException)
            {
                return false;
            }
            return true;
        }

        private static bool TryAggregateRequirements(
            List<InvestmentItemCost> source,
            out Dictionary<string, int> totals)
        {
            totals = new Dictionary<string, int>(StringComparer.Ordinal);
            try
            {
                checked
                {
                    for (int i = 0; i < source.Count; i++)
                    {
                        InvestmentItemCost cost = source[i];
                        if (cost == null ||
                            string.IsNullOrWhiteSpace(cost.ItemId) ||
                            cost.Quantity <= 0)
                        {
                            return false;
                        }
                        int current;
                        totals.TryGetValue(cost.ItemId, out current);
                        totals[cost.ItemId] = current + cost.Quantity;
                    }
                }
            }
            catch (OverflowException)
            {
                return false;
            }
            return true;
        }

        private static InvestmentQuestResult Fail(
            InvestmentQuestResult result,
            InvestmentQuestFailureReason reason)
        {
            result.Success = false;
            result.FailureReason = reason;
            return result;
        }
    }
}
