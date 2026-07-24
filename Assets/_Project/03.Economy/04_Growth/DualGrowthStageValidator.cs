using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum DualGrowthStageFailureReason
    {
        None = 0,
        InvalidInput,
        GrowthIdMismatch,
        PlayerGrowthLevelChanged,
        CaravanGrowthLevelChanged,
        DevelopmentCurrencyChanged,
        InvalidPlan
    }

    [Serializable]
    public sealed class DualGrowthPersistenceSnapshot
    {
        public string GrowthId = string.Empty;
        public int PlayerGrowthLevel;
        public int CaravanGrowthLevel;
        public long DevelopmentCurrency;
    }

    public sealed class DualGrowthStageValidationResult
    {
        public bool Success { get; internal set; }
        public DualGrowthStageFailureReason FailureReason { get; internal set; }
    }

    /// <summary>
    /// Revalidates durable growth state immediately before Framework stages a plan.
    /// </summary>
    public static class DualGrowthStageValidator
    {
        public static DualGrowthStageValidationResult Validate(
            DualGrowthEconomicPlan plan,
            DualGrowthPersistenceSnapshot snapshot)
        {
            if (plan == null ||
                snapshot == null ||
                string.IsNullOrWhiteSpace(plan.GrowthId) ||
                string.IsNullOrWhiteSpace(snapshot.GrowthId) ||
                snapshot.PlayerGrowthLevel < 0 ||
                snapshot.CaravanGrowthLevel < 0 ||
                snapshot.DevelopmentCurrency < 0)
            {
                return Fail(DualGrowthStageFailureReason.InvalidInput);
            }
            if (!string.Equals(
                plan.GrowthId,
                snapshot.GrowthId,
                StringComparison.Ordinal))
            {
                return Fail(DualGrowthStageFailureReason.GrowthIdMismatch);
            }
            if (!IsValidPlan(plan))
                return Fail(DualGrowthStageFailureReason.InvalidPlan);
            if (snapshot.PlayerGrowthLevel != plan.PlayerGrowthLevelBefore)
            {
                return Fail(
                    DualGrowthStageFailureReason.PlayerGrowthLevelChanged);
            }
            if (snapshot.CaravanGrowthLevel != plan.CaravanGrowthLevelBefore)
            {
                return Fail(
                    DualGrowthStageFailureReason.CaravanGrowthLevelChanged);
            }
            if (snapshot.DevelopmentCurrency !=
                plan.DevelopmentCurrencyBefore)
            {
                return Fail(
                    DualGrowthStageFailureReason.DevelopmentCurrencyChanged);
            }

            return new DualGrowthStageValidationResult
            {
                Success = true,
                FailureReason = DualGrowthStageFailureReason.None
            };
        }

        private static bool IsValidPlan(DualGrowthEconomicPlan plan)
        {
            if ((plan.Axis != GrowthAxis.Player &&
                 plan.Axis != GrowthAxis.Caravan) ||
                plan.PreviousLevel < 0 ||
                plan.PlayerGrowthLevelBefore < 0 ||
                plan.CaravanGrowthLevelBefore < 0 ||
                plan.DevelopmentCurrencyBefore < 0 ||
                plan.DevelopmentCurrencyCost <= 0 ||
                plan.DevelopmentCurrencyAfter < 0 ||
                plan.TargetEffects == null)
            {
                return false;
            }

            try
            {
                checked
                {
                    if (plan.TargetLevel != plan.PreviousLevel + 1 ||
                        plan.DevelopmentCurrencyAfter !=
                            plan.DevelopmentCurrencyBefore -
                            plan.DevelopmentCurrencyCost)
                    {
                        return false;
                    }
                }
            }
            catch (OverflowException)
            {
                return false;
            }

            if (plan.Axis == GrowthAxis.Player)
            {
                if (plan.PlayerGrowthLevelBefore != plan.PreviousLevel ||
                    plan.PlayerGrowthLevelAfter != plan.TargetLevel ||
                    plan.CaravanGrowthLevelAfter !=
                        plan.CaravanGrowthLevelBefore)
                {
                    return false;
                }
            }
            else if (plan.CaravanGrowthLevelBefore != plan.PreviousLevel ||
                     plan.CaravanGrowthLevelAfter != plan.TargetLevel ||
                     plan.PlayerGrowthLevelAfter !=
                        plan.PlayerGrowthLevelBefore)
            {
                return false;
            }

            var effectIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < plan.TargetEffects.Count; i++)
            {
                GrowthEffectPlan effect = plan.TargetEffects[i];
                if (effect == null ||
                    string.IsNullOrWhiteSpace(effect.EffectId) ||
                    double.IsNaN(effect.Value) ||
                    double.IsInfinity(effect.Value) ||
                    !effectIds.Add(effect.EffectId))
                {
                    return false;
                }
            }
            return true;
        }

        private static DualGrowthStageValidationResult Fail(
            DualGrowthStageFailureReason reason)
        {
            return new DualGrowthStageValidationResult
            {
                FailureReason = reason
            };
        }
    }
}
