using System;
using System.Collections.Generic;
using UnityEngine;

namespace ND.Economy
{
    [Serializable]
    public sealed class CategorySeasonalSellPriceRule
    {
        [SerializeField] private string ruleId = string.Empty;
        [SerializeField] private bool enabled = true;
        [SerializeField] private global::TradeItemCategory category;
        [SerializeField] private string seasonId = string.Empty;
        [SerializeField] private PriceModifierOperation operation = PriceModifierOperation.Percent;
        [SerializeField] private float value;
        [SerializeField] private PriceModifierType modifierType = PriceModifierType.Season;

        public string RuleId => ruleId;
        public bool Enabled => enabled;
        public global::TradeItemCategory Category => category;
        public string SeasonId => seasonId;
        public PriceModifierOperation Operation => operation;
        public float Value => value;
        public PriceModifierType ModifierType => modifierType;
    }

    [Serializable]
    public sealed class LuckyMoneySellPriceRule
    {
        [SerializeField] private string effectId = "lucky_money_default";
        [SerializeField] private bool enabled = true;
        [SerializeField] private PriceModifierOperation operation = PriceModifierOperation.Percent;
        [SerializeField] private float value = 0.5f;
        [SerializeField] private PriceModifierType modifierType = PriceModifierType.RouteEvent;

        public string EffectId => effectId;
        public bool Enabled => enabled;
        public PriceModifierOperation Operation => operation;
        public float Value => value;
        public PriceModifierType ModifierType => modifierType;
    }

    [Serializable]
    public sealed class DistanceSellPriceRule
    {
        [SerializeField] private string ruleId = string.Empty;
        [SerializeField] private bool enabled = true;
        [SerializeField] private float minimumDistanceKm;
        [SerializeField] private bool hasMaximumDistance;
        [SerializeField] private float maximumDistanceKm;
        [SerializeField] private PriceModifierOperation operation = PriceModifierOperation.Percent;
        [SerializeField] private float value;
        [SerializeField] private PriceModifierType modifierType = PriceModifierType.RouteEvent;

        public string RuleId => ruleId;
        public bool Enabled => enabled;
        public float MinimumDistanceKm => minimumDistanceKm;
        public bool HasMaximumDistance => hasMaximumDistance;
        public float MaximumDistanceKm => maximumDistanceKm;
        public PriceModifierOperation Operation => operation;
        public float Value => value;
        public PriceModifierType ModifierType => modifierType;
    }

    /// <summary>
    /// Inspector-authored rules that produce contextual SellPrice modifiers.
    /// Runtime calculations read the serialized collections without mutating them.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SellPriceModifierPolicy_Default",
        menuName = "Economy/Market/Sell Price Modifier Policy")]
    public sealed class SellPriceModifierPolicy : ScriptableObject
    {
        [SerializeField] private List<CategorySeasonalSellPriceRule> categorySeasonalRules =
            new List<CategorySeasonalSellPriceRule>();
        [SerializeField] private LuckyMoneySellPriceRule luckyMoneyRule = new LuckyMoneySellPriceRule();
        [SerializeField] private List<DistanceSellPriceRule> distanceRules =
            new List<DistanceSellPriceRule>();

        public IReadOnlyList<CategorySeasonalSellPriceRule> CategorySeasonalRules => categorySeasonalRules;
        public LuckyMoneySellPriceRule LuckyMoneyRule => luckyMoneyRule;
        public IReadOnlyList<DistanceSellPriceRule> DistanceRules => distanceRules;

#if UNITY_EDITOR
        private void OnValidate()
        {
            ValidateIds(categorySeasonalRules, rule => rule != null ? rule.RuleId : string.Empty, "category-season");
            ValidateIds(distanceRules, rule => rule != null ? rule.RuleId : string.Empty, "distance");
            ValidateDistanceRules();
        }

        private void ValidateDistanceRules()
        {
            if (distanceRules == null)
                return;

            for (int i = 0; i < distanceRules.Count; i++)
            {
                DistanceSellPriceRule rule = distanceRules[i];
                if (rule == null)
                    continue;

                bool invalidNumber = !IsFinite(rule.MinimumDistanceKm)
                    || (rule.HasMaximumDistance && !IsFinite(rule.MaximumDistanceKm));
                if (invalidNumber || rule.MinimumDistanceKm < 0f
                    || (rule.HasMaximumDistance && rule.MaximumDistanceKm <= rule.MinimumDistanceKm))
                {
                    Debug.LogWarning($"[SellPriceModifierPolicy] Invalid distance range: {rule.RuleId}", this);
                }

                for (int otherIndex = i + 1; otherIndex < distanceRules.Count; otherIndex++)
                {
                    DistanceSellPriceRule other = distanceRules[otherIndex];
                    if (other != null && RangesOverlap(rule, other))
                    {
                        Debug.LogWarning(
                            $"[SellPriceModifierPolicy] Overlapping distance ranges: {rule.RuleId}, {other.RuleId}",
                            this);
                    }
                }
            }
        }

        private void ValidateIds<TRule>(
            IReadOnlyList<TRule> rules,
            Func<TRule, string> getId,
            string label)
        {
            if (rules == null)
                return;

            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < rules.Count; i++)
            {
                string id = getId(rules[i]);
                if (string.IsNullOrWhiteSpace(id))
                    Debug.LogWarning($"[SellPriceModifierPolicy] Empty {label} rule ID.", this);
                else if (!ids.Add(id))
                    Debug.LogWarning($"[SellPriceModifierPolicy] Duplicate {label} rule ID: {id}", this);
            }
        }

        private static bool RangesOverlap(DistanceSellPriceRule left, DistanceSellPriceRule right)
        {
            if (!IsValidRange(left) || !IsValidRange(right))
                return false;

            float leftMaximum = left.HasMaximumDistance ? left.MaximumDistanceKm : float.PositiveInfinity;
            float rightMaximum = right.HasMaximumDistance ? right.MaximumDistanceKm : float.PositiveInfinity;
            return left.MinimumDistanceKm < rightMaximum && right.MinimumDistanceKm < leftMaximum;
        }

        private static bool IsValidRange(DistanceSellPriceRule rule)
        {
            return rule != null
                && IsFinite(rule.MinimumDistanceKm)
                && rule.MinimumDistanceKm >= 0f
                && (!rule.HasMaximumDistance
                    || (IsFinite(rule.MaximumDistanceKm)
                        && rule.MaximumDistanceKm > rule.MinimumDistanceKm));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
#endif
    }
}
