using UnityEngine;

/// <summary>
/// 건물 종류의 고정 정보와 레벨별 외형/건축 요구 조건을 정의하는 원본 데이터다.
/// 현재 건물 레벨이나 보유 재료처럼 플레이 중 변경되는 값은 저장하지 않는다.
/// </summary>
[CreateAssetMenu(fileName = "Build_BuildName", menuName = "Town/BuildData")]
public class BuildData : ScriptableObject
{
    [Header("Build_Default_Info")]
    [Tooltip("SaveData와 건축 진행 상태를 연결할 때 사용하는 고유 ID")]
    [SerializeField] private string buildId;

    [Tooltip("건물 목록과 Popup에 표시할 이름")]
    [SerializeField] private string displayName;

    [Header("Build_Description")]
    [TextArea(2, 8)]
    [Tooltip("건물 상세 Popup에 표시할 설명")]
    [SerializeField] private string description;

    [Header("Build_Data_Per_Level")]
    [Tooltip("각 목표 레벨의 외형 Prefab과 해당 레벨 도달에 필요한 요구 조건")]
    [SerializeField] private DataPerLevel[] dataPerLevels;

    #region
    /// <summary>공통 식별 인터페이스에서 사용하는 건물 고유 ID다.</summary>
    public string Id => buildId;

    /// <summary>건축 UI와 진행 상태 조회에서 사용하는 건물 고유 ID다.</summary>
    public string BuildId => buildId;

    /// <summary>UI에 표시할 건물 이름이다.</summary>
    public string DisplayName => displayName;

    /// <summary>건물 상세 UI에 표시할 설명이다.</summary>
    public string Description => description;

    /// <summary>
    /// 외부에서 원본 배열 자체를 변경하지 못하도록 복사본을 반환한다.
    /// 각 DataPerLevel 객체는 읽기 전용 설정으로 취급해야 한다.
    /// </summary>
    public DataPerLevel[] DataPerLevels => dataPerLevels != null ? (DataPerLevel[])dataPerLevels.Clone() : new DataPerLevel[0];

    #endregion
}
