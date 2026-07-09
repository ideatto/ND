[System.Serializable]
public class TradePrepareConditionInput
{
    public bool isRouteSelected;
    public bool isRouteUnlocked;

    public long currentTradingCurrency;
    public long totalPurchaseCost;

    public float currentLoad;
    public float overloadLimit;
    public float maxLoad;

    public int loadedFoodQuantity;
    public int requiredFoodQuantity;

    public int selectedMercenaryPower;
    public int requiredMercenaryPower;
}
