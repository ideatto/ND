using System;

namespace ND.Economy
{
    public enum DualGrowthTransactionFailureReason
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

    public interface IDualGrowthTransactionSnapshot
    {
    }

    /// <summary>
    /// Framework-owned persistence boundary for one growth-axis purchase.
    /// Stage must atomically prepare currency and both persisted growth levels.
    /// </summary>
    public interface IDualGrowthTransactionPort
    {
        IDualGrowthTransactionSnapshot CaptureSnapshot();
        bool TryStage(DualGrowthEconomicPlan plan, out string errorCode);
        bool TrySave(out string errorCode);
        bool TryRollback(
            IDualGrowthTransactionSnapshot snapshot,
            out string errorCode);
        void CommitRuntime(DualGrowthEconomicPlan plan);
        void PublishSuccess(DualGrowthEconomicPlan plan);
    }

    public sealed class DualGrowthTransactionResult
    {
        public DualGrowthTransactionResult()
        {
            ErrorCode = string.Empty;
        }

        public bool Succeeded { get; internal set; }
        public DualGrowthTransactionFailureReason FailureReason { get; internal set; }
        public string ErrorCode { get; internal set; }
        public bool SaveSucceeded { get; internal set; }
        public bool RollbackAttempted { get; internal set; }
        public bool RollbackSucceeded { get; internal set; }
        public bool RuntimeCommitted { get; internal set; }
        public bool SuccessEventPublished { get; internal set; }
    }

    public static class DualGrowthTransactionExecutor
    {
        public static DualGrowthTransactionResult Execute(
            DualGrowthEconomicPlan plan,
            IDualGrowthTransactionPort port)
        {
            if (plan == null || port == null)
                return Fail(DualGrowthTransactionFailureReason.InvalidInput, "INVALID_INPUT");

            IDualGrowthTransactionSnapshot snapshot;
            try
            {
                snapshot = port.CaptureSnapshot();
            }
            catch (Exception exception)
            {
                return Fail(
                    DualGrowthTransactionFailureReason.SnapshotCaptureFailed,
                    exception.GetType().Name);
            }
            if (snapshot == null)
            {
                return Fail(
                    DualGrowthTransactionFailureReason.SnapshotCaptureFailed,
                    "NULL_SNAPSHOT");
            }

            string errorCode;
            try
            {
                if (!port.TryStage(plan, out errorCode))
                {
                    return RollbackAfterFailure(
                        port,
                        snapshot,
                        DualGrowthTransactionFailureReason.StageFailed,
                        errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port,
                    snapshot,
                    DualGrowthTransactionFailureReason.StageFailed,
                    exception.GetType().Name);
            }

            try
            {
                if (!port.TrySave(out errorCode))
                {
                    return RollbackAfterFailure(
                        port,
                        snapshot,
                        DualGrowthTransactionFailureReason.SaveFailed,
                        errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port,
                    snapshot,
                    DualGrowthTransactionFailureReason.SaveFailed,
                    exception.GetType().Name);
            }

            var result = new DualGrowthTransactionResult
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
                result.FailureReason =
                    DualGrowthTransactionFailureReason.RuntimeCommitFailed;
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
                result.FailureReason =
                    DualGrowthTransactionFailureReason.SuccessEventFailed;
                result.ErrorCode = exception.GetType().Name;
                return result;
            }

            result.Succeeded = true;
            result.FailureReason = DualGrowthTransactionFailureReason.None;
            return result;
        }

        private static DualGrowthTransactionResult RollbackAfterFailure(
            IDualGrowthTransactionPort port,
            IDualGrowthTransactionSnapshot snapshot,
            DualGrowthTransactionFailureReason originalReason,
            string originalError)
        {
            var result = new DualGrowthTransactionResult
            {
                FailureReason = originalReason,
                ErrorCode = originalError ?? string.Empty,
                RollbackAttempted = true
            };

            string rollbackError;
            try
            {
                result.RollbackSucceeded =
                    port.TryRollback(snapshot, out rollbackError);
            }
            catch (Exception exception)
            {
                rollbackError = exception.GetType().Name;
                result.RollbackSucceeded = false;
            }

            if (!result.RollbackSucceeded)
            {
                result.FailureReason =
                    DualGrowthTransactionFailureReason.RollbackFailed;
                result.ErrorCode = string.IsNullOrEmpty(rollbackError)
                    ? originalError ?? string.Empty
                    : rollbackError;
            }
            return result;
        }

        private static DualGrowthTransactionResult Fail(
            DualGrowthTransactionFailureReason reason,
            string errorCode)
        {
            return new DualGrowthTransactionResult
            {
                FailureReason = reason,
                ErrorCode = errorCode ?? string.Empty
            };
        }
    }
}
