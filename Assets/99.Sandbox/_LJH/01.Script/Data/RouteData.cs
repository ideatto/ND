using UnityEngine;

[CreateAssetMenu(fileName = "Route_RouteName", menuName = "Route/RouteData")]
public class RouteData : ScriptableObject, IIdentifiableData
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

    [Header("Route_Legacy_Cost_Compatibility")]
    [Tooltip("Legacy serialized field kept for external compatibility. Keep this value at 0; use BaseRequiredFoodQuantity for the route requirement.")]
    [SerializeField] private long baseFoodCost;
    [Tooltip("Legacy serialized field kept for external compatibility. Keep this value at 0; use BaseRequiredMercenaryPower for the route requirement.")]
    [SerializeField] private long baseMercenaryCost;

    [Header("Route_Base_Requirement")]
    [SerializeField] private int baseRequiredFoodQuantity;
    [SerializeField] private int baseRequiredMercenaryPower;

    [Header("Route_Risk_Info")]
    [Tooltip("Automatically calculated from the highest Combat event value.")]
    [SerializeField] private float baseRiskLevel;

    [Header("Route_EventTable_Info")]
    [Tooltip("Maximum number of non-None events that may occur during one journey.")]
    [Min(0)]
    [SerializeField] private int maxEventCount;
    [Tooltip("Distance in kilometers between route-event checks.")]
    [Min(0.1f)]
    [SerializeField] private float eventCheckIntervalKm = 10f;
    [SerializeField] private RouteEventData[] routeEvents;

    #region Public Properties
    public string Id => routeId;
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
    // Legacy accessors remain because Framework shared-data conversion still references them.
    // Route assets must keep both serialized legacy costs at 0.
    public long BaseFoodCost => baseFoodCost > 0 ? baseFoodCost : 0;
    public long BaseMercenaryCost => baseMercenaryCost > 0 ? baseMercenaryCost : 0;
    public int BaseRequiredDraftAnimalFoodQuantity => Mathf.Max(0, baseRequiredFoodQuantity);
    // Framework shared-data conversion still uses the old general name.
    public int BaseRequiredFoodQuantity => Mathf.Max(0, baseRequiredFoodQuantity);
    public int BaseRequiredMercenaryPower => Mathf.Max(0, baseRequiredMercenaryPower);
    public float BaseRiskLevel => Mathf.Max(0f, baseRiskLevel);
    public int MaxEventCount => Mathf.Max(0, maxEventCount);
    public float EventCheckIntervalKm => Mathf.Max(0.1f, eventCheckIntervalKm);
    // A check may resolve to RouteEvent.None. None must not increase the
    // number of events counted against MaxEventCount.
    public bool HasRouteEvents => routeEvents != null
        && routeEvents.Length > 0
        && MaxEventCount > 0;
    public RouteEventData[] RouteEvents => routeEvents != null ? (RouteEventData[])routeEvents.Clone() : new RouteEventData[0];

    #endregion

    private void OnEnable()
    {
        NormalizeRouteEvents();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        NormalizeRouteEvents();
    }
#endif

    private void NormalizeRouteEvents()
    {
        // Keep serialized configuration safe when an older asset has no value
        // for a newly introduced field or an invalid value is entered.
        maxEventCount = Mathf.Max(0, maxEventCount);
        eventCheckIntervalKm = Mathf.Max(0.1f, eventCheckIntervalKm);

        if (routeEvents == null || routeEvents.Length == 0)
        {
            // Risk is derived data. An empty event table has no Combat risk.
            baseRiskLevel = 0f;
            return;
        }

        int highestCombatPower = 0;

        for (int index = 0; index < routeEvents.Length; index++)
        {
            RouteEventData routeEvent = routeEvents[index];
            if (routeEvent == null)
                continue;

            routeEvent.NormalizeRewards();

            if (routeEvent.eventType == RouteEvent.Combat)
            {
                // Cache the strongest Combat candidate for UI and shared-data views.
                highestCombatPower = Mathf.Max(
                    highestCombatPower,
                    routeEvent.eventValue);
            }
        }

        baseRiskLevel = highestCombatPower;
    }
}
