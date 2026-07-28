using UnityEngine;

[CreateAssetMenu(fileName = "Build_BuildName", menuName = "Town/BuildData")]
public class BuildData : ScriptableObject
{
    [Header("Build_Default_Info")]
    [SerializeField] private string buildId;
    [SerializeField] private string displayName;

    [Header("Build_Description")]
    [TextArea(2, 8)]
    [SerializeField] private string description;

    [Header("Build_Data_Per_Level")]
    [SerializeField] private DataPerLevel[] dataPerLevels;

    #region
    public string Id => buildId;
    public string BuildId => buildId;
    public string DisplayName => displayName;
    public string Description => description;
    public DataPerLevel[] DataPerLevels => dataPerLevels != null ? (DataPerLevel[])dataPerLevels.Clone() : new DataPerLevel[0];

    #endregion
}
