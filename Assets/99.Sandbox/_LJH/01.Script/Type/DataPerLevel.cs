using System;
using UnityEngine;

[System.Serializable]
public class DataPerLevel
{
    [Min(1)] public int level;
    public GameObject buildPrefab;
    public BuildRequirement[] buildRequirements; //If level 1, requirements for constructing Level 1 building.
}

[System.Serializable]
public class BuildRequirement
{
    public RequirementType type;
    public long value; // used to TradingCurrency

    public string requirementItemId;
    public int requirementQuantity;

    public void ValidateRequirementType()
    {
        switch (type)
        {
            case RequirementType.None:
                value = 0;
                requirementItemId = string.Empty;
                requirementQuantity = 0;
                break;

            case RequirementType.TradingCurrency:
                value = Math.Max(0L, value);
                requirementItemId = string.Empty;
                requirementQuantity = 0;
                break;

            case RequirementType.TradeItem:
                value = 0;
                requirementItemId = requirementItemId?.Trim() ?? string.Empty;
                requirementQuantity = Math.Max(0, requirementQuantity);
                break;
        }
    }

}

public enum RequirementType
{
    None,
    TradingCurrency,
    TradeItem
}
