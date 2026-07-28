using System;
using UnityEngine;

[System.Serializable]
public class DataPerLevel
{
    [Min(1)] public int level = 1;
    public GameObject buildPrefab;
    public BuildRequirement buildRequirements = new BuildRequirement(); //If level 1, requirements for constructing Level 1 building.
}

[System.Serializable]
public class BuildRequirement
{
    public BuildRequireCurrency requireCurrency = new BuildRequireCurrency();
    public BuildRequireItem[] requireItems = Array.Empty<BuildRequireItem>();
}

[System.Serializable]
public class BuildRequireCurrency
{
    public bool isRequired;
    [Min(0)] public long value;
}

[System.Serializable]
public class BuildRequireItem
{
    public string itemId = string.Empty;
    [Min(1)] public int quantity = 1;
}