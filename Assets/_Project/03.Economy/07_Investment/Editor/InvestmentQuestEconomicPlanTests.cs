using NUnit.Framework;

namespace ND.Economy.Editor.Tests
{
    public sealed class InvestmentQuestEconomicPlanTests
    {
        [Test]
        public void Build_ValidInvestment_CreatesCompletePlan()
        {
            InvestmentQuestPlanBuildResult result =
                InvestmentQuestEconomicPlanBuilder.Build(Input());

            Assert.That(result.Success, Is.True);
            Assert.That(result.Plan.QuestId, Is.EqualTo("invest-east"));
            Assert.That(result.Plan.CaravanId, Is.EqualTo("caravan-3"));
            Assert.That(result.Plan.TradingCurrencyBefore, Is.EqualTo(500));
            Assert.That(result.Plan.TradingCurrencyCost, Is.EqualTo(150));
            Assert.That(result.Plan.TradingCurrencyAfter, Is.EqualTo(350));
            Assert.That(result.Plan.Items[0].QuantityAfter, Is.EqualTo(4));
            Assert.That(result.Plan.UnlockTownIds[0], Is.EqualTo("town-east"));
            Assert.That(result.Plan.UnlockRouteIds[0], Is.EqualTo("route-east"));
        }

        [Test]
        public void Build_CopiesMutableInputValues()
        {
            InvestmentQuestInput input = Input();

            InvestmentQuestPlanBuildResult result =
                InvestmentQuestEconomicPlanBuilder.Build(input);
            input.CaravanInventory[0].Quantity = 99;
            input.Definition.ItemCosts[0].Quantity = 88;
            input.Definition.UnlockTownIds[0] = "changed-town";

            Assert.That(result.Plan.Items[0].QuantityBefore, Is.EqualTo(7));
            Assert.That(result.Plan.Items[0].RequiredQuantity, Is.EqualTo(3));
            Assert.That(result.Plan.UnlockTownIds[0], Is.EqualTo("town-east"));
        }

        [Test]
        public void Build_AlreadyCompleted_DoesNotCreatePlan()
        {
            InvestmentQuestInput input = Input();
            input.IsAlreadyCompleted = true;

            InvestmentQuestPlanBuildResult result =
                InvestmentQuestEconomicPlanBuilder.Build(input);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(InvestmentQuestFailureReason.AlreadyCompleted));
            Assert.That(result.Plan, Is.Null);
        }

        private static InvestmentQuestInput Input()
        {
            return new InvestmentQuestInput
            {
                RequestedQuestId = "invest-east",
                CaravanId = "caravan-3",
                CanSubmitCaravanAssets = true,
                TradingCurrency = 500,
                Definition = new InvestmentQuestDefinition
                {
                    QuestId = "invest-east",
                    TradingCurrencyCost = 150,
                    ItemCosts =
                    {
                        new InvestmentItemCost
                        {
                            ItemId = "stone",
                            Quantity = 3
                        }
                    },
                    UnlockTownIds = { "town-east" },
                    UnlockRouteIds = { "route-east" }
                },
                CaravanInventory =
                {
                    new InvestmentInventoryEntry
                    {
                        ItemId = "stone",
                        Quantity = 7
                    }
                }
            };
        }
    }
}
