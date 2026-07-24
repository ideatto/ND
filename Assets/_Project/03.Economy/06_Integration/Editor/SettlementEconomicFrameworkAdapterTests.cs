using NUnit.Framework;

namespace ND.Economy.Tests
{
    public sealed class SettlementEconomicFrameworkAdapterTests
    {
        [Test]
        public void TryEvaluate_UsesExactPendingIdentityAndActiveLoan()
        {
            global::ND.Framework.SaveData save = ValidSave();
            save.rescueLoan = new global::ND.Framework.RescueLoanSaveData
            {
                isActive = true,
                originalPrincipal = 200L,
                remainingPrincipal = 120L
            };

            SettlementEconomicValidationResult result;
            SettlementEconomicAdapterFailureReason failure;
            bool evaluated = SettlementEconomicFrameworkAdapter.TryEvaluate(
                save, "caravan-a", "trade-a", 100L, true, out result, out failure);

            Assert.That(evaluated, Is.True);
            Assert.That(failure, Is.EqualTo(SettlementEconomicAdapterFailureReason.None));
            Assert.That(result.IsValid, Is.True);
            Assert.That(result.Breakdown.NetProfit, Is.EqualTo(150L));
            Assert.That(result.SelectedRepaymentRequestAmount, Is.EqualTo(120L));
            Assert.That(save.player.tradingCurrency, Is.EqualTo(1000L));
            Assert.That(save.rescueLoan.remainingPrincipal, Is.EqualTo(120L));
        }

        [Test]
        public void TryCreateValidationInput_RejectsClaimedAndVersionMismatch()
        {
            global::ND.Framework.SaveData claimed = ValidSave();
            claimed.pendingSettlements[0].claimed = true;
            SettlementEconomicValidationInput input;
            SettlementEconomicAdapterFailureReason failure;
            bool claimedResult = SettlementEconomicFrameworkAdapter.TryCreateValidationInput(
                claimed, "caravan-a", "trade-a", 100L, false, out input, out failure);
            Assert.That(claimedResult, Is.False);
            Assert.That(failure, Is.EqualTo(SettlementEconomicAdapterFailureReason.AlreadyClaimed));

            global::ND.Framework.SaveData unsupported = ValidSave();
            unsupported.pendingSettlements[0].resultVersion = 999;
            bool versionResult = SettlementEconomicFrameworkAdapter.TryCreateValidationInput(
                unsupported, "caravan-a", "trade-a", 100L, false, out input, out failure);
            Assert.That(versionResult, Is.False);
            Assert.That(failure, Is.EqualTo(SettlementEconomicAdapterFailureReason.UnsupportedResultVersion));
        }

        [Test]
        public void TryCreateValidationInput_RejectsSnapshotNetMismatch()
        {
            global::ND.Framework.SaveData save = ValidSave();
            save.pendingSettlements[0].netProfit = 999L;

            SettlementEconomicValidationInput input;
            SettlementEconomicAdapterFailureReason failure;
            bool created = SettlementEconomicFrameworkAdapter.TryCreateValidationInput(
                save, "caravan-a", "trade-a", 100L, false, out input, out failure);

            Assert.That(created, Is.False);
            Assert.That(failure, Is.EqualTo(SettlementEconomicAdapterFailureReason.SnapshotNetProfitMismatch));
        }

        [Test]
        public void TryCreateValidationInput_RejectsAmbiguousPendingAndProgress()
        {
            global::ND.Framework.SaveData duplicatePending = ValidSave();
            duplicatePending.pendingSettlements.Add(CreatePending());
            SettlementEconomicValidationInput input;
            SettlementEconomicAdapterFailureReason failure;
            bool pendingResult = SettlementEconomicFrameworkAdapter.TryCreateValidationInput(
                duplicatePending, "caravan-a", "trade-a", 100L, false, out input, out failure);
            Assert.That(pendingResult, Is.False);
            Assert.That(failure, Is.EqualTo(SettlementEconomicAdapterFailureReason.AmbiguousPendingSettlement));

            global::ND.Framework.SaveData duplicateProgress = ValidSave();
            duplicateProgress.tradeProgressEntries.Add(CreateProgress());
            bool progressResult = SettlementEconomicFrameworkAdapter.TryCreateValidationInput(
                duplicateProgress, "caravan-a", "trade-a", 100L, false, out input, out failure);
            Assert.That(progressResult, Is.False);
            Assert.That(failure, Is.EqualTo(SettlementEconomicAdapterFailureReason.AmbiguousTradeProgress));
        }

        [Test]
        public void TryCreateValidationInput_RejectsAutomaticSelectionWithoutLoanThroughValidator()
        {
            global::ND.Framework.SaveData save = ValidSave();

            SettlementEconomicValidationResult result;
            SettlementEconomicAdapterFailureReason failure;
            bool evaluated = SettlementEconomicFrameworkAdapter.TryEvaluate(
                save, "caravan-a", "trade-a", 100L, true, out result, out failure);

            Assert.That(evaluated, Is.True);
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.FailureReason,
                Is.EqualTo(SettlementEconomicValidationFailureReason.RepaymentSelectionWithoutActiveLoan));
        }

        private static global::ND.Framework.SaveData ValidSave()
        {
            var save = new global::ND.Framework.SaveData();
            save.caravans.Clear();
            save.tradeProgressEntries.Clear();
            save.pendingSettlements.Clear();
            save.player.tradingCurrency = 1000L;
            save.caravans.Add(new global::ND.Framework.CaravanSaveData
            {
                caravanId = "caravan-a",
                state = global::JourneyState.Settling
            });
            save.tradeProgressEntries.Add(CreateProgress());
            save.pendingSettlements.Add(CreatePending());
            return save;
        }

        private static global::ND.Framework.TradeProgressSaveData CreateProgress()
        {
            return new global::ND.Framework.TradeProgressSaveData
            {
                caravanId = "caravan-a",
                activeTradeId = "trade-a",
                state = global::ND.Framework.TradeProgressState.SettlementPending
            };
        }

        private static global::ND.Framework.PendingSettlementSaveData CreatePending()
        {
            return new global::ND.Framework.PendingSettlementSaveData
            {
                caravanId = "caravan-a",
                tradeId = "trade-a",
                hasResult = true,
                resultVersion = global::ND.Framework.PendingSettlementSaveData.CurrentResultVersion,
                revenue = 300L,
                cost = 150L,
                netProfit = 150L,
                claimed = false
            };
        }
    }
}
