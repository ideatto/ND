using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CaravanActivityLogPrefabBuilder
{
    private const string FolderPath =
        "Assets/_Project/05.UI/09_QoL/CaravanActivityLog/Prefabs";
    private const string ItemPath = FolderPath + "/CaravanActivityLogItem.prefab";
    private const string PanelPath = FolderPath + "/CaravanActivityLogPanel.prefab";
    private const string CombatPanelPath = FolderPath + "/CaravanCombatSequencePanel.prefab";

    [MenuItem("ND/UI/Create Caravan Activity Log Prefabs")]
    public static void CreatePrefabs()
    {
        EnsureFolders(FolderPath);
        var itemPrefab = CreateItemPrefab();
        CreatePanelPrefab(itemPrefab);
        CreateCombatPanelPrefab();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log(
            $"[Caravan Activity Log] Created {ItemPath}, {PanelPath}, and {CombatPanelPath}.");
    }

    private static CaravanActivityLogItemView CreateItemPrefab()
    {
        var root = CreateRect("CaravanActivityLogItem");
        var rootLayout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
        rootLayout.padding = new RectOffset(12, 12, 8, 8);
        rootLayout.spacing = 10f;
        rootLayout.childAlignment = TextAnchor.UpperLeft;
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = false;
        rootLayout.childForceExpandHeight = false;
        var fitter = root.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var icon = CreateRect("CaravanIcon", root.transform).gameObject.AddComponent<Image>();
        icon.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        icon.type = Image.Type.Sliced;
        icon.raycastTarget = false;
        icon.rectTransform.sizeDelta = new Vector2(28f, 28f);
        var iconLayout = icon.gameObject.AddComponent<LayoutElement>();
        iconLayout.minWidth = 28f;
        iconLayout.preferredWidth = 28f;
        iconLayout.minHeight = 28f;
        iconLayout.preferredHeight = 28f;

        var bubble = CreateRect("Bubble", root.transform);
        var bubbleImage = bubble.gameObject.AddComponent<Image>();
        bubbleImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        bubbleImage.type = Image.Type.Sliced;
        bubbleImage.raycastTarget = false;
        var bubbleLayout = bubble.gameObject.AddComponent<HorizontalLayoutGroup>();
        bubbleLayout.padding = new RectOffset(14, 14, 10, 10);
        bubbleLayout.childControlWidth = true;
        bubbleLayout.childControlHeight = true;
        bubbleLayout.childForceExpandWidth = true;
        bubbleLayout.childForceExpandHeight = false;
        var bubbleFitter = bubble.gameObject.AddComponent<ContentSizeFitter>();
        bubbleFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var bubbleElement = bubble.gameObject.AddComponent<LayoutElement>();
        bubbleElement.flexibleWidth = 1f;

        var text = CreateRect("Message", bubble).gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = 22f;
        text.color = new Color(0.12f, 0.12f, 0.14f, 1f);
        text.enableWordWrapping = true;
        text.raycastTarget = false;

        var view = root.gameObject.AddComponent<CaravanActivityLogItemView>();
        SetObjectReference(view, "caravanIcon", icon);
        SetObjectReference(view, "bubbleBackground", bubbleImage);
        SetObjectReference(view, "messageText", text);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, ItemPath);
        Object.DestroyImmediate(root.gameObject);
        return prefab.GetComponent<CaravanActivityLogItemView>();
    }

    private static void CreatePanelPrefab(CaravanActivityLogItemView itemPrefab)
    {
        var root = CreateRect("CaravanActivityLogPanel");
        root.anchorMin = new Vector2(0f, 0f);
        root.anchorMax = new Vector2(0f, 0f);
        root.pivot = new Vector2(0f, 0f);
        root.sizeDelta = new Vector2(520f, 420f);
        var background = root.gameObject.AddComponent<Image>();
        background.color = new Color(0.06f, 0.07f, 0.09f, 0.88f);
        background.raycastTarget = false;

        var viewport = CreateRect("Viewport", root);
        Stretch(viewport, new Vector2(12f, 12f), new Vector2(-12f, -12f));
        var viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = false;
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = CreateRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
        vertical.padding = new RectOffset(0, 8, 8, 8);
        vertical.spacing = 4f;
        vertical.childControlWidth = true;
        vertical.childControlHeight = true;
        vertical.childForceExpandWidth = true;
        vertical.childForceExpandHeight = false;
        var contentFitter = content.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scrollbarRect = CreateRect("Scrollbar Vertical", root);
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 1f);
        scrollbarRect.anchoredPosition = new Vector2(-3f, -12f);
        scrollbarRect.sizeDelta = new Vector2(10f, -24f);
        var scrollbarImage = scrollbarRect.gameObject.AddComponent<Image>();
        scrollbarImage.color = new Color(1f, 1f, 1f, 0.08f);
        var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        var slidingArea = CreateRect("Sliding Area", scrollbarRect);
        Stretch(slidingArea, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        var handle = CreateRect("Handle", slidingArea);
        Stretch(handle, Vector2.zero, Vector2.zero);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        handleImage.type = Image.Type.Sliced;
        handleImage.color = new Color(1f, 1f, 1f, 0.55f);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;

        var scrollRect = root.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.scrollSensitivity = 24f;

        var panel = root.gameObject.AddComponent<CaravanActivityLogPanel>();
        SetObjectReference(panel, "scrollRect", scrollRect);
        SetObjectReference(panel, "contentRoot", content);
        SetObjectReference(panel, "itemPrefab", itemPrefab);
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, PanelPath);
        Object.DestroyImmediate(root.gameObject);
    }

    private static void CreateCombatPanelPrefab()
    {
        var root = CreateRect("CaravanCombatSequencePanel");
        Stretch(root, Vector2.zero, Vector2.zero);
        var canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var dimmer = root.gameObject.AddComponent<Image>();
        dimmer.color = new Color(0f, 0f, 0f, 0.28f);
        dimmer.raycastTarget = false;

        var card = CreateRect("CombatCard", root);
        card.anchorMin = new Vector2(0.5f, 0.5f);
        card.anchorMax = new Vector2(0.5f, 0.5f);
        card.pivot = new Vector2(0.5f, 0.5f);
        card.sizeDelta = new Vector2(760f, 430f);
        var cardBackground = card.gameObject.AddComponent<Image>();
        cardBackground.sprite =
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        cardBackground.type = Image.Type.Sliced;
        cardBackground.color = new Color(0.18f, 0.16f, 0.13f, 0.94f);
        cardBackground.raycastTarget = false;

        var artworkRect = CreateRect("StageArtwork", card);
        artworkRect.anchorMin = new Vector2(0.5f, 1f);
        artworkRect.anchorMax = new Vector2(0.5f, 1f);
        artworkRect.pivot = new Vector2(0.5f, 1f);
        artworkRect.anchoredPosition = new Vector2(0f, -34f);
        artworkRect.sizeDelta = new Vector2(640f, 280f);
        var artwork = artworkRect.gameObject.AddComponent<Image>();
        artwork.color = Color.white;
        artwork.enabled = false;
        artwork.preserveAspect = true;
        artwork.raycastTarget = false;

        var messageRect = CreateRect("Message", card);
        messageRect.anchorMin = new Vector2(0f, 0f);
        messageRect.anchorMax = new Vector2(1f, 0f);
        messageRect.pivot = new Vector2(0.5f, 0f);
        messageRect.offsetMin = new Vector2(32f, 28f);
        messageRect.offsetMax = new Vector2(-32f, 126f);
        var message = messageRect.gameObject.AddComponent<TextMeshProUGUI>();
        message.alignment = TextAlignmentOptions.Center;
        message.fontSize = 36f;
        message.fontStyle = FontStyles.Bold;
        message.color = Color.white;
        message.enableWordWrapping = true;
        message.raycastTarget = false;

        var vfxRoot = CreateRect("StageVfxRoot", card);
        Stretch(vfxRoot, Vector2.zero, Vector2.zero);
        vfxRoot.SetAsLastSibling();

        var panel = root.gameObject.AddComponent<CaravanCombatSequencePanel>();
        SetObjectReference(panel, "canvasGroup", canvasGroup);
        SetObjectReference(panel, "cardRoot", card);
        SetObjectReference(panel, "backgroundImage", cardBackground);
        SetObjectReference(panel, "stageImage", artwork);
        SetObjectReference(panel, "messageText", message);
        SetObjectReference(panel, "vfxRoot", vfxRoot);

        PrefabUtility.SaveAsPrefabAsset(root.gameObject, CombatPanelPath);
        Object.DestroyImmediate(root.gameObject);
    }

    private static RectTransform CreateRect(string name, Transform parent = null)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)gameObject.transform;
        if (parent != null)
        {
            rect.SetParent(parent, false);
        }
        return rect;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetObjectReference(
        Object target,
        string propertyName,
        Object value)
    {
        var serialized = new SerializedObject(target);
        serialized.FindProperty(propertyName).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureFolders(string assetPath)
    {
        var segments = assetPath.Split('/');
        var current = segments[0];
        for (var index = 1; index < segments.Length; index++)
        {
            var next = current + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[index]);
            }
            current = next;
        }
    }
}
