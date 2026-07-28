using UnityEngine;

[System.Serializable]
public class CurrencyRequirementViewData
{
    public bool isVisible; // Used BuildRequireCurrency.isRequired

    public long ownedAmount;
    public long requiredAmount;

    public bool isSatisfied; // ownedAmount >= requiredAmount
}

[System.Serializable]
public class ItemRequirementViewData
{
    public string itemId = string.Empty;
    public string displayName = string.Empty;
    public Sprite icon;

    public int ownedQuantity;
    public int requiredQuantity;

    public bool isSatisfied;
}
