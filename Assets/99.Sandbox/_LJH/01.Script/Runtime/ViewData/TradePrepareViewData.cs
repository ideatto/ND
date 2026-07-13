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

    public float currentLoad;
    public float overloadLimit;
    public float maxLoad;

    public long totalPurchaseCost;

    public int loadedFoodQuantity;
    public int requiredFoodQuantity;

    public int selectedMercenaryPower;
    public int requiredMercenaryPower;

    public TradePrepareConditionResult startCondition;

    public WagonViewData[] wagons;
    public DraftAnimalViewData[] draftAnimals;
    public MercenaryViewData[] mercenaries;

    public string selectedWagonId;

    public long mercenaryCost;
    public long totalPreparationCost;
    public long estimatedSellRevenue;
    public long estimatedNetProfit;

    public float estimatedTime;
    public float currentMoveSpeed;
}
