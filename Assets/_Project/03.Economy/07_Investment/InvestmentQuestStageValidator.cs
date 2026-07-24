using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum InvestmentQuestStageFailureReason
    {
        None = 0,
        InvalidInput,
        CaravanIdMismatch,
        AlreadyCompleted,
        TradingCurrencyChanged,
        InventoryCorrupted,
        InventoryChanged,
        UnlockStateCorrupted,
        UnlockAlreadyApplied
    }

    [Serializable]
    public sealed class InvestmentQuestPersistenceSnapshot
    {
        public string CaravanId = string.Empty;
        public long TradingCurrency;
        public List<string> CompletedQuestIds = new List<string>();
        public List<string> UnlockedTownIds = new List<string>();
        public List<string> UnlockedRouteIds = new List<string>();
        public List<InvestmentInventoryEntry> CaravanInventory =
            new List<InvestmentInventoryEntry>();
    }

    public sealed class InvestmentQuestStageValidationResult
    {
        public bool Success { get; internal set; }
        public InvestmentQuestStageFailureReason FailureReason { get; internal set; }
    }

    /// <summary>
    /// Revalidates durable state immediately before a Framework port stages a plan.
    /// This closes the gap between preview/plan creation and persistence.
    /// </summary>
    public static class InvestmentQuestStageValidator
    {
        public static InvestmentQuestStageValidationResult Validate(
            InvestmentQuestEconomicPlan plan,
            InvestmentQuestPersistenceSnapshot snapshot)
        {
            if (plan == null ||
                snapshot == null ||
                string.IsNullOrWhiteSpace(plan.QuestId) ||
                string.IsNullOrWhiteSpace(plan.CaravanId) ||
                string.IsNullOrWhiteSpace(snapshot.CaravanId) ||
                snapshot.TradingCurrency < 0)
            {
                return Fail(InvestmentQuestStageFailureReason.InvalidInput);
            }
            if (!string.Equals(
                plan.CaravanId,
                snapshot.CaravanId,
                StringComparison.Ordinal))
            {
                return Fail(InvestmentQuestStageFailureReason.CaravanIdMismatch);
            }

            HashSet<string> completed;
            if (!TryCreateUniqueSet(snapshot.CompletedQuestIds, out completed))
            {
                return Fail(InvestmentQuestStageFailureReason.InvalidInput);
            }
            if (completed.Contains(plan.QuestId))
                return Fail(InvestmentQuestStageFailureReason.AlreadyCompleted);

            if (snapshot.TradingCurrency != plan.TradingCurrencyBefore)
            {
                return Fail(
                    InvestmentQuestStageFailureReason.TradingCurrencyChanged);
            }

            Dictionary<string, int> inventory;
            if (!TryAggregateInventory(snapshot.CaravanInventory, out inventory))
                return Fail(InvestmentQuestStageFailureReason.InventoryCorrupted);

            var plannedItems = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.Items.Count; i++)
            {
                InvestmentItemPlan item = plan.Items[i];
                if (item == null ||
                    string.IsNullOrWhiteSpace(item.ItemId) ||
                    item.RequiredQuantity <= 0 ||
                    item.QuantityBefore < item.RequiredQuantity ||
                    item.QuantityAfter !=
                        item.QuantityBefore - item.RequiredQuantity ||
                    !plannedItems.Add(item.ItemId))
                {
                    return Fail(InvestmentQuestStageFailureReason.InvalidInput);
                }

                int persistedQuantity;
                inventory.TryGetValue(item.ItemId, out persistedQuantity);
                if (persistedQuantity != item.QuantityBefore)
                    return Fail(InvestmentQuestStageFailureReason.InventoryChanged);
            }

            HashSet<string> towns;
            HashSet<string> routes;
            if (!TryCreateUniqueSet(snapshot.UnlockedTownIds, out towns) ||
                !TryCreateUniqueSet(snapshot.UnlockedRouteIds, out routes))
            {
                return Fail(
                    InvestmentQuestStageFailureReason.UnlockStateCorrupted);
            }
            if (ContainsAny(towns, plan.UnlockTownIds) ||
                ContainsAny(routes, plan.UnlockRouteIds))
            {
                return Fail(
                    InvestmentQuestStageFailureReason.UnlockAlreadyApplied);
            }

            return new InvestmentQuestStageValidationResult
            {
                Success = true,
                FailureReason = InvestmentQuestStageFailureReason.None
            };
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

        private static bool TryCreateUniqueSet(
            List<string> source,
            out HashSet<string> set)
        {
            set = new HashSet<string>(StringComparer.Ordinal);
            if (source == null)
                return false;
            for (int i = 0; i < source.Count; i++)
            {
                string id = source[i];
                if (string.IsNullOrWhiteSpace(id) || !set.Add(id))
                    return false;
            }
            return true;
        }

        private static bool ContainsAny(
            HashSet<string> persisted,
            IReadOnlyList<string> planned)
        {
            var plannedUnique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < planned.Count; i++)
            {
                string id = planned[i];
                if (string.IsNullOrWhiteSpace(id) ||
                    !plannedUnique.Add(id) ||
                    persisted.Contains(id))
                {
                    return true;
                }
            }
            return false;
        }

        private static InvestmentQuestStageValidationResult Fail(
            InvestmentQuestStageFailureReason reason)
        {
            return new InvestmentQuestStageValidationResult
            {
                FailureReason = reason
            };
        }
    }
}
