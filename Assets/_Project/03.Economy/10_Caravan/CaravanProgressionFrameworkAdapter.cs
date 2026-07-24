using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum CaravanProgressionAdapterFailureReason
    {
        None = 0,
        InvalidInput,
        MissingStableCaravanId,
        DuplicateCaravanId,
        MissingSlotBinding,
        DuplicateSlotBinding,
        DuplicateOccupiedSlot,
        CaravanNotFound,
        AmbiguousTradeProgress
    }

    [Serializable]
    public sealed class CaravanSlotBinding
    {
        public string CaravanId = string.Empty;
        public int SlotIndex = -1;
    }

    public static class CaravanProgressionFrameworkAdapter
    {
        public static bool TryCreateCreationInput(
            global::ND.Framework.SaveData saveData,
            IList<CaravanSlotBinding> slotBindings,
            int requestedSlotIndex,
            CaravanProgressionPolicyDefinition definition,
            int questUnlockedSlotBonus,
            bool duplicateRequestInProgress,
            string initialTownId,
            out CaravanProgressionInput input,
            out CaravanProgressionAdapterFailureReason failureReason)
        {
            input = null;
            failureReason = CaravanProgressionAdapterFailureReason.None;
            if (saveData == null || saveData.player == null || saveData.caravans == null ||
                slotBindings == null || definition == null)
            {
                failureReason = CaravanProgressionAdapterFailureReason.InvalidInput;
                return false;
            }

            var caravanIds = new HashSet<string>(StringComparer.Ordinal);
            var occupiedSlots = new List<int>();
            var occupiedSlotSet = new HashSet<int>();
            for (int index = 0; index < saveData.caravans.Count; index++)
            {
                global::ND.Framework.CaravanSaveData caravan = saveData.caravans[index];
                if (caravan == null || string.IsNullOrWhiteSpace(caravan.caravanId))
                {
                    failureReason = CaravanProgressionAdapterFailureReason.MissingStableCaravanId;
                    return false;
                }
                if (!caravanIds.Add(caravan.caravanId))
                {
                    failureReason = CaravanProgressionAdapterFailureReason.DuplicateCaravanId;
                    return false;
                }

                int slotIndex;
                CaravanProgressionAdapterFailureReason bindingFailure;
                if (!TryGetUniqueSlot(slotBindings, caravan.caravanId, out slotIndex, out bindingFailure))
                {
                    failureReason = bindingFailure;
                    return false;
                }
                if (!occupiedSlotSet.Add(slotIndex))
                {
                    failureReason = CaravanProgressionAdapterFailureReason.DuplicateOccupiedSlot;
                    return false;
                }
                occupiedSlots.Add(slotIndex);
            }

            input = new CaravanProgressionInput
            {
                Definition = definition,
                CaravanGrowthLevel = Math.Max(0, saveData.player.caravanGrowthLevel),
                QuestUnlockedSlotBonus = Math.Max(0, questUnlockedSlotBonus),
                CurrentCaravanCount = saveData.caravans.Count,
                RequestedSlotIndex = requestedSlotIndex,
                OccupiedSlotIndices = occupiedSlots,
                TradingCurrency = Math.Max(0L, saveData.player.tradingCurrency),
                DuplicateRequestInProgress = duplicateRequestInProgress,
                HasInitialTown = !string.IsNullOrWhiteSpace(initialTownId)
            };
            return true;
        }

        public static bool TryEvaluateCreation(
            global::ND.Framework.SaveData saveData,
            IList<CaravanSlotBinding> slotBindings,
            int requestedSlotIndex,
            CaravanProgressionPolicyDefinition definition,
            int questUnlockedSlotBonus,
            bool duplicateRequestInProgress,
            string initialTownId,
            out CaravanProgressionResult result,
            out CaravanProgressionAdapterFailureReason adapterFailure)
        {
            result = null;
            CaravanProgressionInput input;
            if (!TryCreateCreationInput(
                saveData, slotBindings, requestedSlotIndex, definition, questUnlockedSlotBonus,
                duplicateRequestInProgress, initialTownId, out input, out adapterFailure))
            {
                return false;
            }

            result = CaravanProgressionPolicyCalculator.Evaluate(input);
            return true;
        }

        public static bool TryCreateAssetAvailabilityInput(
            global::ND.Framework.SaveData saveData,
            string caravanId,
            string baseTownId,
            out CaravanAssetAvailabilityInput input,
            out CaravanProgressionAdapterFailureReason failureReason)
        {
            input = null;
            failureReason = CaravanProgressionAdapterFailureReason.None;
            if (saveData == null || string.IsNullOrWhiteSpace(caravanId))
            {
                failureReason = CaravanProgressionAdapterFailureReason.InvalidInput;
                return false;
            }

            global::ND.Framework.CaravanSaveData caravan;
            if (!global::ND.Framework.SaveDataLookup.TryGetCaravan(saveData, caravanId, out caravan))
            {
                failureReason = CaravanProgressionAdapterFailureReason.CaravanNotFound;
                return false;
            }

            global::ND.Framework.TradeProgressSaveData progress;
            bool hasProgress = TryGetUniqueProgress(saveData.tradeProgressEntries, caravanId, out progress);
            if (!hasProgress && HasMultipleProgressEntries(saveData.tradeProgressEntries, caravanId))
            {
                failureReason = CaravanProgressionAdapterFailureReason.AmbiguousTradeProgress;
                return false;
            }

            CaravanEconomicState state = ResolveState(caravan, progress);
            bool hasActiveTrade = state == CaravanEconomicState.Traveling ||
                                  state == CaravanEconomicState.SettlementPending;
            input = new CaravanAssetAvailabilityInput
            {
                State = state,
                HasActiveTrade = hasActiveTrade,
                IsAtBaseTown = !string.IsNullOrWhiteSpace(baseTownId) &&
                               string.Equals(caravan.currentTownId, baseTownId, StringComparison.Ordinal)
            };
            return state != CaravanEconomicState.Invalid;
        }

        public static bool TryEvaluateAssetAvailability(
            global::ND.Framework.SaveData saveData,
            string caravanId,
            string baseTownId,
            out CaravanAssetAvailabilityResult result,
            out CaravanProgressionAdapterFailureReason adapterFailure)
        {
            result = null;
            CaravanAssetAvailabilityInput input;
            if (!TryCreateAssetAvailabilityInput(saveData, caravanId, baseTownId, out input, out adapterFailure))
            {
                return false;
            }
            result = CaravanAssetAvailabilityCalculator.Evaluate(input);
            return true;
        }

        private static bool TryGetUniqueSlot(
            IList<CaravanSlotBinding> bindings,
            string caravanId,
            out int slotIndex,
            out CaravanProgressionAdapterFailureReason failureReason)
        {
            slotIndex = -1;
            failureReason = CaravanProgressionAdapterFailureReason.MissingSlotBinding;
            bool found = false;
            for (int index = 0; index < bindings.Count; index++)
            {
                CaravanSlotBinding binding = bindings[index];
                if (binding == null || !string.Equals(binding.CaravanId, caravanId, StringComparison.Ordinal))
                    continue;
                if (found)
                {
                    failureReason = CaravanProgressionAdapterFailureReason.DuplicateSlotBinding;
                    return false;
                }
                found = true;
                slotIndex = binding.SlotIndex;
            }
            return found;
        }

        private static bool TryGetUniqueProgress(
            IList<global::ND.Framework.TradeProgressSaveData> entries,
            string caravanId,
            out global::ND.Framework.TradeProgressSaveData progress)
        {
            progress = null;
            if (entries == null) return false;
            for (int index = 0; index < entries.Count; index++)
            {
                global::ND.Framework.TradeProgressSaveData candidate = entries[index];
                if (candidate == null || !string.Equals(candidate.caravanId, caravanId, StringComparison.Ordinal))
                    continue;
                if (progress != null)
                {
                    progress = null;
                    return false;
                }
                progress = candidate;
            }
            return progress != null;
        }

        private static bool HasMultipleProgressEntries(
            IList<global::ND.Framework.TradeProgressSaveData> entries,
            string caravanId)
        {
            if (entries == null) return false;
            int count = 0;
            for (int index = 0; index < entries.Count; index++)
            {
                if (entries[index] != null && string.Equals(entries[index].caravanId, caravanId, StringComparison.Ordinal) && ++count > 1)
                    return true;
            }
            return false;
        }

        private static CaravanEconomicState ResolveState(
            global::ND.Framework.CaravanSaveData caravan,
            global::ND.Framework.TradeProgressSaveData progress)
        {
            if (progress != null)
            {
                if (progress.state == global::ND.Framework.TradeProgressState.Traveling)
                    return CaravanEconomicState.Traveling;
                if (progress.state == global::ND.Framework.TradeProgressState.SettlementPending)
                    return CaravanEconomicState.SettlementPending;
            }
            if (caravan.state == global::JourneyState.Traveling)
                return CaravanEconomicState.Traveling;
            if (caravan.state == global::JourneyState.Settling)
                return CaravanEconomicState.SettlementPending;
            if (caravan.state == global::JourneyState.Prepare || caravan.state == global::JourneyState.Completed)
                return CaravanEconomicState.Prepare;
            return CaravanEconomicState.Invalid;
        }
    }
}
