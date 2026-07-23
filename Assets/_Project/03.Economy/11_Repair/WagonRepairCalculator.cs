using System;

namespace ND.Economy
{
    public enum WagonRepairFailureReason
    {
        None = 0,
        InvalidInput,
        NoWagon,
        WagonDestroyed,
        InJourney,
        AlreadyFull,
        InvalidRepairAmount,
        ExceedsMaximumDurability,
        InvalidUnitCost,
        InvalidRarityMultiplier,
        InsufficientCurrency,
        ArithmeticOverflow
    }

    [Serializable]
    public sealed class WagonRepairInput
    {
        public bool HasWagon = true;
        public bool IsInJourney;
        public int CurrentDurability;
        public int MaximumDurability;
        public int RequestedRepairAmount;
        public long RepairCostPerDurability;
        public double RarityMultiplier = 1d;
        public long TradingCurrency;
    }

    [Serializable]
    public sealed class WagonRepairResult
    {
        public bool IsValid;
        public bool CanRepair;
        public WagonRepairFailureReason FailureReason;
        public int DurabilityBefore;
        public int DurabilityAfter;
        public int RepairedDurability;
        public long RepairCost;
        public long CurrencyBefore;
        public long CurrencyAfter;
    }

    /// <summary>
    /// Calculates a repair preview without mutating caravan or save data.
    /// The same result can be used to validate the eventual repair command.
    /// </summary>
    public static class WagonRepairCalculator
    {
        public static WagonRepairResult Evaluate(WagonRepairInput input)
        {
            var result = new WagonRepairResult();
            if (input == null)
            {
                return Fail(result, WagonRepairFailureReason.InvalidInput);
            }

            result.DurabilityBefore = input.CurrentDurability;
            result.DurabilityAfter = input.CurrentDurability;
            result.CurrencyBefore = input.TradingCurrency;
            result.CurrencyAfter = input.TradingCurrency;

            if (!input.HasWagon)
                return Fail(result, WagonRepairFailureReason.NoWagon);
            if (input.MaximumDurability <= 0)
                return Fail(result, WagonRepairFailureReason.InvalidInput);
            if (input.CurrentDurability <= 0)
                return Fail(result, WagonRepairFailureReason.WagonDestroyed);
            if (input.CurrentDurability > input.MaximumDurability)
                return Fail(result, WagonRepairFailureReason.InvalidInput);
            if (input.IsInJourney)
                return Fail(result, WagonRepairFailureReason.InJourney);
            if (input.CurrentDurability == input.MaximumDurability)
                return Fail(result, WagonRepairFailureReason.AlreadyFull);
            if (input.RequestedRepairAmount <= 0)
                return Fail(result, WagonRepairFailureReason.InvalidRepairAmount);

            int repairableAmount = input.MaximumDurability - input.CurrentDurability;
            if (input.RequestedRepairAmount > repairableAmount)
                return Fail(result, WagonRepairFailureReason.ExceedsMaximumDurability);
            if (input.RepairCostPerDurability <= 0)
                return Fail(result, WagonRepairFailureReason.InvalidUnitCost);
            if (double.IsNaN(input.RarityMultiplier) ||
                double.IsInfinity(input.RarityMultiplier) ||
                input.RarityMultiplier <= 0d)
            {
                return Fail(result, WagonRepairFailureReason.InvalidRarityMultiplier);
            }
            if (input.TradingCurrency < 0)
                return Fail(result, WagonRepairFailureReason.InvalidInput);

            double rawCost =
                input.RequestedRepairAmount *
                (double)input.RepairCostPerDurability *
                input.RarityMultiplier;
            if (double.IsNaN(rawCost) ||
                double.IsInfinity(rawCost) ||
                rawCost > long.MaxValue)
            {
                return Fail(result, WagonRepairFailureReason.ArithmeticOverflow);
            }

            long repairCost = Math.Max(1L, (long)Math.Floor(rawCost));
            result.RepairedDurability = input.RequestedRepairAmount;
            result.RepairCost = repairCost;

            if (input.TradingCurrency < repairCost)
                return Fail(result, WagonRepairFailureReason.InsufficientCurrency);

            result.IsValid = true;
            result.CanRepair = true;
            result.FailureReason = WagonRepairFailureReason.None;
            result.DurabilityAfter = input.CurrentDurability + input.RequestedRepairAmount;
            result.CurrencyAfter = input.TradingCurrency - repairCost;
            return result;
        }

        private static WagonRepairResult Fail(
            WagonRepairResult result,
            WagonRepairFailureReason reason)
        {
            result.IsValid = reason != WagonRepairFailureReason.InvalidInput &&
                             reason != WagonRepairFailureReason.InvalidUnitCost &&
                             reason != WagonRepairFailureReason.InvalidRarityMultiplier &&
                             reason != WagonRepairFailureReason.ArithmeticOverflow;
            result.CanRepair = false;
            result.FailureReason = reason;
            return result;
        }
    }
}
