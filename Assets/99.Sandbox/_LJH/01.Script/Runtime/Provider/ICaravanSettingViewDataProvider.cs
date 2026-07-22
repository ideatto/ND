public interface ICaravanSettingViewDataProvider
{
    // Supplies a fresh S3 snapshot for exactly one Caravan without exposing mutable runtime or SaveData objects.
    // Implementations return null when the requested Caravan cannot be resolved.
    CaravanSettingViewData GetSetting(string caravanId);
}
