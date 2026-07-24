using System.Linq;
using NUnit.Framework;

namespace ND.Economy.Editor
{
    public sealed class TravelSettlementCalculatorTests
    {
        [Test]
        public void Execute_TravelSettlement_DoesNotCreateItemSale()
        {
            var input = new EconomyM1LoopInput
            {
                CalculateItemTrade = false,
                CurrencyState = new CurrencyState
                {
                    TradeMoney = 1000L,
                    DevelopmentCurrency = 0L
                },
                TradeId = "travel-no-sale",
                FoodCost = 10L,
                MercenaryCost = 20L,
                PriceInput = new PriceCalculationInput
                {
                    TradeItemId = string.Empty,
                    Quantity = 999,
                    BaseBuyPrice = 100L,
                    BaseSellPrice = 200L
                }
            };

            EconomyM1LoopResult result = EconomyM1LoopCalculator.Execute(input);

            Assert.That(result.Success, Is.True, result.ErrorCode);
            Assert.That(result.PriceResult, Is.Null);
            Assert.That(result.Settlement.TotalRevenue, Is.Zero);
            Assert.That(result.Settlement.GrossTradeProfit, Is.Zero);
            Assert.That(result.Settlement.NetProfit, Is.EqualTo(-30L));
            Assert.That(result.FinalCurrencyState.TradeMoney, Is.EqualTo(970L));
            Assert.That(result.Settlement.Entries
                .Where(entry => entry.EntryType == SettlementEntryType.ItemSaleRevenue)
                .All(entry => entry.Amount == 0L), Is.True);
        }

        [Test]
        public void Execute_DirectItemTrade_RemainsAvailableForLegacyCallers()
        {
            var input = new EconomyM1LoopInput
            {
                CalculateItemTrade = true,
                CurrencyState = new CurrencyState { TradeMoney = 1000L },
                TradeId = "direct-item-trade",
                PriceInput = new PriceCalculationInput
                {
                    TradeItemId = "apple",
                    FromTownId = "BaseCamp",
                    ToTownId = "TradeTown",
                    RouteId = "route-a",
                    Quantity = 2,
                    BaseBuyPrice = 10L,
                    BaseSellPrice = 20L
                }
            };

            EconomyM1LoopResult result = EconomyM1LoopCalculator.Execute(input);

            Assert.That(result.Success, Is.True, result.ErrorCode);
            Assert.That(result.PriceResult, Is.Not.Null);
            Assert.That(result.Settlement.TotalRevenue, Is.GreaterThan(0L));
        }
    }
}
