using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class InvestmentQuestInputAdapterTests
    {
        [Test]
        public void Build_ExactIds_MapsSelectedCaravanState()
        {
            InvestmentQuestInputAdapterResult result =
                InvestmentQuestInputAdapter.Build(
                    "invest-west",
                    "caravan-2",
                    State("caravan-2"),
                    Definition("invest-west"));

            Assert.That(result.Success, Is.True);
            Assert.That(result.QuestId, Is.EqualTo("invest-west"));
            Assert.That(result.CaravanId, Is.EqualTo("caravan-2"));
            Assert.That(result.Input.TradingCurrency, Is.EqualTo(600));
            Assert.That(result.Input.CaravanInventory[0].Quantity, Is.EqualTo(5));
            Assert.That(result.Input.IsAlreadyCompleted, Is.False);
        }

        [Test]
        public void Build_DifferentCaravan_IsRejected()
        {
            InvestmentQuestInputAdapterResult result =
                InvestmentQuestInputAdapter.Build(
                    "invest-west",
                    "caravan-3",
                    State("caravan-2"),
                    Definition("invest-west"));

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    InvestmentQuestInputAdapterFailureReason.CaravanIdMismatch));
            Assert.That(result.Input, Is.Null);
        }

        [Test]
        public void Build_DifferentQuestDefinition_IsRejected()
        {
            InvestmentQuestInputAdapterResult result =
                InvestmentQuestInputAdapter.Build(
                    "invest-west",
                    "caravan-2",
                    State("caravan-2"),
                    Definition("invest-east"));

            Assert.That(result.FailureReason,
                Is.EqualTo(
                    InvestmentQuestInputAdapterFailureReason.QuestIdMismatch));
        }

        [Test]
        public void Build_CompletedQuest_IsMappedForPolicyRejection()
        {
            InvestmentQuestStateSnapshot state = State("caravan-2");
            state.CompletedQuestIds.Add("invest-west");

            InvestmentQuestInputAdapterResult adapted =
                InvestmentQuestInputAdapter.Build(
                    "invest-west",
                    "caravan-2",
                    state,
                    Definition("invest-west"));
            InvestmentQuestResult calculated =
                InvestmentQuestPolicyCalculator.Evaluate(adapted.Input);

            Assert.That(adapted.Success, Is.True);
            Assert.That(calculated.FailureReason,
                Is.EqualTo(InvestmentQuestFailureReason.AlreadyCompleted));
        }

        [Test]
        public void Build_CopiesCaravanInventory()
        {
            InvestmentQuestStateSnapshot state = State("caravan-2");

            InvestmentQuestInputAdapterResult result =
                InvestmentQuestInputAdapter.Build(
                    "invest-west",
                    "caravan-2",
                    state,
                    Definition("invest-west"));
            state.CaravanInventory[0].Quantity = 99;

            Assert.That(result.Input.CaravanInventory[0].Quantity, Is.EqualTo(5));
        }

        private static InvestmentQuestStateSnapshot State(string caravanId)
        {
            return new InvestmentQuestStateSnapshot
            {
                CaravanId = caravanId,
                CanSubmitCaravanAssets = true,
                TradingCurrency = 600,
                CaravanInventory =
                {
                    new InvestmentInventoryEntry
                    {
                        ItemId = "iron",
                        Quantity = 5
                    }
                }
            };
        }

        private static InvestmentQuestDefinition Definition(string questId)
        {
            return new InvestmentQuestDefinition
            {
                QuestId = questId,
                TradingCurrencyCost = 200,
                ItemCosts =
                {
                    new InvestmentItemCost
                    {
                        ItemId = "iron",
                        Quantity = 3
                    }
                },
                UnlockRouteIds = { "route-west" }
            };
        }
    }
}
