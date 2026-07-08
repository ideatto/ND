using UnityEngine;

[System.Serializable]
public class RouteViewData
{
    public string routeId;
    public string displayName;

    public string fromTownId;
    public string fromTownName;
    public string toTownId;
    public string toTownName;

    public float distance;
    public float estimatedTime;
    public int requiredFoodQuantity;
    public int requiredMercenaryPower;
    public float riskLevel;

    public bool isUnlocked;
    public bool canSelect;
    public string disabledReason;
}
