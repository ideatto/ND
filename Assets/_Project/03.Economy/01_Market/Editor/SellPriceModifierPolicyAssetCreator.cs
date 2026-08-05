using System.Collections.Generic;
using System.Reflection;
using ND.Framework;
using UnityEditor;
using UnityEngine;

namespace ND.Economy.Editor
{
    /// <summary>
    /// Creates the version-controlled default policy through Unity serialization.
    /// </summary>
    public static class SellPriceModifierPolicyAssetCreator
    {
        private const string DataFolder = "Assets/_Project/03.Economy/01_Market/Data";
        private const string AssetPath = DataFolder + "/SellPriceModifierPolicy_Default.asset";

        public static void CreateDefaultAsset()
        {
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder("Assets/_Project/03.Economy/01_Market", "Data");

            SellPriceModifierPolicy policy = AssetDatabase.LoadAssetAtPath<SellPriceModifierPolicy>(AssetPath);
            if (policy == null)
            {
                policy = ScriptableObject.CreateInstance<SellPriceModifierPolicy>();
                AssetDatabase.CreateAsset(policy, AssetPath);
            }

            SetField(policy, "categorySeasonalRules", new List<CategorySeasonalSellPriceRule>
            {
                Category("spring-food", TradeItemCategory.Food, GameCalendarDate.SpringId, 0.05f),
                Category("summer-food", TradeItemCategory.Food, GameCalendarDate.SummerId, -0.1f),
                Category("autumn-material", TradeItemCategory.Material, GameCalendarDate.AutumnId, 0.1f),
                Category("autumn-luxury", TradeItemCategory.LuxuryGoods, GameCalendarDate.AutumnId, 0.05f),
                Category("winter-food", TradeItemCategory.Food, GameCalendarDate.WinterId, 0.2f),
                Category("winter-material", TradeItemCategory.Material, GameCalendarDate.WinterId, 0.05f)
            });
            SetField(policy, "luckyMoneyRule", Lucky());
            SetField(policy, "distanceRules", new List<DistanceSellPriceRule>
            {
                Distance("distance_0_100", 0f, true, 100f, 0f),
                Distance("distance_100_300", 100f, true, 300f, 0.05f),
                Distance("distance_300_600", 300f, true, 600f, 0.1f),
                Distance("distance_600_plus", 600f, false, 0f, 0.15f)
            });

            EditorUtility.SetDirty(policy);
            AssetDatabase.SaveAssets();
        }

        private static CategorySeasonalSellPriceRule Category(
            string id,
            TradeItemCategory category,
            string seasonId,
            float value)
        {
            var rule = new CategorySeasonalSellPriceRule();
            SetField(rule, "ruleId", id);
            SetField(rule, "enabled", true);
            SetField(rule, "category", category);
            SetField(rule, "seasonId", seasonId);
            SetField(rule, "operation", PriceModifierOperation.Percent);
            SetField(rule, "value", value);
            SetField(rule, "modifierType", PriceModifierType.Season);
            return rule;
        }

        private static LuckyMoneySellPriceRule Lucky()
        {
            var rule = new LuckyMoneySellPriceRule();
            SetField(rule, "effectId", "lucky_money_default");
            SetField(rule, "enabled", true);
            SetField(rule, "operation", PriceModifierOperation.Percent);
            SetField(rule, "value", 0.5f);
            SetField(rule, "modifierType", PriceModifierType.RouteEvent);
            return rule;
        }

        private static DistanceSellPriceRule Distance(
            string id,
            float minimum,
            bool hasMaximum,
            float maximum,
            float value)
        {
            var rule = new DistanceSellPriceRule();
            SetField(rule, "ruleId", id);
            SetField(rule, "enabled", true);
            SetField(rule, "minimumDistanceKm", minimum);
            SetField(rule, "hasMaximumDistance", hasMaximum);
            SetField(rule, "maximumDistanceKm", maximum);
            SetField(rule, "operation", PriceModifierOperation.Percent);
            SetField(rule, "value", value);
            SetField(rule, "modifierType", PriceModifierType.RouteEvent);
            return rule;
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }
    }
}
