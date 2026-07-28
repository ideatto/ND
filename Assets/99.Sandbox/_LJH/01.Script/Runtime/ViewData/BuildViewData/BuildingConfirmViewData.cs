using System;
using UnityEngine;

[System.Serializable]
public class BuildingConfirmViewData
{
    public string buildId = string.Empty;
    public string displayName = string.Empty;

    public int targetLevel = 1;
    public bool isConstruction;

    public GameObject previewPrefab;

    public CurrencyRequirementViewData currencyRequirement = new CurrencyRequirementViewData();
    public ItemRequirementViewData[] itemRequirements = Array.Empty<ItemRequirementViewData>();

    public bool canConfirm;
    public string disabledReason = string.Empty;
}
