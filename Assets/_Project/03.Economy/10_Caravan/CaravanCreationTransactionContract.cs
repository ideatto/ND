using System;

namespace ND.Economy
{
    public enum CaravanCreationTransactionFailureReason
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

    public interface ICaravanCreationTransactionSnapshot
    {
    }

    /// <summary>
    /// Framework-owned persistence boundary for Caravan creation.
    /// Stage must atomically prepare currency deduction, slot binding,
    /// and the new Caravan save entry.
    /// </summary>
    public interface ICaravanCreationTransactionPort
    {
        ICaravanCreationTransactionSnapshot CaptureSnapshot();
        bool TryStage(
            CaravanCreationEconomicPlan plan,
            out string errorCode);
        bool TrySave(out string errorCode);
        bool TryRollback(
            ICaravanCreationTransactionSnapshot snapshot,
            out string errorCode);
        void CommitRuntime(CaravanCreationEconomicPlan plan);
        void PublishSuccess(CaravanCreationEconomicPlan plan);
    }

    public sealed class CaravanCreationTransactionResult
    {
        public CaravanCreationTransactionResult()
        {
            ErrorCode = string.Empty;
        }

        public bool Succeeded { get; internal set; }
        public CaravanCreationTransactionFailureReason FailureReason
        {
            get;
            internal set;
        }
        public string ErrorCode { get; internal set; }
        public bool SaveSucceeded { get; internal set; }
        public bool RollbackAttempted { get; internal set; }
        public bool RollbackSucceeded { get; internal set; }
        public bool RuntimeCommitted { get; internal set; }
        public bool SuccessEventPublished { get; internal set; }
    }

    public static class CaravanCreationTransactionExecutor
    {
        public static CaravanCreationTransactionResult Execute(
            CaravanCreationEconomicPlan plan,
            ICaravanCreationTransactionPort port)
        {
            if (plan == null || port == null)
            {
                return Fail(
                    CaravanCreationTransactionFailureReason.InvalidInput,
                    "INVALID_INPUT");
            }

            ICaravanCreationTransactionSnapshot snapshot;
            try
            {
                snapshot = port.CaptureSnapshot();
            }
            catch (Exception exception)
            {
                return Fail(
                    CaravanCreationTransactionFailureReason
                        .SnapshotCaptureFailed,
                    exception.GetType().Name);
            }
            if (snapshot == null)
            {
                return Fail(
                    CaravanCreationTransactionFailureReason
                        .SnapshotCaptureFailed,
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
                        CaravanCreationTransactionFailureReason.StageFailed,
                        errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port,
                    snapshot,
                    CaravanCreationTransactionFailureReason.StageFailed,
                    exception.GetType().Name);
            }

            try
            {
                if (!port.TrySave(out errorCode))
                {
                    return RollbackAfterFailure(
                        port,
                        snapshot,
                        CaravanCreationTransactionFailureReason.SaveFailed,
                        errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port,
                    snapshot,
                    CaravanCreationTransactionFailureReason.SaveFailed,
                    exception.GetType().Name);
            }

            var result = new CaravanCreationTransactionResult
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
                    CaravanCreationTransactionFailureReason
                        .RuntimeCommitFailed;
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
                    CaravanCreationTransactionFailureReason
                        .SuccessEventFailed;
                result.ErrorCode = exception.GetType().Name;
                return result;
            }

            result.Succeeded = true;
            result.FailureReason =
                CaravanCreationTransactionFailureReason.None;
            return result;
        }

        private static CaravanCreationTransactionResult RollbackAfterFailure(
            ICaravanCreationTransactionPort port,
            ICaravanCreationTransactionSnapshot snapshot,
            CaravanCreationTransactionFailureReason originalReason,
            string originalError)
        {
            var result = new CaravanCreationTransactionResult
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
                    CaravanCreationTransactionFailureReason.RollbackFailed;
                result.ErrorCode = string.IsNullOrEmpty(rollbackError)
                    ? originalError ?? string.Empty
                    : rollbackError;
            }
            return result;
        }

        private static CaravanCreationTransactionResult Fail(
            CaravanCreationTransactionFailureReason reason,
            string errorCode)
        {
            return new CaravanCreationTransactionResult
            {
                FailureReason = reason,
                ErrorCode = errorCode ?? string.Empty
            };
        }
    }
}
