using System;
using UnityEngine;

/// <summary>
/// 최종 건축/증축 확인 Popup의 화면 데이터를 전달한다.
/// 확인 버튼 요청을 위한 표시 데이터이며 재료 차감과 저장은 수행하지 않는다.
/// </summary>
[System.Serializable]
public class BuildingConfirmViewData
{
    // Detail Popup에서 선택한 건물의 식별자와 표시 이름이다.
    public string buildId = string.Empty;
    public string displayName = string.Empty;

    // 실행 성공 시 도달할 레벨과 신축/증축 구분을 전달한다.
    public int targetLevel = 1;
    public bool isConstruction;

    // 목표 레벨의 3D 건물 외형을 최종 미리보기에 전달한다.
    public GameObject previewPrefab;

    // 확인 시점에 다시 표시할 재화 및 아이템 요구 조건이다.
    public CurrencyRequirementViewData currencyRequirement = new CurrencyRequirementViewData();
    public ItemRequirementViewData[] itemRequirements = Array.Empty<ItemRequirementViewData>();

    // 모든 요구 조건을 만족할 때만 최종 확인 버튼을 활성화한다.
    public bool canConfirm;
    public string disabledReason = string.Empty;
}
