#if UNITY_EDITOR
using System.Collections.Generic;
using ND.UI.RescueLoan;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ND.UI.RescueLoanEditor
{
    public static class RescueLoanPanelPrefabGenerator
    {
        public const string PrefabPath = "Assets/_Project/05.UI/12_RescueLoan/Prefabs/RescueLoanPanel.prefab";
        public const string InGameScenePath = "Assets/_Project/07.Scenes/04_InGame/InGame.unity";

        [MenuItem("ND/UI/Build Rescue Loan Panel")]
        public static void Build()
        {
            GameObject root = Create(null, "RescueLoanPanel");
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = Vector2.zero;

                Button launcher = MakeButton(root.transform, "LoanLauncherButton", "구조 대출", 150f, 44f);
                PanelOpener panelOpener = launcher.gameObject.AddComponent<PanelOpener>();
                RectTransform launcherRect = launcher.GetComponent<RectTransform>();
                launcherRect.anchorMin = launcherRect.anchorMax = new Vector2(0.5f, 0f);
                launcherRect.pivot = new Vector2(0.5f, 0.5f);
                launcherRect.anchoredPosition = new Vector2(390f, 400f);

                GameObject modal = Create(root.transform, "LoanModal", typeof(Image), typeof(CanvasGroup), typeof(VerticalLayoutGroup));
                RectTransform modalRect = modal.GetComponent<RectTransform>();
                modalRect.anchorMin = modalRect.anchorMax = new Vector2(0.5f, 0.5f);
                modalRect.sizeDelta = new Vector2(520f, 570f);
                modal.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.98f);
                CanvasGroup modalGroup = modal.GetComponent<CanvasGroup>();
                modalGroup.alpha = 1f; modalGroup.interactable = true; modalGroup.blocksRaycasts = true;
                VerticalLayoutGroup layout = modal.GetComponent<VerticalLayoutGroup>();
                layout.padding = new RectOffset(28, 28, 24, 24); layout.spacing = 10f;
                layout.childControlWidth = true; layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;

                TMP_Text title = MakeText(modal.transform, "Title", "구조 대출 안내", 30f, 48f, TextAlignmentOptions.Center);
                TMP_Text status = MakeText(modal.transform, "Status", "상태 확인 중", 22f, 52f, TextAlignmentOptions.Center);
                TMP_Text money = MakeText(modal.transform, "TradeMoney", "현재 무역 자금  0", 20f, 36f);
                TMP_Text minimum = MakeText(modal.transform, "MinimumTradeCost", "최소 무역 비용  0", 20f, 36f);
                TMP_Text principal = MakeText(modal.transform, "OriginalPrincipal", "고정 대출 원금  0", 20f, 36f);
                TMP_Text remaining = MakeText(modal.transform, "RemainingPrincipal", "남은 원금  0", 20f, 36f);
                TMP_Text restriction = MakeText(modal.transform, "Restriction", "제한 상태: 없음", 18f, 44f);

                GameObject repaymentRow = Create(modal.transform, "RepaymentRow", typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                repaymentRow.GetComponent<LayoutElement>().preferredHeight = 50f;
                HorizontalLayoutGroup rowLayout = repaymentRow.GetComponent<HorizontalLayoutGroup>();
                rowLayout.spacing = 8f; rowLayout.childControlWidth = true; rowLayout.childControlHeight = true;
                rowLayout.childForceExpandWidth = true; rowLayout.childForceExpandHeight = true;
                TMP_InputField input = MakeInput(repaymentRow.transform);

                Button action = MakeButton(modal.transform, "ActionButton", "고정 원금 대출 받기", 0f, 50f);
                TMP_Text message = MakeText(modal.transform, "Message", string.Empty, 17f, 42f, TextAlignmentOptions.Center);
                Button close = MakeButton(modal.transform, "CloseButton", "닫기", 0f, 44f);

                RescueLoanPanelView view = root.AddComponent<RescueLoanPanelView>();
                RescueLoanPanelPresenter presenter = root.AddComponent<RescueLoanPanelPresenter>();
                RescueLoanPanelVisibility visibility = root.AddComponent<RescueLoanPanelVisibility>();
                SerializedObject viewData = new SerializedObject(view);
                Set(viewData, "launcherObject", launcher.gameObject); Set(viewData, "launcherLabel", launcher.GetComponentInChildren<TMP_Text>());
                Set(viewData, "titleText", title); Set(viewData, "statusText", status); Set(viewData, "tradeMoneyText", money);
                Set(viewData, "minimumTradeCostText", minimum); Set(viewData, "principalText", principal);
                Set(viewData, "remainingPrincipalText", remaining); Set(viewData, "restrictionText", restriction);
                Set(viewData, "messageText", message); Set(viewData, "repaymentRow", repaymentRow);
                Set(viewData, "repaymentInput", input); Set(viewData, "actionButton", action);
                Set(viewData, "actionLabel", action.GetComponentInChildren<TMP_Text>()); viewData.ApplyModifiedPropertiesWithoutUndo();
                SerializedObject presenterData = new SerializedObject(presenter); Set(presenterData, "view", view); presenterData.ApplyModifiedPropertiesWithoutUndo();
                SerializedObject visibilityData = new SerializedObject(visibility);
                SerializedObject openerData = new SerializedObject(panelOpener);
                Set(openerData, "panel", modal); Set(openerData, "titleText", title);
                openerData.FindProperty("title").stringValue = "구조 대출";
                openerData.ApplyModifiedPropertiesWithoutUndo();
                Set(visibilityData, "modalObject", modal); Set(visibilityData, "panelOpener", panelOpener);
                Set(visibilityData, "launcherButton", launcher); Set(visibilityData, "closeButton", close);
                visibilityData.ApplyModifiedPropertiesWithoutUndo();
                modal.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            }
            finally { Object.DestroyImmediate(root); }
        }

        [MenuItem("ND/UI/Install Rescue Loan Panel InGame")]
        public static void InstallInGameScene()
        {
            Build();
            Scene scene = EditorSceneManager.OpenScene(InGameScenePath, OpenSceneMode.Single);
            var existingPanels = new List<GameObject>();
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                foreach (RescueLoanPanelVisibility panel in sceneRoot.GetComponentsInChildren<RescueLoanPanelVisibility>(true))
                {
                    if (!existingPanels.Contains(panel.gameObject)) existingPanels.Add(panel.gameObject);
                }
            }
            foreach (GameObject existingPanel in existingPanels) Object.DestroyImmediate(existingPanel);

            Canvas canvas = FindNamedCanvas(scene, "MainUICanvas");
            if (canvas == null) throw new System.InvalidOperationException("MainUICanvas was not found.");
            GameObject panelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, canvas.transform);
            RectTransform rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            instance.transform.SetAsLastSibling();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
        }

        private static GameObject Create(Transform parent, string name, params System.Type[] components)
        { var types = new List<System.Type> { typeof(RectTransform) }; types.AddRange(components); GameObject go = new GameObject(name, types.ToArray()); if (parent != null) go.transform.SetParent(parent, false); return go; }
        private static TMP_Text MakeText(Transform parent, string name, string value, float size, float height, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
        { GameObject go = Create(parent, name, typeof(CanvasRenderer), typeof(TextMeshProUGUI), typeof(LayoutElement)); go.GetComponent<LayoutElement>().preferredHeight = height; TMP_Text t = go.GetComponent<TMP_Text>(); t.text = value; t.fontSize = size; t.color = Color.white; t.alignment = align; return t; }
        private static Button MakeButton(Transform parent, string name, string label, float width, float height)
        { GameObject go = Create(parent, name, typeof(Image), typeof(Button), typeof(LayoutElement)); LayoutElement le = go.GetComponent<LayoutElement>(); le.preferredHeight = height; if (width > 0f) { le.preferredWidth = width; le.flexibleWidth = 0f; } go.GetComponent<Image>().color = new Color(0.25f, 0.39f, 0.58f, 1f); TMP_Text t = MakeText(go.transform, "Label", label, 20f, height, TextAlignmentOptions.Center); Stretch(t.rectTransform); return go.GetComponent<Button>(); }
        private static TMP_InputField MakeInput(Transform parent)
        { GameObject go = Create(parent, "RepaymentInput", typeof(Image), typeof(TMP_InputField)); go.GetComponent<Image>().color = new Color(1f,1f,1f,0.12f); TMP_Text value = MakeText(go.transform,"Text",string.Empty,20f,50f); Stretch(value.rectTransform); TMP_Text placeholder = MakeText(go.transform,"Placeholder","상환 금액 입력",18f,50f); placeholder.color = new Color(1f,1f,1f,0.45f); Stretch(placeholder.rectTransform); TMP_InputField input=go.GetComponent<TMP_InputField>(); input.textComponent=value; input.placeholder=placeholder; input.contentType=TMP_InputField.ContentType.IntegerNumber; return input; }
        private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=Vector2.zero; rect.offsetMax=Vector2.zero; }
        private static void Set(SerializedObject so, string name, Object value) => so.FindProperty(name).objectReferenceValue = value;
        private static T FindInScene<T>(Scene scene) where T:Component { foreach(GameObject root in scene.GetRootGameObjects()){T found=root.GetComponentInChildren<T>(true);if(found!=null)return found;}return null; }
        private static Canvas FindNamedCanvas(Scene scene,string name){foreach(GameObject root in scene.GetRootGameObjects())foreach(Canvas c in root.GetComponentsInChildren<Canvas>(true))if(c.name==name)return c;return null;}
    }
}
#endif
