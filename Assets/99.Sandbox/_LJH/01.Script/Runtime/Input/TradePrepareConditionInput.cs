[System.Serializable]
public class TradePrepareConditionInput
{
    public bool isTradeAlreadyActive;
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
    public bool hasInvalidSellQuantity;
    public bool hasUnsupportedPreDepartureSelling;
    public int cargoTypeCount;
    public int maxSupportedCargoTypeCount;
    public int usedInventorySlotCount;
    public int maxInventorySlotCount;

    public long currentTradingCurrency;
    public long totalPurchaseCost;
    public long totalPreparationCost;

    public float currentLoad;
    public float overloadLimit;
    public float maxLoad;

    public int loadedFoodQuantity;
    public int requiredFoodQuantity;

    public int selectedMercenaryPower;
    public int requiredMercenaryPower;
    public bool hasInvalidMercenarySelection;
}
