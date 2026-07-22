using System;

[System.Serializable]
public class TradePrepareViewData
{
    // Contains the Caravan presets that can be selected inside the trade-preparation flow.
    // The Overview focus is intentionally not reused as the departure selection.
    public TradePrepareCaravanOptionViewData[] caravanOptions = Array.Empty<TradePrepareCaravanOptionViewData>();

    // Identifies the Caravan selected inside the current departure Draft.
    // UI code forwards the Provider-owned ID and never derives a replacement ID.
    public string departureCaravanId = string.Empty;

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

[System.Serializable]
public sealed class TradePrepareCaravanOptionViewData
{
    // Identifies one Caravan preset available to the current preparation session.
    public string caravanId = string.Empty;

    // Provides the user-facing preset name displayed by the selection UI.
    public string displayName = string.Empty;

    // Describes the current journey state without exposing mutable runtime data.
    public JourneyState state = JourneyState.Prepare;

    // Indicates whether this Caravan may be selected for a new departure Draft.
    public bool canSelect;

    // Explains why this Caravan cannot be selected for departure.
    // This value must remain empty while canSelect is true.
    public string disabledReason = string.Empty;
}
