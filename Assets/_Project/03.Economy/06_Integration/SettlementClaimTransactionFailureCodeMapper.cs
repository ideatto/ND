namespace ND.Economy
{
    public static class SettlementClaimTransactionFailureCodeMapper
    {
        public const string None = "";
        public const string InvalidInput = "CLAIM_INVALID_INPUT";
        public const string SnapshotCaptureFailed = "CLAIM_SNAPSHOT_FAILED";
        public const string StageFailed = "CLAIM_STAGE_FAILED";
        public const string SaveFailed = "CLAIM_SAVE_FAILED";
        public const string RollbackFailed = "CLAIM_ROLLBACK_FAILED";
        public const string RuntimeCommitFailed = "CLAIM_RUNTIME_COMMIT_FAILED";
        public const string SuccessEventFailed = "CLAIM_SUCCESS_EVENT_FAILED";

        public static string ToStableCode(SettlementClaimTransactionResult result)
        {
            if (result == null) return InvalidInput;
            switch (result.FailureReason)
            {
                case SettlementClaimTransactionFailureReason.None: return None;
                case SettlementClaimTransactionFailureReason.InvalidInput: return InvalidInput;
                case SettlementClaimTransactionFailureReason.SnapshotCaptureFailed: return SnapshotCaptureFailed;
                case SettlementClaimTransactionFailureReason.StageFailed: return StageFailed;
                case SettlementClaimTransactionFailureReason.SaveFailed: return SaveFailed;
                case SettlementClaimTransactionFailureReason.RollbackFailed: return RollbackFailed;
                case SettlementClaimTransactionFailureReason.RuntimeCommitFailed: return RuntimeCommitFailed;
                case SettlementClaimTransactionFailureReason.SuccessEventFailed: return SuccessEventFailed;
                default: return StageFailed;
            }
        }
    }
}
