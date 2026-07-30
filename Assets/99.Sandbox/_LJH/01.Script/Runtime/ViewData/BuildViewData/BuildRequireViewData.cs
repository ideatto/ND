using UnityEngine;

/// <summary>
/// 요구 아이템 한 종류의 표시 정보와 현재 보유 수량을 전달한다.
/// </summary>
[System.Serializable]
public class ItemRequirementViewData
{
    // itemId는 인벤토리 조회 및 건축 Command 연결에 사용한다.
    public string itemId = string.Empty;

    // displayName과 icon은 Shared Game Data의 아이템 정의에서 가져온다.
    public string displayName = string.Empty;
    public Sprite icon;

    // UI에서는 "보유량 / 요구량" 형식으로 표시한다.
    public int ownedQuantity;
    public int requiredQuantity;

    // 보유 수량이 요구 수량 이상인지 나타낸다.
    public bool isSatisfied;
}
