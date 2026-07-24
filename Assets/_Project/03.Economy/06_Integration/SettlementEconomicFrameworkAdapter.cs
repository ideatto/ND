using System;
using System.Collections.Generic;

namespace ND.Economy
{
    public enum SettlementEconomicAdapterFailureReason
    {
        None = 0,
        InvalidInput,
        CaravanNotFound,
        PendingSettlementNotFound,
        AmbiguousPendingSettlement,
        InvalidPendingResult,
        AlreadyClaimed,
        UnsupportedResultVersion,
        TradeProgressNotFound,
        AmbiguousTradeProgress,
        NotSettlementPending,
        IdentityMismatch,
        InvalidSnapshotAmount,
        SnapshotNetProfitMismatch,
        InvalidLoanState
    }

    public static class SettlementEconomicFrameworkAdapter
    {
        public static bool TryCreateValidationInput(
            global::ND.Framework.SaveData saveData,
            string caravanId,
            string tradeId,
            long minimumRecoveryMoney,
            bool selectLoanRepayment,
            out SettlementEconomicValidationInput input,
            out SettlementEconomicAdapterFailureReason failureReason)
        {
            input = null;
            failureReason = SettlementEconomicAdapterFailureReason.None;
            if (saveData == null || saveData.player == null || string.IsNullOrWhiteSpace(caravanId) ||
                string.IsNullOrWhiteSpace(tradeId) || minimumRecoveryMoney <= 0L)
            {
                failureReason = SettlementEconomicAdapterFailureReason.InvalidInput;
                return false;
            }

            global::ND.Framework.CaravanSaveData caravan;
            if (!global::ND.Framework.SaveDataLookup.TryGetCaravan(saveData, caravanId, out caravan))
            {
                failureReason = SettlementEconomicAdapterFailureReason.CaravanNotFound;
                return false;
            }

            global::ND.Framework.PendingSettlementSaveData pending;
            int pendingCount = FindPending(saveData.pendingSettlements, caravanId, tradeId, out pending);
            if (pendingCount == 0)
            {
                failureReason = SettlementEconomicAdapterFailureReason.PendingSettlementNotFound;
                return false;
            }
            if (pendingCount > 1)
            {
                failureReason = SettlementEconomicAdapterFailureReason.AmbiguousPendingSettlement;
                return false;
            }
            if (!pending.hasResult)
            {
                failureReason = SettlementEconomicAdapterFailureReason.InvalidPendingResult;
                return false;
            }
            if (pending.claimed)
            {
                failureReason = SettlementEconomicAdapterFailureReason.AlreadyClaimed;
                return false;
            }
            if (pending.resultVersion != global::ND.Framework.PendingSettlementSaveData.CurrentResultVersion)
            {
                failureReason = SettlementEconomicAdapterFailureReason.UnsupportedResultVersion;
                return false;
            }

            global::ND.Framework.TradeProgressSaveData progress;
            int progressCount = FindProgress(saveData.tradeProgressEntries, caravanId, out progress);
            if (progressCount == 0)
            {
                failureReason = SettlementEconomicAdapterFailureReason.TradeProgressNotFound;
                return false;
            }
            if (progressCount > 1)
            {
                failureReason = SettlementEconomicAdapterFailureReason.AmbiguousTradeProgress;
                return false;
            }
            if (progress.state != global::ND.Framework.TradeProgressState.SettlementPending)
            {
                failureReason = SettlementEconomicAdapterFailureReason.NotSettlementPending;
                return false;
            }
            if (!string.Equals(progress.activeTradeId, tradeId, StringComparison.Ordinal) ||
                !string.Equals(pending.caravanId, caravanId, StringComparison.Ordinal) ||
                !string.Equals(pending.tradeId, tradeId, StringComparison.Ordinal))
            {
                failureReason = SettlementEconomicAdapterFailureReason.IdentityMismatch;
                return false;
            }
            if (pending.revenue < 0L || pending.cost < 0L)
            {
                failureReason = SettlementEconomicAdapterFailureReason.InvalidSnapshotAmount;
                return false;
            }

            long calculatedNet;
            try
            {
                calculatedNet = checked(pending.revenue - pending.cost);
            }
            catch (OverflowException)
            {
                failureReason = SettlementEconomicAdapterFailureReason.InvalidSnapshotAmount;
                return false;
            }
            if (calculatedNet != pending.netProfit)
            {
                failureReason = SettlementEconomicAdapterFailureReason.SnapshotNetProfitMismatch;
                return false;
            }

            global::ND.Framework.RescueLoanSaveData loan = saveData.rescueLoan;
            bool hasActiveLoan = loan != null && loan.isActive;
            long remainingPrincipal = hasActiveLoan ? loan.remainingPrincipal : 0L;
            if ((hasActiveLoan && (remainingPrincipal <= 0L || loan.originalPrincipal <= 0L ||
                                   remainingPrincipal > loan.originalPrincipal)) ||
                (!hasActiveLoan && loan != null && loan.remainingPrincipal != 0L))
            {
                failureReason = SettlementEconomicAdapterFailureReason.InvalidLoanState;
                return false;
            }

            var settlement = new SettlementInput
            {
                TradeId = tradeId,
                TradeMoneyBefore = Math.Max(0L, saveData.player.tradingCurrency),
                LoanRepayment = 0L
            };
            settlement.SoldItems.Add(new SoldItemInput
            {
                TradeItemId = "confirmed_pending_snapshot",
                Quantity = 1,
                TotalBuyPrice = pending.cost,
                TotalSellPrice = pending.revenue
            });

            input = new SettlementEconomicValidationInput
            {
                Settlement = settlement,
                MinimumRecoveryMoney = minimumRecoveryMoney,
                HasActiveLoan = hasActiveLoan,
                RemainingLoanPrincipal = remainingPrincipal,
                SelectLoanRepayment = selectLoanRepayment
            };
            return true;
        }

