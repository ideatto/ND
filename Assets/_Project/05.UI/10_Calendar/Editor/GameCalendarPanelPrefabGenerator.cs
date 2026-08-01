#if UNITY_EDITOR
using System.Collections.Generic;
using ND.UI.Calendar;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.CalendarEditor
{
    public static class GameCalendarPanelPrefabGenerator
    {
        public const string PrefabFolder = "Assets/_Project/05.UI/10_Calendar/Prefabs";
        public const string PrefabPath = PrefabFolder + "/GameCalendarPanel.prefab";

        [InitializeOnLoadMethod]
        private static void GenerateOnceAfterImport()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                EditorApplication.delayCall += Generate;
            }
        }

        [MenuItem("ND/UI/Calendar/Generate Example Calendar Panel")]
        public static void Generate()
        {
            EnsureFolder();
            GameObject root = CreateObject(null, "GameCalendarPanel", typeof(Image));
            try
            {
                RectTransform rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(760f, 300f);
                root.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.96f);
                var rootLayout = root.AddComponent<VerticalLayoutGroup>();
                rootLayout.padding = new RectOffset(28, 28, 24, 24);
                rootLayout.spacing = 12f;
                rootLayout.childControlWidth = true;
                rootLayout.childControlHeight = false;
                rootLayout.childForceExpandWidth = true;
                rootLayout.childForceExpandHeight = false;

                GameCalendarPanelView view = root.AddComponent<GameCalendarPanelView>();
                GameCalendarPanelPresenter presenter = root.AddComponent<GameCalendarPanelPresenter>();
                GameObject content = CreateObject(root.transform, "Content");
                var contentLayout = content.AddComponent<VerticalLayoutGroup>();
                contentLayout.spacing = 10f;
                contentLayout.childControlWidth = true;
                contentLayout.childControlHeight = false;
                contentLayout.childForceExpandWidth = true;
                contentLayout.childForceExpandHeight = false;

                Transform header = Row(content.transform, "Header", 42f);
                TMP_Text yearMonth = Text(header, "YearMonthText", "1년 1월", 30f, TextAlignmentOptions.MidlineLeft);
                TMP_Text season = Text(header, "SeasonText", "봄", 25f, TextAlignmentOptions.MidlineRight);

                GameObject disasterRow = CreateObject(content.transform, "DisasterRow", typeof(LayoutElement));
                disasterRow.GetComponent<LayoutElement>().preferredHeight = 36f;
                TMP_Text disaster = Text(disasterRow.transform, "DisasterText", "홍수", 22f, TextAlignmentOptions.Center);
                Stretch(disaster.rectTransform);

                GameObject months = CreateObject(content.transform, "Months", typeof(GridLayoutGroup), typeof(LayoutElement));
                months.GetComponent<LayoutElement>().preferredHeight = 112f;
                GridLayoutGroup grid = months.GetComponent<GridLayoutGroup>();
                grid.cellSize = new Vector2(106f, 48f);
                grid.spacing = new Vector2(10f, 10f);
                grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                grid.constraintCount = 6;

                var slots = new List<GameCalendarMonthSlotView>(12);
                for (int month = 1; month <= 12; month++)
                {
                    GameObject slotObject = CreateObject(months.transform, $"Month{month:00}", typeof(Image));
                    slotObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
                    GameObject highlight = CreateObject(slotObject.transform, "CurrentHighlight", typeof(Image));
                    Stretch(highlight.GetComponent<RectTransform>());
                    highlight.GetComponent<Image>().color = new Color(0.22f, 0.58f, 0.9f, 0.78f);
                    TMP_Text label = Text(slotObject.transform, "MonthText", month.ToString(), 22f, TextAlignmentOptions.Center);
                    Stretch(label.rectTransform);
                    label.transform.SetAsLastSibling();
                    GameCalendarMonthSlotView slot = slotObject.AddComponent<GameCalendarMonthSlotView>();
                    var slotSerialized = new SerializedObject(slot);
                    slotSerialized.FindProperty("month").intValue = month;
                    slotSerialized.FindProperty("monthText").objectReferenceValue = label;
                    slotSerialized.FindProperty("currentHighlight").objectReferenceValue = highlight;
                    slotSerialized.ApplyModifiedPropertiesWithoutUndo();
                    highlight.SetActive(month == 1);
                    slots.Add(slot);
                }

                var viewSerialized = new SerializedObject(view);
                Set(viewSerialized, "content", content);
                Set(viewSerialized, "yearMonthText", yearMonth);
                Set(viewSerialized, "seasonText", season);
                Set(viewSerialized, "disasterRow", disasterRow);
                Set(viewSerialized, "disasterText", disaster);
                SerializedProperty slotArray = viewSerialized.FindProperty("monthSlots");
                slotArray.arraySize = slots.Count;
                for (int i = 0; i < slots.Count; i++)
                {
                    slotArray.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                }
                viewSerialized.ApplyModifiedPropertiesWithoutUndo();

                var presenterSerialized = new SerializedObject(presenter);
                Set(presenterSerialized, "view", view);
                presenterSerialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static GameObject CreateObject(Transform parent, string name, params System.Type[] components)
        {
            var types = new List<System.Type> { typeof(RectTransform) };
            types.AddRange(components);
            GameObject result = new GameObject(name, types.ToArray());
            if (parent != null) result.transform.SetParent(parent, false);
            return result;
        }

        private static Transform Row(Transform parent, string name, float height)
        {
            GameObject row = CreateObject(parent, name, typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.GetComponent<LayoutElement>().preferredHeight = height;
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            return row.transform;
        }

        private static TMP_Text Text(Transform parent, string name, string value, float size, TextAlignmentOptions alignment)
        {
            GameObject textObject = CreateObject(parent, name, typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.text = value;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = alignment;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Set(SerializedObject serialized, string property, Object value)
        {
            serialized.FindProperty(property).objectReferenceValue = value;
        }

        private static void EnsureFolder()
        {
            const string root = "Assets/_Project/05.UI/10_Calendar";
            if (!AssetDatabase.IsValidFolder(root + "/Prefabs"))
            {
                AssetDatabase.CreateFolder(root, "Prefabs");
            }
        }
    }
}
#endif
