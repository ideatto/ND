using UnityEngine;

[CreateAssetMenu(fileName = "Route_RouteName", menuName = "RouteData")]
public class RouteData : ScriptableObject
{
    [Header("Route_Default_Info")]
    [SerializeField] private string routeId;
    [SerializeField] private TownData fromTown;
    [SerializeField] private TownData toTown;
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

    #region Public Properties
    public string RouteId => routeId;
    public TownData FromTown => fromTown;
    public TownData ToTown => toTown;
    public string FromTownId => fromTown != null ? fromTown.TownId : string.Empty;
    public string FromTownName => fromTown != null ? fromTown.DisplayName : string.Empty;
    public string ToTownId => toTown != null ? toTown.TownId : string.Empty;
    public string ToTownName => toTown != null ? toTown.DisplayName : string.Empty;
    public string DisplayName => displayName;
    public float Distance => Mathf.Max(0f, distance);
    public bool UnlockedByDefault => unlockedByDefault;
    public float DefaultElapsedTime => Mathf.Max(0f, defaultElapsedTime);
    public int BaseFoodCost => Mathf.Max(0, baseFoodCost);
    public int BaseMercenaryCost => Mathf.Max(0, baseMercenaryCost);
    public float BaseRiskLevel => Mathf.Max(0f, baseRiskLevel);
    public RouteEventData[] RouteEvents => routeEvents != null ? (RouteEventData[])routeEvents.Clone() : new RouteEventData[0];
    public int MaxEventCount => Mathf.Max(0, maxEventCount);
    #endregion
}
