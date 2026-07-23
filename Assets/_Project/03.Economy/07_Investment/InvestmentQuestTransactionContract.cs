using System;

namespace ND.Economy
{
    public enum InvestmentQuestTransactionFailureReason
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

    public interface IInvestmentQuestTransactionSnapshot
    {
    }

    /// <summary>
    /// Framework-owned persistence boundary for one-shot investment completion.
    /// Stage must prepare currency, caravan items, completion and unlocks atomically.
    /// </summary>
    public interface IInvestmentQuestTransactionPort
    {
        IInvestmentQuestTransactionSnapshot CaptureSnapshot();
        bool TryStage(InvestmentQuestEconomicPlan plan, out string errorCode);
        bool TrySave(out string errorCode);
        bool TryRollback(
            IInvestmentQuestTransactionSnapshot snapshot,
            out string errorCode);
        void CommitRuntime(InvestmentQuestEconomicPlan plan);
        void PublishSuccess(InvestmentQuestEconomicPlan plan);
    }

    public sealed class InvestmentQuestTransactionResult
    {
        public InvestmentQuestTransactionResult()
        {
            ErrorCode = string.Empty;
        }

        public bool Succeeded { get; internal set; }
        public InvestmentQuestTransactionFailureReason FailureReason { get; internal set; }
        public string ErrorCode { get; internal set; }
        public bool SaveSucceeded { get; internal set; }
        public bool RollbackAttempted { get; internal set; }
        public bool RollbackSucceeded { get; internal set; }
        public bool RuntimeCommitted { get; internal set; }
        public bool SuccessEventPublished { get; internal set; }
    }

    public static class InvestmentQuestTransactionExecutor
    {
        public static InvestmentQuestTransactionResult Execute(
            InvestmentQuestEconomicPlan plan,
            IInvestmentQuestTransactionPort port)
        {
            if (plan == null || port == null)
            {
                return Fail(
                    InvestmentQuestTransactionFailureReason.InvalidInput,
                    "INVALID_INPUT");
            }

            IInvestmentQuestTransactionSnapshot snapshot;
            try
            {
                snapshot = port.CaptureSnapshot();
            }
            catch (Exception exception)
            {
                return Fail(
                    InvestmentQuestTransactionFailureReason.SnapshotCaptureFailed,
                    exception.GetType().Name);
            }
            if (snapshot == null)
            {
                return Fail(
                    InvestmentQuestTransactionFailureReason.SnapshotCaptureFailed,
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
                        InvestmentQuestTransactionFailureReason.StageFailed,
                        errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port,
                    snapshot,
                    InvestmentQuestTransactionFailureReason.StageFailed,
                    exception.GetType().Name);
            }

            try
            {
                if (!port.TrySave(out errorCode))
                {
                    return RollbackAfterFailure(
                        port,
                        snapshot,
                        InvestmentQuestTransactionFailureReason.SaveFailed,
                        errorCode);
                }
            }
            catch (Exception exception)
            {
                return RollbackAfterFailure(
                    port,
                    snapshot,
                    InvestmentQuestTransactionFailureReason.SaveFailed,
                    exception.GetType().Name);
            }

            var result = new InvestmentQuestTransactionResult
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
                    InvestmentQuestTransactionFailureReason.RuntimeCommitFailed;
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
                    InvestmentQuestTransactionFailureReason.SuccessEventFailed;
                result.ErrorCode = exception.GetType().Name;
                return result;
            }

            result.Succeeded = true;
            result.FailureReason = InvestmentQuestTransactionFailureReason.None;
            return result;
        }

        private static InvestmentQuestTransactionResult RollbackAfterFailure(
            IInvestmentQuestTransactionPort port,
            IInvestmentQuestTransactionSnapshot snapshot,
            InvestmentQuestTransactionFailureReason originalReason,
            string originalError)
        {
            var result = new InvestmentQuestTransactionResult
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
                    InvestmentQuestTransactionFailureReason.RollbackFailed;
                result.ErrorCode = string.IsNullOrEmpty(rollbackError)
                    ? originalError ?? string.Empty
                    : rollbackError;
            }
            return result;
        }

        private static InvestmentQuestTransactionResult Fail(
            InvestmentQuestTransactionFailureReason reason,
            string errorCode)
        {
            return new InvestmentQuestTransactionResult
            {
                FailureReason = reason,
                ErrorCode = errorCode ?? string.Empty
            };
        }
    }
}
