using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ND.Economy
{
    public sealed class GrowthEffectPlan
    {
        public GrowthEffectPlan(string effectId, double value)
        {
            EffectId = effectId ?? string.Empty;
            Value = value;
        }

        public string EffectId { get; }
        public double Value { get; }
    }

    public sealed class DualGrowthEconomicPlan
    {
        private readonly ReadOnlyCollection<GrowthEffectPlan> targetEffects;

        public DualGrowthEconomicPlan(
            string growthId,
            GrowthAxis axis,
            int previousLevel,
            int targetLevel,
            int playerGrowthLevelBefore,
            int playerGrowthLevelAfter,
            int caravanGrowthLevelBefore,
            int caravanGrowthLevelAfter,
            long developmentCurrencyBefore,
            long developmentCurrencyCost,
            long developmentCurrencyAfter,
            IEnumerable<GrowthEffectPlan> targetEffects)
        {
            GrowthId = growthId ?? string.Empty;
            Axis = axis;
            PreviousLevel = previousLevel;
            TargetLevel = targetLevel;
            PlayerGrowthLevelBefore = playerGrowthLevelBefore;
            PlayerGrowthLevelAfter = playerGrowthLevelAfter;
            CaravanGrowthLevelBefore = caravanGrowthLevelBefore;
            CaravanGrowthLevelAfter = caravanGrowthLevelAfter;
            DevelopmentCurrencyBefore = developmentCurrencyBefore;
            DevelopmentCurrencyCost = developmentCurrencyCost;
            DevelopmentCurrencyAfter = developmentCurrencyAfter;

            var copy = targetEffects != null
                ? new List<GrowthEffectPlan>(targetEffects)
                : new List<GrowthEffectPlan>();
            this.targetEffects = copy.AsReadOnly();
        }

        public string GrowthId { get; }
        public GrowthAxis Axis { get; }
        public int PreviousLevel { get; }
        public int TargetLevel { get; }
        public int PlayerGrowthLevelBefore { get; }
        public int PlayerGrowthLevelAfter { get; }
        public int CaravanGrowthLevelBefore { get; }
        public int CaravanGrowthLevelAfter { get; }
        public long DevelopmentCurrencyBefore { get; }
        public long DevelopmentCurrencyCost { get; }
        public long DevelopmentCurrencyAfter { get; }
        public IReadOnlyList<GrowthEffectPlan> TargetEffects => targetEffects;
    }

    public sealed class DualGrowthPlanBuildResult
    {
        public bool Success { get; internal set; }
        public DualGrowthFailureReason FailureReason { get; internal set; }
        public DualGrowthEconomicPlan Plan { get; internal set; }
    }

    public static class DualGrowthEconomicPlanBuilder
    {
        public static DualGrowthPlanBuildResult Build(DualGrowthInput input)
        {
            DualGrowthResult calculation =
                DualGrowthPolicyCalculator.Evaluate(input);
            if (calculation == null || !calculation.Success || input == null)
            {
                return Fail(
                    calculation != null
                        ? calculation.FailureReason
                        : DualGrowthFailureReason.InvalidInput);
            }

            var effects = new List<GrowthEffectPlan>(
                calculation.TargetEffects.Count);
            for (int i = 0; i < calculation.TargetEffects.Count; i++)
            {
                GrowthLevelEffect effect = calculation.TargetEffects[i];
                if (effect == null ||
                    string.IsNullOrWhiteSpace(effect.EffectId) ||
                    double.IsNaN(effect.Value) ||
                    double.IsInfinity(effect.Value))
                {
                    return Fail(DualGrowthFailureReason.InvalidDefinition);
                }
                effects.Add(new GrowthEffectPlan(effect.EffectId, effect.Value));
            }

            return new DualGrowthPlanBuildResult
            {
                Success = true,
                FailureReason = DualGrowthFailureReason.None,
                Plan = new DualGrowthEconomicPlan(
                    calculation.GrowthId,
                    calculation.Axis,
                    calculation.PreviousLevel,
                    calculation.TargetLevel,
                    input.PlayerGrowthLevel,
                    calculation.PlayerGrowthLevelAfter,
                    input.CaravanGrowthLevel,
                    calculation.CaravanGrowthLevelAfter,
                    input.DevelopmentCurrency,
                    calculation.DevelopmentCurrencyCost,
                    calculation.DevelopmentCurrencyAfter,
                    effects)
            };
        }

        private static DualGrowthPlanBuildResult Fail(
            DualGrowthFailureReason reason)
        {
            return new DualGrowthPlanBuildResult
            {
                FailureReason = reason
            };
        }
    }
}
