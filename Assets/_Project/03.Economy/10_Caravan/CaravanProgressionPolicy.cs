using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum CaravanCreationFailureReason
    {
        None = 0,
        InvalidInput,
        SlotOutOfRange,
        SlotLocked,
        SlotOccupied,
        MaximumCaravansReached,
        DuplicateRequest,
        MissingInitialTown,
        InsufficientCurrency,
        ArithmeticOverflow
    }

    [Serializable]
    public sealed class CaravanProgressionPolicyDefinition
    {
        public int MaximumSupportedSlots = 4;
        public int BaseUnlockedSlots = 1;
        public int SlotsPerGrowthLevel = 1;
        public int MaximumGrowthSlotBonus = 2;
        public long CreationCost;
    }

    [Serializable]
    public sealed class CaravanProgressionInput
    {
        public CaravanProgressionPolicyDefinition Definition = new CaravanProgressionPolicyDefinition();
        public int CaravanGrowthLevel;
        public int QuestUnlockedSlotBonus;
        public int CurrentCaravanCount;
        public int RequestedSlotIndex = -1;
        public List<int> OccupiedSlotIndices = new List<int>();
        public long TradingCurrency;
        public bool DuplicateRequestInProgress;
        public bool HasInitialTown = true;
    }

    [Serializable]
    public sealed class CaravanProgressionResult
    {
        public bool IsValid;
        public bool CanCreate;
        public CaravanCreationFailureReason FailureReason;
        public int MaximumSupportedSlots;
        public int UnlockedSlotCount;
        public int MaximumOwnedCaravans;
        public long CreationCost;
        public long CurrencyAfterCreation;
    }

    public static class CaravanProgressionPolicyCalculator
    {
        public static CaravanProgressionResult Evaluate(CaravanProgressionInput input)
        {
            var result = new CaravanProgressionResult();
            if (!HasValidInput(input))
            {
                return Fail(result, CaravanCreationFailureReason.InvalidInput, false);
            }

            CaravanProgressionPolicyDefinition definition = input.Definition;
            int growthBonus;
            try
            {
                growthBonus = checked(input.CaravanGrowthLevel * definition.SlotsPerGrowthLevel);
            }
            catch (OverflowException)
            {
                return Fail(result, CaravanCreationFailureReason.ArithmeticOverflow, false);
            }

            growthBonus = Math.Min(growthBonus, definition.MaximumGrowthSlotBonus);
            int unlocked;
            try
            {
                unlocked = checked(definition.BaseUnlockedSlots + growthBonus + input.QuestUnlockedSlotBonus);
            }
            catch (OverflowException)
            {
                return Fail(result, CaravanCreationFailureReason.ArithmeticOverflow, false);
            }

            unlocked = Clamp(unlocked, 0, definition.MaximumSupportedSlots);
            result.IsValid = true;
            result.MaximumSupportedSlots = definition.MaximumSupportedSlots;
            result.UnlockedSlotCount = unlocked;
            result.MaximumOwnedCaravans = unlocked;
            result.CreationCost = definition.CreationCost;
            result.CurrencyAfterCreation = input.TradingCurrency >= definition.CreationCost
                ? input.TradingCurrency - definition.CreationCost
                : 0L;

            if (input.RequestedSlotIndex < 0 || input.RequestedSlotIndex >= definition.MaximumSupportedSlots)
                return Fail(result, CaravanCreationFailureReason.SlotOutOfRange, true);
            if (input.RequestedSlotIndex >= unlocked)
                return Fail(result, CaravanCreationFailureReason.SlotLocked, true);
            if (Contains(input.OccupiedSlotIndices, input.RequestedSlotIndex))
                return Fail(result, CaravanCreationFailureReason.SlotOccupied, true);
            if (input.CurrentCaravanCount >= unlocked)
                return Fail(result, CaravanCreationFailureReason.MaximumCaravansReached, true);
            if (input.DuplicateRequestInProgress)
                return Fail(result, CaravanCreationFailureReason.DuplicateRequest, true);
            if (!input.HasInitialTown)
                return Fail(result, CaravanCreationFailureReason.MissingInitialTown, true);
            if (input.TradingCurrency < definition.CreationCost)
                return Fail(result, CaravanCreationFailureReason.InsufficientCurrency, true);

            result.CanCreate = true;
            result.FailureReason = CaravanCreationFailureReason.None;
            return result;
        }

        private static bool HasValidInput(CaravanProgressionInput input)
        {
            return input != null && input.Definition != null && input.OccupiedSlotIndices != null &&
                   input.Definition.MaximumSupportedSlots > 0 && input.Definition.BaseUnlockedSlots >= 0 &&
                   input.Definition.BaseUnlockedSlots <= input.Definition.MaximumSupportedSlots &&
                   input.Definition.SlotsPerGrowthLevel >= 0 && input.Definition.MaximumGrowthSlotBonus >= 0 &&
                   input.Definition.CreationCost >= 0L && input.CaravanGrowthLevel >= 0 &&
                   input.QuestUnlockedSlotBonus >= 0 && input.CurrentCaravanCount >= 0 &&
                   input.CurrentCaravanCount <= input.Definition.MaximumSupportedSlots && input.TradingCurrency >= 0L &&
                   HasOnlyUniqueValidSlots(input.OccupiedSlotIndices, input.Definition.MaximumSupportedSlots) &&
                   input.OccupiedSlotIndices.Count == input.CurrentCaravanCount;
        }

        private static bool HasOnlyUniqueValidSlots(List<int> slots, int maximum)
        {
            var unique = new HashSet<int>();
            for (int index = 0; index < slots.Count; index++)
            {
                if (slots[index] < 0 || slots[index] >= maximum || !unique.Add(slots[index]))
                    return false;
            }
            return true;
        }

        private static bool Contains(List<int> values, int value)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] == value) return true;
            }
            return false;
        }

        private static CaravanProgressionResult Fail(
            CaravanProgressionResult result,
            CaravanCreationFailureReason reason,
            bool valid)
        {
            result.IsValid = valid;
            result.CanCreate = false;
            result.FailureReason = reason;
            return result;
        }

        private static int Clamp(int value, int min, int max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }

    public enum CaravanEconomicState
    {
        Prepare = 0,
        Traveling,
        SettlementPending,
        Invalid
    }

    public enum CaravanAssetBlockReason
    {
        None = 0,
        InvalidInput,
        Traveling,
        SettlementPending,
        NotAtBaseTown,
        ActiveTradeExists
    }

    [Serializable]
    public sealed class CaravanAssetAvailabilityInput
    {
        public CaravanEconomicState State;
        public bool IsAtBaseTown;
        public bool HasActiveTrade;
    }

    [Serializable]
    public sealed class CaravanAssetAvailabilityResult
    {
        public bool IsValid;
        public CaravanAssetBlockReason BlockReason;
        public bool CanEditConfiguration;
        public bool CanUseCargo;
        public bool CanUseWagon;
        public bool CanUseDraftAnimals;
        public bool CanUseMercenaries;
        public bool CanSubmitInvestmentAssets;
        public bool CanTransferToBaseInventory;
        public bool CanStartTrade;
    }

    public static class CaravanAssetAvailabilityCalculator
    {
        public static CaravanAssetAvailabilityResult Evaluate(CaravanAssetAvailabilityInput input)
        {
            var result = new CaravanAssetAvailabilityResult();
            if (input == null || input.State == CaravanEconomicState.Invalid)
            {
                result.BlockReason = CaravanAssetBlockReason.InvalidInput;
                return result;
            }

            result.IsValid = true;
            if (input.State == CaravanEconomicState.Traveling)
            {
                result.BlockReason = CaravanAssetBlockReason.Traveling;
                return result;
            }
            if (input.State == CaravanEconomicState.SettlementPending)
            {
                result.BlockReason = CaravanAssetBlockReason.SettlementPending;
                return result;
            }
            if (input.HasActiveTrade)
            {
                result.BlockReason = CaravanAssetBlockReason.ActiveTradeExists;
                return result;
            }

            result.CanEditConfiguration = true;
            result.CanUseCargo = true;
            result.CanUseWagon = true;
            result.CanUseDraftAnimals = true;
            result.CanUseMercenaries = true;
            result.CanSubmitInvestmentAssets = true;
            result.CanStartTrade = true;
            result.CanTransferToBaseInventory = input.IsAtBaseTown;

            if (!input.IsAtBaseTown)
                result.BlockReason = CaravanAssetBlockReason.NotAtBaseTown;
            else
                result.BlockReason = CaravanAssetBlockReason.None;

            return result;
        }
    }
}
