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

#if UNITY_EDITOR
    private void OnValidate()
    {
        // ID 입력 과정에서 실수로 들어간 앞뒤 공백을 제거한다.
        buildId = buildId?.Trim() ?? string.Empty;

        if (dataPerLevels == null)
        {
            return;
        }

        foreach (DataPerLevel levelData in dataPerLevels)
        {
            if (levelData == null)
            {
                continue;
            }

            // Inspector의 Min 속성 외에도 실제 직렬화 값을 보정한다.
            levelData.level = Mathf.Max(1, levelData.level);

            if (levelData.buildRequirements == null)
            {
                continue;
            }

            foreach (BuildRequirement requirement
                     in levelData.buildRequirements)
            {
                if (requirement == null)
                {
                    continue;
                }

                // BuildData가 내부 Serializable 객체의 검증을 실행한다.
                requirement.ValidateRequirementType();
            }
        }
    }
#endif
}
