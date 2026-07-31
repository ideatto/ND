using ND.Framework;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 건축 테스트용 Logs/Stone 지급 버튼이다.
/// 일반 UI Button의 클릭 시점에만 인벤토리를 변경하며, Scene에는 자동 생성하지 않는다.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class BuildingMaterialTestButton : MonoBehaviour
{
    private const int GrantQuantity = 100;

    private void Awake()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // Prefab 자체가 클릭 이벤트를 소유해 Scene별 onClick 직렬화가 필요하지 않다.
        GetComponent<Button>().onClick.AddListener(GrantMaterials);
#else
        // Debug Prefab이 실수로 Release Scene에 배치돼도 기능과 UI가 노출되지 않게 한다.
        gameObject.SetActive(false);
#endif
    }

    /// <summary>
    /// 건축 완료 흐름을 거치지 않고 테스트 재료만 현재 플레이어 인벤토리에 추가한다.
    /// </summary>
    public void GrantMaterials()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PlayerMainManager player = PlayerMainManager.Instance;
        FrameworkRoot root = FrameworkRoot.Instance;

        if (player == null
            || root == null
            || root.SharedGameData == null
            || !root.SharedGameData.TryGetTradeItem("Logs", out SharedTradeItemDefinition logs)
            || !root.SharedGameData.TryGetTradeItem("Stone", out SharedTradeItemDefinition stone))
        {
            Debug.LogWarning("[Building Debug] Logs/Stone 지급에 필요한 데이터가 준비되지 않았습니다.", this);
            return;
        }

        player.AddItem(CreateSaveItem(logs), GrantQuantity);
        player.AddItem(CreateSaveItem(stone), GrantQuantity);
        Debug.Log($"[Building Debug] Logs {player.GetItemCount("Logs")} / Stone {player.GetItemCount("Stone")}", this);
#endif
    }

    private static TradeItemSaveData CreateSaveItem(SharedTradeItemDefinition definition)
    {
        return new TradeItemSaveData
        {
            itemId = definition.Id,
            itemName = definition.DisplayName,
            weight = Mathf.Max(0f, definition.Weight),
            basePrice = System.Math.Max(0L, definition.BaseBuyPrice),
            maxCount = Mathf.Max(1, definition.MaxCount)
        };
    }
}
