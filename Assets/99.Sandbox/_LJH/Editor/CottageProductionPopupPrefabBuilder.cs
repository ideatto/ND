using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 오두막 UI를 에디터에서 고정 Prefab/Scene 오브젝트로 조립하는 전용 도구다.
/// 런타임 UI 생성 코드가 아니며, Build/Install 메뉴 실행 결과만 Player에 포함된다.
/// </summary>
public static class CottageProductionPopupPrefabBuilder
{
    private const string ScenePath = "Assets/_Project/07.Scenes/04_InGame/Build UI.unity";
    private const string PrefabFolder = "Assets/_Project/08.Prefabs/UI/Cottage";
    private const string PrefabPath = PrefabFolder + "/CottageProductionPopup.prefab";
    private const string MainUiPrefabPath =
        // InGame.unity가 실제로 참조하는 MainUICanvas 원본에 설치한다.
        "Assets/_Project/08.Prefabs/MainUICanvas.prefab";
    private static TMP_FontAsset font;

    [MenuItem("Tools/LJH/Build Cottage Production Popup")]
    public static void Build()
    {
        if (SceneManager.GetActiveScene().path != ScenePath)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/_Project/09.Art/05_Fonts/문화재돌봄체 Bold SDF.asset");

        if (!AssetDatabase.IsValidFolder(PrefabFolder))
            AssetDatabase.CreateFolder("Assets/_Project/08.Prefabs/UI", "Cottage");

        var canvas = GameObject.Find("BuildingPopupPreviewCanvas");
        if (canvas == null)
            throw new System.InvalidOperationException("BuildingPopupPreviewCanvas를 찾을 수 없습니다.");

        var old = GameObject.Find("CottageProductionPopup");
        if (old != null)
            Object.DestroyImmediate(old);

        foreach (Transform child in canvas.transform)
            child.gameObject.SetActive(false);

        var root = CreateRect("CottageProductionPopup", canvas.transform, Vector2.zero, Vector2.one,
            new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        root.AddComponent<CanvasGroup>();
        var popupView = root.AddComponent<CottageProductionPopupView>();

        var backdrop = CreateImage("Backdrop", root.transform, new Color(0f, 0f, 0f, 0.52f),
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        var backdropButton = backdrop.gameObject.AddComponent<Button>();

        var panel = CreateImage("Panel", root.transform, new Color(0.93f, 0.86f, 0.70f),
            Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero,
            new Vector2(1040f, 650f)).gameObject;

        var header = CreateImage("Header", panel.transform, new Color(0.78f, 0.66f, 0.49f),
            new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero,
            new Vector2(0f, 80f));
        CreateText("TitleText", header.transform, "오두막", 36f, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        var closeButton = CreateButton("CloseButton", header.transform, "X", new Color(0.70f, 0.30f, 0.25f),
            new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
            new Vector2(-16f, 0f), new Vector2(54f, 54f));
        popupView.ConfigureCloseButtons(backdropButton, closeButton);

        var content = CreateRect("ProductionContent", panel.transform, Vector2.zero, Vector2.one,
            Vector2.one * 0.5f, new Vector2(0f, -5f), new Vector2(-60f, -130f));
        CreateCard("WagonProductionCard", content.transform, "중형 마차", GetIcon(
                "Assets/_Project/02.Data/01_ScriptableObjects/Wagons/Wagon_WagonM.asset"),
            new Vector2(0.26f, 0.55f), "1");
        CreateCard("DraftAnimalProductionCard", content.transform, "말", GetIcon(
                "Assets/_Project/02.Data/01_ScriptableObjects/DraftAnimals/DraftAnimal_Horse.asset"),
            new Vector2(0.74f, 0.55f), "2");

        var receiveAllButton = CreateButton("ReceiveAllButton", panel.transform, "모두 받기", new Color(0.40f, 0.62f, 0.28f),
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 62f), new Vector2(330f, 58f));
        CreateText("GuideText", panel.transform, "수령한 마차와 동물은 목장에서 관리할 수 있습니다.", 19f,
            TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f), new Vector2(0f, 23f), new Vector2(760f, 28f));

        Transform wagonCard = content.transform.Find("WagonProductionCard");
        Transform animalCard = content.transform.Find("DraftAnimalProductionCard");
        popupView.ConfigureProductionReferences(
            wagonCard.Find("IconFrame/ProductIcon").GetComponent<Image>(),
            wagonCard.Find("ProductNameText").GetComponent<TMP_Text>(),
            wagonCard.Find("StoredAmountText").GetComponent<TMP_Text>(),
            wagonCard.Find("RemainingTimeText").GetComponent<TMP_Text>(),
            wagonCard.Find("ReceiveButton").GetComponent<Button>(),
            animalCard.Find("IconFrame/ProductIcon").GetComponent<Image>(),
            animalCard.Find("ProductNameText").GetComponent<TMP_Text>(),
            animalCard.Find("StoredAmountText").GetComponent<TMP_Text>(),
            animalCard.Find("RemainingTimeText").GetComponent<TMP_Text>(),
            animalCard.Find("ReceiveButton").GetComponent<Button>(),
            receiveAllButton);
        var presenter = root.AddComponent<CottageProductionPopupPresenter>();
        presenter.Configure(popupView, null);

        PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.AutomatedAction);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
    }

