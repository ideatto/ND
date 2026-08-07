using System;
using System.Collections.Generic;
using ND.Framework;

namespace ND.Economy
{
    /// <summary>
    /// Selects existing item-season modifiers and policy-authored contextual modifiers.
    /// Price arithmetic remains owned by <see cref="PriceCalculator"/>.
    /// </summary>
    public static class ContextualSellPriceModifierResolver
    {
        public static IReadOnlyList<PriceModifierInput> Resolve(
            global::TradeItemData item,
            SellPriceCalculationContext context,
            SellPriceModifierPolicy policy)
        {
            if (item == null)
                return new List<PriceModifierInput>();

            List<PriceModifierInput> itemModifiers = item.AffectModify
                ? LjhEconomyM1InputAdapter.ToPriceModifierInputs(item.Modifiers)
                : new List<PriceModifierInput>();
            bool isLocalSpecialtyAtDestination = ContainsCanonicalItemId(
                context.DestinationLocalSpecialtyItemIds,
                item.ItemId);
            List<PriceModifierInput> resolved = isLocalSpecialtyAtDestination
                ? SelectWithoutSeasonalSellPriceModifiers(itemModifiers)
                : SeasonalSellPriceModifierSelector.SelectForSellPrice(
                    itemModifiers,
                    context.SeasonId);

            if (policy != null)
            {
                if (!isLocalSpecialtyAtDestination)
                {
                    AddCategorySeasonalModifier(resolved, item.Category, context.SeasonId, policy);
                }

                AddLuckyMoneyModifier(resolved, context.IsLuckyMoneyActive, policy.LuckyMoneyRule);
                if (!isLocalSpecialtyAtDestination)
                {
                    AddDistanceModifier(resolved, context.RouteDistanceKm, policy.DistanceRules);
                }
            }

            return Deduplicate(resolved);
        }

        private static List<PriceModifierInput> SelectWithoutSeasonalSellPriceModifiers(
            IEnumerable<PriceModifierInput> modifiers)
        {
            var selected = new List<PriceModifierInput>();
            if (modifiers == null)
                return selected;

            foreach (PriceModifierInput modifier in modifiers)
            {
                if (modifier == null)
                    continue;

                bool targetsSellPrice = modifier.Target == PriceModifierTarget.SellPrice
                    || modifier.Target == PriceModifierTarget.Both;
                if (modifier.ModifierType != PriceModifierType.Season || !targetsSellPrice)
                {
                    selected.Add(modifier);
                }
            }

            return selected;
        }

        private static bool ContainsCanonicalItemId(IReadOnlyList<string> itemIds, string targetItemId)
        {
            if (itemIds == null || string.IsNullOrWhiteSpace(targetItemId))
                return false;

            for (int i = 0; i < itemIds.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(itemIds[i])
                    && string.Equals(itemIds[i], targetItemId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddCategorySeasonalModifier(
            List<PriceModifierInput> modifiers,
            global::TradeItemCategory category,
            string seasonId,
            SellPriceModifierPolicy policy)
        {
            if (!IsCanonicalSeasonId(seasonId) || policy.CategorySeasonalRules == null)
                return;

            var matches = new List<CategorySeasonalSellPriceRule>();
            for (int i = 0; i < policy.CategorySeasonalRules.Count; i++)
            {
                CategorySeasonalSellPriceRule rule = policy.CategorySeasonalRules[i];
                if (rule != null && rule.Enabled && rule.Category == category
                    && string.Equals(rule.SeasonId, seasonId, StringComparison.Ordinal))
                {
                    matches.Add(rule);
                }
            }

            matches.Sort((left, right) => string.Compare(left.RuleId, right.RuleId, StringComparison.Ordinal));
            for (int i = 0; i < matches.Count; i++)
            {
                CategorySeasonalSellPriceRule rule = matches[i];
                AddPolicyModifier(
                    modifiers,
                    rule.ModifierType,
                    $"category-season:{seasonId}:{category}:{rule.RuleId}",
                    rule.Operation,
                    rule.Value);
            }
        }

        private static void AddLuckyMoneyModifier(
            List<PriceModifierInput> modifiers,
            bool isActive,
            LuckyMoneySellPriceRule rule)
        {
            if (!isActive || rule == null || !rule.Enabled)
                return;

            AddPolicyModifier(
                modifiers,
                rule.ModifierType,
                $"lucky-money:{rule.EffectId}",
                rule.Operation,
                rule.Value);
        }

        private static void AddDistanceModifier(
            List<PriceModifierInput> modifiers,
            float distanceKm,
            IReadOnlyList<DistanceSellPriceRule> rules)
        {
            if (!IsFinite(distanceKm) || rules == null)
                return;

            distanceKm = Math.Max(0f, distanceKm);
            var matches = new List<DistanceSellPriceRule>();
            for (int i = 0; i < rules.Count; i++)
            {
                DistanceSellPriceRule rule = rules[i];
                if (IsValidDistanceRule(rule)
                    && distanceKm >= rule.MinimumDistanceKm
                    && (!rule.HasMaximumDistance || distanceKm < rule.MaximumDistanceKm))
                {
                    matches.Add(rule);
                }
            }

            matches.Sort((left, right) => string.Compare(left.RuleId, right.RuleId, StringComparison.Ordinal));
            if (matches.Count == 0)
                return;

            DistanceSellPriceRule selected = matches[0];
            if (selected.Value == 0f)
                return;

            AddPolicyModifier(
                modifiers,
                selected.ModifierType,
                $"distance:{selected.RuleId}",
                selected.Operation,
                selected.Value);
        }

        private static void AddPolicyModifier(
            List<PriceModifierInput> modifiers,
            PriceModifierType modifierType,
            string sourceId,
            PriceModifierOperation operation,
            float value)
        {
            modifiers.Add(new PriceModifierInput
            {
                ModifierType = modifierType,
                SourceId = sourceId,
                Target = PriceModifierTarget.SellPrice,
                Operation = operation,
                Value = value
            });
        }

        private static IReadOnlyList<PriceModifierInput> Deduplicate(List<PriceModifierInput> modifiers)
        {
            var result = new List<PriceModifierInput>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < modifiers.Count; i++)
            {
                PriceModifierInput modifier = modifiers[i];
                if (modifier == null)
                    continue;

                string identity = $"{(int)modifier.ModifierType}\u001f{modifier.SourceId}\u001f{(int)modifier.Target}\u001f{(int)modifier.Operation}";
                if (identities.Add(identity))
                    result.Add(modifier);
            }

            return result;
        }

        private static bool IsValidDistanceRule(DistanceSellPriceRule rule)
        {
            return rule != null && rule.Enabled
                && IsFinite(rule.MinimumDistanceKm)
                && rule.MinimumDistanceKm >= 0f
                && (!rule.HasMaximumDistance
                    || (IsFinite(rule.MaximumDistanceKm)
                        && rule.MaximumDistanceKm > rule.MinimumDistanceKm));
        }

        private static bool IsCanonicalSeasonId(string seasonId)
        {
            return string.Equals(seasonId, GameCalendarDate.SpringId, StringComparison.Ordinal)
                || string.Equals(seasonId, GameCalendarDate.SummerId, StringComparison.Ordinal)
                || string.Equals(seasonId, GameCalendarDate.AutumnId, StringComparison.Ordinal)
                || string.Equals(seasonId, GameCalendarDate.WinterId, StringComparison.Ordinal);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
