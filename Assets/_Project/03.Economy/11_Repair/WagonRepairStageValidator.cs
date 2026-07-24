using System;

namespace ND.Economy
{
    public enum WagonRepairStageFailureReason
    {
        None = 0,
        InvalidInput,
        CaravanIdMismatch,
        DurabilityChanged,
        CurrencyChanged,
        InvalidPlan
    }

    [Serializable]
    public sealed class WagonRepairPersistenceSnapshot
    {
        public string CaravanId = string.Empty;
        public int CurrentDurability;
        public int MaximumDurability;
        public long TradingCurrency;
        public bool HasWagon;
        public bool IsInJourney;
        public bool IsDestroyed;
    }

    public sealed class WagonRepairStageValidationResult
    {
        public bool Success { get; internal set; }
        public WagonRepairStageFailureReason FailureReason { get; internal set; }
    }

    /// <summary>
    /// Revalidates the current persisted caravan immediately before repair staging.
    /// </summary>
    public static class WagonRepairStageValidator
    {
        public static WagonRepairStageValidationResult Validate(
            WagonRepairEconomicPlan plan,
            WagonRepairPersistenceSnapshot snapshot)
        {
            if (plan == null ||
                snapshot == null ||
                string.IsNullOrWhiteSpace(plan.CaravanId) ||
                string.IsNullOrWhiteSpace(snapshot.CaravanId) ||
                snapshot.MaximumDurability <= 0 ||
                snapshot.TradingCurrency < 0)
            {
                return Fail(WagonRepairStageFailureReason.InvalidInput);
            }
            if (!string.Equals(
                plan.CaravanId,
                snapshot.CaravanId,
                StringComparison.Ordinal))
            {
                return Fail(WagonRepairStageFailureReason.CaravanIdMismatch);
            }
            if (!IsValidPlan(plan, snapshot.MaximumDurability))
                return Fail(WagonRepairStageFailureReason.InvalidPlan);

            if (!snapshot.HasWagon ||
                snapshot.IsDestroyed ||
                snapshot.IsInJourney ||
                snapshot.CurrentDurability != plan.DurabilityBefore)
            {
                return Fail(WagonRepairStageFailureReason.DurabilityChanged);
            }
            if (snapshot.TradingCurrency != plan.CurrencyBefore)
                return Fail(WagonRepairStageFailureReason.CurrencyChanged);

            return new WagonRepairStageValidationResult
            {
                Success = true,
                FailureReason = WagonRepairStageFailureReason.None
            };
        }

        private static bool IsValidPlan(
            WagonRepairEconomicPlan plan,
            int maximumDurability)
        {
            if (plan.DurabilityBefore <= 0 ||
                plan.RepairedDurability <= 0 ||
                plan.RepairCost <= 0 ||
                plan.CurrencyBefore < plan.RepairCost ||
                plan.CurrencyAfter != plan.CurrencyBefore - plan.RepairCost)
            {
                return false;
            }

            try
            {
                checked
                {
                    return plan.DurabilityAfter ==
                               plan.DurabilityBefore + plan.RepairedDurability &&
                           plan.DurabilityAfter <= maximumDurability;
                }
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static WagonRepairStageValidationResult Fail(
            WagonRepairStageFailureReason reason)
        {
            return new WagonRepairStageValidationResult
            {
                FailureReason = reason
            };
        }
    }
}
