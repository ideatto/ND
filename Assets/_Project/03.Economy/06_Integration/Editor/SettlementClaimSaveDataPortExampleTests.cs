using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class SettlementClaimSaveDataPortExampleTests
    {
        [Test]
        public void ExamplePort_AppliesPlanOnlyAfterSuccessfulSave()
        {
            global::ND.Framework.SaveData save = ValidSave();
            bool committed = false;
            bool published = false;
            var port = new SettlementClaimSaveDataPortExample(
                save,
                () => true,
                plan => committed = true,
                plan => published = true);

            SettlementClaimTransactionResult result = SettlementClaimTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(save.player.tradingCurrency, Is.EqualTo(1030L));
            Assert.That(save.rescueLoan.remainingPrincipal, Is.Zero);
            Assert.That(save.rescueLoan.isActive, Is.False);
            Assert.That(save.pendingSettlements[0].claimed, Is.True);
            Assert.That(save.tradeProgressEntries[0].state, Is.EqualTo(global::ND.Framework.TradeProgressState.Completed));
            Assert.That(committed, Is.True);
            Assert.That(published, Is.True);
        }

        [Test]
        public void ExamplePort_SaveFailureRestoresEveryStagedField()
        {
            global::ND.Framework.SaveData save = ValidSave();
            bool published = false;
            var port = new SettlementClaimSaveDataPortExample(
                save,
                () => false,
                null,
                plan => published = true);

            SettlementClaimTransactionResult result = SettlementClaimTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.RollbackSucceeded, Is.True);
            Assert.That(save.player.tradingCurrency, Is.EqualTo(1000L));
            Assert.That(save.rescueLoan.remainingPrincipal, Is.EqualTo(120L));
            Assert.That(save.rescueLoan.isActive, Is.True);
            Assert.That(save.pendingSettlements[0].claimed, Is.False);
            Assert.That(save.tradeProgressEntries[0].state,
                Is.EqualTo(global::ND.Framework.TradeProgressState.SettlementPending));
            Assert.That(save.caravans[0].state, Is.EqualTo(global::JourneyState.Settling));
            Assert.That(published, Is.False);
            Assert.That(SettlementClaimTransactionFailureCodeMapper.ToStableCode(result),
                Is.EqualTo(SettlementClaimTransactionFailureCodeMapper.SaveFailed));
        }

        [Test]
        public void ExamplePort_RejectsStaleCurrencyBeforeMutation()
        {
            global::ND.Framework.SaveData save = ValidSave();
            save.player.tradingCurrency = 999L;
            var port = new SettlementClaimSaveDataPortExample(save, () => true);

            SettlementClaimTransactionResult result = SettlementClaimTransactionExecutor.Execute(Plan(), port);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(SettlementClaimTransactionFailureReason.StageFailed));
            Assert.That(save.player.tradingCurrency, Is.EqualTo(999L));
            Assert.That(save.pendingSettlements[0].claimed, Is.False);
        }

        private static SettlementClaimEconomicPlan Plan()
        {
            return new SettlementClaimEconomicPlan(
                "caravan-a", "trade-a", 300L, 150L, 150L,
                1000L, 1150L, 120L, 1030L,
                120L, 0L, false, false, false, 0L);
        }

        private static global::ND.Framework.SaveData ValidSave()
        {
            var save = new global::ND.Framework.SaveData();
            save.caravans.Clear();
            save.tradeProgressEntries.Clear();
            save.pendingSettlements.Clear();
            save.player.tradingCurrency = 1000L;
            save.rescueLoan = new global::ND.Framework.RescueLoanSaveData
            {
                isActive = true,
                originalPrincipal = 200L,
                remainingPrincipal = 120L
            };
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
                revenue = 300L,
                cost = 150L,
                netProfit = 150L
            });
            return save;
        }
    }
}
