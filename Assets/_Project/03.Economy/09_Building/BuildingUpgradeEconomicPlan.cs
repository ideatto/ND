using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ND.Economy
{
    public sealed class BuildingUpgradeMaterialPlan
    {
        public BuildingUpgradeMaterialPlan(
            string itemId,
            int requiredQuantity,
            int quantityBefore,
            int quantityAfter)
        {
            ItemId = itemId ?? string.Empty;
            RequiredQuantity = requiredQuantity;
            QuantityBefore = quantityBefore;
            QuantityAfter = quantityAfter;
        }

        public string ItemId { get; }
        public int RequiredQuantity { get; }
        public int QuantityBefore { get; }
        public int QuantityAfter { get; }
    }

    public sealed class BuildingUpgradeEffectPlan
    {
        public BuildingUpgradeEffectPlan(string effectId, double value)
        {
            EffectId = effectId ?? string.Empty;
            Value = value;
        }

        public string EffectId { get; }
        public double Value { get; }
    }

    public sealed class BuildingUpgradeEconomicPlan
    {
        private readonly ReadOnlyCollection<BuildingUpgradeMaterialPlan> materials;
        private readonly ReadOnlyCollection<BuildingUpgradeEffectPlan> effects;

        public BuildingUpgradeEconomicPlan(
            string buildingId,
            int previousLevel,
            int targetLevel,
            IEnumerable<BuildingUpgradeMaterialPlan> materials,
            IEnumerable<BuildingUpgradeEffectPlan> effects)
        {
            BuildingId = buildingId ?? string.Empty;
            PreviousLevel = previousLevel;
            TargetLevel = targetLevel;
            List<BuildingUpgradeMaterialPlan> materialCopy = materials != null
                ? new List<BuildingUpgradeMaterialPlan>(materials)
                : new List<BuildingUpgradeMaterialPlan>();
            List<BuildingUpgradeEffectPlan> effectCopy = effects != null
                ? new List<BuildingUpgradeEffectPlan>(effects)
                : new List<BuildingUpgradeEffectPlan>();
            this.materials = materialCopy.AsReadOnly();
            this.effects = effectCopy.AsReadOnly();
        }

        public string BuildingId { get; }
        public int PreviousLevel { get; }
        public int TargetLevel { get; }
        public IReadOnlyList<BuildingUpgradeMaterialPlan> Materials => materials;
        public IReadOnlyList<BuildingUpgradeEffectPlan> Effects => effects;
    }

    public sealed class BuildingUpgradePlanBuildResult
    {
        public bool Success { get; internal set; }
        public BuildingUpgradeFailureReason FailureReason { get; internal set; }
        public BuildingUpgradeEconomicPlan Plan { get; internal set; }
    }

    public static class BuildingUpgradeEconomicPlanBuilder
    {
        public static BuildingUpgradePlanBuildResult Build(BuildingUpgradeInput input)
        {
            BuildingUpgradeResult calculation =
                BuildingUpgradePolicyCalculator.Evaluate(input);
            if (calculation == null || !calculation.Success)
            {
                return new BuildingUpgradePlanBuildResult
                {
                    FailureReason = calculation != null
                        ? calculation.FailureReason
                        : BuildingUpgradeFailureReason.InvalidInput
                };
            }

            var materials = new List<BuildingUpgradeMaterialPlan>(
                calculation.Materials.Count);
            for (int i = 0; i < calculation.Materials.Count; i++)
            {
                BuildingUpgradeMaterialDelta material = calculation.Materials[i];
                if (material == null ||
                    string.IsNullOrWhiteSpace(material.ItemId) ||
                    material.RequiredQuantity <= 0 ||
                    material.MissingQuantity != 0 ||
                    material.QuantityBefore < material.RequiredQuantity ||
                    material.QuantityAfter !=
                        material.QuantityBefore - material.RequiredQuantity)
                {
                    return Fail(BuildingUpgradeFailureReason.InvalidDefinition);
                }

                materials.Add(new BuildingUpgradeMaterialPlan(
                    material.ItemId,
                    material.RequiredQuantity,
                    material.QuantityBefore,
                    material.QuantityAfter));
            }

            var effects = new List<BuildingUpgradeEffectPlan>(
                calculation.TargetEffects.Count);
            for (int i = 0; i < calculation.TargetEffects.Count; i++)
            {
                BuildingUpgradeEffect effect = calculation.TargetEffects[i];
                if (effect == null ||
                    string.IsNullOrWhiteSpace(effect.EffectId) ||
                    double.IsNaN(effect.Value) ||
                    double.IsInfinity(effect.Value))
                {
                    return Fail(BuildingUpgradeFailureReason.InvalidDefinition);
                }
                effects.Add(new BuildingUpgradeEffectPlan(effect.EffectId, effect.Value));
            }

            return new BuildingUpgradePlanBuildResult
            {
                Success = true,
                FailureReason = BuildingUpgradeFailureReason.None,
                Plan = new BuildingUpgradeEconomicPlan(
                    calculation.BuildingId,
                    calculation.PreviousLevel,
                    calculation.TargetLevel,
                    materials,
                    effects)
            };
        }

        private static BuildingUpgradePlanBuildResult Fail(
            BuildingUpgradeFailureReason reason)
        {
            return new BuildingUpgradePlanBuildResult
            {
                FailureReason = reason
            };
        }
    }
}
