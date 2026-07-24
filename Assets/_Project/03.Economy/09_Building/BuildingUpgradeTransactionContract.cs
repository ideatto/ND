using System;

namespace ND.Economy
{
    public enum BuildingUpgradeTransactionFailureReason
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

    public interface IBuildingUpgradeTransactionSnapshot
    {
    }

    /// <summary>
    /// Framework-owned persistence boundary for a building upgrade.
    /// Stage must atomically prepare both home-inventory consumption and level change.
    /// </summary>
    public interface IBuildingUpgradeTransactionPort
    {
        IBuildingUpgradeTransactionSnapshot CaptureSnapshot();
        bool TryStage(BuildingUpgradeEconomicPlan plan, out string errorCode);
        bool TrySave(out string errorCode);
        bool TryRollback(
            IBuildingUpgradeTransactionSnapshot snapshot,
            out string errorCode);
        void CommitRuntime(BuildingUpgradeEconomicPlan plan);
        void PublishSuccess(BuildingUpgradeEconomicPlan plan);
    }

    public sealed class BuildingUpgradeTransactionResult
    {
        public BuildingUpgradeTransactionResult()
        {
            ErrorCode = string.Empty;
        }

        public bool Succeeded { get; internal set; }
        public BuildingUpgradeTransactionFailureReason FailureReason { get; internal set; }
        public string ErrorCode { get; internal set; }
        public bool SaveSucceeded { get; internal set; }
        public bool RollbackAttempted { get; internal set; }
        public bool RollbackSucceeded { get; internal set; }
        public bool RuntimeCommitted { get; internal set; }
        public bool SuccessEventPublished { get; internal set; }
    }

    public static class BuildingUpgradeTransactionExecutor
    {
        public static BuildingUpgradeTransactionResult Execute(
            BuildingUpgradeEconomicPlan plan,
            IBuildingUpgradeTransactionPort port)
        {
            if (plan == null || port == null)
                return Fail(BuildingUpgradeTransactionFailureReason.InvalidInput, "INVALID_INPUT");

            IBuildingUpgradeTransactionSnapshot snapshot;
            try
            {
                snapshot = port.CaptureSnapshot();
            }
            catch (Exception exception)
            {
                return Fail(
                    BuildingUpgradeTransactionFailureReason.SnapshotCaptureFailed,
                    exception.GetType().Name);
            }
            if (snapshot == null)
            {
                return Fail(
                    BuildingUpgradeTransactionFailureReason.SnapshotCaptureFailed,
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
                        BuildingUpgradeTransactionFailureReason.StageFailed,
                        errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port,
                    snapshot,
                    BuildingUpgradeTransactionFailureReason.StageFailed,
                    exception.GetType().Name);
            }

            try
            {
                if (!port.TrySave(out errorCode))
                {
                    return RollbackAfterFailure(
                        port,
                        snapshot,
                        BuildingUpgradeTransactionFailureReason.SaveFailed,
                        errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port,
                    snapshot,
                    BuildingUpgradeTransactionFailureReason.SaveFailed,
                    exception.GetType().Name);
            }

            var result = new BuildingUpgradeTransactionResult
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
                    BuildingUpgradeTransactionFailureReason.RuntimeCommitFailed;
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
                    BuildingUpgradeTransactionFailureReason.SuccessEventFailed;
                result.ErrorCode = exception.GetType().Name;
                return result;
            }

            result.Succeeded = true;
            result.FailureReason = BuildingUpgradeTransactionFailureReason.None;
            return result;
        }

        private static BuildingUpgradeTransactionResult RollbackAfterFailure(
            IBuildingUpgradeTransactionPort port,
            IBuildingUpgradeTransactionSnapshot snapshot,
            BuildingUpgradeTransactionFailureReason originalReason,
            string originalError)
        {
            var result = new BuildingUpgradeTransactionResult
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
                    BuildingUpgradeTransactionFailureReason.RollbackFailed;
                result.ErrorCode = string.IsNullOrEmpty(rollbackError)
                    ? originalError ?? string.Empty
                    : rollbackError;
            }
            return result;
        }

        private static BuildingUpgradeTransactionResult Fail(
            BuildingUpgradeTransactionFailureReason reason,
            string errorCode)
        {
            return new BuildingUpgradeTransactionResult
            {
                FailureReason = reason,
                ErrorCode = errorCode ?? string.Empty
            };
        }
    }
}
