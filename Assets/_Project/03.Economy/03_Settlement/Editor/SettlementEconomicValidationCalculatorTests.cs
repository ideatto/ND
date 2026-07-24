using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class SettlementEconomicValidationCalculatorTests
    {
        [Test]
        public void Evaluate_CalculatesPositivePayoutAndSelectedRepaymentRequest()
        {
            SettlementEconomicValidationInput input = ValidInput();
            input.HasActiveLoan = true;
            input.RemainingLoanPrincipal = 120L;
            input.SelectLoanRepayment = true;

            SettlementEconomicValidationResult result = SettlementEconomicValidationCalculator.Evaluate(input);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Breakdown.NetProfit, Is.EqualTo(150L));
            Assert.That(result.PositiveSettlementPayout, Is.EqualTo(150L));
            Assert.That(result.MaximumRepaymentFromPayout, Is.EqualTo(120L));
            Assert.That(result.SelectedRepaymentRequestAmount, Is.EqualTo(120L));
            Assert.That(result.Breakdown.Entries.Exists(e => e.EntryType == SettlementEntryType.LoanRepayment), Is.False);
        }

        [Test]
        public void Evaluate_DoesNotRequestRepaymentWithoutUserSelection()
        {
            SettlementEconomicValidationInput input = ValidInput();
            input.HasActiveLoan = true;
            input.RemainingLoanPrincipal = 100L;

            SettlementEconomicValidationResult result = SettlementEconomicValidationCalculator.Evaluate(input);

            Assert.That(result.IsValid, Is.True);
            Assert.That(result.MaximumRepaymentFromPayout, Is.EqualTo(100L));
            Assert.That(result.SelectedRepaymentRequestAmount, Is.Zero);
        }

        [Test]
        public void Evaluate_RejectsAutomaticLoanRepaymentField()
        {
            SettlementEconomicValidationInput input = ValidInput();
            input.Settlement.LoanRepayment = 1L;

            SettlementEconomicValidationResult result = SettlementEconomicValidationCalculator.Evaluate(input);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(SettlementEconomicValidationFailureReason.AutomaticLoanRepaymentNotAllowed));
        }

        [Test]
        public void EconomyM1Loop_RejectsAutomaticLoanRepaymentField()
        {
            EconomyM1LoopResult result = EconomyM1LoopCalculator.Execute(new EconomyM1LoopInput
            {
                CalculateItemTrade = false,
                CurrencyState = new CurrencyState { TradeMoney = 1000L },
                LoanRepayment = 1L
            });

            Assert.That(result.Success, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("AutomaticLoanRepaymentNotAllowed"));
            Assert.That(result.FinalCurrencyState.TradeMoney, Is.EqualTo(1000L));
        }

        [Test]
        public void Evaluate_DistinguishesLoanOfferAndRebankruptcy()
        {
            SettlementEconomicValidationInput noLoan = ValidInput();
            noLoan.Settlement.TradeMoneyBefore = 0L;
            noLoan.Settlement.SoldItems.Clear();
            noLoan.Settlement.FoodCost = 50L;
            SettlementEconomicValidationResult offer = SettlementEconomicValidationCalculator.Evaluate(noLoan);

            SettlementEconomicValidationInput activeLoan = ValidInput();
            activeLoan.Settlement.TradeMoneyBefore = 0L;
            activeLoan.Settlement.SoldItems.Clear();
            activeLoan.Settlement.FoodCost = 50L;
            activeLoan.HasActiveLoan = true;
            activeLoan.RemainingLoanPrincipal = 100L;
            SettlementEconomicValidationResult rebankrupt = SettlementEconomicValidationCalculator.Evaluate(activeLoan);

            Assert.That(offer.CanOfferRescueLoan, Is.True);
            Assert.That(offer.IsRebankrupt, Is.False);
            Assert.That(rebankrupt.CanOfferRescueLoan, Is.False);
            Assert.That(rebankrupt.IsRebankrupt, Is.True);
        }

        [Test]
        public void Evaluate_RejectsArithmeticOverflowBeforeSettlementCalculator()
        {
            SettlementEconomicValidationInput input = ValidInput();
            input.Settlement.SoldItems.Add(new SoldItemInput { TotalSellPrice = long.MaxValue });

            SettlementEconomicValidationResult result = SettlementEconomicValidationCalculator.Evaluate(input);

            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(SettlementEconomicValidationFailureReason.ArithmeticOverflow));
        }

        private static SettlementEconomicValidationInput ValidInput()
        {
            var input = new SettlementEconomicValidationInput
            {
                MinimumRecoveryMoney = 100L,
                Settlement = new SettlementInput
                {
                    TradeId = "trade-a",
                    TradeMoneyBefore = 1000L,
                    FoodCost = 50L
                }
            };
            input.Settlement.SoldItems.Add(new SoldItemInput
            {
                TradeItemId = "apple",
                Quantity = 1,
                TotalBuyPrice = 100L,
                TotalSellPrice = 300L
            });
            return input;
        }
    }
}
