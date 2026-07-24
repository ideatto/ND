using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class InvestmentQuestPolicyCalculatorTests
    {
        [Test]
        public void Evaluate_AllCostsAvailable_ReturnsAtomicCompletion()
        {
            InvestmentQuestResult result =
                InvestmentQuestPolicyCalculator.Evaluate(Input());

            Assert.That(result.Success, Is.True);
            Assert.That(result.QuestId, Is.EqualTo("invest-north"));
            Assert.That(result.CaravanId, Is.EqualTo("caravan-2"));
            Assert.That(result.TradingCurrencyAfter, Is.EqualTo(300));
            Assert.That(result.Items[0].QuantityAfter, Is.EqualTo(2));
            Assert.That(result.UnlockTownIds, Is.EqualTo(new[] { "town-north" }));
            Assert.That(result.UnlockRouteIds, Is.EqualTo(new[] { "route-north" }));
        }

        [Test]
        public void Evaluate_InsufficientMoney_DoesNotReturnPartialPayment()
        {
            InvestmentQuestInput input = Input();
            input.TradingCurrency = 199;

            InvestmentQuestResult result =
                InvestmentQuestPolicyCalculator.Evaluate(input);

            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestFailureReason.InsufficientTradingCurrency));
            Assert.That(result.TradingCurrencyAfter, Is.EqualTo(199));
            Assert.That(result.Items, Is.Empty);
        }

        [Test]
        public void Evaluate_InsufficientItems_PreservesCurrencyAndInventory()
        {
            InvestmentQuestInput input = Input();
            input.CaravanInventory[0].Quantity = 2;

            InvestmentQuestResult result =
                InvestmentQuestPolicyCalculator.Evaluate(input);

            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestFailureReason.InsufficientItems));
            Assert.That(result.TradingCurrencyAfter, Is.EqualTo(500));
            Assert.That(result.Items[0].MissingQuantity, Is.EqualTo(1));
            Assert.That(input.CaravanInventory[0].Quantity, Is.EqualTo(2));
        }

        [Test]
        public void Evaluate_CompletedQuest_IsRejected()
        {
            InvestmentQuestInput input = Input();
            input.IsAlreadyCompleted = true;

            InvestmentQuestResult result =
                InvestmentQuestPolicyCalculator.Evaluate(input);

            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestFailureReason.AlreadyCompleted));
        }

        [Test]
        public void Evaluate_TravelingCaravanSubmission_IsRejected()
        {
            InvestmentQuestInput input = Input();
            input.CanSubmitCaravanAssets = false;

            InvestmentQuestResult result =
                InvestmentQuestPolicyCalculator.Evaluate(input);

            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestFailureReason.CaravanUnavailable));
        }

        private static InvestmentQuestInput Input()
        {
            return new InvestmentQuestInput
            {
                RequestedQuestId = "invest-north",
                CaravanId = "caravan-2",
                CanSubmitCaravanAssets = true,
                TradingCurrency = 500,
                Definition = new InvestmentQuestDefinition
                {
                    QuestId = "invest-north",
                    TradingCurrencyCost = 200,
                    ItemCosts =
                    {
                        new InvestmentItemCost
                        {
                            ItemId = "timber",
                            Quantity = 3
                        }
                    },
                    UnlockTownIds = { "town-north" },
                    UnlockRouteIds = { "route-north" }
                },
                CaravanInventory =
                {
                    new InvestmentInventoryEntry
                    {
                        ItemId = "timber",
                        Quantity = 5
                    }
                }
            };
        }
    }
}
