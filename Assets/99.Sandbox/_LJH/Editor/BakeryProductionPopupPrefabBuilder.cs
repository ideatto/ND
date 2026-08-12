#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class BakeryProductionPopupPrefabBuilder
{
    private const string PrefabPath = "Assets/_Project/08.Prefabs/UI/Bakery/BakeryProductionPopup.prefab";
    private const string BuildUiScenePath = "Assets/_Project/07.Scenes/04_InGame/Build UI.unity";
    private const string MainUiPrefabPath = "Assets/_Project/08.Prefabs/MainUICanvas.prefab";
    private const string ProductionReadyIconPath =
        "Assets/_Project/09.Art/04_UI/icons/알림 UI ICON.png";

    [MenuItem("ND/UI/Build Bakery Production Popup")]
    public static void Build()
    {
        EnsureFolder("Assets/_Project/08.Prefabs/UI/Bakery");
        GameObject root = Rect("BakeryProductionPopup", null, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        root.AddComponent<CanvasGroup>();
        GameObject backdropObject = Rect("Backdrop", root.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        backdropObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, .45f);
        Button backdropButton = backdropObject.AddComponent<Button>();

        GameObject panel = Rect("Panel", root.transform, new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(-310f, -270f), new Vector2(310f, 270f));
        panel.AddComponent<Image>().color = new Color(.94f, .86f, .68f, 1f);
        GameObject headerBar = Rect("Header", panel.transform, Vector2.up, Vector2.one,
            new Vector2(0f, -80f), Vector2.zero);
        headerBar.AddComponent<Image>().color = new Color(.78f, .66f, .49f, 1f);
        Text("TitleText", headerBar.transform, "빵집", 30,
            new Vector2(72, -72), new Vector2(-72, 0), TextAlignmentOptions.Center);
        Button close = ButtonObject("CloseButton", headerBar.transform, "X",
            new Color(.70f, .30f, .25f));
        SetRect(close.GetComponent<RectTransform>(), new Vector2(1f, .5f), new Vector2(1f, .5f),
            new Vector2(-70f, -27f), new Vector2(-16f, 27f), new Vector2(1f, .5f));

        GameObject content = Rect("ProductionContent", panel.transform, Vector2.zero, Vector2.one,
            new Vector2(30f, 60f), new Vector2(-30f, -70f));
        GameObject card = Rect("BreadProductionCard", content.transform, new Vector2(.5f, .55f),
            new Vector2(.5f, .55f), new Vector2(-215f, -225f), new Vector2(215f, 145f));
        card.AddComponent<Image>().color = new Color(.97f, .94f, .86f, 1f);
        GameObject iconFrame = Rect("IconFrame", card.transform, new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(-65, -150), new Vector2(65, -20));
        iconFrame.AddComponent<Image>().color = new Color(.85f, .80f, .69f, 1f);
        Image icon = Rect("ProductIcon", iconFrame.transform, Vector2.zero, Vector2.one,
            new Vector2(10, 10), new Vector2(-10, -10)).AddComponent<Image>();
        icon.preserveAspect = true;

        TextMeshProUGUI name = Text("ProductNameText", card.transform, "빵", 30,
            Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        SetRect(name.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(-180, -214), new Vector2(180, -174), new Vector2(.5f, 1f));
        TextMeshProUGUI amountLabel = Text("StoredAmountLabel", card.transform, "생성된 수량", 21,
            Vector2.zero, Vector2.zero, TextAlignmentOptions.Left);
        SetRect(amountLabel.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(-122.5f, -254), new Vector2(12.5f, -222), new Vector2(.5f, 1f));
        TextMeshProUGUI amount = Text("StoredAmountText", card.transform, "0 / 5", 23,
            Vector2.zero, Vector2.zero, TextAlignmentOptions.Right);
        amount.textWrappingMode = TextWrappingModes.NoWrap;
        SetRect(amount.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(0, -254), new Vector2(100, -222), new Vector2(.5f, 1f));
        TextMeshProUGUI remaining = Text("RemainingTimeText", card.transform,
            "다음 생산까지 01:00", 21, Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        SetRect(remaining.rectTransform, new Vector2(.5f, 1f), new Vector2(.5f, 1f),
            new Vector2(-190, -297), new Vector2(190, -265), new Vector2(.5f, 1f));

        Button partialReceive = ButtonObject("PartialReceiveButton", card.transform, "부분 수령",
            new Color(.55f, .66f, .38f));
        SetRect(partialReceive.GetComponent<RectTransform>(), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(-170, -345), new Vector2(-10, -305),
            new Vector2(.5f, 1f));
        Button receiveAll = ButtonObject("ReceiveAllButton", card.transform, "모두 수령",
            new Color(.40f, .62f, .28f));
        SetRect(receiveAll.GetComponent<RectTransform>(), new Vector2(.5f, 1f),
            new Vector2(.5f, 1f), new Vector2(10, -345), new Vector2(170, -305),
            new Vector2(.5f, 1f));

        TextMeshProUGUI guide = Text("GuideText", panel.transform,
            "수령한 빵은 창고에서 관리할 수 있습니다.", 19,
            Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        SetRect(guide.rectTransform, new Vector2(.5f, 0f), new Vector2(.5f, 0f),
            new Vector2(-270, 23), new Vector2(270, 51), new Vector2(.5f, 0f));

        GameObject modal = CreateQuantityModal(root.transform, out Button min, out Button minus,
            out TextMeshProUGUI quantity, out Button plus, out Button max, out Slider slider,
            out Button cancel, out Button confirm);
        BakeryProductionPopupView view = root.AddComponent<BakeryProductionPopupView>();
        view.Configure(backdropButton, close, icon, name, amount, remaining, partialReceive,
            receiveAll, modal, min, minus, quantity, plus, max, slider, cancel, confirm);
        root.AddComponent<BakeryProductionPopupPresenter>().Configure(view, null);
        root.SetActive(false);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        var scene = EditorSceneManager.OpenScene(BuildUiScenePath, OpenSceneMode.Single);
        Transform canvas = Object.FindFirstObjectByType<Canvas>(FindObjectsInactive.Include)?.transform;
        if (canvas == null) throw new MissingReferenceException("Build UI scene Canvas was not found.");
        Transform old = canvas.Find("BakeryProductionPopup");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        instance.name = "BakeryProductionPopup";
        instance.transform.SetParent(canvas, false);
        instance.transform.SetAsLastSibling();
        instance.SetActive(false);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeObject = prefab;
    }

    /// <summary>
    /// 최신 MainUICanvas 원본에 빵집 Entry와 고정 Popup 인스턴스를 조립한다.
    /// Player 런타임에는 UI를 생성하지 않고 이 에디터 메뉴가 저장한 오브젝트만 사용한다.
    /// 반복 실행 시 기존 Popup을 교체하고 Entry를 재사용하여 중복 조립을 막는다.
    /// </summary>
    [MenuItem("ND/UI/Install Bakery Production Into Main UI")]
    public static void InstallIntoMainUi()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(MainUiPrefabPath);
        try
        {
            Transform existing = root.transform.Find("BakeryProductionPopup");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            GameObject popupPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (popupPrefab == null)
                throw new System.InvalidOperationException("Bakery popup prefab을 찾을 수 없습니다.");

            var popup = (GameObject)PrefabUtility.InstantiatePrefab(popupPrefab, root.transform);
            popup.name = "BakeryProductionPopup";

            BuildingListPanel buildingList = root.GetComponentInChildren<BuildingListPanel>(true);
            NoticeUI notice = root.GetComponentInChildren<NoticeUI>(true);
            BakeryProductionPopupPresenter presenter =
                popup.GetComponent<BakeryProductionPopupPresenter>();
            BakeryProductionPopupView view = popup.GetComponent<BakeryProductionPopupView>();
            if (buildingList == null || presenter == null || view == null)
                throw new System.InvalidOperationException("Bakery Main UI 연결 대상이 누락되었습니다.");

            presenter.Configure(view, notice);
            BakeryProductionMainUiEntry entry = root.GetComponent<BakeryProductionMainUiEntry>();
            if (entry == null)
                entry = root.AddComponent<BakeryProductionMainUiEntry>();
            Sprite readyIcon = AssetDatabase.LoadAssetAtPath<Sprite>(ProductionReadyIconPath);
            if (readyIcon == null)
                throw new System.InvalidOperationException("Bakery 생산 완료 아이콘을 찾을 수 없습니다.");
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
        Debug.Log("Installed Bakery production popup into " + MainUiPrefabPath);
    }

    private static GameObject CreateQuantityModal(Transform parent, out Button min,
        out Button minus, out TextMeshProUGUI quantity, out Button plus, out Button max,
        out Slider slider, out Button cancel, out Button confirm)
    {
        GameObject modal = Rect("QuantityModal", parent, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        modal.AddComponent<Image>().color = new Color(0f, 0f, 0f, .55f);
        GameObject card = Rect("QuantityCard", modal.transform, new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(-245f, -180f), new Vector2(245f, 165f));
        card.AddComponent<Image>().color = new Color(.95f, .89f, .76f, 1f);
        Text("TitleText", card.transform, "부분 수령", 28,
            new Vector2(25f, -58f), new Vector2(-25f, -12f), TextAlignmentOptions.Center);
        min = ButtonObject("MinButton", card.transform, "최소", new Color(.55f, .55f, .52f));
        minus = ButtonObject("MinusButton", card.transform, "−", new Color(.66f, .58f, .44f));
        GameObject quantityBox = Rect("SelectedQuantity", card.transform, new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(-55f, 17f), new Vector2(55f, 73f));
        quantityBox.AddComponent<Image>().color = new Color(.99f, .98f, .93f, 1f);
        quantity = Text("QuantityText", quantityBox.transform, "1", 27,
            Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        SetRect(quantity.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Vector2(.5f, .5f));
        plus = ButtonObject("PlusButton", card.transform, "+", new Color(.66f, .58f, .44f));
        max = ButtonObject("MaxButton", card.transform, "최대", new Color(.55f, .55f, .52f));
        SetCentered(min, -190, -108, 17, 73);
        SetCentered(minus, -98, -58, 17, 73);
        SetCentered(plus, 58, 98, 17, 73);
        SetCentered(max, 108, 190, 17, 73);
        slider = CreateQuantitySlider(card.transform);
        cancel = ButtonObject("CancelButton", card.transform, "취소", new Color(.55f, .50f, .46f));
        confirm = ButtonObject("ConfirmReceiveButton", card.transform, "수령",
            new Color(.40f, .62f, .28f));
        SetCentered(cancel, -190, -10, -150, -102);
        SetCentered(confirm, 10, 190, -150, -102);
        modal.SetActive(false);
        return modal;
    }

    private static Slider CreateQuantitySlider(Transform parent)
    {
        GameObject sliderObject = Rect("QuantitySlider", parent, new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(-190f, -52f), new Vector2(190f, -18f));
        Slider slider = sliderObject.AddComponent<Slider>();
        slider.wholeNumbers = true;
        slider.minValue = 0;
        slider.maxValue = 1;

        GameObject background = Rect("Background", sliderObject.transform,
            new Vector2(0f, .5f), new Vector2(1f, .5f), new Vector2(0f, -5f),
            new Vector2(0f, 5f));
        background.AddComponent<Image>().color = new Color(.54f, .49f, .40f, 1f);
        GameObject fillArea = Rect("Fill Area", sliderObject.transform, Vector2.zero, Vector2.one,
            new Vector2(8f, 8f), new Vector2(-8f, -8f));
        GameObject fill = Rect("Fill", fillArea.transform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
        fill.AddComponent<Image>().color = new Color(.40f, .62f, .28f, 1f);
        GameObject handleArea = Rect("Handle Slide Area", sliderObject.transform,
            Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
        GameObject handle = Rect("Handle", handleArea.transform, new Vector2(0f, .5f),
            new Vector2(0f, .5f), new Vector2(-13f, -13f), new Vector2(13f, 13f));
        Image handleImage = handle.AddComponent<Image>();
        handleImage.color = new Color(.92f, .80f, .55f, 1f);
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    private static GameObject Rect(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        return go;
    }

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax, Vector2 pivot)
    {
        // Pivot 변경 시 위치 보정이 들어가므로 pivot을 먼저 적용한 뒤 offset을 기록한다.
        rect.pivot = pivot; rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
    }

    private static void SetCentered(Button button, float left, float right, float bottom, float top)
    {
        SetRect(button.GetComponent<RectTransform>(), new Vector2(.5f, .5f),
            new Vector2(.5f, .5f), new Vector2(left, bottom), new Vector2(right, top),
            new Vector2(.5f, .5f));
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, float size,
        Vector2 offsetMin, Vector2 offsetMax, TextAlignmentOptions alignment)
    {
        GameObject go = Rect(name, parent, Vector2.up, Vector2.one, offsetMin, offsetMax);
        var text = go.AddComponent<TextMeshProUGUI>();
        text.text = value; text.fontSize = size; text.alignment = alignment;
        text.color = new Color(.16f, .12f, .08f); text.raycastTarget = false;
        return text;
    }

    private static Button ButtonObject(string name, Transform parent, string label, Color color)
    {
        GameObject go = Rect(name, parent, new Vector2(0, 1), Vector2.one,
            Vector2.zero, Vector2.zero);
        go.AddComponent<Image>().color = color;
        Button button = go.AddComponent<Button>();
        TextMeshProUGUI text = Text("Label", go.transform, label, 22,
            Vector2.zero, Vector2.zero, TextAlignmentOptions.Center);
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            new Vector2(.5f, .5f));
        text.color = Color.white;
        return button;
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
