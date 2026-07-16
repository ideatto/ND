/// <summary>
/// UI contract for the Framework preparation, traveling, and settlement screen states.
/// Implementations display supplied data and must not change Framework trade state.
/// </summary>
public interface ITradeScreenView
{
    void ShowPreparation();
    void ShowTraveling(TradeProgressViewData viewData);
    // Added because hiding the entire trade root in Settlement also disables the S8/S9 views.
    // An explicit route keeps the root active while SettlementUiDataAdapter supplies Framework data.
    void ShowSettlement();
    void HideTradeScreens();
}
