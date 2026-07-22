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

        [Test]
        public void CalculateMarketTransaction_RejectsCargoSlotOverflow()
        {
            MarketTransactionInput input = Input(new MarketTransactionItemInput
            {
                ItemId = "wood",
                CargoQuantityBefore = 10,
                MarketStockBefore = 10,
                BuyQuantity = 1,
                BuyUnitPrice = 10L,
                UnitWeight = 0f,
                MaxStackQuantity = 10
            });
            input.CurrentCargoSlots = 1;
            input.MaximumCargoSlots = 1;

            MarketTransactionResult result = MarketTransactionCalculator.CalculateMarketTransaction(input);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(MarketTransactionFailureReason.CargoSlotExceeded));
            Assert.That(result.CargoSlotsAfter, Is.EqualTo(result.CargoSlotsBefore));
        }

        [Test]
        public void CalculateMarketTransaction_AllowsSaleThatReducesExistingOverload()
        {
            MarketTransactionInput input = Input(new MarketTransactionItemInput
            {
                ItemId = "wood",
                CargoQuantityBefore = 10,
                MarketStockBefore = 0,
                SellQuantity = 1,
                SellUnitPrice = 10L,
                UnitWeight = 1f,
                MaxStackQuantity = 10
            });
            input.CurrentCargoWeight = 10f;
            input.MaximumCargoWeight = 5f;
            input.CurrentCargoSlots = 1;
            input.MaximumCargoSlots = 0;

            MarketTransactionResult result = MarketTransactionCalculator.CalculateMarketTransaction(input);

            Assert.That(result.Success, Is.True);
            Assert.That(result.CargoWeightAfter, Is.EqualTo(9f));
            Assert.That(result.CargoSlotsAfter, Is.EqualTo(1));
        }

        [Test]
        public void CalculateMarketTransaction_KeepsStoverMarketStockAt999AfterPurchase()
        {
            MarketTransactionResult result = MarketTransactionCalculator.CalculateMarketTransaction(
                Input(new MarketTransactionItemInput
                {
                    ItemId = "Stover",
                    MarketStockBefore = 3,
                    BuyQuantity = 2,
                    BuyUnitPrice = 1L,
                    UnitWeight = 0.1f,
                    MaxStackQuantity = 99
                }));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Items[0].MarketStockBefore, Is.EqualTo(999));
            Assert.That(result.Items[0].MarketStockAfter, Is.EqualTo(999));
        }

        [Test]
        public void CalculateMarketTransaction_KeepsStoverMarketStockAt999AfterSale()
        {
            MarketTransactionResult result = MarketTransactionCalculator.CalculateMarketTransaction(
                Input(new MarketTransactionItemInput
                {
                    ItemId = "stover",
                    CargoQuantityBefore = 2,
                    MarketStockBefore = 0,
                    SellQuantity = 1,
                    SellUnitPrice = 1L,
                    UnitWeight = 0.1f,
                    MaxStackQuantity = 99
                }));

            Assert.That(result.Success, Is.True);
            Assert.That(result.Items[0].MarketStockBefore, Is.EqualTo(999));
            Assert.That(result.Items[0].MarketStockAfter, Is.EqualTo(999));
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
