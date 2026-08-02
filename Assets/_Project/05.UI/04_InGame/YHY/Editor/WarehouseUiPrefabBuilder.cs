using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.InGame.Warehouse.Editor
{
    internal static class WarehouseUiPrefabBuilder
    {
        private const string Folder = "Assets/_Project/08.Prefabs/UI/Warehouse";
        private const string PopupPath = Folder + "/WarehouseInventoryPopup.prefab";
        private const string RowPath = Folder + "/WarehousePriceGroupRow.prefab";
        private const string ModalPath = Folder + "/WarehousePriceGroupModal.prefab";
        private const string TooltipPath = Folder + "/WarehouseItemTooltip.prefab";

        private static TMP_FontAsset font;
        private static readonly Color Dark = new Color32(30, 29, 27, 248);
        private static readonly Color Gold = new Color32(198, 152, 71, 255);
        private static readonly Color Line = new Color32(128, 105, 71, 255);

        [MenuItem("Tools/ND/Warehouse/Rebuild Selection UI Prefabs")]
        public static void Build()
        {
            LoadFont();
            BuildPriceGroupRow();
            BuildPriceGroupModal();
            BuildTooltip();
            IntegratePopupLayers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Warehouse UI] Price group selection and shared tooltip prefabs rebuilt.");
        }

        private static void LoadFont()
        {
            GameObject source = PrefabUtility.LoadPrefabContents(Folder + "/WarehouseQuantityModal.prefab");
            TMP_Text sample = source.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault();
            font = sample != null ? sample.font : null;
            PrefabUtility.UnloadPrefabContents(source);
        }

        private static void BuildPriceGroupRow()
        {
            GameObject root = Panel("WarehousePriceGroupRow", null, new Color32(48, 45, 40, 255));
            SetRect(root.GetComponent<RectTransform>(), 0, 0, 460, 54);
            root.GetComponent<Image>().raycastTarget = true;
            root.AddComponent<Button>();

            HorizontalLayoutGroup layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 6, 6);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;

            GameObject price = Text("PurchaseUnitPriceText", root.transform, "8 G", 24, TextAlignmentOptions.MidlineRight);
            price.GetComponent<TMP_Text>().margin = new Vector4(0, 0, 16, 0);
            price.AddComponent<LayoutElement>().preferredWidth = 210;
            GameObject divider = Panel("Divider", root.transform, Line);
            divider.AddComponent<LayoutElement>().preferredWidth = 2;
            GameObject quantity = Text("OwnedQuantityText", root.transform, "35개", 24, TextAlignmentOptions.MidlineRight);
            quantity.AddComponent<LayoutElement>().preferredWidth = 210;

            PrefabUtility.SaveAsPrefabAsset(root, RowPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildPriceGroupModal()
        {
            GameObject root = Panel("WarehousePriceGroupModal", null, Color.clear);
            SetRect(root.GetComponent<RectTransform>(), 0, 0, 520, 430);
            GameObject card = Panel("PriceGroupCard", root.transform, Dark);
            SetRect(card.GetComponent<RectTransform>(), 0, 0, 520, 430);
            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(2, -2);

            SetRect(Text("TitleText", card.transform, "구매가 선택", 30, TextAlignmentOptions.Center).GetComponent<RectTransform>(), 0, 177, 460, 48);
                        GameObject summary = Panel("SelectedItemSummary", card.transform, new Color32(44, 41, 36, 255));
            SetRect(summary.GetComponent<RectTransform>(), 0, 119, 460, 58);
            GameObject icon = Panel("ItemIcon", summary.transform, new Color32(135, 112, 112, 255));
            SetRect(icon.GetComponent<RectTransform>(), -190, 0, 46, 46);
            GameObject itemName = Text("SelectedItemNameText", summary.transform, "사과", 22, TextAlignmentOptions.Left);
            itemName.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            SetRect(itemName.GetComponent<RectTransform>(), 10, 13, 320, 28);
            GameObject totalQuantity = Text("SelectedItemTotalQuantityText", summary.transform, "총 보유 95개", 17, TextAlignmentOptions.Left);
            totalQuantity.GetComponent<TMP_Text>().color = new Color32(194, 184, 163, 255);
            SetRect(totalQuantity.GetComponent<RectTransform>(), 10, -14, 320, 24);

            GameObject header = Panel("FixedHeader", card.transform, new Color32(91, 72, 48, 255));
                        SetRect(header.GetComponent<RectTransform>(), 0, 64, 460, 44);
            HorizontalLayoutGroup headerLayout = header.AddComponent<HorizontalLayoutGroup>();
            headerLayout.padding = new RectOffset(16, 16, 4, 4);
            headerLayout.childControlWidth = true;
            headerLayout.childControlHeight = true;
            headerLayout.childForceExpandWidth = false;
            headerLayout.childForceExpandHeight = true;
            Text("PurchasePriceHeader", header.transform, "구매가", 22, TextAlignmentOptions.Center).AddComponent<LayoutElement>().preferredWidth = 210;
            Panel("Divider", header.transform, Line).AddComponent<LayoutElement>().preferredWidth = 2;
            Text("OwnedQuantityHeader", header.transform, "보유 개수", 22, TextAlignmentOptions.Center).AddComponent<LayoutElement>().preferredWidth = 210;

            GameObject viewport = Panel("RowsViewport", card.transform, new Color32(22, 22, 21, 255));
                        SetRect(viewport.GetComponent<RectTransform>(), 0, -44, 460, 168);
            viewport.AddComponent<RectMask2D>();
            GameObject content = Node("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup rowsLayout = content.AddComponent<VerticalLayoutGroup>();
            rowsLayout.spacing = 2;
            rowsLayout.childControlWidth = true;
            rowsLayout.childControlHeight = false;
            rowsLayout.childForceExpandWidth = true;
            rowsLayout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject template = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RowPath));
            template.name = "RowTemplate";
            template.transform.SetParent(content.transform, false);
            template.SetActive(false);
            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;

            GameObject cancel = Panel("CancelButton", card.transform, new Color32(86, 68, 48, 255));
            SetRect(cancel.GetComponent<RectTransform>(), 0, -178, 180, 48);
            cancel.GetComponent<Image>().raycastTarget = true;
            cancel.AddComponent<Button>();
            Stretch(Text("Label", cancel.transform, "취소", 22, TextAlignmentOptions.Center).GetComponent<RectTransform>());

            PrefabUtility.SaveAsPrefabAsset(root, ModalPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildTooltip()
        {
            GameObject root = Panel("WarehouseItemTooltip", null, Dark);
            SetRect(root.GetComponent<RectTransform>(), 0, 0, 420, 220);
            Outline outline = root.AddComponent<Outline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(2, -2);
            VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 18, 18);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            GameObject displayName = Text("DisplayNameText", root.transform, "아이템 이름", 28, TextAlignmentOptions.Left);
            displayName.GetComponent<TMP_Text>().fontStyle = FontStyles.Bold;
            displayName.AddComponent<LayoutElement>().minHeight = 38;
            LayoutElement dividerLayout = Panel("HeaderDivider", root.transform, Line).AddComponent<LayoutElement>();
            dividerLayout.minHeight = 2;
            dividerLayout.preferredHeight = 2;
            GameObject basePrice = Text("BasePriceText", root.transform, "기본 구매가  100 G", 20, TextAlignmentOptions.Left);
            basePrice.GetComponent<TMP_Text>().color = new Color32(224, 190, 117, 255);
            basePrice.AddComponent<LayoutElement>().minHeight = 28;
            GameObject description = Text("DescriptionText", root.transform, "아이템 설명이 여기에 표시됩니다.\n명시적인 줄바꿈과 전체 내용을 유지합니다.", 19, TextAlignmentOptions.TopLeft);
            description.GetComponent<TMP_Text>().color = new Color32(211, 205, 190, 255);
            description.GetComponent<TMP_Text>().overflowMode = TextOverflowModes.Overflow;
            description.AddComponent<LayoutElement>().minHeight = 36;
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;

            PrefabUtility.SaveAsPrefabAsset(root, TooltipPath);
            Object.DestroyImmediate(root);
        }

        private static void IntegratePopupLayers()
        {
            GameObject popup = PrefabUtility.LoadPrefabContents(PopupPath);
            Transform existingSelection = popup.transform.Find("SelectionModalLayer");
            Transform existingTooltip = popup.transform.Find("TooltipLayer");
            if (existingSelection != null) Object.DestroyImmediate(existingSelection.gameObject);
            if (existingTooltip != null) Object.DestroyImmediate(existingTooltip.gameObject);

            GameObject tooltipLayer = Node("TooltipLayer", popup.transform);
            Stretch(tooltipLayer.GetComponent<RectTransform>());
            GameObject tooltip = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TooltipPath), tooltipLayer.transform);
            tooltip.name = "SharedItemTooltip";
            tooltip.SetActive(false);
            RectTransform tooltipRect = tooltip.GetComponent<RectTransform>();
            tooltipRect.anchorMin = tooltipRect.anchorMax = new Vector2(0, 1);
            tooltipRect.pivot = new Vector2(0, 1);
            tooltipRect.anchoredPosition = new Vector2(760, -250);

            GameObject selectionLayer = Node("SelectionModalLayer", popup.transform);
            Stretch(selectionLayer.GetComponent<RectTransform>());
            selectionLayer.SetActive(false);
            GameObject blocker = Panel("ModalBlocker", selectionLayer.transform, new Color32(0, 0, 0, 145));
            Stretch(blocker.GetComponent<RectTransform>());
            blocker.GetComponent<Image>().raycastTarget = true;
            blocker.AddComponent<Button>();
            GameObject priceModal = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModalPath), selectionLayer.transform);
            priceModal.name = "PriceGroupModal";
            priceModal.SetActive(false);
            Transform quantityModal = popup.transform.Find("QuantityModal");
            if (quantityModal != null)
                quantityModal.SetParent(selectionLayer.transform, false);

            PrefabUtility.SaveAsPrefabAsset(popup, PopupPath);
            PrefabUtility.UnloadPrefabContents(popup);
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
            Image image = result.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return result;
        }

        private static GameObject Text(string name, Transform parent, string value, float size, TextAlignmentOptions alignment)
        {
            GameObject result = Node(name, parent);
            TextMeshProUGUI text = result.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = new Color32(239, 231, 211, 255);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return result;
        }

        private static void SetRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
