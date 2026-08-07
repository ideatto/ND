using System;
using System.Collections.Generic;
using System.Reflection;
using ND.Economy;
using ND.Framework;
using ND.UI.InGame.SellPriceModifierBuff;
using NUnit.Framework;
using UnityEngine;

public sealed class SellPriceModifierBuffBuilderTests
{
    [Test]
    public void SeasonCategory_UsesOnlyCurrentEnabledNonZeroRules()
    {
        var policy = ScriptableObject.CreateInstance<SellPriceModifierPolicy>();
        SetField(policy, "categorySeasonalRules", new List<CategorySeasonalSellPriceRule>
        {
            CategoryRule(GameCalendarDate.WinterId, TradeItemCategory.Food, 0.2f),
            CategoryRule(GameCalendarDate.WinterId, TradeItemCategory.Material, 0f),
            CategoryRule(GameCalendarDate.SummerId, TradeItemCategory.Food, 0.3f)
        });

        SellPriceModifierBuffViewData data = SeasonBuffViewDataBuilder.Build(policy, GameCalendarDate.WinterId, null);

        StringAssert.Contains("식량 +20%", data.NormalBody);
        StringAssert.DoesNotContain("재료", data.NormalBody);
        StringAssert.DoesNotContain("+30%", data.NormalBody);
        UnityEngine.Object.DestroyImmediate(policy);
    }

    [Test]
    public void SeasonItems_UseOnlyCurrentSeasonSellBundle()
    {
        var items = new List<TradeItemData>
        {
            TradeItem("spring", GameCalendarDate.SpringId, 0.11f, 0.10f),
            TradeItem("summer", GameCalendarDate.SummerId, 0.21f, 0.20f),
            TradeItem("autumn", GameCalendarDate.AutumnId, 0.31f, 0.30f),
            TradeItem("winter", GameCalendarDate.WinterId, 0.41f, 0.40f)
        };

        string detail = SeasonBuffViewDataBuilder.Build(null, GameCalendarDate.SpringId, items).DetailBody;

        StringAssert.Contains("spring +10%", detail);
        StringAssert.DoesNotContain("+11%", detail);
        StringAssert.DoesNotContain("+20%", detail);
        StringAssert.DoesNotContain("+21%", detail);
        StringAssert.DoesNotContain("+30%", detail);
        StringAssert.DoesNotContain("+31%", detail);
        StringAssert.DoesNotContain("+40%", detail);
        StringAssert.DoesNotContain("+41%", detail);
        Assert.That(CountItemRows(detail), Is.EqualTo(1));
        DestroyItems(items);
    }

    [TestCase("")]
    [TestCase("Summer")]
    [TestCase("fall")]
    [TestCase("겨울이")]
    public void SeasonItems_IgnoreEmptyOrInvalidSourceId(string sourceId)
    {
        var items = new List<TradeItemData> { TradeItem("invalid", sourceId, 0.10f, 0.20f) };

        string detail = SeasonBuffViewDataBuilder.Build(null, GameCalendarDate.SpringId, items).DetailBody;

        StringAssert.DoesNotContain("invalid +", detail);
        Assert.That(CountItemRows(detail), Is.Zero);
        DestroyItems(items);
    }

    [Test]
    public void SeasonItems_IgnoreItemsWithAffectModifyDisabled()
    {
        var item = TradeItem("disabled", GameCalendarDate.SpringId, 0f, 0.50f, false);

        string detail = SeasonBuffViewDataBuilder.Build(null, GameCalendarDate.SpringId, new[] { item }).DetailBody;

        StringAssert.DoesNotContain("+50%", detail);
        Assert.That(CountItemRows(detail), Is.Zero);
        UnityEngine.Object.DestroyImmediate(item);
    }

    [TestCase(0.5f, "+50%")]
    [TestCase(1f, "+100%")]
    [TestCase(-0.1f, "-10%")]
    public void LuckyPercent_FormatsValueAsPercentIncrease(float value, string expected)
    {
        Assert.That(SellPriceModifierValueFormatter.Format(PriceModifierOperation.Percent, value), Is.EqualTo(expected));
    }