        public static bool TryEvaluate(
            global::ND.Framework.SaveData saveData,
            string caravanId,
            string tradeId,
            long minimumRecoveryMoney,
            bool selectLoanRepayment,
            out SettlementEconomicValidationResult result,
            out SettlementEconomicAdapterFailureReason adapterFailure)
        {
            result = null;
            SettlementEconomicValidationInput input;
            if (!TryCreateValidationInput(
                saveData, caravanId, tradeId, minimumRecoveryMoney, selectLoanRepayment,
                out input, out adapterFailure))
            {
                return false;
            }

            result = SettlementEconomicValidationCalculator.Evaluate(input);
            return true;
        }

        private static int FindPending(
            IList<global::ND.Framework.PendingSettlementSaveData> entries,
            string caravanId,
            string tradeId,
            out global::ND.Framework.PendingSettlementSaveData pending)
        {
            pending = null;
            if (entries == null) return 0;
            int count = 0;
            for (int index = 0; index < entries.Count; index++)
            {
                global::ND.Framework.PendingSettlementSaveData candidate = entries[index];
                if (candidate == null || !string.Equals(candidate.caravanId, caravanId, StringComparison.Ordinal) ||
                    !string.Equals(candidate.tradeId, tradeId, StringComparison.Ordinal))
                    continue;
                pending = candidate;
                count++;
            }
            return count;
        }

        private static int FindProgress(
            IList<global::ND.Framework.TradeProgressSaveData> entries,
            string caravanId,
            out global::ND.Framework.TradeProgressSaveData progress)
        {
            progress = null;
            if (entries == null) return 0;
            int count = 0;
            for (int index = 0; index < entries.Count; index++)
            {
                global::ND.Framework.TradeProgressSaveData candidate = entries[index];
                if (candidate == null || !string.Equals(candidate.caravanId, caravanId, StringComparison.Ordinal))
                    continue;
                progress = candidate;
                count++;
            }
            return count;
        }
    }
}
