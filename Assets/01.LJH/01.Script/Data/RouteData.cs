using UnityEngine;

[CreateAssetMenu(fileName = "Route_RouteName", menuName = "RouteData")]
public class RouteData : ScriptableObject
{
    [Header("Route_Default_Info")]
    [SerializeField] private string routeID;
    [SerializeField] private TownData startTown;
    [SerializeField] private TownData destinationTown;
    [SerializeField] private string displayName;

    [Header("Route_Travel_Info")]
    [SerializeField] private float distance;
    [SerializeField] private float defaultElapsedTime;

    [Header("Route_Recommended_Food_Quantity")]
    [SerializeField] private int recommendedFoodQuantity;

    [Header("Route_Risk_Info")]
    [SerializeField] private float riskLevel;

    [Header("Route_EventTable_Info")]
    [SerializeField] private RouteEventData[] routeEvents;
    [SerializeField] private int maxEventCount;

    [Header("Route_Lock_Info")]
    [SerializeField] private bool isLocked;

    #region
    public string RouteID => routeID;
    public TownData StartTown => startTown;
    public TownData DestinationTown => destinationTown;
    public string DisplayName => displayName;
    public float Distance => distance;
    public float DefaultElapsedTime => defaultElapsedTime;
    public int RecommendedFoodQuantity => recommendedFoodQuantity;
    public float RiskLevel => riskLevel;
    public RouteEventData[] RouteEvents => routeEvents;
    public int MaxEventCount => maxEventCount;
    public bool IsLocked => isLocked;
    #endregion
}
