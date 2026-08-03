using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace ND.Economy.Editor.Tests
{
    public sealed class MarketPriceModifierTests
    {
        [Test]
        public void CalculateUnitPrices_AppliesBuyAndSellModifiersIndependently()
        {
            var modifiers = new List<PriceModifierInput>
            {
                new PriceModifierInput
                {
                    ModifierType = PriceModifierType.Town,
                    Target = PriceModifierTarget.BuyPrice,
                    Operation = PriceModifierOperation.Percent,
                    Value = 0.25f
                },
                new PriceModifierInput
                {
                    ModifierType = PriceModifierType.Season,
                    Target = PriceModifierTarget.SellPrice,
                    Operation = PriceModifierOperation.Add,
                    Value = 30f
                }
            };

            PriceCalculationResult result = PriceCalculator.CalculateUnitPrices(100L, 200L, modifiers);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.UnitBuyPrice, Is.EqualTo(125L));
            Assert.That(result.UnitSellPrice, Is.EqualTo(230L));
        }

        [Test]
        public void MarketData_ItemMinimumQuantity_ClampsToOne()
        {
            MarketData market = ScriptableObject.CreateInstance<MarketData>();
            try
            {
                typeof(MarketData)
                    .GetField("itemMinimumQuantity", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(market, -10);

                Assert.That(market.ItemMinimumQuantity, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(market);
            }
        }
    }
}
