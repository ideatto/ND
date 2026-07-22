public interface ICaravanSettingViewDataProvider
{
    // Supplies a fresh S3 snapshot for exactly one Caravan without exposing mutable runtime or SaveData objects.
    // Implementations return null when the requested Caravan cannot be resolved.
    CaravanSettingViewData GetSetting(string caravanId);
}

public interface ICaravanLoadSettingViewDataProvider
{
    // Supplies a fresh S4 snapshot for exactly one Overview-selected Caravan.
    CaravanLoadSettingViewData GetLoadSetting(string caravanId);
}

public interface ICaravanCargoCatalogProvider
{
    CaravanCargoCatalogData GetCargoCatalog(string caravanId);
}

public sealed class CaravanCargoCatalogData
{
    public TradeItemData[] items = System.Array.Empty<TradeItemData>();
    public int[] stocks = System.Array.Empty<int>();
    public long[] buyUnitPrices = System.Array.Empty<long>();
}
