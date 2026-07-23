using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class WagonRepairCalculatorTests
    {
        [Test]
        public void Evaluate_PartialRepair_AppliesFlooredCost()
        {
            WagonRepairResult result = WagonRepairCalculator.Evaluate(
                ValidInput(current: 40, maximum: 100, amount: 25, unitCost: 3, multiplier: 1.25, currency: 500));

            Assert.That(result.CanRepair, Is.True);
            Assert.That(result.DurabilityAfter, Is.EqualTo(65));
            Assert.That(result.RepairedDurability, Is.EqualTo(25));
            Assert.That(result.RepairCost, Is.EqualTo(93));
            Assert.That(result.CurrencyAfter, Is.EqualTo(407));
        }

        [Test]
        public void Evaluate_FullRepair_ReachesMaximumDurability()
        {
            WagonRepairResult result = WagonRepairCalculator.Evaluate(
                ValidInput(current: 80, maximum: 100, amount: 20, unitCost: 2, multiplier: 1, currency: 40));

            Assert.That(result.CanRepair, Is.True);
            Assert.That(result.DurabilityAfter, Is.EqualTo(100));
            Assert.That(result.CurrencyAfter, Is.Zero);
        }

        [Test]
        public void Evaluate_PositiveRepairWithSubUnitRawCost_ChargesAtLeastOne()
        {
            WagonRepairResult result = WagonRepairCalculator.Evaluate(
                ValidInput(current: 9, maximum: 10, amount: 1, unitCost: 1, multiplier: 0.1, currency: 1));

            Assert.That(result.CanRepair, Is.True);
            Assert.That(result.RepairCost, Is.EqualTo(1));
        }

        [TestCase(0, WagonRepairFailureReason.WagonDestroyed)]
        [TestCase(100, WagonRepairFailureReason.AlreadyFull)]
        public void Evaluate_NonRepairableDurability_IsRejected(
            int current,
            WagonRepairFailureReason expected)
        {
            WagonRepairInput input = ValidInput(current, 100, 1, 1, 1, 100);

            WagonRepairResult result = WagonRepairCalculator.Evaluate(input);

            Assert.That(result.CanRepair, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(expected));
        }

        [Test]
        public void Evaluate_AmountBeyondMaximum_IsRejected()
        {
            WagonRepairResult result = WagonRepairCalculator.Evaluate(
                ValidInput(current: 90, maximum: 100, amount: 11, unitCost: 1, multiplier: 1, currency: 100));

            Assert.That(result.CanRepair, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(WagonRepairFailureReason.ExceedsMaximumDurability));
        }

        [Test]
        public void Evaluate_InsufficientCurrency_PreservesBeforeValues()
        {
            WagonRepairResult result = WagonRepairCalculator.Evaluate(
                ValidInput(current: 50, maximum: 100, amount: 10, unitCost: 5, multiplier: 2, currency: 99));

            Assert.That(result.CanRepair, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(WagonRepairFailureReason.InsufficientCurrency));
            Assert.That(result.DurabilityAfter, Is.EqualTo(50));
            Assert.That(result.CurrencyAfter, Is.EqualTo(99));
            Assert.That(result.RepairCost, Is.EqualTo(100));
        }

        [Test]
        public void Evaluate_NonFiniteMultiplier_IsRejected()
        {
            WagonRepairResult result = WagonRepairCalculator.Evaluate(
                ValidInput(current: 50, maximum: 100, amount: 10, unitCost: 5, multiplier: double.NaN, currency: 100));

            Assert.That(result.CanRepair, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(WagonRepairFailureReason.InvalidRarityMultiplier));
        }

        [Test]
        public void Evaluate_Overflow_IsRejected()
        {
            WagonRepairResult result = WagonRepairCalculator.Evaluate(
                ValidInput(current: 1, maximum: int.MaxValue, amount: int.MaxValue - 1,
                    unitCost: long.MaxValue, multiplier: 2, currency: long.MaxValue));

            Assert.That(result.CanRepair, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(WagonRepairFailureReason.ArithmeticOverflow));
        }

        private static WagonRepairInput ValidInput(
            int current,
            int maximum,
            int amount,
            long unitCost,
            double multiplier,
            long currency)
        {
            return new WagonRepairInput
            {
                HasWagon = true,
                CurrentDurability = current,
                MaximumDurability = maximum,
                RequestedRepairAmount = amount,
                RepairCostPerDurability = unitCost,
                RarityMultiplier = multiplier,
                TradingCurrency = currency
            };
        }
    }
}
