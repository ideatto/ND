using System;

namespace ND.Economy.Tests
{
    /// <summary>
    /// Editor-only reference implementation. Framework production code should inject its own
    /// SaveResult, runtime cache commit and event publisher while preserving this ordering.
    /// </summary>
    internal sealed class SettlementClaimSaveDataPortExample : ISettlementClaimTransactionPort
    {
        private readonly global::ND.Framework.SaveData saveData;
        private readonly Func<bool> save;
        private readonly Action<SettlementClaimEconomicPlan> commitRuntime;
        private readonly Action<SettlementClaimEconomicPlan> publishSuccess;

        public SettlementClaimSaveDataPortExample(
            global::ND.Framework.SaveData saveData,
            Func<bool> save,
            Action<SettlementClaimEconomicPlan> commitRuntime = null,
            Action<SettlementClaimEconomicPlan> publishSuccess = null)
        {
            this.saveData = saveData;
            this.save = save;
            this.commitRuntime = commitRuntime;
            this.publishSuccess = publishSuccess;
        }

        public ISettlementClaimTransactionSnapshot CaptureSnapshot()
        {
            if (saveData == null || saveData.player == null || saveData.rescueLoan == null)
                return null;

            return new Snapshot
            {
                TradeMoney = saveData.player.tradingCurrency,
                OriginalPrincipal = saveData.rescueLoan.originalPrincipal,
                RemainingPrincipal = saveData.rescueLoan.remainingPrincipal,
                LoanActive = saveData.rescueLoan.isActive,
                RestrictedPreparation = saveData.rescueLoan.isRestrictedPreparation
            };
        }

        public bool TryStage(SettlementClaimEconomicPlan plan, out string errorCode)
        {
            errorCode = string.Empty;
            if (plan == null || saveData == null || saveData.player == null || saveData.rescueLoan == null)
                return Fail("INVALID_STATE", out errorCode);
            if (saveData.player.tradingCurrency != plan.TradeMoneyBefore)
                return Fail("CURRENCY_CHANGED", out errorCode);

            global::ND.Framework.CaravanSaveData caravan;
            global::ND.Framework.TradeProgressSaveData progress;
            global::ND.Framework.PendingSettlementSaveData pending;
            if (!global::ND.Framework.SaveDataLookup.TryGetCaravan(saveData, plan.CaravanId, out caravan) ||
                !global::ND.Framework.SaveDataLookup.TryGetTradeProgress(saveData, plan.CaravanId, out progress) ||
                !global::ND.Framework.SaveDataLookup.TryGetPendingSettlement(saveData, plan.CaravanId, plan.TradeId, out pending))
                return Fail("CLAIM_IDENTITY_NOT_FOUND", out errorCode);
            if (pending.claimed || !pending.hasResult || progress.state != global::ND.Framework.TradeProgressState.SettlementPending ||
                !string.Equals(progress.activeTradeId, plan.TradeId, StringComparison.Ordinal))
                return Fail("CLAIM_STATE_CHANGED", out errorCode);
            if (pending.revenue != plan.ConfirmedRevenue || pending.cost != plan.ConfirmedCost ||
                pending.netProfit != plan.ConfirmedNetProfit)
                return Fail("CLAIM_SNAPSHOT_CHANGED", out errorCode);

            Snapshot snapshot = CaptureSnapshot() as Snapshot;
            if (snapshot == null) return Fail("SNAPSHOT_FAILED", out errorCode);
            snapshot.Caravan = caravan;
            snapshot.CaravanState = caravan.state;
            snapshot.Progress = progress;
            snapshot.ProgressState = progress.state;
            snapshot.Pending = pending;
            snapshot.PendingClaimed = pending.claimed;
            stagedSnapshot = snapshot;

            saveData.player.tradingCurrency = plan.TradeMoneyAfterPlan;
            saveData.rescueLoan.remainingPrincipal = plan.RemainingPrincipalAfter;
            saveData.rescueLoan.isActive = plan.LoanActiveAfter;
            if (!plan.LoanActiveAfter) saveData.rescueLoan.isRestrictedPreparation = false;
            pending.claimed = true;
            progress.state = global::ND.Framework.TradeProgressState.Completed;
            caravan.state = global::JourneyState.Completed;
            return true;
        }

        public bool TrySave(out string errorCode)
        {
            errorCode = string.Empty;
            if (save == null || !save())
                return Fail("SAVE_FAILED", out errorCode);
            return true;
        }

        public bool TryRollback(ISettlementClaimTransactionSnapshot snapshot, out string errorCode)
        {
            errorCode = string.Empty;
            Snapshot value = stagedSnapshot ?? snapshot as Snapshot;
            if (value == null || saveData == null || saveData.player == null || saveData.rescueLoan == null)
                return Fail("ROLLBACK_SNAPSHOT_INVALID", out errorCode);

            saveData.player.tradingCurrency = value.TradeMoney;
            saveData.rescueLoan.originalPrincipal = value.OriginalPrincipal;
            saveData.rescueLoan.remainingPrincipal = value.RemainingPrincipal;
            saveData.rescueLoan.isActive = value.LoanActive;
            saveData.rescueLoan.isRestrictedPreparation = value.RestrictedPreparation;
            if (value.Pending != null) value.Pending.claimed = value.PendingClaimed;
            if (value.Progress != null) value.Progress.state = value.ProgressState;
            if (value.Caravan != null) value.Caravan.state = value.CaravanState;
            stagedSnapshot = null;
            return true;
        }

        public void CommitRuntime(SettlementClaimEconomicPlan plan)
        {
            stagedSnapshot = null;
            if (commitRuntime != null) commitRuntime(plan);
        }

        public void PublishSuccess(SettlementClaimEconomicPlan plan)
        {
            if (publishSuccess != null) publishSuccess(plan);
        }

        private Snapshot stagedSnapshot;

        private static bool Fail(string code, out string errorCode)
        {
            errorCode = code;
            return false;
        }

        private sealed class Snapshot : ISettlementClaimTransactionSnapshot
        {
            public long TradeMoney;
            public long OriginalPrincipal;
            public long RemainingPrincipal;
            public bool LoanActive;
            public bool RestrictedPreparation;
            public global::ND.Framework.CaravanSaveData Caravan;
            public global::JourneyState CaravanState;
            public global::ND.Framework.TradeProgressSaveData Progress;
            public global::ND.Framework.TradeProgressState ProgressState;
            public global::ND.Framework.PendingSettlementSaveData Pending;
            public bool PendingClaimed;
        }
    }
}
