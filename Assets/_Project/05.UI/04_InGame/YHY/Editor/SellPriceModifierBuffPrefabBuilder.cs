using System.Linq;
using ND.UI.InGame.SellPriceModifierBuff;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.InGame.SellPriceModifierBuff.Editor
{
    internal static class SellPriceModifierBuffPrefabBuilder
    {
        private const string Folder = "Assets/_Project/08.Prefabs/UI/SellPriceModifierBuff";
        private const string PrefabPath = Folder + "/SellPriceModifierBuffBar.prefab";
        private const string FontSourcePath = "Assets/_Project/08.Prefabs/UI/Warehouse/WarehouseQuantityModal.prefab";

        [MenuItem("Tools/ND/Sell Price Modifier Buff/Rebuild Prefab")]
        public static void BuildPrefab()
        {
            EnsureFolder();
            TMP_FontAsset font = LoadFont();
            GameObject root = Node("SellPriceModifierBuffBar", null);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(204f, 60f);
            HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            SellPriceModifierBuffBarController controller = root.AddComponent<SellPriceModifierBuffBarController>();
            SellPriceModifierBuffView season = CreateBuff("SeasonBuff", root.transform);
            SellPriceModifierBuffView distance = CreateBuff("DistanceBuff", root.transform);
            SellPriceModifierBuffView lucky = CreateBuff("LuckyMoneyBuff", root.transform);

            GameObject tooltipObject = Panel("SharedTooltip", root.transform, new Color32(30, 34, 32, 245));
            RectTransform tooltipRect = tooltipObject.GetComponent<RectTransform>();
            tooltipRect.sizeDelta = new Vector2(390f, 240f);
            tooltipRect.pivot = new Vector2(0f, 1f);
            VerticalLayoutGroup tooltipLayout = tooltipObject.AddComponent<VerticalLayoutGroup>();
            tooltipLayout.padding = new RectOffset(18, 18, 14, 14);
            tooltipLayout.spacing = 10f;
            tooltipLayout.childControlHeight = true;
            tooltipLayout.childControlWidth = true;
            ContentSizeFitter fitter = tooltipObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            TMP_Text title = Text("TitleText", tooltipObject.transform, "판매 효과", 22f, font);
            title.fontStyle = FontStyles.Bold;
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            TMP_Text body = Text("BodyText", tooltipObject.transform, string.Empty, 17f, font);
            body.gameObject.AddComponent<LayoutElement>().minHeight = 40f;
            SellPriceModifierBuffTooltipController tooltip = tooltipObject.AddComponent<SellPriceModifierBuffTooltipController>();
            tooltip.ConfigureReferences(tooltipRect, null, title, body);
            tooltipObject.SetActive(false);
            controller.ConfigureReferences(season, distance, lucky, tooltip);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Sell Price Modifier Buff] Prefab rebuilt. No scene was modified.");
        }

        private static SellPriceModifierBuffView CreateBuff(string name, Transform parent)
        {
            GameObject node = Panel(name, parent, new Color32(71, 78, 72, 255));
            RectTransform rect = node.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(60f, 60f);
            LayoutElement layout = node.AddComponent<LayoutElement>();
            layout.preferredWidth = 60f;
            layout.preferredHeight = 60f;
            Image icon = Panel("Icon", node.transform, Color.white).GetComponent<Image>();
            RectTransform iconRect = icon.rectTransform;
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(6f, 6f);
            iconRect.offsetMax = new Vector2(-6f, -6f);
            icon.raycastTarget = false;
            SellPriceModifierBuffView view = node.AddComponent<SellPriceModifierBuffView>();
            view.ConfigureReferences(icon);
            return view;
        }

        private static GameObject Node(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform));
            if (parent != null) result.transform.SetParent(parent, false);
            return result;
        }

        private static GameObject Panel(string name, Transform parent, Color color)
        {
            GameObject result = Node(name, parent);
            Image image = result.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return result;
        }

        private static TMP_Text Text(string name, Transform parent, string value, float size, TMP_FontAsset font)
        {
            GameObject result = Node(name, parent);
            TextMeshProUGUI text = result.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/08.Prefabs/UI/SellPriceModifierBuff"))
                AssetDatabase.CreateFolder("Assets/_Project/08.Prefabs/UI", "SellPriceModifierBuff");
        }

        private static TMP_FontAsset LoadFont()
        {
            GameObject source = PrefabUtility.LoadPrefabContents(FontSourcePath);
            TMP_FontAsset font = source.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault()?.font;
            PrefabUtility.UnloadPrefabContents(source);
            return font;
        }
    }
}
