using System;
using UnityEngine;

/// <summary>
/// 건물 선택 후 표시되는 상세 Popup의 화면 데이터를 전달한다.
/// 표시와 버튼 활성화 판단에만 사용하며 저장 상태를 직접 변경하지 않는다.
/// </summary>
[System.Serializable]
public class BuildingDetailViewData
{
    // 선택한 건물을 식별하고 기본 정보를 표시한다.
    public string buildId = string.Empty;
    public string displayName = string.Empty;
    public string description = string.Empty;

    // currentLevel은 현재 건물 상태, targetLevel은 건축/증축 후 목표 레벨이다.
    public int currentLevel;
    public int targetLevel = 1;

    // currentLevel이 0이면 신축, 1 이상이면 증축으로 표시한다.
    public bool isConstruction;

    // 목표 레벨의 3D 건물 외형을 미리보기에 전달한다.
    public GameObject previewPrefab;

    // PlayerMainManager의 현재 보유량과 BuildData의 재료 요구량을 조합한 결과다.
    public ItemRequirementViewData[] itemRequirements = Array.Empty<ItemRequirementViewData>();

    // 모든 요구 조건을 만족할 때만 다음 단계 버튼을 활성화한다.
    public bool canProceed;
    public string disabledReason = string.Empty;
}
