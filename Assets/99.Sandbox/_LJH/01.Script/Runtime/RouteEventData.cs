using UnityEngine;

[System.Serializable]
public class RouteEventData
{
    [SerializeField] private RouteEvent routeEvent;

    [SerializeField] private float eventValue;

    [SerializeField] private float eventReward;
    [SerializeField] private float minReward;
    [SerializeField] private float maxReward;

    #region
    public RouteEvent RouteEvent => routeEvent;
    public float EventValue => eventValue;
    public float EventReward => eventReward;
    public float MinReward => minReward;
    public float MaxReward => maxReward;
    #endregion
}
