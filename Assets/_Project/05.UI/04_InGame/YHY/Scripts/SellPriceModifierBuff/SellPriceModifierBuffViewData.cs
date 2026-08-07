using System;
using System.Collections.Generic;
using System.Globalization;
using ND.Economy;
using ND.Framework;

namespace ND.UI.InGame.SellPriceModifierBuff
{
    public sealed class SellPriceModifierBuffViewData
    {
        public SellPriceModifierBuffViewData(string title, string normalBody, string detailBody)
        {
            Title = title ?? string.Empty;
            NormalBody = normalBody ?? string.Empty;
            DetailBody = detailBody ?? string.Empty;
        }

        public string Title { get; }
        public string NormalBody { get; }
        public string DetailBody { get; }
    }

    public static class SellPriceModifierValueFormatter
    {
        public static string Format(PriceModifierOperation operation, float value)
        {
            switch (operation)
            {
                case PriceModifierOperation.Percent:
                    return FormatSigned(value * 100f, "%");
                case PriceModifierOperation.Add:
                    return FormatSigned(value, string.Empty);
                case PriceModifierOperation.Multiply:
                    return $"x{value.ToString("0.##", CultureInfo.InvariantCulture)}";
                default:
                    return value.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        private static string FormatSigned(float value, string suffix)
        {
            string sign = value > 0f ? "+" : string.Empty;
            return $"{sign}{value.ToString("0.##", CultureInfo.InvariantCulture)}{suffix}";
        }
    }

    public static class SeasonBuffViewDataBuilder
    {
        public static SellPriceModifierBuffViewData Build(
            SellPriceModifierPolicy policy,
            string seasonId,
            IEnumerable<global::TradeItemData> tradeItems)
        {
            string title = $"{GetSeasonName(seasonId)} 판매 효과";
            var categoryRows = new List<string>();
            if (policy != null && policy.CategorySeasonalRules != null)
            {
                foreach (CategorySeasonalSellPriceRule rule in policy.CategorySeasonalRules)
                {
                    if (rule == null || !rule.Enabled || rule.Value == 0f
                        || !string.Equals(rule.SeasonId, seasonId, StringComparison.Ordinal))
                        continue;

                    categoryRows.Add($"{GetCategoryName(rule.Category)} {SellPriceModifierValueFormatter.Format(rule.Operation, rule.Value)}");
                }
            }

            var itemRows = new List<string>();
            if (tradeItems != null)
            {
                foreach (global::TradeItemData item in tradeItems)
                {
                    if (item == null || !item.AffectModify)
                        continue;

                    List<PriceModifierInput> converted = LjhEconomyM1InputAdapter.ToPriceModifierInputs(item.Modifiers);
                    List<PriceModifierInput> selected = SeasonalSellPriceModifierSelector.SelectForSellPrice(converted, seasonId);
                    foreach (PriceModifierInput modifier in selected)
                    {
                        if (modifier == null
                            || modifier.ModifierType != PriceModifierType.Season
                            || (modifier.Target != PriceModifierTarget.SellPrice && modifier.Target != PriceModifierTarget.Both)
                            || modifier.Value == 0f
                            || !IsCanonicalSeasonId(modifier.SourceId)
                            || !string.Equals(modifier.SourceId, seasonId, StringComparison.Ordinal))
                            continue;

                        string displayName = string.IsNullOrWhiteSpace(item.DisplayName) ? item.ItemId : item.DisplayName;
                        itemRows.Add($"{displayName} {SellPriceModifierValueFormatter.Format(modifier.Operation, modifier.Value)}");
                    }
                }
            }

            string normal = categoryRows.Count > 0
                ? string.Join("\n", categoryRows)
                : "현재 계절에는 카테고리별 판매 보정이 없습니다.";
            string detail = $"[카테고리 효과]\n{normal}\n\n[개별 무역품 효과]\n"
                + (itemRows.Count > 0 ? string.Join("\n", itemRows) : "적용 중인 개별 효과가 없습니다.");
            return new SellPriceModifierBuffViewData(title, normal, detail);
        }

        private static bool IsCanonicalSeasonId(string seasonId)
        {
            return string.Equals(seasonId, GameCalendarDate.SpringId, StringComparison.Ordinal)
                || string.Equals(seasonId, GameCalendarDate.SummerId, StringComparison.Ordinal)
                || string.Equals(seasonId, GameCalendarDate.AutumnId, StringComparison.Ordinal)
                || string.Equals(seasonId, GameCalendarDate.WinterId, StringComparison.Ordinal);
        }

        private static string GetSeasonName(string seasonId)
        {
            if (string.Equals(seasonId, GameCalendarDate.SpringId, StringComparison.Ordinal)) return "봄";
            if (string.Equals(seasonId, GameCalendarDate.SummerId, StringComparison.Ordinal)) return "여름";
            if (string.Equals(seasonId, GameCalendarDate.AutumnId, StringComparison.Ordinal)) return "가을";
            if (string.Equals(seasonId, GameCalendarDate.WinterId, StringComparison.Ordinal)) return "겨울";
            return string.IsNullOrWhiteSpace(seasonId) ? "계절" : seasonId;
        }

        private static string GetCategoryName(global::TradeItemCategory category)
        {
            switch (category.ToString())
            {
                case "Food": return "식량";
                case "Material": return "재료";
                case "Valuable": return "귀중품";
                case "LuxuryGoods": return "사치품";
                case "DraftAnimalsFood": return "견인동물 식량";
                default: return category.ToString();
            }
        }
    }

    public static class DistanceBuffViewDataBuilder
    {
        public static SellPriceModifierBuffViewData Build(SellPriceModifierPolicy policy)
        {
            var rows = new List<string>();
            if (policy != null && policy.DistanceRules != null)
            {
                foreach (DistanceSellPriceRule rule in policy.DistanceRules)
                {
                    if (rule == null || !rule.Enabled || rule.Value == 0f)
                        continue;

                    string range = rule.HasMaximumDistance
                        ? $"{FormatDistance(rule.MinimumDistanceKm)}km 이상 ~ {FormatDistance(rule.MaximumDistanceKm)}km 미만"
                        : $"{FormatDistance(rule.MinimumDistanceKm)}km 이상";
                    rows.Add($"{range} : {SellPriceModifierValueFormatter.Format(rule.Operation, rule.Value)}");
                }
            }

            string detail = rows.Count > 0 ? string.Join("\n", rows) : "적용 중인 거리 판매 보정이 없습니다.";
            return new SellPriceModifierBuffViewData(
                "거리 판매 효과",
                "일정 거리 구간마다 판매 비율이 증가합니다.",
                detail);
        }

        private static string FormatDistance(float value)
            => value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    public static class LuckyMoneyBuffViewDataBuilder
    {
        public static SellPriceModifierBuffViewData Build(
            SellPriceModifierPolicy policy,
            ND.Framework.SaveData saveData,
            Func<string, bool> isLuckyActive)
        {
            LuckyMoneySellPriceRule rule = policy != null ? policy.LuckyMoneyRule : null;
            string amount = rule != null && rule.Enabled
                ? SellPriceModifierValueFormatter.Format(rule.Operation, rule.Value)
                : "+0%";
            string normal = "로드맵의 먹구름 아래를 지나는 캐러밴은 행운의 빛을 받을 수 있습니다.\n"
                + $"행운 효과가 적용된 캐러밴은 판매 시 {amount}만큼 추가 수익을 얻습니다.\n"
                + "행운 효과는 아무것도 판매하지 않아도 소모됩니다.";

            List<string> names = BuildActiveCaravanNames(saveData, isLuckyActive);
            string detail = names.Count > 0
                ? "행운 효과 적용 캐러밴\n\n" + string.Join("\n", names)
                : "현재 행운 효과를 받는 캐러밴이 없습니다.";
            return new SellPriceModifierBuffViewData("행운 판매 효과", normal, detail);
        }

        public static List<string> BuildActiveCaravanNames(ND.Framework.SaveData saveData, Func<string, bool> isLuckyActive)
        {
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            if (saveData?.tradeProgressEntries == null || isLuckyActive == null)
                return names;

            foreach (ND.Framework.TradeProgressSaveData progress in saveData.tradeProgressEntries)
            {
                if (progress == null || string.IsNullOrWhiteSpace(progress.activeTradeId)
                    || string.IsNullOrWhiteSpace(progress.caravanId)
                    || !isLuckyActive(progress.activeTradeId)
                    || !seen.Add(progress.caravanId))
                    continue;

                string displayName = progress.caravanId;
                if (saveData.caravans != null)
                {
                    foreach (ND.Framework.CaravanSaveData caravan in saveData.caravans)
                    {
                        if (caravan == null || !string.Equals(caravan.caravanId, progress.caravanId, StringComparison.Ordinal))
                            continue;

                        displayName = string.IsNullOrWhiteSpace(caravan.displayName)
                            ? progress.caravanId
                            : caravan.displayName;
                        break;
                    }
                }

                names.Add(displayName);
            }

            return names;
        }
    }
}
