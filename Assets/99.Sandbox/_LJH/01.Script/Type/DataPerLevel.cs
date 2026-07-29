using System;
using UnityEngine;

/// <summary>
/// 건물이 특정 목표 레벨에 도달했을 때 사용할 외형과,
/// 해당 레벨을 건축/증축하기 위해 필요한 조건을 정의한다.
/// </summary>
[System.Serializable]
public class DataPerLevel
{
    [Tooltip("이 항목이 정의하는 목표 건물 레벨")]
    [Min(1)] public int level = 1;

    [Tooltip("해당 레벨의 실제 건물 외형 및 UI 미리보기에 사용할 Prefab")]
    public GameObject buildPrefab;

    [Tooltip("현재 레벨에서 이 목표 레벨로 올라갈 때 필요한 재화와 아이템")]
    public BuildRequirement buildRequirements = new BuildRequirement();
}

/// <summary>
/// 목표 레벨에 도달하기 위해 필요한 재화와 아이템 요구 조건을 묶는다.
/// </summary>
[System.Serializable]
public class BuildRequirement
{
    [Tooltip("거래 재화 요구 조건")]
    public BuildRequireCurrency requireCurrency = new BuildRequireCurrency();

    [Tooltip("거점 인벤토리에서 확인할 요구 아이템 목록")]
    public BuildRequireItem[] requireItems = Array.Empty<BuildRequireItem>();
}

/// <summary>
/// 건축에 거래 재화가 필요한지와 필요한 금액을 정의한다.
/// </summary>
[System.Serializable]
public class BuildRequireCurrency
{
    [Tooltip("활성화하면 value만큼의 거래 재화를 요구한다.")]
    public bool isRequired;

    [Tooltip("건축/증축에 필요한 거래 재화")]
    [Min(0)] public long value;
}

/// <summary>
/// 건축에 필요한 아이템 ID와 수량을 정의한다.
/// 실제 보유량은 PlayerMainManager의 거점 인벤토리에서 조회한다.
/// </summary>
[System.Serializable]
public class BuildRequireItem
{
    [Tooltip("Shared Game Data 및 PlayerMainManager 조회에 사용할 아이템 ID")]
    public string itemId = string.Empty;

    [Tooltip("건축/증축에 필요한 아이템 수량")]
    [Min(1)] public int quantity = 1;
}
