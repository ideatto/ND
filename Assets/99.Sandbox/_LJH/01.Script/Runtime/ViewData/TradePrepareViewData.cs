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
    public int usedInventorySlotCount;
    public int maxInventorySlotCount;

    public long totalPurchaseCost;

    public int loadedDraftAnimalFoodQuantity;
    public int requiredDraftAnimalFoodQuantity;

    public int selectedMercenaryPower;
    public int requiredMercenaryPower;

    public TradePrepareConditionResult startCondition;

    public WagonViewData[] wagons;
    public DraftAnimalViewData[] draftAnimals;
    public CargoItemViewData[] loadedItems;
    public MercenaryViewData[] mercenaries;

    public string selectedWagonId;

    public long draftAnimalFoodCost;
    public long mercenaryCost;
    public long totalPreparationCost;
    public long estimatedSellRevenue;
    public long estimatedNetProfit;

    public float baseExpectedTravelTime;
    public float finalExpectedTravelTime;
    public float selectedMoveSpeed;
}
