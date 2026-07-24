namespace ND.Economy
{
    public enum InvestmentQuestCommandFailurePhase
    {
        None = 0,
        Validation,
        Persistence
    }

    public sealed class InvestmentQuestCommandResult
    {
        public InvestmentQuestCommandResult()
        {
            ErrorCode = string.Empty;
        }

        public bool Succeeded { get; internal set; }
        public InvestmentQuestCommandFailurePhase FailurePhase { get; internal set; }
        public InvestmentQuestFailureReason ValidationFailureReason { get; internal set; }
        public InvestmentQuestTransactionFailureReason TransactionFailureReason { get; internal set; }
        public string ErrorCode { get; internal set; }
        public InvestmentQuestEconomicPlan Plan { get; internal set; }
        public bool SaveSucceeded { get; internal set; }
        public bool RuntimeCommitted { get; internal set; }
        public bool SuccessEventPublished { get; internal set; }
    }

    public static class InvestmentQuestCommand
    {
        public static InvestmentQuestCommandResult Execute(
            InvestmentQuestInput input,
            IInvestmentQuestTransactionPort port)
        {
            InvestmentQuestPlanBuildResult build =
                InvestmentQuestEconomicPlanBuilder.Build(input);
            if (build == null || !build.Success || build.Plan == null)
            {
                InvestmentQuestFailureReason reason = build != null
                    ? build.FailureReason
                    : InvestmentQuestFailureReason.InvalidInput;
                return new InvestmentQuestCommandResult
                {
                    FailurePhase = InvestmentQuestCommandFailurePhase.Validation,
                    ValidationFailureReason = reason,
                    ErrorCode = ToValidationErrorCode(reason)
                };
            }

            InvestmentQuestTransactionResult transaction =
                InvestmentQuestTransactionExecutor.Execute(build.Plan, port);
            if (transaction == null || !transaction.Succeeded)
            {
                return new InvestmentQuestCommandResult
                {
                    FailurePhase = InvestmentQuestCommandFailurePhase.Persistence,
                    TransactionFailureReason = transaction != null
                        ? transaction.FailureReason
                        : InvestmentQuestTransactionFailureReason.InvalidInput,
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

            return new InvestmentQuestCommandResult
            {
                Succeeded = true,
                FailurePhase = InvestmentQuestCommandFailurePhase.None,
                ValidationFailureReason = InvestmentQuestFailureReason.None,
                TransactionFailureReason =
                    InvestmentQuestTransactionFailureReason.None,
                Plan = build.Plan,
                SaveSucceeded = true,
                RuntimeCommitted = true,
                SuccessEventPublished = true
            };
        }

        private static string ToValidationErrorCode(
            InvestmentQuestFailureReason reason)
        {
            switch (reason)
            {
                case InvestmentQuestFailureReason.InvalidDefinition:
                    return "INVESTMENT_INVALID_DEFINITION";
                case InvestmentQuestFailureReason.AlreadyCompleted:
                    return "INVESTMENT_ALREADY_COMPLETED";
                case InvestmentQuestFailureReason.CaravanUnavailable:
                    return "INVESTMENT_CARAVAN_UNAVAILABLE";
                case InvestmentQuestFailureReason.InsufficientTradingCurrency:
                    return "INVESTMENT_INSUFFICIENT_TRADING_CURRENCY";
                case InvestmentQuestFailureReason.InventoryCorrupted:
                    return "INVESTMENT_INVENTORY_CORRUPTED";
                case InvestmentQuestFailureReason.InsufficientItems:
                    return "INVESTMENT_INSUFFICIENT_ITEMS";
                case InvestmentQuestFailureReason.ArithmeticOverflow:
                    return "INVESTMENT_ARITHMETIC_OVERFLOW";
                default:
                    return "INVESTMENT_INVALID_INPUT";
            }
        }
    }
}
