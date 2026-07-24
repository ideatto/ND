public interface ITradePrepareCaravanOptionProvider
{
    // Supplies the runtime Caravans that TradePrepareUI may display as departure presets.
    // Implementations own availability rules and must return a fresh, non-null snapshot.
    TradePrepareCaravanOptionViewData[] GetOptions();
}
