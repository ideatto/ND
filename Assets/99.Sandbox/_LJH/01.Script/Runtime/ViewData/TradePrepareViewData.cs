using UnityEngine;

[System.Serializable]
public class TradePrepareViewData
{
    public string currentTownId;
    public string currentTownName;

    public long currentTradingCurrency;
    public long currentDevelopmentCurrency;

    public TownViewData[] towns;
    public RouteViewData[] routes;
    public TradeItemViewData[] tradeItems;

    public string selectedRouteId;

    public int currentLoad;
    public int maxLoad;

    public long totalPurchaseCost;

    public bool canStart;
    public string startDisabledReason;
}