    [Test]
    public void LuckyCaravans_ReturnsOnlyActiveUniqueNamesWithIdFallback()
    {
        var save = new ND.Framework.SaveData
        {
            caravans = new List<ND.Framework.CaravanSaveData>
            {
                new ND.Framework.CaravanSaveData { caravanId = "a", displayName = "붉은 사우" },
                new ND.Framework.CaravanSaveData { caravanId = "c", displayName = string.Empty }
            },
            tradeProgressEntries = new List<ND.Framework.TradeProgressSaveData>
            {
                new ND.Framework.TradeProgressSaveData { caravanId = "a", activeTradeId = "lucky-a" },
                new ND.Framework.TradeProgressSaveData { caravanId = "b", activeTradeId = "plain-b" },
                new ND.Framework.TradeProgressSaveData { caravanId = "c", activeTradeId = "lucky-c" },
                new ND.Framework.TradeProgressSaveData { caravanId = "a", activeTradeId = "lucky-a-duplicate" }
            }
        };

        List<string> names = LuckyMoneyBuffViewDataBuilder.BuildActiveCaravanNames(
            save,
            tradeId => tradeId.StartsWith("lucky", StringComparison.Ordinal));

        Assert.That(names, Is.EqualTo(new[] { "붉은 사우", "c" }));
    }

    [Test]
    public void Distance_UsesEnabledNonZeroPolicyRowsInPolicyOrder()
    {
        var policy = ScriptableObject.CreateInstance<SellPriceModifierPolicy>();
        SetField(policy, "distanceRules", new List<DistanceSellPriceRule>
        {
            DistanceRule(100f, true, 300f, 0.05f),
            DistanceRule(300f, true, 600f, 0.1f),
            DistanceRule(600f, false, 0f, 0.15f)
        });

        string detail = DistanceBuffViewDataBuilder.Build(policy).DetailBody;

        Assert.That(detail.Split('\n'), Is.EqualTo(new[]
        {
            "100km 이상 ~ 300km 미만 : +5%",
            "300km 이상 ~ 600km 미만 : +10%",
            "600km 이상 : +15%"
        }));
        UnityEngine.Object.DestroyImmediate(policy);
    }

    private static CategorySeasonalSellPriceRule CategoryRule(string seasonId, TradeItemCategory category, float value)
    {
        var rule = new CategorySeasonalSellPriceRule();
        SetField(rule, "enabled", true);
        SetField(rule, "seasonId", seasonId);
        SetField(rule, "category", category);
        SetField(rule, "operation", PriceModifierOperation.Percent);
        SetField(rule, "value", value);
        return rule;
    }

    private static DistanceSellPriceRule DistanceRule(float minimum, bool hasMaximum, float maximum, float value)
    {
        var rule = new DistanceSellPriceRule();
        SetField(rule, "enabled", true);
        SetField(rule, "minimumDistanceKm", minimum);
        SetField(rule, "hasMaximumDistance", hasMaximum);
        SetField(rule, "maximumDistanceKm", maximum);
        SetField(rule, "operation", PriceModifierOperation.Percent);
        SetField(rule, "value", value);
        return rule;
    }

    private static TradeItemData TradeItem(
        string displayName,
        string sourceId,
        float buyValue,
        float sellValue,
        bool affectModify = true)
    {
        var item = ScriptableObject.CreateInstance<TradeItemData>();
        SetField(item, "itemId", displayName);
        SetField(item, "displayName", displayName);
        SetField(item, "affectModify", affectModify);
        SetField(item, "modifiers", new[]
        {
            new ModifierInput
            {
                modifierType = ModifierType.Season,
                sourceId = sourceId,
                modifierBundles = new[]
                {
                    new ModifierBundle
                    {
                        modifierTarget = Target.BuyPrice,
                        modifierOperation = Operation.Percent,
                        value = buyValue
                    },
                    new ModifierBundle
                    {
                        modifierTarget = Target.SellPrice,
                        modifierOperation = Operation.Percent,
                        value = sellValue
                    }
                }
            }
        });
        return item;
    }

    private static int CountItemRows(string detail)
    {
        const string marker = "[개별 무역품 효과]\n";
        int markerIndex = detail.IndexOf(marker, StringComparison.Ordinal);
        string rows = detail.Substring(markerIndex + marker.Length);
        return rows == "적용 중인 개별 효과가 없습니다." ? 0 : rows.Split('\n').Length;
    }

    private static void DestroyItems(IEnumerable<TradeItemData> items)
    {
        foreach (TradeItemData item in items)
            UnityEngine.Object.DestroyImmediate(item);
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }
}
