using System;
using UnityEngine;

[System.Serializable]
public class BuildingDetailViewData
{
    public string buildId = string.Empty;
    public string displayName = string.Empty;
    public string description = string.Empty;

    public int currentLevel;
    public int targetLevel = 1;

    public bool isConstruction; // True when currentLevel is 0; used to select the button label.

    public GameObject previewPrefab;

    public CurrencyRequirementViewData currencyRequirement = new CurrencyRequirementViewData();
    public ItemRequirementViewData[] itemRequirements = Array.Empty<ItemRequirementViewData>();

    public bool canProceed;
    public string disabledReason = string.Empty;
}
