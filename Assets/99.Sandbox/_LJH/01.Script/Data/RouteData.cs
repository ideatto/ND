using UnityEngine;

[CreateAssetMenu(fileName = "Route_RouteName", menuName = "RouteData")]
public class RouteData : ScriptableObject
{
    [Header("Route_Default_Info")]
    [SerializeField] private string routeId;
    [SerializeField] private string fromTownId;
    [SerializeField] private string toTownId;
    [SerializeField] private string displayName;

    [Header("Route_Unlocked_Default")]
    [SerializeField] private bool unlockedByDefault = false;

    [Header("Route_Travel_Info")]
    [SerializeField] private float distance;
    [SerializeField] private float defaultElapsedTime;

    [Header("Route_Base_Cost")]
    [SerializeField] private int baseFoodCost;
    [SerializeField] private int baseMercenaryCost;

    [Header("Route_Risk_Info")]
    [SerializeField] private float baseRiskLevel;

    [Header("Route_EventTable_Info")]
    [SerializeField] private RouteEventData[] routeEvents;
    [SerializeField] private int maxEventCount;

    #region
    public string RouteId => routeId;
    public string FromTownId => fromTownId;
    public string ToTownId => toTownId;
    public string DisplayName => displayName;
    public float Distance => distance;
    public bool UnlockedByDefault => unlockedByDefault;
    public float DefaultElapsedTime => defaultElapsedTime;
    public int BaseFoodCost => baseFoodCost;
    public int BaseMercenaryCost => baseMercenaryCost;
    public float BaseRiskLevel => baseRiskLevel;
    public RouteEventData[] RouteEvents => routeEvents;
    public int MaxEventCount => maxEventCount;
    #endregion
}
