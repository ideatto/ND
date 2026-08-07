using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class TransportInventoryRewardDebugButtonPrefabBuilder
{
    private const string PrefabPath =
        "Assets/_Project/08.Prefabs/Debug/TransportInventoryRewardDebugButton.prefab";
    private const string FontSourcePath =
        "Assets/_Project/08.Prefabs/UI/TransportInventory/TransportInventoryPopup.prefab";

    [MenuItem("Tools/ND/Transport Inventory/Rebuild Reward Debug Button")]
    public static void Build()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<GameObject>(FontSourcePath)
            ?.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault()?.font;

        var root = new GameObject("TransportInventoryRewardDebugButton",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button),
            typeof(TransportInventoryRewardDebugButton));
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(260f, 56f);
        Image image = root.GetComponent<Image>();
        image.color = new Color32(140, 117, 77, 255);
        root.GetComponent<Button>().targetGraphic = image;

        var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(root.transform, false);
        RectTransform labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.text = "운송 수단 테스트 지급";
        text.font = font;
        text.fontSize = 22f;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color32(31, 36, 33, 255);
        text.raycastTarget = false;

        SerializedObject serialized = new SerializedObject(root.GetComponent<TransportInventoryRewardDebugButton>());
        SerializedProperty rewards = serialized.FindProperty("rewards");
        rewards.arraySize = 3;
        SetReward(rewards.GetArrayElementAtIndex(0), TransportRewardType.Wagon, "Wagon_M", 1);
        SetReward(rewards.GetArrayElementAtIndex(1), TransportRewardType.Wagon, "Wagon_S", 1);
        SetReward(rewards.GetArrayElementAtIndex(2), TransportRewardType.DraftAnimal, "Horse", 2);
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        Debug.Log("[Transport Reward Debug] Prefab rebuilt. No scene was modified.");
    }

    private static void SetReward(
        SerializedProperty property, TransportRewardType type, string contentId, int quantity)
    {
        property.FindPropertyRelative("type").enumValueIndex = (int)type;
        property.FindPropertyRelative("contentId").stringValue = contentId;
        property.FindPropertyRelative("quantity").intValue = quantity;
    }
}
