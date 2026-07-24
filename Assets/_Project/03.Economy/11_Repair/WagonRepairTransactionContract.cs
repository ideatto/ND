using System;

namespace ND.Economy
{
    public enum WagonRepairTransactionFailureReason
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

    public interface IWagonRepairTransactionSnapshot
    {
    }

    /// <summary>
    /// Framework-owned persistence boundary. Stage must not publish success events.
    /// </summary>
    public interface IWagonRepairTransactionPort
    {
        IWagonRepairTransactionSnapshot CaptureSnapshot();
        bool TryStage(WagonRepairEconomicPlan plan, out string errorCode);
        bool TrySave(out string errorCode);
        bool TryRollback(IWagonRepairTransactionSnapshot snapshot, out string errorCode);
        void CommitRuntime(WagonRepairEconomicPlan plan);
        void PublishSuccess(WagonRepairEconomicPlan plan);
    }

    public sealed class WagonRepairTransactionResult
    {
        public WagonRepairTransactionResult()
        {
            ErrorCode = string.Empty;
        }

        public bool Succeeded { get; internal set; }
        public WagonRepairTransactionFailureReason FailureReason { get; internal set; }
        public string ErrorCode { get; internal set; }
        public bool SaveSucceeded { get; internal set; }
        public bool RollbackAttempted { get; internal set; }
        public bool RollbackSucceeded { get; internal set; }
        public bool RuntimeCommitted { get; internal set; }
        public bool SuccessEventPublished { get; internal set; }
    }

    public static class WagonRepairTransactionExecutor
    {
        public static WagonRepairTransactionResult Execute(
            WagonRepairEconomicPlan plan,
            IWagonRepairTransactionPort port)
        {
            if (plan == null || port == null)
                return Fail(WagonRepairTransactionFailureReason.InvalidInput, "INVALID_INPUT");

            IWagonRepairTransactionSnapshot snapshot;
            try
            {
                snapshot = port.CaptureSnapshot();
            }
            catch (Exception exception)
            {
                return Fail(
                    WagonRepairTransactionFailureReason.SnapshotCaptureFailed,
                    exception.GetType().Name);
            }
            if (snapshot == null)
                return Fail(WagonRepairTransactionFailureReason.SnapshotCaptureFailed, "NULL_SNAPSHOT");

            string errorCode;
            try
            {
                if (!port.TryStage(plan, out errorCode))
                {
                    return RollbackAfterFailure(
                        port, snapshot, WagonRepairTransactionFailureReason.StageFailed, errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port, snapshot, WagonRepairTransactionFailureReason.StageFailed,
                    exception.GetType().Name);
            }

            try
            {
                if (!port.TrySave(out errorCode))
                {
                    return RollbackAfterFailure(
                        port, snapshot, WagonRepairTransactionFailureReason.SaveFailed, errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port, snapshot, WagonRepairTransactionFailureReason.SaveFailed,
                    exception.GetType().Name);
            }

            var result = new WagonRepairTransactionResult
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
                result.FailureReason = WagonRepairTransactionFailureReason.RuntimeCommitFailed;
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
                result.FailureReason = WagonRepairTransactionFailureReason.SuccessEventFailed;
                result.ErrorCode = exception.GetType().Name;
                return result;
            }

            result.Succeeded = true;
            result.FailureReason = WagonRepairTransactionFailureReason.None;
            return result;
        }

        private static WagonRepairTransactionResult RollbackAfterFailure(
            IWagonRepairTransactionPort port,
            IWagonRepairTransactionSnapshot snapshot,
            WagonRepairTransactionFailureReason originalReason,
            string originalError)
        {
            var result = new WagonRepairTransactionResult
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
                result.FailureReason = WagonRepairTransactionFailureReason.RollbackFailed;
                result.ErrorCode = string.IsNullOrEmpty(rollbackError)
                    ? originalError ?? string.Empty
                    : rollbackError;
            }
            return result;
        }

        private static WagonRepairTransactionResult Fail(
            WagonRepairTransactionFailureReason reason,
            string errorCode)
        {
            return new WagonRepairTransactionResult
            {
                FailureReason = reason,
                ErrorCode = errorCode ?? string.Empty
            };
        }
    }
}
