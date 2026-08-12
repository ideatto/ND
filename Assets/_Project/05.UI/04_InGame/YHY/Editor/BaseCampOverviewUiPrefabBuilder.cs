#if UNITY_EDITOR
using System.IO;
using ND.UI.InGame.BaseCamp;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class BaseCampOverviewUiPrefabBuilder
{
    private const string Folder = "Assets/_Project/08.Prefabs/UI/Building";
    private const string PrefabPath = Folder + "/BaseCampOverviewPopup.prefab";
    // InGame.unity가 실제로 참조하는 MainUICanvas 원본에 설치해야 BuildingListPanel 이벤트를 받을 수 있다.
    private const string MainUiPrefabPath = "Assets/_Project/08.Prefabs/MainUICanvas.prefab";

    [MenuItem("ND/UI/Build BaseCamp Overview Popup")]
    public static void Build()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
        }

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        GameObject root = Node("BaseCampOverviewPopup", null);
        Stretch((RectTransform)root.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject backdrop = Panel("Backdrop", root.transform, new Color32(39, 32, 27, 180));
        Stretch((RectTransform)backdrop.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Button backdropButton = backdrop.AddComponent<Button>();
        backdropButton.targetGraphic = backdrop.GetComponent<Image>();

        GameObject card = Panel("Card", root.transform, new Color32(239, 225, 195, 255));
        RectTransform cardRect = (RectTransform)card.transform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(600f, 740f);

        GameObject header = Panel("Header", card.transform, new Color32(216, 195, 159, 255));
        Stretch((RectTransform)header.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -78f), Vector2.zero);
        TextNode("Title", header.transform, "베이스 캠프", 32f, TextAlignmentOptions.Center, font);

        GameObject closeObject = Panel("CloseButton", header.transform, new Color32(180, 91, 75, 255));
        RectTransform closeRect = (RectTransform)closeObject.transform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-14f, 0f);
        closeRect.sizeDelta = new Vector2(54f, 48f);
        Button closeButton = closeObject.AddComponent<Button>();
        closeButton.targetGraphic = closeObject.GetComponent<Image>();
        TextNode("Label", closeObject.transform, "X", 25f, TextAlignmentOptions.Center, font);

        TextMeshProUGUI overviewTitle = TextNode("OverviewTitle", card.transform, "거점 건물 현황", 27f, TextAlignmentOptions.Center, font);
        AnchorTop((RectTransform)overviewTitle.transform, 36f, -132f, -36f, -88f);

        TextMeshProUGUI guide = TextNode(
            "UnlockGuide",
            card.transform,
            "다음 레벨을 해금하려면 베이스 캠프를 증축하세요.",
            15f,
            TextAlignmentOptions.MidlineRight,
            font);
        AnchorTop((RectTransform)guide.transform, 150f, -158f, -54f, -134f);

        GameObject columns = Panel("ColumnHeader", card.transform, new Color32(197, 190, 176, 255));
        AnchorTop((RectTransform)columns.transform, 54f, -204f, -54f, -162f);
        TextMeshProUGUI buildingColumn = TextNode("BuildingColumn", columns.transform, "건물", 21f, TextAlignmentOptions.Center, font);
        SetHorizontalRange((RectTransform)buildingColumn.transform, 0f, 0.62f, 12f, -6f);
        TextMeshProUGUI levelColumn = TextNode("LevelColumn", columns.transform, "현재 레벨", 21f, TextAlignmentOptions.Center, font);
        SetHorizontalRange((RectTransform)levelColumn.transform, 0.62f, 1f, 6f, -12f);

        string[] names = { "창고", "목장", "상점", "빵집", "오두막", "풍차" };
        TextMeshProUGUI[] labels = new TextMeshProUGUI[names.Length];
        TextMeshProUGUI baseLevel = null;
        for (int i = 0; i <= names.Length; i++)
        {
            GameObject row = Panel(
                "BuildingRow_" + i,
                card.transform,
                i % 2 == 0 ? new Color32(246, 239, 223, 255) : new Color32(229, 220, 201, 255));
            float top = -212f - i * 46f;
            AnchorTop((RectTransform)row.transform, 54f, top - 42f, -54f, top);

            string buildingName = i == 0 ? "베이스 캠프" : names[i - 1];
            TextMeshProUGUI nameLabel = TextNode("BuildingName", row.transform, buildingName, 20f, TextAlignmentOptions.Center, font);
            SetHorizontalRange((RectTransform)nameLabel.transform, 0f, 0.62f, 12f, -6f);

            TextMeshProUGUI levelLabel = TextNode("Level", row.transform, "Lv.0", 20f, TextAlignmentOptions.Center, font);
            SetHorizontalRange((RectTransform)levelLabel.transform, 0.62f, 1f, 6f, -12f);
            if (i == 0)
                baseLevel = levelLabel;
            else
                labels[i - 1] = levelLabel;
        }

        GameObject divider = Panel("Divider", card.transform, new Color32(174, 151, 113, 255));
        AnchorTop((RectTransform)divider.transform, 54f, -542f, -54f, -539f);

        // Caravan progression is a separate static block below the building table. Runtime code
        // changes only the values and never creates UI objects.
        GameObject caravanProgressPanel = Panel(
            "CaravanProgressPanel",
            card.transform,
            new Color32(226, 207, 177, 255));
        AnchorTop((RectTransform)caravanProgressPanel.transform, 54f, -690f, -54f, -552f);

        TextMeshProUGUI caravanSlotProgress = TextNode(
            "CaravanSlotProgress",
            caravanProgressPanel.transform,
            "현재 캐러밴 슬롯: 0 / 4",
            23f,
            TextAlignmentOptions.MidlineLeft,
            font);
        AnchorTop((RectTransform)caravanSlotProgress.transform, 24f, -61f, -24f, -13f);

        TextMeshProUGUI nextUnlock = TextNode(
            "NextUnlock",
            caravanProgressPanel.transform,
            "다음 레벨: 캐러밴 슬롯 해금",
            21f,
            TextAlignmentOptions.MidlineLeft,
            font);
        AnchorTop((RectTransform)nextUnlock.transform, 24f, -117f, -24f, -69f);

        BaseCampOverviewPopupController controller = root.AddComponent<BaseCampOverviewPopupController>();
        SerializedObject serialized = new SerializedObject(controller);
        serialized.FindProperty("baseCampLevelText").objectReferenceValue = baseLevel;
        serialized.FindProperty("unlockGuideText").objectReferenceValue = guide;
        serialized.FindProperty("caravanSlotProgressText").objectReferenceValue = caravanSlotProgress;
        serialized.FindProperty("nextUnlockText").objectReferenceValue = nextUnlock;
        serialized.FindProperty("backdropButton").objectReferenceValue = backdropButton;
        serialized.FindProperty("closeButton").objectReferenceValue = closeButton;
        SerializedProperty rows = serialized.FindProperty("buildingRows");
        rows.arraySize = names.Length;
        for (int i = 0; i < names.Length; i++)
        {
            SerializedProperty element = rows.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("buildingDisplayName").stringValue = names[i];
            element.FindPropertyRelative("label").objectReferenceValue = labels[i];
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();

        root.SetActive(false);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Created " + PrefabPath);
    }

    [MenuItem("ND/UI/Install BaseCamp Overview Into Main UI")]
    public static void InstallIntoMainUi()
    {
        GameObject popupAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (popupAsset == null)
            throw new FileNotFoundException("Build the BaseCamp overview popup first.", PrefabPath);

        GameObject root = PrefabUtility.LoadPrefabContents(MainUiPrefabPath);
        try
        {
            BuildingListPanel buildingListPanel = root.GetComponentInChildren<BuildingListPanel>(true);
            if (buildingListPanel == null)
                throw new MissingComponentException("MainUICanvas prefab has no BuildingListPanel.");

            NoticeUI noticeUI = root.GetComponentInChildren<NoticeUI>(true);
            if (noticeUI == null)
                throw new MissingComponentException("MainUICanvas prefab has no NoticeUI.");

            BaseCampOverviewPopupController popup = root.GetComponentInChildren<BaseCampOverviewPopupController>(true);
            if (popup == null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(popupAsset);
                instance.name = "BaseCampOverviewPopup";
                instance.transform.SetParent(root.transform, false);
                instance.transform.SetAsLastSibling();
                popup = instance.GetComponent<BaseCampOverviewPopupController>();
            }

            BaseCampMainUiEntry entry = buildingListPanel.GetComponent<BaseCampMainUiEntry>();
            if (entry == null)
                entry = buildingListPanel.gameObject.AddComponent<BaseCampMainUiEntry>();

            SerializedObject serialized = new SerializedObject(entry);
            serialized.FindProperty("buildingListPanel").objectReferenceValue = buildingListPanel;
            serialized.FindProperty("popup").objectReferenceValue = popup;
            serialized.FindProperty("noticeUI").objectReferenceValue = noticeUI;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            popup.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, MainUiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("Installed BaseCamp overview popup into " + MainUiPrefabPath);
    }

    private static GameObject Node(string name, Transform parent)
    {
        GameObject result = new GameObject(name, typeof(RectTransform));
        if (parent != null) result.transform.SetParent(parent, false);
        return result;
    }

    private static GameObject Panel(string name, Transform parent, Color color)
    {
        GameObject result = Node(name, parent);
        result.AddComponent<Image>().color = color;
        return result;
    }

    private static TextMeshProUGUI TextNode(
        string name,
        Transform parent,
        string value,
        float size,
        TextAlignmentOptions alignment,
        TMP_FontAsset font)
    {
        GameObject result = Node(name, parent);
        TextMeshProUGUI text = result.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = new Color32(50, 43, 35, 255);
        text.raycastTarget = false;
        Stretch((RectTransform)result.transform, Vector2.zero, Vector2.one, new Vector2(12f, 4f), new Vector2(-12f, -4f));
        return text;
    }

    private static void Stretch(RectTransform target, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        target.anchorMin = min;
        target.anchorMax = max;
        target.offsetMin = offsetMin;
        target.offsetMax = offsetMax;
    }

    private static void AnchorTop(RectTransform target, float left, float bottom, float right, float top)
    {
        target.anchorMin = new Vector2(0f, 1f);
        target.anchorMax = new Vector2(1f, 1f);
        target.offsetMin = new Vector2(left, bottom);
        target.offsetMax = new Vector2(right, top);
    }

    private static void SetHorizontalRange(
        RectTransform target,
        float anchorMinX,
        float anchorMaxX,
        float left,
        float right)
    {
        target.anchorMin = new Vector2(anchorMinX, 0f);
        target.anchorMax = new Vector2(anchorMaxX, 1f);
        target.offsetMin = new Vector2(left, 4f);
        target.offsetMax = new Vector2(right, -4f);
    }
}
#endif
