/// <summary>
/// UI contract for the Framework preparation, traveling, and settlement screen states.
/// Implementations display supplied data and must not change Framework trade state.
/// </summary>
public interface ITradeScreenView
{
    void ShowPreparation();
    void ShowTraveling(TradeProgressViewData viewData);
    void HideTradeScreens();
}
