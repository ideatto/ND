using System;

namespace ND.Economy
{
    public enum SettlementClaimTransactionFailureReason
    {
        None = 0,
        InvalidInput,
        SnapshotCaptureFailed,
        StageFailed,
        SaveFailed,
        RollbackFailed,
        RuntimeCommitFailed,
        SuccessEventFailed
    }

    public interface ISettlementClaimTransactionSnapshot
    {
    }

    /// <summary>
    /// Framework-owned port for applying an immutable claim plan.
    /// Implementations stage changes on a working copy and must not publish events themselves.
    /// </summary>
    public interface ISettlementClaimTransactionPort
    {
        ISettlementClaimTransactionSnapshot CaptureSnapshot();
        bool TryStage(SettlementClaimEconomicPlan plan, out string errorCode);
        bool TrySave(out string errorCode);
        bool TryRollback(ISettlementClaimTransactionSnapshot snapshot, out string errorCode);
        void CommitRuntime(SettlementClaimEconomicPlan plan);
        void PublishSuccess(SettlementClaimEconomicPlan plan);
    }

    public sealed class SettlementClaimTransactionResult
    {
        public bool Succeeded { get; internal set; }
        public SettlementClaimTransactionFailureReason FailureReason { get; internal set; }
        public string ErrorCode { get; internal set; } = string.Empty;
        public bool SaveSucceeded { get; internal set; }
        public bool RollbackAttempted { get; internal set; }
        public bool RollbackSucceeded { get; internal set; }
        public bool RuntimeCommitted { get; internal set; }
        public bool SuccessEventPublished { get; internal set; }
    }

    /// <summary>
    /// Enforces snapshot -> stage -> save -> runtime commit -> success event ordering.
    /// </summary>
    public static class SettlementClaimTransactionExecutor
    {
        public static SettlementClaimTransactionResult Execute(
            SettlementClaimEconomicPlan plan,
            ISettlementClaimTransactionPort port)
        {
            if (plan == null || port == null)
            {
                return Fail(SettlementClaimTransactionFailureReason.InvalidInput, "INVALID_INPUT");
            }

            ISettlementClaimTransactionSnapshot snapshot;
            try
            {
                snapshot = port.CaptureSnapshot();
            }
            catch (Exception exception)
            {
                return Fail(SettlementClaimTransactionFailureReason.SnapshotCaptureFailed, exception.GetType().Name);
            }
            if (snapshot == null)
            {
                return Fail(SettlementClaimTransactionFailureReason.SnapshotCaptureFailed, "NULL_SNAPSHOT");
            }

            string errorCode;
            try
            {
                if (!port.TryStage(plan, out errorCode))
                {
                    return RollbackAfterFailure(
                        port, snapshot, SettlementClaimTransactionFailureReason.StageFailed, errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port, snapshot, SettlementClaimTransactionFailureReason.StageFailed, exception.GetType().Name);
            }

            try
            {
                if (!port.TrySave(out errorCode))
                {
                    return RollbackAfterFailure(
                        port, snapshot, SettlementClaimTransactionFailureReason.SaveFailed, errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port, snapshot, SettlementClaimTransactionFailureReason.SaveFailed, exception.GetType().Name);
            }

            var result = new SettlementClaimTransactionResult
            {
                SaveSucceeded = true
            };
            try
            {
                port.CommitRuntime(plan);
                result.RuntimeCommitted = true;
            }
            catch (Exception exception)
            {
                result.FailureReason = SettlementClaimTransactionFailureReason.RuntimeCommitFailed;
                result.ErrorCode = exception.GetType().Name;
                return result;
            }

            try
            {
                port.PublishSuccess(plan);
                result.SuccessEventPublished = true;
            }
            catch (Exception exception)
            {
                result.FailureReason = SettlementClaimTransactionFailureReason.SuccessEventFailed;
                result.ErrorCode = exception.GetType().Name;
                return result;
            }

            result.Succeeded = true;
            result.FailureReason = SettlementClaimTransactionFailureReason.None;
            return result;
        }

        private static SettlementClaimTransactionResult RollbackAfterFailure(
            ISettlementClaimTransactionPort port,
            ISettlementClaimTransactionSnapshot snapshot,
            SettlementClaimTransactionFailureReason originalReason,
            string originalError)
        {
            var result = new SettlementClaimTransactionResult
            {
                FailureReason = originalReason,
                ErrorCode = originalError ?? string.Empty,
                RollbackAttempted = true
            };
            string rollbackError;
            try
            {
                result.RollbackSucceeded = port.TryRollback(snapshot, out rollbackError);
            }
            catch (Exception exception)
            {
                rollbackError = exception.GetType().Name;
                result.RollbackSucceeded = false;
            }
            if (!result.RollbackSucceeded)
            {
                result.FailureReason = SettlementClaimTransactionFailureReason.RollbackFailed;
                result.ErrorCode = string.IsNullOrEmpty(rollbackError)
                    ? originalError ?? string.Empty
                    : rollbackError;
            }
            return result;
        }

        private static SettlementClaimTransactionResult Fail(
            SettlementClaimTransactionFailureReason reason,
            string errorCode)
        {
            return new SettlementClaimTransactionResult
            {
                FailureReason = reason,
                ErrorCode = errorCode ?? string.Empty
            };
        }
    }
}
