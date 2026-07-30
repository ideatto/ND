#if UNITY_EDITOR || DEVELOPMENT_BUILD
using ND.Framework;
using UnityEngine;

/// <summary>
/// 건축 외형/증축 확인을 위해 Logs와 Stone을 빠르게 지급하는 독립 테스트 버튼이다.
/// 공용 DebugPanel과 Scene 직렬화를 변경하지 않으며 Editor/Development Build에서만 자동 생성된다.
/// </summary>
public sealed class BuildingMaterialTestButton : MonoBehaviour
{
    private const int GrantQuantity = 100;
    private string status = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForDevelopment()
    {
        if (FindFirstObjectByType<BuildingMaterialTestButton>() != null) return;

        GameObject host = new GameObject(nameof(BuildingMaterialTestButton));
        DontDestroyOnLoad(host);
        host.AddComponent<BuildingMaterialTestButton>();
    }

    private void OnGUI()
    {
        PlayerMainManager player = PlayerMainManager.Instance;
        FrameworkRoot root = FrameworkRoot.Instance;
        SharedTradeItemDefinition logs = null;
        SharedTradeItemDefinition stone = null;
        bool ready = player != null
            && root != null
            && root.SharedGameData != null
            && root.SharedGameData.TryGetTradeItem("Logs", out logs)
            && root.SharedGameData.TryGetTradeItem("Stone", out stone);

        bool previousEnabled = GUI.enabled;
        GUI.enabled = ready;
        if (GUI.Button(new Rect(20f, 20f, 270f, 44f), "TEST: Logs +100 / Stone +100"))
        {
            player.AddItem(CreateSaveItem(logs), GrantQuantity);
            player.AddItem(CreateSaveItem(stone), GrantQuantity);
            status = $"Logs {player.GetItemCount("Logs")} / Stone {player.GetItemCount("Stone")}";
        }
        GUI.enabled = previousEnabled;

        if (!string.IsNullOrEmpty(status))
            GUI.Label(new Rect(20f, 66f, 300f, 24f), status);
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
#endif
