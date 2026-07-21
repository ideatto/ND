using NUnit.Framework;

namespace ND.Economy.Editor
{
    public sealed class MarketTransactionCalculatorTests
    {
        [Test]
        public void CalculateMarketTransaction_AppliesBuyAndSellBreakdown()
        {
            var input = new MarketTransactionInput
            {
                TradingCurrencyBefore = 1000L,
                CurrentCargoWeight = 10f,
                MaximumCargoWeight = 20f,
                Items =
                {
                    new MarketTransactionItemInput
                    {
                        ItemId = "wood",
                        CargoQuantityBefore = 1,
                        MarketStockBefore = 10,
                        BuyQuantity = 2,
                        BuyUnitPrice = 100L,
                        SellUnitPrice = 50L,
                        UnitWeight = 2f
                    },
                    new MarketTransactionItemInput
                    {
                        ItemId = "cloth",
                        CargoQuantityBefore = 4,
                        MarketStockBefore = 3,
                        SellQuantity = 2,
                        BuyUnitPrice = 80L,
                        SellUnitPrice = 60L,
                        UnitWeight = 1f
                    }
                }
            };

            MarketTransactionResult result = MarketTransactionCalculator.CalculateMarketTransaction(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.TotalPurchaseCost, Is.EqualTo(200L));
            Assert.That(result.TotalSaleRevenue, Is.EqualTo(120L));
            Assert.That(result.TradingCurrencyAfter, Is.EqualTo(920L));
            Assert.That(result.CargoWeightAfter, Is.EqualTo(12f));
            Assert.That(result.Items[0].CargoQuantityAfter, Is.EqualTo(3));
            Assert.That(result.Items[1].CargoQuantityAfter, Is.EqualTo(2));
            Assert.That(input.TradingCurrencyBefore, Is.EqualTo(1000L));
            Assert.That(input.Items[0].CargoQuantityBefore, Is.EqualTo(1));
        }

        [Test]
        public void CalculateMarketTransaction_RejectsInsufficientCargo()
        {
            MarketTransactionResult result = MarketTransactionCalculator.CalculateMarketTransaction(
                Input(new MarketTransactionItemInput
                {
                    ItemId = "wood",
                    CargoQuantityBefore = 1,
                    MarketStockBefore = 10,
                    SellQuantity = 2,
                    SellUnitPrice = 10L,
                    UnitWeight = 1f
                }));

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(MarketTransactionFailureReason.InsufficientCargo));
        }

        [Test]
        public void CalculateMarketTransaction_RejectsWeightOverflow()
        {
            MarketTransactionInput input = Input(new MarketTransactionItemInput
            {
                ItemId = "wood",
                MarketStockBefore = 10,
                BuyQuantity = 2,
                BuyUnitPrice = 10L,
                UnitWeight = 3f
            });
            input.CurrentCargoWeight = 5f;
            input.MaximumCargoWeight = 10f;

            MarketTransactionResult result = MarketTransactionCalculator.CalculateMarketTransaction(input);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(MarketTransactionFailureReason.CargoWeightExceeded));
        }

        [Test]
        public void CalculateMarketTransaction_RejectsArithmeticOverflow()
        {
            MarketTransactionResult result = MarketTransactionCalculator.CalculateMarketTransaction(
                Input(new MarketTransactionItemInput
                {
                    ItemId = "wood",
                    MarketStockBefore = int.MaxValue,
                    BuyQuantity = int.MaxValue,
                    BuyUnitPrice = long.MaxValue,
                    UnitWeight = 0f
                }));

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(MarketTransactionFailureReason.Overflow));
        }

        private static MarketTransactionInput Input(MarketTransactionItemInput item)
        {
            var input = new MarketTransactionInput
            {
                TradingCurrencyBefore = 1000L,
                CurrentCargoWeight = 0f,
                MaximumCargoWeight = 100f
            };
            input.Items.Add(item);
            return input;
        }
    }
}
