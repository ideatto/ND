public interface ICaravanSettingCommand
{
    // Validates and persists one Caravan setting Draft as a single Framework-owned transaction.
    // UI must keep the edit panel open when the returned result fails.
    CaravanSettingCommandResult Execute(CaravanSettingDraft draft);
}

public sealed class CaravanSettingCommandResult
{
    public bool succeeded;
    public string errorCode = string.Empty;
    public string userMessage = string.Empty;

    // Keeps successful result construction consistent across temporary and production implementations.
    public static CaravanSettingCommandResult Success()
    {
        return new CaravanSettingCommandResult { succeeded = true };
    }

    // Preserves a stable machine-readable code separately from the user-facing NoticeUI message.
    public static CaravanSettingCommandResult Failure(string errorCode, string userMessage)
    {
        return new CaravanSettingCommandResult
        {
            succeeded = false,
            errorCode = Normalize(errorCode),
            userMessage = Normalize(userMessage)
        };
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}

public static class CaravanSettingFailureCodes
{
    // These codes describe UI-visible validation boundaries; Framework may map its internal failures to them.
    public const string InvalidDraft = "INVALID_DRAFT";
    public const string CaravanNotFound = "CARAVAN_NOT_FOUND";
    public const string CaravanNotEditable = "CARAVAN_NOT_EDITABLE";
    public const string AssetNotOwned = "ASSET_NOT_OWNED";
    public const string AssetLocked = "ASSET_LOCKED";
    public const string InvalidComposition = "INVALID_COMPOSITION";
    public const string CargoCapacityExceeded = "CARGO_CAPACITY_EXCEEDED";
    public const string SaveFailed = "SAVE_FAILED";
    public const string ServiceUnavailable = "SERVICE_UNAVAILABLE";
}
