namespace ND.Economy
{
    public enum BuildingUpgradeCommandFailurePhase
    {
        None = 0,
        Validation,
        Persistence
    }

    public sealed class BuildingUpgradeCommandResult
    {
        public BuildingUpgradeCommandResult()
        {
            ErrorCode = string.Empty;
        }

        public bool Succeeded { get; internal set; }
        public BuildingUpgradeCommandFailurePhase FailurePhase { get; internal set; }
        public BuildingUpgradeFailureReason ValidationFailureReason { get; internal set; }
        public BuildingUpgradeTransactionFailureReason TransactionFailureReason { get; internal set; }
        public string ErrorCode { get; internal set; }
        public BuildingUpgradeEconomicPlan Plan { get; internal set; }
        public bool SaveSucceeded { get; internal set; }
        public bool RuntimeCommitted { get; internal set; }
        public bool SuccessEventPublished { get; internal set; }
    }

    /// <summary>
    /// Application entry point that validates, plans and persists one building level.
    /// Persistence is never called when policy validation fails.
    /// </summary>
    public static class BuildingUpgradeCommand
    {
        public static BuildingUpgradeCommandResult Execute(
            BuildingUpgradeInput input,
            IBuildingUpgradeTransactionPort port)
        {
            BuildingUpgradePlanBuildResult build =
                BuildingUpgradeEconomicPlanBuilder.Build(input);
            if (build == null || !build.Success || build.Plan == null)
            {
                BuildingUpgradeFailureReason reason = build != null
                    ? build.FailureReason
                    : BuildingUpgradeFailureReason.InvalidInput;
                return new BuildingUpgradeCommandResult
                {
                    FailurePhase = BuildingUpgradeCommandFailurePhase.Validation,
                    ValidationFailureReason = reason,
                    ErrorCode = ToValidationErrorCode(reason)
                };
            }

            BuildingUpgradeTransactionResult transaction =
                BuildingUpgradeTransactionExecutor.Execute(build.Plan, port);
            if (transaction == null || !transaction.Succeeded)
            {
                return new BuildingUpgradeCommandResult
                {
                    FailurePhase = BuildingUpgradeCommandFailurePhase.Persistence,
                    TransactionFailureReason = transaction != null
                        ? transaction.FailureReason
                        : BuildingUpgradeTransactionFailureReason.InvalidInput,
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

            return new BuildingUpgradeCommandResult
            {
                Succeeded = true,
                FailurePhase = BuildingUpgradeCommandFailurePhase.None,
                ValidationFailureReason = BuildingUpgradeFailureReason.None,
                TransactionFailureReason =
                    BuildingUpgradeTransactionFailureReason.None,
                Plan = build.Plan,
                SaveSucceeded = true,
                RuntimeCommitted = true,
                SuccessEventPublished = true
            };
        }

        private static string ToValidationErrorCode(
            BuildingUpgradeFailureReason reason)
        {
            switch (reason)
            {
                case BuildingUpgradeFailureReason.InvalidDefinition:
                    return "BUILDING_INVALID_DEFINITION";
                case BuildingUpgradeFailureReason.AlreadyMaxLevel:
                    return "BUILDING_ALREADY_MAX_LEVEL";
                case BuildingUpgradeFailureReason.LevelDefinitionNotFound:
                    return "BUILDING_LEVEL_NOT_FOUND";
                case BuildingUpgradeFailureReason.HomeInventoryCorrupted:
                    return "BUILDING_HOME_INVENTORY_CORRUPTED";
                case BuildingUpgradeFailureReason.InsufficientMaterials:
                    return "BUILDING_INSUFFICIENT_MATERIALS";
                case BuildingUpgradeFailureReason.ArithmeticOverflow:
                    return "BUILDING_ARITHMETIC_OVERFLOW";
                default:
                    return "BUILDING_INVALID_INPUT";
            }
        }
    }
}
