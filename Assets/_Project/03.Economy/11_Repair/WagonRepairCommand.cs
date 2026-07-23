namespace ND.Economy
{
    public enum WagonRepairCommandFailurePhase
    {
        None = 0,
        Validation,
        Persistence
    }

    public sealed class WagonRepairCommandResult
    {
        public bool Succeeded { get; internal set; }
        public WagonRepairCommandFailurePhase FailurePhase { get; internal set; }
        public WagonRepairFailureReason ValidationFailureReason { get; internal set; }
        public WagonRepairTransactionFailureReason TransactionFailureReason { get; internal set; }
        public string ErrorCode { get; internal set; }
        public WagonRepairEconomicPlan Plan { get; internal set; }
        public bool SaveSucceeded { get; internal set; }
        public bool RuntimeCommitted { get; internal set; }
        public bool SuccessEventPublished { get; internal set; }

        public WagonRepairCommandResult()
        {
            ErrorCode = string.Empty;
        }
    }

    /// <summary>
    /// Single application entry point for repair validation and persistence.
    /// It does not call the persistence port when plan validation fails.
    /// </summary>
    public static class WagonRepairCommand
    {
        public static WagonRepairCommandResult Execute(
            string caravanId,
            WagonRepairInput input,
            IWagonRepairTransactionPort port)
        {
            WagonRepairPlanBuildResult build =
                WagonRepairEconomicPlanBuilder.Build(caravanId, input);
            if (build == null || !build.Success || build.Plan == null)
            {
                return new WagonRepairCommandResult
                {
                    FailurePhase = WagonRepairCommandFailurePhase.Validation,
                    ValidationFailureReason = build != null
                        ? build.FailureReason
                        : WagonRepairFailureReason.InvalidInput,
                    ErrorCode = ToValidationErrorCode(
                        build != null
                            ? build.FailureReason
                            : WagonRepairFailureReason.InvalidInput)
                };
            }

            WagonRepairTransactionResult transaction =
                WagonRepairTransactionExecutor.Execute(build.Plan, port);
            if (transaction == null || !transaction.Succeeded)
            {
                return new WagonRepairCommandResult
                {
                    FailurePhase = WagonRepairCommandFailurePhase.Persistence,
                    TransactionFailureReason = transaction != null
                        ? transaction.FailureReason
                        : WagonRepairTransactionFailureReason.InvalidInput,
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

            return new WagonRepairCommandResult
            {
                Succeeded = true,
                FailurePhase = WagonRepairCommandFailurePhase.None,
                ValidationFailureReason = WagonRepairFailureReason.None,
                TransactionFailureReason = WagonRepairTransactionFailureReason.None,
                Plan = build.Plan,
                SaveSucceeded = true,
                RuntimeCommitted = true,
                SuccessEventPublished = true
            };
        }

        private static string ToValidationErrorCode(WagonRepairFailureReason reason)
        {
            switch (reason)
            {
                case WagonRepairFailureReason.NoWagon:
                    return "REPAIR_NO_WAGON";
                case WagonRepairFailureReason.WagonDestroyed:
                    return "REPAIR_WAGON_DESTROYED";
                case WagonRepairFailureReason.InJourney:
                    return "REPAIR_IN_JOURNEY";
                case WagonRepairFailureReason.AlreadyFull:
                    return "REPAIR_ALREADY_FULL";
                case WagonRepairFailureReason.InvalidRepairAmount:
                    return "REPAIR_INVALID_AMOUNT";
                case WagonRepairFailureReason.ExceedsMaximumDurability:
                    return "REPAIR_EXCEEDS_MAXIMUM";
                case WagonRepairFailureReason.InvalidUnitCost:
                    return "REPAIR_INVALID_UNIT_COST";
                case WagonRepairFailureReason.InvalidRarityMultiplier:
                    return "REPAIR_INVALID_RARITY_MULTIPLIER";
                case WagonRepairFailureReason.InsufficientCurrency:
                    return "REPAIR_INSUFFICIENT_CURRENCY";
                case WagonRepairFailureReason.ArithmeticOverflow:
                    return "REPAIR_ARITHMETIC_OVERFLOW";
                default:
                    return "REPAIR_INVALID_INPUT";
            }
        }
    }
}
