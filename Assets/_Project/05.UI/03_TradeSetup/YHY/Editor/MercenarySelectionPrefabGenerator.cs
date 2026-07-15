#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class MercenarySelectionPrefabGenerator
{
    private const string PrefabPath = "Assets/_Project/08.Prefabs/UI/Trade/MercenarySelectionPanel.prefab";

    [InitializeOnLoadMethod]
    private static void GenerateOnceAfterImport()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            return;
        EditorApplication.delayCall += Generate;
    }

    [MenuItem("ND/UI/Generate Mercenary Selection Panel")]
    public static void Generate()
    {
        GameObject root = new GameObject("MercenarySelectionPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        root.GetComponent<Image>().color = new Color(0.08f, 0.08f, 0.1f, 0.88f);

        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 40, 40);
        layout.spacing = 14f;
        layout.childControlHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text title = Text(root.transform, "Title", "용병 고용", 34f, 52f, FontStyles.Bold);
        TMP_Text power = Text(root.transform, "PowerText", "용병 전투력  0 / 0", 24f, 38f);
        TMP_Text currency = Text(root.transform, "CurrencyText", "현재 소지 금액  0 G", 22f, 34f);
        TMP_Text cost = Text(root.transform, "CostText", "용병 고용 가격  0 G", 22f, 34f);
        TMP_Text message = Text(root.transform, "MessageText", "용병 고용은 선택 사항입니다.", 20f, 46f);

        GameObject scrollGo = new GameObject("MercenaryScroll", typeof(RectTransform), typeof(Image), typeof(ScrollRect), typeof(LayoutElement));
        scrollGo.transform.SetParent(root.transform, false);
        scrollGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        scrollGo.GetComponent<LayoutElement>().preferredHeight = 420f;
        RectTransform viewport = Rect(scrollGo.transform, "Viewport");
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Rect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 8f;
        contentLayout.childControlHeight = true;
        contentLayout.childControlWidth = true;
        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;

        Button template = Button(content, "MercenaryRowTemplate", "용병 이름\n전투력 0 / 0 G", 72f);
        template.gameObject.SetActive(false);
        Button confirm = Button(root.transform, "ConfirmButton", "확인 (용병 없이 진행 가능)", 64f);

        MercenarySelectionPanel panel = root.AddComponent<MercenarySelectionPanel>();
        SerializedObject serialized = new SerializedObject(panel);
        serialized.FindProperty("powerText").objectReferenceValue = power;
        serialized.FindProperty("currencyText").objectReferenceValue = currency;
        serialized.FindProperty("costText").objectReferenceValue = cost;
        serialized.FindProperty("messageText").objectReferenceValue = message;
        serialized.FindProperty("listContainer").objectReferenceValue = content;
        serialized.FindProperty("rowTemplate").objectReferenceValue = template;
        serialized.FindProperty("confirmButton").objectReferenceValue = confirm;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log($"[UI] Generated {PrefabPath}");
    }

    private static RectTransform Rect(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private static TMP_Text Text(Transform parent, string name, string value, float size, float height, FontStyles style = FontStyles.Normal)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.text = value;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Left;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        return text;
    }

    private static Button Button(Transform parent, string name, string label, float height)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = new Color(0.25f, 0.3f, 0.4f, 1f);
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        go.GetComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = Text(go.transform, "Label", label, 21f, height);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(18f, 4f);
        rect.offsetMax = new Vector2(-18f, -4f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return button;
    }
}
#endif
