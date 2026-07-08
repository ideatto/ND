using UnityEngine;

[CreateAssetMenu(fileName = "Town_TownName", menuName = "TownData")]
public class TownData : ScriptableObject
{
    [Header("Town_Default_Info")]
    [SerializeField] private string townId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Town_Unlocked_Default")]
    [SerializeField] private bool unlockedByDefault = false;

    [Header("Town_Description")]
    [TextArea(3, 10)]
    [SerializeField] private string description;

    [Header("Town_Market_Info")]
    [SerializeField] private MarketData market;

    [Header("Town_Available_Route_Info")]
    [SerializeField] private RouteData[] availableRoutes;

    [Header("Town_Maximum_Contribution_Info")]
    [SerializeField] private bool canContribute;
    [SerializeField] private float maximumContributionLimit;

    #region Public Properties
    public string TownId => townId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public bool UnlockedByDefault => unlockedByDefault;
    public string Description => description;
    public MarketData Market => market;
    public RouteData[] AvailableRoutes => availableRoutes;
    public bool CanContribute => canContribute;
    public float MaximumContributionLimit => Mathf.Max(0, maximumContributionLimit);
    #endregion
}
