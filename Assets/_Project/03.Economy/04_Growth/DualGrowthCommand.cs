namespace ND.Economy
{
    public enum DualGrowthCommandFailurePhase
    {
        None = 0,
        Validation,
        Persistence
    }

    public sealed class DualGrowthCommandResult
    {
        public DualGrowthCommandResult()
        {
            ErrorCode = string.Empty;
        }

        public bool Succeeded { get; internal set; }
        public DualGrowthCommandFailurePhase FailurePhase { get; internal set; }
        public DualGrowthFailureReason ValidationFailureReason { get; internal set; }
        public DualGrowthTransactionFailureReason TransactionFailureReason { get; internal set; }
        public string ErrorCode { get; internal set; }
        public DualGrowthEconomicPlan Plan { get; internal set; }
        public bool SaveSucceeded { get; internal set; }
        public bool RuntimeCommitted { get; internal set; }
        public bool SuccessEventPublished { get; internal set; }
    }

    public static class DualGrowthCommand
    {
        public static DualGrowthCommandResult Execute(
            DualGrowthInput input,
            IDualGrowthTransactionPort port)
        {
            DualGrowthPlanBuildResult build =
                DualGrowthEconomicPlanBuilder.Build(input);
            if (build == null || !build.Success || build.Plan == null)
            {
                DualGrowthFailureReason reason = build != null
                    ? build.FailureReason
                    : DualGrowthFailureReason.InvalidInput;
                return new DualGrowthCommandResult
                {
                    FailurePhase = DualGrowthCommandFailurePhase.Validation,
                    ValidationFailureReason = reason,
                    ErrorCode = ToValidationErrorCode(reason)
                };
            }

            DualGrowthTransactionResult transaction =
                DualGrowthTransactionExecutor.Execute(build.Plan, port);
            if (transaction == null || !transaction.Succeeded)
            {
                return new DualGrowthCommandResult
                {
                    FailurePhase = DualGrowthCommandFailurePhase.Persistence,
                    TransactionFailureReason = transaction != null
                        ? transaction.FailureReason
                        : DualGrowthTransactionFailureReason.InvalidInput,
                    ErrorCode = transaction != null
                        ? transaction.ErrorCode
                        : "NULL_TRANSACTION_RESULT",
                    Plan = build.Plan,
                    SaveSucceeded = transaction != null && transaction.SaveSucceeded,
                    RuntimeCommitted = transaction != null && transaction.RuntimeCommitted,
                    SuccessEventPublished =
                        transaction != null && transaction.SuccessEventPublished
                };
            }

            return new DualGrowthCommandResult
            {
                Succeeded = true,
                FailurePhase = DualGrowthCommandFailurePhase.None,
                ValidationFailureReason = DualGrowthFailureReason.None,
                TransactionFailureReason =
                    DualGrowthTransactionFailureReason.None,
                Plan = build.Plan,
                SaveSucceeded = true,
                RuntimeCommitted = true,
                SuccessEventPublished = true
            };
        }

        private static string ToValidationErrorCode(
            DualGrowthFailureReason reason)
        {
            switch (reason)
            {
                case DualGrowthFailureReason.InvalidDefinition:
                    return "GROWTH_INVALID_DEFINITION";
                case DualGrowthFailureReason.GrowthIdMismatch:
                    return "GROWTH_ID_MISMATCH";
                case DualGrowthFailureReason.AxisMismatch:
                    return "GROWTH_AXIS_MISMATCH";
                case DualGrowthFailureReason.AlreadyMaxLevel:
                    return "GROWTH_ALREADY_MAX_LEVEL";
                case DualGrowthFailureReason.LevelDefinitionNotFound:
                    return "GROWTH_LEVEL_NOT_FOUND";
                case DualGrowthFailureReason.InsufficientDevelopmentCurrency:
                    return "GROWTH_INSUFFICIENT_DEVELOPMENT_CURRENCY";
                case DualGrowthFailureReason.ArithmeticOverflow:
                    return "GROWTH_ARITHMETIC_OVERFLOW";
                default:
                    return "GROWTH_INVALID_INPUT";
            }
        }
    }
}
