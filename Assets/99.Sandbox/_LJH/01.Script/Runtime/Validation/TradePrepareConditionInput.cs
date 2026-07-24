[System.Serializable]
public class TradePrepareConditionInput
{
    public bool isTradeAlreadyActive;

    // Requires an explicit departure selection only after a multi-Caravan option Provider is connected.
    // Legacy single-Caravan scenes remain usable while that Provider and panel binding are pending.
    public bool isDepartureCaravanSelectionRequired;

    // Indicates whether TradePrepareUI selected a departure Caravan for the current Draft.
    public bool isDepartureCaravanSelected;
    public bool isRouteSelected;
    public bool isRouteUnlocked;

    public bool isWagonRequired;
    public bool isWagonSelected;
    public bool isSelectedWagonOwned;
    public int currentWagonDurability;
    public WagonType selectedWagonType;
    public int selectedDraftAnimalCount;
    public int minRequiredDraftAnimalCount;
    public int maxAllowedDraftAnimalCount;
    public DraftAnimalType[] selectedDraftAnimalTypes;
    public DraftAnimalType[] eligibleDraftAnimalTypes;
    public bool hasInvalidDraftAnimalSelection;

    public bool hasCargo;
    public bool hasInvalidCargoSelection;
    public int usedInventorySlotCount;
    public int maxInventorySlotCount;

    public long currentTradingCurrency;
    public long totalPurchaseCost;
    public long totalPreparationCost;

    public float currentLoad;
    public float overloadLimit;
    public float maxLoad;

    public int loadedDraftAnimalFoodQuantity;
    public int requiredDraftAnimalFoodQuantity;

    public int selectedMercenaryPower;
    public int requiredMercenaryPower;
    public bool hasInvalidMercenarySelection;
}
