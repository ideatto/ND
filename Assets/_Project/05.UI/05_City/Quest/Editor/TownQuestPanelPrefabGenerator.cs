#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.Quest.Editor
{
    public static class TownQuestPanelPrefabGenerator
    {
        public const string PrefabFolder =
            "Assets/_Project/05.UI/05_City/Quest/Prefabs";
        public const string PrefabPath =
            PrefabFolder + "/TownQuestPanel.prefab";

        [InitializeOnLoadMethod]
        private static void GenerateOnceAfterImport()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                return;
            EditorApplication.delayCall += Generate;
        }

        [MenuItem("ND/UI/Quest/Generate Test Town Quest Panel")]
        public static void Generate()
        {
            EnsureFolder();
            GameObject root = CreateRoot();
            try
            {
                TownQuestPanelController controller =
                    root.AddComponent<TownQuestPanelController>();

                GameObject caravanSelectionPanel = Panel(
                    root.transform, "CaravanSelectionPanel");
                Text(
                    caravanSelectionPanel.transform,
                    "Title",
                    "퀘스트를 확인할 캐러반 선택",
                    34f,
                    58f);
                ScrollParts caravanSelectionScroll = Scroll(
                    caravanSelectionPanel.transform,
                    "CaravanSelectionScroll",
                    470f);
                Button caravanSelectionTemplate = Button(
                    caravanSelectionScroll.Content,
                    "CaravanSelectionRowTemplate",
                    "Caravan 1  —  BaseCamp",
                    76f);
                caravanSelectionTemplate.gameObject.SetActive(false);
                TMP_Text emptyCaravanSelectionText = Text(
                    caravanSelectionPanel.transform,
                    "EmptyCaravanSelectionText",
                    "선택할 수 있는 캐러반이 없습니다.",
                    22f,
                    54f);
                Button caravanSelectionClose = Button(
                    caravanSelectionPanel.transform,
                    "CaravanSelectionCloseButton",
                    "닫기",
                    58f);

                GameObject listPanel = Panel(root.transform, "QuestListPanel");
                TMP_Text questListContext = Text(
                    listPanel.transform,
                    "QuestListContextText",
                    "Caravan 1  /  BaseCamp",
                    22f,
                    42f,
                    FontStyles.Bold);
                Text(listPanel.transform, "Title", "마을 퀘스트", 34f, 58f);
                ScrollParts questScroll = Scroll(
                    listPanel.transform, "QuestListScroll", 470f);
                Button questTemplate = Button(
                    questScroll.Content, "QuestRowTemplate",
                    "퀘스트 이름\n[상태]", 76f);
                questTemplate.gameObject.SetActive(false);
                TMP_Text emptyText = Text(
                    listPanel.transform, "EmptyListText",
                    "현재 활성화된 퀘스트가 없습니다.", 22f, 54f);
                Button listClose = Button(
                    listPanel.transform, "ListCloseButton", "닫기", 58f);

                GameObject offerPanel = Panel(root.transform, "QuestOfferPanel");
                TMP_Text offerTitle = Text(
                    offerPanel.transform, "OfferTitle", "퀘스트 제안", 34f, 58f);
                TMP_Text offerDescription = Text(
                    offerPanel.transform, "OfferDescription",
                    "퀘스트 설명", 23f, 170f);
                TMP_Text offerCondition = Text(
                    offerPanel.transform, "OfferCondition",
                    "퀘스트 조건", 22f, 230f);
                Transform offerActions = Horizontal(
                    offerPanel.transform, "OfferActions", 66f);
                Button accept = Button(
                    offerActions, "AcceptButton", "수락", 60f);
                Button reject = Button(
                    offerActions, "RejectButton", "거절", 60f);

                GameObject progressPanel = Panel(
                    root.transform, "QuestProgressPanel");
                TMP_Text progressTitle = Text(
                    progressPanel.transform, "ProgressTitle",
                    "퀘스트 진행", 34f, 58f);
                TMP_Text progressDescription = Text(
                    progressPanel.transform, "ProgressDescription",
                    "조건을 충족한 캐러반이 발행 마을에 도착해야 합니다.",
                    23f, 150f);
                TMP_Text progressCondition = Text(
                    progressPanel.transform, "ProgressCondition",
                    "퀘스트 조건", 22f, 260f);
                Button progressClose = Button(
                    progressPanel.transform, "ProgressCloseButton", "닫기", 60f);

                GameObject paymentPanel = Panel(
                    root.transform, "QuestPaymentPanel", 840f);
                TMP_Text paymentTitle = Text(
                    paymentPanel.transform, "PaymentTitle",
                    "퀘스트 결제", 34f, 56f);
                TMP_Text paymentCondition = Text(
                    paymentPanel.transform, "PaymentCondition",
                    "결제 조건", 21f, 180f);
                Text(paymentPanel.transform, "CaravanHeader",
                    "조건 충족 캐러반 선택", 23f, 42f, FontStyles.Bold);
                ScrollParts caravanScroll = Scroll(
                    paymentPanel.transform, "CaravanListScroll", 230f);
                Button caravanTemplate = Button(
                    caravanScroll.Content, "CaravanRowTemplate",
                    "캐러반 1", 60f);
                caravanTemplate.gameObject.SetActive(false);
                TMP_Text selectedCaravan = Text(
                    paymentPanel.transform, "SelectedCaravanText",
                    "결제할 캐러반을 선택하세요.", 21f, 42f);
                TMP_Text errorText = Text(
                    paymentPanel.transform, "PaymentErrorText",
                    string.Empty, 19f, 48f);
                errorText.color = new Color(1f, 0.5f, 0.45f);
                Transform paymentActions = Horizontal(
                    paymentPanel.transform, "PaymentActions", 68f);
                Button currency = Button(
                    paymentActions, "TradingCurrencyPaymentButton",
                    "골드 결제", 60f);
                Button items = Button(
                    paymentActions, "AllItemsPaymentButton",
                    "아이템 전체 제출", 60f);
                Button cancel = Button(
                    paymentActions, "PaymentCancelButton", "취소", 60f);

                SerializedObject serialized = new SerializedObject(controller);
                Set(serialized, "caravanSelectionPanel", caravanSelectionPanel);
                Set(serialized, "listPanel", listPanel);
                Set(serialized, "offerPanel", offerPanel);
                Set(serialized, "progressPanel", progressPanel);
                Set(serialized, "paymentPanel", paymentPanel);
                Set(
                    serialized,
                    "caravanSelectionContainer",
                    caravanSelectionScroll.Content);
                Set(
                    serialized,
                    "caravanSelectionRowTemplate",
                    caravanSelectionTemplate);
                Set(
                    serialized,
                    "emptyCaravanSelectionText",
                    emptyCaravanSelectionText);
                Set(
                    serialized,
                    "caravanSelectionCloseButton",
                    caravanSelectionClose);
                Set(serialized, "questListContextText", questListContext);
                Set(serialized, "questListContainer", questScroll.Content);
                Set(serialized, "questRowTemplate", questTemplate);
                Set(serialized, "emptyListText", emptyText);
                Set(serialized, "listCloseButton", listClose);
                Set(serialized, "offerTitleText", offerTitle);
                Set(serialized, "offerDescriptionText", offerDescription);
                Set(serialized, "offerConditionText", offerCondition);
                Set(serialized, "acceptButton", accept);
                Set(serialized, "rejectButton", reject);
                Set(serialized, "progressTitleText", progressTitle);
                Set(serialized, "progressDescriptionText", progressDescription);
                Set(serialized, "progressConditionText", progressCondition);
                Set(serialized, "progressCloseButton", progressClose);
                Set(serialized, "paymentTitleText", paymentTitle);
                Set(serialized, "paymentConditionText", paymentCondition);
                Set(serialized, "caravanListContainer", caravanScroll.Content);
                Set(serialized, "caravanRowTemplate", caravanTemplate);
                Set(serialized, "selectedCaravanText", selectedCaravan);
                Set(serialized, "paymentErrorText", errorText);
                Set(serialized, "tradingCurrencyPaymentButton", currency);
                Set(serialized, "allItemsPaymentButton", items);
                Set(serialized, "paymentCancelButton", cancel);
                serialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject =
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Debug.Log($"[Quest UI] Generated {PrefabPath}");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateRoot()
        {
            GameObject root = new GameObject(
                "TownQuestPanel",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color =
                new Color(0f, 0f, 0f, 0.62f);
            return root;
        }

        private static GameObject Panel(
            Transform parent, string name, float height = 740f)
        {
            GameObject panel = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(VerticalLayoutGroup));
            panel.transform.SetParent(parent, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(760f, height);
            panel.GetComponent<Image>().color =
                new Color(0.09f, 0.11f, 0.15f, 0.98f);
            VerticalLayoutGroup layout =
                panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 28, 28);
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return panel;
        }

        private static ScrollParts Scroll(
            Transform parent, string name, float height)
        {
            GameObject scrollObject = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(ScrollRect),
                typeof(LayoutElement));
            scrollObject.transform.SetParent(parent, false);
            scrollObject.GetComponent<Image>().color =
                new Color(1f, 1f, 1f, 0.06f);
            scrollObject.GetComponent<LayoutElement>().preferredHeight = height;

            RectTransform viewport = Rect(scrollObject.transform, "Viewport");
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = Rect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            VerticalLayoutGroup layout =
                content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 10, 10);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            ContentSizeFitter fitter =
                content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            return new ScrollParts(content);
        }

        private static Transform Horizontal(
            Transform parent, string name, float height)
        {
            GameObject row = new GameObject(
                name, typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            row.GetComponent<LayoutElement>().preferredHeight = height;
            HorizontalLayoutGroup layout =
                row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row.transform;
        }

        private static Button Button(
            Transform parent, string name, string label, float height)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = new Color(0.22f, 0.34f, 0.5f, 1f);
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            TMP_Text text = Text(
                go.transform, "Label", label, 21f, height);
            Stretch(text.rectTransform);
            text.margin = new Vector4(16f, 4f, 16f, 4f);
            text.alignment = TextAlignmentOptions.Center;
            return button;
        }

        private static TMP_Text Text(
            Transform parent,
            string name,
            string value,
            float size,
            float height,
            FontStyles style = FontStyles.Normal)
        {
            GameObject go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            go.GetComponent<LayoutElement>().preferredHeight = height;
            return text;
        }

        private static RectTransform Rect(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            Stretch(rect);
            return rect;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Set(
            SerializedObject serialized, string property, Object value)
        {
            serialized.FindProperty(property).objectReferenceValue = value;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder(
                    "Assets/_Project/05.UI/05_City/Quest", "Prefabs");
        }

        private readonly struct ScrollParts
        {
            public ScrollParts(RectTransform content)
            {
                Content = content;
            }

            public RectTransform Content { get; }
        }
    }
}
#endif
