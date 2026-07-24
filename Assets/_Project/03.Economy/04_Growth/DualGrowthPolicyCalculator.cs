using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum GrowthAxis
    {
        Player = 0,
        Caravan = 1
    }

    public enum DualGrowthFailureReason
    {
        None = 0,
        InvalidInput,
        InvalidDefinition,
        GrowthIdMismatch,
        AxisMismatch,
        AlreadyMaxLevel,
        LevelDefinitionNotFound,
        InsufficientDevelopmentCurrency,
        ArithmeticOverflow
    }

    [Serializable]
    public sealed class GrowthLevelEffect
    {
        public string EffectId = string.Empty;
        public double Value;
    }

    [Serializable]
    public sealed class GrowthLevelDefinition
    {
        public int Level;
        public long DevelopmentCurrencyCost;
        public List<GrowthLevelEffect> Effects = new List<GrowthLevelEffect>();
    }

    [Serializable]
    public sealed class GrowthAxisDefinition
    {
        public string GrowthId = string.Empty;
        public GrowthAxis Axis;
        public int MaximumLevel;
        public List<GrowthLevelDefinition> Levels =
            new List<GrowthLevelDefinition>();
    }

    [Serializable]
    public sealed class DualGrowthInput
    {
        public string RequestedGrowthId = string.Empty;
        public GrowthAxis RequestedAxis;
        public int PlayerGrowthLevel;
        public int CaravanGrowthLevel;
        public long DevelopmentCurrency;
        public GrowthAxisDefinition Definition;
    }

    [Serializable]
    public sealed class DualGrowthResult
    {
        public bool Success;
        public DualGrowthFailureReason FailureReason;
        public string GrowthId = string.Empty;
        public GrowthAxis Axis;
        public int PreviousLevel;
        public int TargetLevel;
        public int PlayerGrowthLevelAfter;
        public int CaravanGrowthLevelAfter;
        public long DevelopmentCurrencyCost;
        public long DevelopmentCurrencyAfter;
        public List<GrowthLevelEffect> TargetEffects =
            new List<GrowthLevelEffect>();
    }

    public static class DualGrowthPolicyCalculator
    {
        public static DualGrowthResult Evaluate(DualGrowthInput input)
        {
            var result = new DualGrowthResult
            {
                GrowthId = input != null
                    ? input.RequestedGrowthId ?? string.Empty
                    : string.Empty,
                Axis = input != null ? input.RequestedAxis : GrowthAxis.Player,
                PlayerGrowthLevelAfter = input != null ? input.PlayerGrowthLevel : 0,
                CaravanGrowthLevelAfter = input != null ? input.CaravanGrowthLevel : 0,
                DevelopmentCurrencyAfter =
                    input != null ? input.DevelopmentCurrency : 0
            };

            if (input == null ||
                string.IsNullOrWhiteSpace(input.RequestedGrowthId) ||
                input.PlayerGrowthLevel < 0 ||
                input.CaravanGrowthLevel < 0 ||
                input.DevelopmentCurrency < 0 ||
                input.Definition == null)
            {
                return Fail(result, DualGrowthFailureReason.InvalidInput);
            }

            GrowthAxisDefinition definition = input.Definition;
            if (string.IsNullOrWhiteSpace(definition.GrowthId) ||
                definition.MaximumLevel <= 0 ||
                definition.Levels == null)
            {
                return Fail(result, DualGrowthFailureReason.InvalidDefinition);
            }
            if (!string.Equals(
                input.RequestedGrowthId,
                definition.GrowthId,
                StringComparison.Ordinal))
            {
                return Fail(result, DualGrowthFailureReason.GrowthIdMismatch);
            }
            if (input.RequestedAxis != definition.Axis)
                return Fail(result, DualGrowthFailureReason.AxisMismatch);

            int currentLevel = input.RequestedAxis == GrowthAxis.Player
                ? input.PlayerGrowthLevel
                : input.CaravanGrowthLevel;
            result.PreviousLevel = currentLevel;
            result.TargetLevel = currentLevel;

            if (currentLevel >= definition.MaximumLevel)
                return Fail(result, DualGrowthFailureReason.AlreadyMaxLevel);

            int targetLevel;
            try
            {
                targetLevel = checked(currentLevel + 1);
            }
            catch (OverflowException)
            {
                return Fail(result, DualGrowthFailureReason.ArithmeticOverflow);
            }

            GrowthLevelDefinition target = null;
            var levelNumbers = new HashSet<int>();
            for (int i = 0; i < definition.Levels.Count; i++)
            {
                GrowthLevelDefinition level = definition.Levels[i];
                if (level == null ||
                    level.Level <= 0 ||
                    level.DevelopmentCurrencyCost <= 0 ||
                    !levelNumbers.Add(level.Level))
                {
                    return Fail(result, DualGrowthFailureReason.InvalidDefinition);
                }
                if (level.Level == targetLevel)
                    target = level;
            }
            if (target == null)
                return Fail(result, DualGrowthFailureReason.LevelDefinitionNotFound);

            if (!TryCopyEffects(target.Effects, result.TargetEffects))
                return Fail(result, DualGrowthFailureReason.InvalidDefinition);

            result.DevelopmentCurrencyCost = target.DevelopmentCurrencyCost;
            if (input.DevelopmentCurrency < target.DevelopmentCurrencyCost)
            {
                return Fail(
                    result,
                    DualGrowthFailureReason.InsufficientDevelopmentCurrency);
            }

            result.Success = true;
            result.FailureReason = DualGrowthFailureReason.None;
            result.TargetLevel = targetLevel;
            result.DevelopmentCurrencyAfter =
                input.DevelopmentCurrency - target.DevelopmentCurrencyCost;
            if (input.RequestedAxis == GrowthAxis.Player)
                result.PlayerGrowthLevelAfter = targetLevel;
            else
                result.CaravanGrowthLevelAfter = targetLevel;
            return result;
        }

        private static bool TryCopyEffects(
            List<GrowthLevelEffect> effects,
            List<GrowthLevelEffect> destination)
        {
            if (effects == null)
                return false;

            var effectIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < effects.Count; i++)
            {
                GrowthLevelEffect effect = effects[i];
                if (effect == null ||
                    string.IsNullOrWhiteSpace(effect.EffectId) ||
                    double.IsNaN(effect.Value) ||
                    double.IsInfinity(effect.Value) ||
                    !effectIds.Add(effect.EffectId))
                {
                    return false;
                }
                destination.Add(new GrowthLevelEffect
                {
                    EffectId = effect.EffectId,
                    Value = effect.Value
                });
            }
            return true;
        }

        private static DualGrowthResult Fail(
            DualGrowthResult result,
            DualGrowthFailureReason reason)
        {
            result.Success = false;
            result.FailureReason = reason;
            return result;
        }
    }
}
