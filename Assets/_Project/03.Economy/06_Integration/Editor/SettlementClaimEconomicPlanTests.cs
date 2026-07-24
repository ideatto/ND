using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class SettlementClaimEconomicPlanTests
    {
        [Test]
        public void Build_CreatesImmutableSettlementAndRepaymentPlanWithoutMutation()
        {
            global::ND.Framework.SaveData save = ValidSave(1000L, 300L, 150L);
            save.rescueLoan = new global::ND.Framework.RescueLoanSaveData
            {
                isActive = true,
                originalPrincipal = 200L,
                remainingPrincipal = 120L
            };

            SettlementClaimPlanBuildResult result = SettlementClaimEconomicPlanBuilder.Build(
                save, "caravan-a", "trade-a", 100L, true);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Plan.ConfirmedNetProfit, Is.EqualTo(150L));
            Assert.That(result.Plan.TradeMoneyAfterSettlement, Is.EqualTo(1150L));
            Assert.That(result.Plan.RepaymentAmount, Is.EqualTo(120L));
            Assert.That(result.Plan.TradeMoneyAfterPlan, Is.EqualTo(1030L));
            Assert.That(result.Plan.RemainingPrincipalAfter, Is.Zero);
            Assert.That(result.Plan.LoanActiveAfter, Is.False);
            Assert.That(save.player.tradingCurrency, Is.EqualTo(1000L));
            Assert.That(save.rescueLoan.remainingPrincipal, Is.EqualTo(120L));
            Assert.That(save.pendingSettlements[0].claimed, Is.False);
        }

        [Test]
        public void Build_UnselectedRepaymentKeepsLoanStateInPlan()
        {
            global::ND.Framework.SaveData save = ValidSave(1000L, 300L, 150L);
            save.rescueLoan = new global::ND.Framework.RescueLoanSaveData
            {
                isActive = true,
                originalPrincipal = 200L,
                remainingPrincipal = 120L
            };

            SettlementClaimPlanBuildResult result = SettlementClaimEconomicPlanBuilder.Build(
                save, "caravan-a", "trade-a", 100L, false);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Plan.IncludesRepayment, Is.False);
            Assert.That(result.Plan.TradeMoneyAfterPlan, Is.EqualTo(1150L));
            Assert.That(result.Plan.RemainingPrincipalAfter, Is.EqualTo(120L));
            Assert.That(result.Plan.LoanActiveAfter, Is.True);
        }

        [Test]
        public void Build_RejectsRepaymentThatWouldTriggerRecovery()
        {
            global::ND.Framework.SaveData save = ValidSave(0L, 150L, 0L);
            save.rescueLoan = new global::ND.Framework.RescueLoanSaveData
            {
                isActive = true,
                originalPrincipal = 200L,
                remainingPrincipal = 200L
            };

            SettlementClaimPlanBuildResult result = SettlementClaimEconomicPlanBuilder.Build(
                save, "caravan-a", "trade-a", 100L, true);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(SettlementClaimPlanFailureReason.RepaymentRejected));
            Assert.That(result.RepaymentFailure, Is.EqualTo(RescueLoanFailureReason.RepaymentWouldTriggerRecovery));
            Assert.That(save.player.tradingCurrency, Is.Zero);
            Assert.That(save.rescueLoan.remainingPrincipal, Is.EqualTo(200L));
        }

        [Test]
        public void Build_PropagatesAdapterIdentityFailure()
        {
            global::ND.Framework.SaveData save = ValidSave(1000L, 300L, 150L);

            SettlementClaimPlanBuildResult result = SettlementClaimEconomicPlanBuilder.Build(
                save, "caravan-a", "wrong-trade", 100L, false);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(SettlementClaimPlanFailureReason.AdapterRejected));
            Assert.That(result.AdapterFailure, Is.EqualTo(SettlementEconomicAdapterFailureReason.PendingSettlementNotFound));
        }

        private static global::ND.Framework.SaveData ValidSave(long currency, long revenue, long cost)
        {
            var save = new global::ND.Framework.SaveData();
            save.caravans.Clear();
            save.tradeProgressEntries.Clear();
            save.pendingSettlements.Clear();
            save.player.tradingCurrency = currency;
            save.caravans.Add(new global::ND.Framework.CaravanSaveData
            {
                caravanId = "caravan-a",
                state = global::JourneyState.Settling
            });
            save.tradeProgressEntries.Add(new global::ND.Framework.TradeProgressSaveData
            {
                caravanId = "caravan-a",
                activeTradeId = "trade-a",
                state = global::ND.Framework.TradeProgressState.SettlementPending
            });
            save.pendingSettlements.Add(new global::ND.Framework.PendingSettlementSaveData
            {
                caravanId = "caravan-a",
                tradeId = "trade-a",
                hasResult = true,
                resultVersion = global::ND.Framework.PendingSettlementSaveData.CurrentResultVersion,
                revenue = revenue,
                cost = cost,
                netProfit = revenue - cost
            });
            return save;
        }
    }
}
