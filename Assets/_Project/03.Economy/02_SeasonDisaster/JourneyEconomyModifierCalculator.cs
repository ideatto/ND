using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum JourneyModifierSourceType
    {
        Season = 0,
        Disaster,
        Distance,
        RouteEvent,
        Combat,
        Loss
    }

    public enum JourneyModifierFailureReason
    {
        None = 0,
        InvalidInput,
        InvalidPolicy,
        InvalidModifier,
        DuplicateModifier,
        ArithmeticOverflow
    }

    [Serializable]
    public sealed class JourneyEconomyModifier
    {
        public JourneyModifierSourceType SourceType;
        public string SourceId = string.Empty;
        public double PriceFactor = 1d;
        public double SpeedFactor = 1d;
        public double FoodFactor = 1d;
        public double RiskFactor = 1d;
        public double LossFactor = 1d;
    }

    [Serializable]
    public sealed class JourneyModifierClampPolicy
    {
        public double MaximumPriceFactor = 10d;
        public double MaximumSpeedFactor = 3d;
        public double MaximumFoodFactor = 5d;
        public double MaximumRiskFactor = 5d;
        public double MaximumLossFactor = 5d;
    }

    [Serializable]
    public sealed class JourneyEconomyModifierInput
    {
        public long BaseUnitPrice;
        public double BaseSpeed;
        public double BaseFoodConsumption;
        public double BaseRiskRate;
        public double BaseLossRate;
        public JourneyModifierClampPolicy ClampPolicy =
            new JourneyModifierClampPolicy();
        public List<JourneyEconomyModifier> Modifiers =
            new List<JourneyEconomyModifier>();
    }

    [Serializable]
    public sealed class JourneyEconomyModifierResult
    {
        public bool Success;
        public JourneyModifierFailureReason FailureReason;
        public long AdjustedUnitPrice;
        public double AdjustedSpeed;
        public double AdjustedFoodConsumption;
        public double AdjustedRiskRate;
        public double AdjustedLossRate;
        public double CombinedPriceFactor = 1d;
        public double CombinedSpeedFactor = 1d;
        public double CombinedFoodFactor = 1d;
        public double CombinedRiskFactor = 1d;
        public double CombinedLossFactor = 1d;
        public int AppliedModifierCount;
    }

    public static class JourneyEconomyModifierCalculator
    {
        public static JourneyEconomyModifierResult Evaluate(
            JourneyEconomyModifierInput input)
        {
            var result = new JourneyEconomyModifierResult();
            if (!IsValidInput(input))
                return Fail(result, JourneyModifierFailureReason.InvalidInput);
            if (!IsValidPolicy(input.ClampPolicy))
                return Fail(result, JourneyModifierFailureReason.InvalidPolicy);

            var sourceKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < input.Modifiers.Count; i++)
            {
                JourneyEconomyModifier modifier = input.Modifiers[i];
                if (!IsValidModifier(modifier))
                    return Fail(result, JourneyModifierFailureReason.InvalidModifier);

                string sourceKey =
                    ((int)modifier.SourceType).ToString() + ":" + modifier.SourceId;
                if (!sourceKeys.Add(sourceKey))
                    return Fail(result, JourneyModifierFailureReason.DuplicateModifier);

                result.CombinedPriceFactor = MultiplyAndClamp(
                    result.CombinedPriceFactor,
                    modifier.PriceFactor,
                    input.ClampPolicy.MaximumPriceFactor);
                result.CombinedSpeedFactor = MultiplyAndClamp(
                    result.CombinedSpeedFactor,
                    modifier.SpeedFactor,
                    input.ClampPolicy.MaximumSpeedFactor);
                result.CombinedFoodFactor = MultiplyAndClamp(
                    result.CombinedFoodFactor,
                    modifier.FoodFactor,
                    input.ClampPolicy.MaximumFoodFactor);
                result.CombinedRiskFactor = MultiplyAndClamp(
                    result.CombinedRiskFactor,
                    modifier.RiskFactor,
                    input.ClampPolicy.MaximumRiskFactor);
                result.CombinedLossFactor = MultiplyAndClamp(
                    result.CombinedLossFactor,
                    modifier.LossFactor,
                    input.ClampPolicy.MaximumLossFactor);
                result.AppliedModifierCount++;
            }

            double rawPrice = input.BaseUnitPrice * result.CombinedPriceFactor;
            if (!IsFinite(rawPrice) || rawPrice > long.MaxValue)
                return Fail(result, JourneyModifierFailureReason.ArithmeticOverflow);

            result.AdjustedUnitPrice = (long)Math.Floor(Math.Max(0d, rawPrice));
            result.AdjustedSpeed = FiniteProduct(
                input.BaseSpeed,
                result.CombinedSpeedFactor);
            result.AdjustedFoodConsumption = FiniteProduct(
                input.BaseFoodConsumption,
                result.CombinedFoodFactor);
            result.AdjustedRiskRate = Clamp01(FiniteProduct(
                input.BaseRiskRate,
                result.CombinedRiskFactor));
            result.AdjustedLossRate = Clamp01(FiniteProduct(
                input.BaseLossRate,
                result.CombinedLossFactor));

            if (!IsFinite(result.AdjustedSpeed) ||
                !IsFinite(result.AdjustedFoodConsumption) ||
                !IsFinite(result.AdjustedRiskRate) ||
                !IsFinite(result.AdjustedLossRate))
            {
                return Fail(result, JourneyModifierFailureReason.ArithmeticOverflow);
            }

            result.Success = true;
            result.FailureReason = JourneyModifierFailureReason.None;
            return result;
        }

        private static bool IsValidInput(JourneyEconomyModifierInput input)
        {
            return input != null &&
                   input.BaseUnitPrice >= 0 &&
                   IsFiniteNonNegative(input.BaseSpeed) &&
                   IsFiniteNonNegative(input.BaseFoodConsumption) &&
                   IsFiniteRate(input.BaseRiskRate) &&
                   IsFiniteRate(input.BaseLossRate) &&
                   input.ClampPolicy != null &&
                   input.Modifiers != null;
        }

        private static bool IsValidPolicy(JourneyModifierClampPolicy policy)
        {
            return IsFinitePositive(policy.MaximumPriceFactor) &&
                   IsFinitePositive(policy.MaximumSpeedFactor) &&
                   IsFinitePositive(policy.MaximumFoodFactor) &&
                   IsFinitePositive(policy.MaximumRiskFactor) &&
                   IsFinitePositive(policy.MaximumLossFactor);
        }

        private static bool IsValidModifier(JourneyEconomyModifier modifier)
        {
            return modifier != null &&
                   string.IsNullOrWhiteSpace(modifier.SourceId) == false &&
                   Enum.IsDefined(
                       typeof(JourneyModifierSourceType),
                       modifier.SourceType) &&
                   IsFiniteNonNegative(modifier.PriceFactor) &&
                   IsFiniteNonNegative(modifier.SpeedFactor) &&
                   IsFiniteNonNegative(modifier.FoodFactor) &&
                   IsFiniteNonNegative(modifier.RiskFactor) &&
                   IsFiniteNonNegative(modifier.LossFactor);
        }

        private static double MultiplyAndClamp(
            double current,
            double factor,
            double maximum)
        {
            if (current == 0d || factor == 0d)
                return 0d;
            if (current >= maximum || factor >= maximum)
                return maximum;

            double product = current * factor;
            if (!IsFinite(product) || product > maximum)
                return maximum;
            return product;
        }

        private static double FiniteProduct(double value, double factor)
        {
            if (value == 0d || factor == 0d)
                return 0d;
            if (value > double.MaxValue / factor)
                return double.PositiveInfinity;
            return value * factor;
        }

        private static double Clamp01(double value)
        {
            if (value <= 0d)
                return 0d;
            return value >= 1d ? 1d : value;
        }

        private static bool IsFiniteRate(double value)
        {
            return IsFinite(value) && value >= 0d && value <= 1d;
        }

        private static bool IsFinitePositive(double value)
        {
            return IsFinite(value) && value > 0d;
        }

        private static bool IsFiniteNonNegative(double value)
        {
            return IsFinite(value) && value >= 0d;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static JourneyEconomyModifierResult Fail(
            JourneyEconomyModifierResult result,
            JourneyModifierFailureReason reason)
        {
            result.Success = false;
            result.FailureReason = reason;
            return result;
        }
    }
}
