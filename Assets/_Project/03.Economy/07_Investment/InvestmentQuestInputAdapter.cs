using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum InvestmentQuestInputAdapterFailureReason
    {
        None = 0,
        InvalidRequest,
        InvalidState,
        QuestIdMismatch,
        CaravanIdMismatch,
        InvalidContentDefinition,
        InventoryCorrupted,
        CompletedQuestStateCorrupted
    }

    [Serializable]
    public sealed class InvestmentQuestStateSnapshot
    {
        public string CaravanId = string.Empty;
        public bool CanSubmitCaravanAssets;
        public long TradingCurrency;
        public List<string> CompletedQuestIds = new List<string>();

        /// <summary>
        /// Cargo from the resolved CaravanId only.
        /// </summary>
        public List<InvestmentInventoryEntry> CaravanInventory =
            new List<InvestmentInventoryEntry>();
    }

    public sealed class InvestmentQuestInputAdapterResult
    {
        public InvestmentQuestInputAdapterResult()
        {
            QuestId = string.Empty;
            CaravanId = string.Empty;
        }

        public bool Success { get; internal set; }
        public InvestmentQuestInputAdapterFailureReason FailureReason { get; internal set; }
        public string QuestId { get; internal set; }
        public string CaravanId { get; internal set; }
        public InvestmentQuestInput Input { get; internal set; }
    }

    public static class InvestmentQuestInputAdapter
    {
        public static InvestmentQuestInputAdapterResult Build(
            string requestedQuestId,
            string requestedCaravanId,
            InvestmentQuestStateSnapshot state,
            InvestmentQuestDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(requestedQuestId) ||
                string.IsNullOrWhiteSpace(requestedCaravanId))
            {
                return Fail(
                    InvestmentQuestInputAdapterFailureReason.InvalidRequest);
            }
            if (state == null ||
                string.IsNullOrWhiteSpace(state.CaravanId) ||
                state.TradingCurrency < 0)
            {
                return Fail(InvestmentQuestInputAdapterFailureReason.InvalidState);
            }
            if (!string.Equals(
                requestedCaravanId,
                state.CaravanId,
                StringComparison.Ordinal))
            {
                return Fail(
                    InvestmentQuestInputAdapterFailureReason.CaravanIdMismatch);
            }
            if (definition == null ||
                string.IsNullOrWhiteSpace(definition.QuestId))
            {
                return Fail(
                    InvestmentQuestInputAdapterFailureReason.InvalidContentDefinition);
            }
            if (!string.Equals(
                requestedQuestId,
                definition.QuestId,
                StringComparison.Ordinal))
            {
                return Fail(
                    InvestmentQuestInputAdapterFailureReason.QuestIdMismatch);
            }

            bool alreadyCompleted;
            if (!TryReadCompletion(
                state.CompletedQuestIds,
                requestedQuestId,
                out alreadyCompleted))
            {
                return Fail(
                    InvestmentQuestInputAdapterFailureReason.CompletedQuestStateCorrupted);
            }

            List<InvestmentInventoryEntry> inventory;
            if (!TryCopyInventory(state.CaravanInventory, out inventory))
            {
                return Fail(
                    InvestmentQuestInputAdapterFailureReason.InventoryCorrupted);
            }

            return new InvestmentQuestInputAdapterResult
            {
                Success = true,
                FailureReason = InvestmentQuestInputAdapterFailureReason.None,
                QuestId = requestedQuestId,
                CaravanId = requestedCaravanId,
                Input = new InvestmentQuestInput
                {
                    RequestedQuestId = requestedQuestId,
                    CaravanId = requestedCaravanId,
                    CanSubmitCaravanAssets = state.CanSubmitCaravanAssets,
                    IsAlreadyCompleted = alreadyCompleted,
                    TradingCurrency = state.TradingCurrency,
                    Definition = definition,
                    CaravanInventory = inventory
                }
            };
        }

        private static bool TryReadCompletion(
            List<string> completedQuestIds,
            string requestedQuestId,
            out bool completed)
        {
            completed = false;
            if (completedQuestIds == null)
                return false;

            var unique = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < completedQuestIds.Count; i++)
            {
                string id = completedQuestIds[i];
                if (string.IsNullOrWhiteSpace(id) || !unique.Add(id))
                    return false;
                if (string.Equals(id, requestedQuestId, StringComparison.Ordinal))
                    completed = true;
            }
            return true;
        }

        private static bool TryCopyInventory(
            List<InvestmentInventoryEntry> source,
            out List<InvestmentInventoryEntry> copy)
        {
            copy = new List<InvestmentInventoryEntry>();
            if (source == null)
                return false;

            for (int i = 0; i < source.Count; i++)
            {
                InvestmentInventoryEntry entry = source[i];
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.ItemId) ||
                    entry.Quantity < 0)
                {
                    return false;
                }
                copy.Add(new InvestmentInventoryEntry
                {
                    ItemId = entry.ItemId,
                    Quantity = entry.Quantity
                });
            }
            return true;
        }

        private static InvestmentQuestInputAdapterResult Fail(
            InvestmentQuestInputAdapterFailureReason reason)
        {
            return new InvestmentQuestInputAdapterResult
            {
                FailureReason = reason
            };
        }
    }
}