    [MenuItem("Tools/LJH/Install Cottage Production Popup To Main UI")]
    public static void InstallToMainUi()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MainUiPrefabPath);
        try
        {
            Transform existing = root.transform.Find("CottageProductionPopup");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            GameObject popupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (popupPrefab == null)
                throw new System.InvalidOperationException("Cottage popup prefab을 찾을 수 없습니다.");
            var popup = (GameObject)PrefabUtility.InstantiatePrefab(
                popupPrefab, root.transform);
            popup.name = "CottageProductionPopup";

            BuildingListPanel buildingList =
                root.GetComponentInChildren<BuildingListPanel>(true);
            NoticeUI notice = root.GetComponentInChildren<NoticeUI>(true);
            CottageProductionPopupPresenter presenter =
                popup.GetComponent<CottageProductionPopupPresenter>();
            CottageProductionPopupView view =
                popup.GetComponent<CottageProductionPopupView>();
            if (buildingList == null || presenter == null || view == null)
                throw new System.InvalidOperationException("Cottage Main UI 연결 대상이 누락되었습니다.");

            presenter.Configure(view, notice);
            CottageProductionMainUiEntry entry =
                root.GetComponent<CottageProductionMainUiEntry>();
            if (entry == null)
                entry = root.AddComponent<CottageProductionMainUiEntry>();
            Sprite readyIcon = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/_Project/09.Art/04_UI/icons/Icon_caravan_slot_settling.png");
            entry.Configure(buildingList, presenter, readyIcon);

            if (notice != null)
                popup.transform.SetSiblingIndex(notice.transform.GetSiblingIndex());
            popup.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, MainUiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

    private static void CreateCard(string name, Transform parent, string title, Sprite icon,
        Vector2 anchor, string maxCount)
    {
        var card = CreateImage(name, parent, new Color(0.97f, 0.94f, 0.86f), anchor, anchor,
            Vector2.one * 0.5f, new Vector2(0f, -10f), new Vector2(430f, 370f)).gameObject;
        var frame = CreateImage("IconFrame", card.transform, new Color(0.85f, 0.80f, 0.69f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -20f), new Vector2(130f, 130f));
        var productIcon = CreateImage("ProductIcon", frame.transform, Color.white, Vector2.zero,
            Vector2.one, Vector2.one * 0.5f, Vector2.zero, new Vector2(-20f, -20f));
        productIcon.sprite = icon;
        productIcon.preserveAspect = true;

        CreateText("ProductNameText", card.transform, title, 30f, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -174f), new Vector2(360f, 40f));
        CreateText("StoredAmountLabel", card.transform, "생성된 수량", 21f, TextAlignmentOptions.Left,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(-55f, -222f), new Vector2(135f, 32f));
        CreateText("StoredAmountText", card.transform, "0 / " + maxCount, 23f, TextAlignmentOptions.Right,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(65f, -222f), new Vector2(70f, 32f));
        CreateText("RemainingTimeText", card.transform, "다음 생산까지 05:00", 21f,
            TextAlignmentOptions.Center, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -265f), new Vector2(380f, 32f));
        CreateButton("ReceiveButton", card.transform, "받기", new Color(0.40f, 0.62f, 0.28f),
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -305f), new Vector2(230f, 40f));
    }

    private static GameObject CreateRect(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return go;
    }

    private static Image CreateImage(string name, Transform parent, Color color, Vector2 min,
        Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var image = CreateRect(name, parent, min, max, pivot, position, size).AddComponent<Image>();
        image.color = color;
        return image;
    }

    private static TMP_Text CreateText(string name, Transform parent, string value, float size,
        TextAlignmentOptions alignment, Vector2 min, Vector2 max, Vector2 pivot,
        Vector2 position, Vector2 rectSize)
    {
        var text = CreateRect(name, parent, min, max, pivot, position, rectSize)
            .AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = new Color(0.18f, 0.15f, 0.12f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string label, Color color,
        Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var image = CreateImage(name, parent, color, min, max, pivot, position, size);
        var button = image.gameObject.AddComponent<Button>();
        var labelText = CreateText("Label", image.transform, label, 26f, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, Vector2.one * 0.5f, Vector2.zero, Vector2.zero);
        labelText.color = Color.white;
        return button;
    }

    private static Sprite GetIcon(string dataPath)
    {
        var data = AssetDatabase.LoadMainAssetAtPath(dataPath);
        if (data == null)
            return null;
        var icon = new SerializedObject(data).FindProperty("icon");
        return icon != null ? icon.objectReferenceValue as Sprite : null;
    }
}
