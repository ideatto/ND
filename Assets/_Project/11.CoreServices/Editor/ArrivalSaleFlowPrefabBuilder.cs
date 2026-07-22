#if UNITY_EDITOR
using System.Linq;
using ND.UI.Market;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ArrivalSaleFlowPrefabBuilder
{
    private const string MenuPath = "ND/UI/Create Arrival Sale Flow Prefab";
    private const string FolderPath = "Assets/_Project/08.Prefabs/UI/Market";
    private const string PrefabPath = FolderPath + "/ArrivalSaleFlow.prefab";

    static ArrivalSaleFlowPrefabBuilder()
    {
        EditorApplication.delayCall += CreateMissingPrefab;
    }

    private static void CreateMissingPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null || prefab.GetComponent<ArrivalSalePanelView>() == null)
            Build();
    }

    [MenuItem(MenuPath)]
    private static void Build()
    {
        EnsureFolders();

        var root = new GameObject("ArrivalSaleFlow", typeof(RectTransform));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        MarketTradePanelController marketPanel = root.AddComponent<MarketTradePanelController>();
        CaravanArrivalSaleController saleController = root.AddComponent<CaravanArrivalSaleController>();
        ArrivalSalePanelView saleView = root.AddComponent<ArrivalSalePanelView>();
        saleView.Configure(marketPanel, saleController);

        var saleButtonObject = CreateButton(root.transform, "ArrivalSaleButton", "판매", new Vector2(120f, 44f));
        var saleButton = saleButtonObject.AddComponent<CaravanArrivalSaleButton>();
        RectTransform saleRect = saleButtonObject.GetComponent<RectTransform>();
        saleRect.anchorMin = new Vector2(1f, 0f);
        saleRect.anchorMax = new Vector2(1f, 0f);
        saleRect.pivot = new Vector2(1f, 0f);
        saleRect.anchoredPosition = new Vector2(-24f, 24f);

        MarketData[] markets = AssetDatabase.FindAssets("t:MarketData")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<MarketData>)
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.MarketId))
            .GroupBy(value => value.MarketId)
            .Select(group => group.First())
            .ToArray();

        var controllerObject = new SerializedObject(saleController);
        controllerObject.FindProperty("marketPanel").objectReferenceValue = marketPanel;
        SerializedProperty catalog = controllerObject.FindProperty("marketCatalog");
        catalog.arraySize = markets.Length;
        for (int i = 0; i < markets.Length; i++)
            catalog.GetArrayElementAtIndex(i).objectReferenceValue = markets[i];
        controllerObject.ApplyModifiedPropertiesWithoutUndo();

        var buttonObject = new SerializedObject(saleButton);
        buttonObject.FindProperty("saleController").objectReferenceValue = saleController;
        buttonObject.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Debug.Log($"[Arrival Sale UI] Created {PrefabPath} with {markets.Length} MarketData assets.");
    }

    private static GameObject CreateButton(
        Transform parent,
        string name,
        string label,
        Vector2 size)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<RectTransform>().sizeDelta = size;
        buttonObject.GetComponent<Image>().color = new Color(0.25f, 0.39f, 0.6f, 1f);

        var textObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        Text text = textObject.GetComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 18;
        return buttonObject;
    }

    private static void EnsureFolders()
    {
        const string uiFolder = "Assets/_Project/08.Prefabs/UI";
        if (!AssetDatabase.IsValidFolder(uiFolder + "/Market"))
            AssetDatabase.CreateFolder(uiFolder, "Market");
    }
}
#endif
