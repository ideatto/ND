using UnityEngine;

[CreateAssetMenu(fileName = "RouteData", menuName = "GameData/Route")]
public class RouteData : ScriptableObject
{
    [Header("Identity")]
    public string RouteId;
    public string DisplayName;

    [Header("Endpoints")]
    public string FromTownId;
    public string ToTownId;

    [Header("Travel")]
    public int Distance;
    public int BaseDuration;
    public int BaseRisk;
}
