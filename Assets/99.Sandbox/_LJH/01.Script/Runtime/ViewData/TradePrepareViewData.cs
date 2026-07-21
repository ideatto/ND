using UnityEngine;

[System.Serializable]
public class TradePrepareViewData
{
    // Identifies the Caravan selected before the route and departure flow begins.
    // Framework supplies this stable ID from the overview selection.
    public string selectedCaravanId;

    public string currentTownId;
    public string currentTownName;

    public long currentTradingCurrency;
    public long currentDevelopmentCurrency;

    public TownViewData[] towns;
    public RouteViewData[] routes;

    // Transitional field for the current S4 binding.
    // The overview-owned load setting flow should use CaravanLoadSettingViewData instead.
    public TradeItemViewData[] tradeItems;

    public string selectedRouteId;

    public float currentLoad;
    public float overloadLimit;
    public float maxLoad;
    public int usedInventorySlotCount;
    public int maxInventorySlotCount;

    public long totalPurchaseCost;
    public long estimatedCurrencyAfterPurchase;
    public bool canPurchaseCargo;

    public int loadedDraftAnimalFoodQuantity;
    public int requiredDraftAnimalFoodQuantity;

    public int selectedMercenaryPower;
    public int requiredMercenaryPower;

    public TradePrepareConditionResult startCondition;

    // Transitional fields for the current S3 binding.
    // The overview-owned Caravan setting flow should use CaravanSettingViewData instead.
    public WagonViewData[] wagons;
    public DraftAnimalViewData[] draftAnimals;

    // Preserves the selected Caravan cargo for departure summary and validation.
    // Editing the cargo belongs to CaravanLoadSettingViewData before this flow begins.
    public CargoItemViewData[] loadedItems;
    public MercenaryViewData[] mercenaries;

    // Preserves the assigned wagon ID for departure summary and validation.
    // Editing the wagon belongs to CaravanSettingViewData before this flow begins.
    public string selectedWagonId;

    public long draftAnimalFoodCost;
    public long mercenaryCost;
    public long totalPreparationCost;
    public long estimatedCurrencyAfterHire;
    public bool canHireSelectedMercenaries;
    public long estimatedSellRevenue;
    public long estimatedNetProfit;

    public float baseExpectedTravelTime;
    public float finalExpectedTravelTime;
    public float selectedMoveSpeed;
}
