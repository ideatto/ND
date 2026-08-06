using System.Linq;
using ND.UI.InGame.TransportInventory;
using ND.UI.InGame.Warehouse;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ND.UI.InGame.TransportInventory.Editor
{
    internal static class TransportInventoryUiPrefabBuilder
    {
        private const string Folder = "Assets/_Project/08.Prefabs/UI/TransportInventory";
        private const string PopupPath = Folder + "/TransportInventoryPopup.prefab";
        private const string SlotPath = Folder + "/TransportInventorySlot.prefab";
        private const string WarehouseSlotPath = "Assets/_Project/08.Prefabs/UI/Warehouse/WarehouseInventorySlot.prefab";
        private const string WarehouseTooltipPath = "Assets/_Project/08.Prefabs/UI/Warehouse/WarehouseItemTooltip.prefab";
        private const string WarehouseLockedOverlayPath = "Assets/_Project/08.Prefabs/UI/Warehouse/WarehouseLockedAreaOverlay.prefab";
        private const string FontSourcePath = "Assets/_Project/08.Prefabs/UI/Warehouse/WarehouseQuantityModal.prefab";
        private const string ScenePath = "Assets/_Project/07.Scenes/04_InGame/Build UI.unity";
        private const int GridColumns = 5;
        private const int MockFarmLevel = 1;
        private const int WagonSlotsPerLevel = 10;
        private const int AnimalSlotsPerLevel = 20;

        private static TMP_FontAsset font;
        private static readonly Color Backdrop = new Color32(10, 14, 18, 184);
        private static readonly Color Panel = new Color32(219, 217, 209, 255);
        private static readonly Color Panel2 = new Color32(168, 163, 153, 255);
        private static readonly Color Header = new Color32(214, 194, 166, 255);
        private static readonly Color Gold = new Color32(140, 117, 77, 255);
        private static readonly Color Line = new Color32(140, 117, 77, 255);
        private static readonly Color TextColor = new Color32(31, 36, 33, 255);

        [MenuItem("Tools/ND/Transport Inventory/Rebuild Runtime Prefabs")]
        public static void BuildPrefabs()
        {
            EnsureFolder();
            LoadFont();
            BuildSlot();
            BuildPopup();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Transport Inventory UI] Runtime prefabs rebuilt. No scene was modified.");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/08.Prefabs/UI/TransportInventory"))
                AssetDatabase.CreateFolder("Assets/_Project/08.Prefabs/UI", "TransportInventory");
        }

        private static void LoadFont()
        {
            GameObject source = PrefabUtility.LoadPrefabContents(FontSourcePath);
            font = source.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault()?.font;
            PrefabUtility.UnloadPrefabContents(source);
        }

        private static void BuildSlot()
        {
            GameObject warehouseSlot = AssetDatabase.LoadAssetAtPath<GameObject>(WarehouseSlotPath);
            GameObject root = (GameObject)PrefabUtility.InstantiatePrefab(warehouseSlot);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            root.name = "TransportInventorySlot";
            WarehouseInventorySlotView warehouseView = root.GetComponent<WarehouseInventorySlotView>();
            if (warehouseView != null) Object.DestroyImmediate(warehouseView);
            TransportInventorySlotView slotView = root.AddComponent<TransportInventorySlotView>();
            Image slotBackground = root.GetComponent<Image>();
            slotBackground.color = new Color32(246, 243, 234, 255);
            Outline slotOutline = root.GetComponent<Outline>();
            if (slotOutline != null)
            {
                slotOutline.effectColor = new Color32(105, 91, 70, 220);
                slotOutline.effectDistance = new Vector2(1.5f, -1.5f);
            }

            Transform quantityBadge = root.transform.Find("QuantityBadge");
            if (quantityBadge != null) quantityBadge.gameObject.SetActive(false);
            Transform emptyLabel = root.transform.Find("EmptyLabel");
            if (emptyLabel != null) emptyLabel.gameObject.SetActive(false);

            GameObject namePlate = PanelNode("NamePlate", root.transform, new Color32(214, 194, 166, 242));
            Anchor(namePlate.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 31));
            TextNode("NameText", namePlate.transform, "장착 중", 15, TextAlignmentOptions.Center);
            Stretch(namePlate.transform.Find("NameText").GetComponent<RectTransform>());

            GameObject badge = PanelNode("InfoBadge", root.transform, new Color32(168, 163, 153, 245));
            RectTransform badgeRect = badge.GetComponent<RectTransform>();
            badgeRect.anchorMin = badgeRect.anchorMax = new Vector2(1, 1);
            badgeRect.pivot = new Vector2(1, 1);
            badgeRect.anchoredPosition = new Vector2(-5, -5);
            badgeRect.sizeDelta = new Vector2(78, 23);
            TextNode("InfoText", badge.transform, "84 / 100", 12, TextAlignmentOptions.Center);
            Stretch(badge.transform.Find("InfoText").GetComponent<RectTransform>());

            slotView.ConfigureReferences(
                root.GetComponent<Image>(),
                root.transform.Find("ItemIcon")?.GetComponent<Image>(),
                badge,
                badge.transform.Find("InfoText")?.GetComponent<TMP_Text>(),
                namePlate,
                namePlate.transform.Find("NameText")?.GetComponent<TMP_Text>());

            PrefabUtility.SaveAsPrefabAsset(root, SlotPath);
            Object.DestroyImmediate(root);
        }

        private static void BuildPopup()
        {
            GameObject root = Node("TransportInventoryPopup", null);
            Stretch(root.GetComponent<RectTransform>());
            TransportInventoryPopupController controller = root.AddComponent<TransportInventoryPopupController>();

            GameObject backdrop = PanelNode("Backdrop", root.transform, Backdrop);
            Stretch(backdrop.GetComponent<RectTransform>());
            backdrop.GetComponent<Image>().raycastTarget = true;
            backdrop.AddComponent<Button>();

            GameObject card = PanelNode("InventoryCard", root.transform, Panel);
            card.GetComponent<Image>().raycastTarget = true;
            SetRect(card.GetComponent<RectTransform>(), 0, 0, 820, 638);
            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = Gold;
            outline.effectDistance = new Vector2(2, -2);

            GameObject header = PanelNode("Header", card.transform, Header);
            Anchor(header.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -82), Vector2.zero);
            TMP_Text title = TextNode("Title", header.transform, "목장", 32, TextAlignmentOptions.Center).GetComponent<TMP_Text>();
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, Vector2.zero, Vector2.one, new Vector2(80, 0), new Vector2(-80, 0));
            GameObject close = PanelNode("CloseButton", header.transform, new Color32(173, 69, 64, 224));
            SetRect(close.GetComponent<RectTransform>(), 350, 0, 54, 54);
            close.GetComponent<Image>().raycastTarget = true;
            close.AddComponent<Button>();
            TextNode("Label", close.transform, "X", 25, TextAlignmentOptions.Center);
            Stretch(close.transform.Find("Label").GetComponent<RectTransform>());

            GameObject tabs = Node("Tabs", card.transform);
            Anchor(tabs.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-280, -150), new Vector2(280, -92));
            CreateTab("WagonTab", "마차", tabs.transform, 0);
            CreateTab("DraftAnimalTab", "동물", tabs.transform, 280);

            CreateInventoryPanel("WagonPanel", card.transform, "마차 보관함", "08 / 10", true);
            CreateInventoryPanel("DraftAnimalPanel", card.transform, "동물 보관함", "14 / 20", false);

            GameObject footer = PanelNode("Footer", card.transform, Header);
            Anchor(footer.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 54));
            TextNode("GuideText", footer.transform, "마차와 동물은 개체당 한 칸을 사용합니다. 탭을 선택해 보유 개체를 확인하세요.", 17, TextAlignmentOptions.Center);
            Stretch(footer.transform.Find("GuideText").GetComponent<RectTransform>());

            GameObject tooltipAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WarehouseTooltipPath);
            if (tooltipAsset != null)
            {
                GameObject tooltip = (GameObject)PrefabUtility.InstantiatePrefab(tooltipAsset, root.transform);
                tooltip.name = "SharedItemTooltip";
                if (tooltip.GetComponent<TransportInventoryTooltipView>() == null)
                    tooltip.AddComponent<TransportInventoryTooltipView>();
                tooltip.SetActive(false);
                tooltip.transform.SetAsLastSibling();
            }

            TransportInventoryPanelView wagonPanel = root.GetComponentsInChildren<TransportInventoryPanelView>(true)
                .First(view => view.name == "WagonPanel");
            TransportInventoryPanelView animalPanel = root.GetComponentsInChildren<TransportInventoryPanelView>(true)
                .First(view => view.name == "DraftAnimalPanel");
            TransportInventoryTooltipView tooltipView = root.GetComponentInChildren<TransportInventoryTooltipView>(true);
            if (tooltipView != null)
            {
                tooltipView.ConfigureReferences(
                    FindText(tooltipView.transform, "DisplayNameText"),
                    FindText(tooltipView.transform, "BasePriceText"),
                    FindText(tooltipView.transform, "DescriptionText"),
                    root.GetComponent<RectTransform>());
            }
            Transform wagonTab = FindChild(root.transform, "WagonTab");
            Transform animalTab = FindChild(root.transform, "DraftAnimalTab");
            controller.ConfigureReferences(
                wagonPanel,
                animalPanel,
                tooltipView,
                wagonTab?.GetComponent<Button>(),
                animalTab?.GetComponent<Button>(),
                FindChild(root.transform, "CloseButton")?.GetComponent<Button>(),
                FindChild(root.transform, "Backdrop")?.GetComponent<Button>(),
                wagonTab?.Find("Label")?.GetComponent<TMP_Text>(),
                animalTab?.Find("Label")?.GetComponent<TMP_Text>());

            root.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PopupPath);
            Object.DestroyImmediate(root);
        }

        private static void CreateInventoryPanel(string name, Transform parent, string heading, string capacity, bool wagon)
        {
            GameObject panel = PanelNode(name, parent, Panel2);
            panel.AddComponent<TransportInventoryPanelView>();
            Anchor(panel.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(30, 72), new Vector2(-30, -166));
            panel.SetActive(wagon);

            TMP_Text title = TextNode("PanelTitle", panel.transform, heading, 27, TextAlignmentOptions.Center).GetComponent<TMP_Text>();
            title.fontStyle = FontStyles.Bold;
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(180, -52), new Vector2(-180, -12));
            TMP_Text count = TextNode("CapacityText", panel.transform, capacity, 21, TextAlignmentOptions.Right).GetComponent<TMP_Text>();
            count.color = new Color32(31, 36, 33, 255);
            Anchor(count.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-180, -52), new Vector2(-24, -12));
            GameObject divider = PanelNode("Divider", panel.transform, Line);
            Anchor(divider.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -64), new Vector2(-20, -62));

            GameObject viewport = PanelNode("Viewport", panel.transform, new Color32(212, 209, 199, 255));
            viewport.GetComponent<Image>().raycastTarget = true;
            Anchor(viewport.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(20, 20), new Vector2(-20, -78));
            viewport.AddComponent<RectMask2D>();
            GameObject content = Node("Content", viewport.transform);
            RectTransform contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = new Vector2(0, -24);
            contentRect.sizeDelta = new Vector2(-32, 0);
            GridLayoutGroup grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(112, 112);
            grid.spacing = new Vector2(24, 18);
            grid.padding = new RectOffset(16, 16, 18, 18);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = GridColumns;
            grid.childAlignment = TextAnchor.UpperLeft;
            int totalRows = Mathf.CeilToInt((wagon ? 50 : 100) / (float)GridColumns);
            float contentHeight = grid.padding.vertical + totalRows * grid.cellSize.y
                + Mathf.Max(0, totalRows - 1) * grid.spacing.y;
            contentRect.sizeDelta = new Vector2(contentRect.sizeDelta.x, contentHeight);

            GameObject templateAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SlotPath);
            int occupied = wagon ? 8 : 14;
            int maximumSlots = wagon ? 50 : 100;
            int unlockedSlots = (wagon ? WagonSlotsPerLevel : AnimalSlotsPerLevel) * MockFarmLevel;
            for (int i = 0; i < unlockedSlots; i++)
            {
                GameObject slot = (GameObject)PrefabUtility.InstantiatePrefab(templateAsset, content.transform);
                slot.name = $"Slot_{i + 1:00}";
                slot.SetActive(i < unlockedSlots);
                bool hasItem = i < occupied;
                Image icon = slot.transform.Find("ItemIcon")?.GetComponent<Image>();
                if (icon != null)
                {
                    icon.enabled = hasItem;
                    icon.color = wagon ? new Color32(190, 135, 73, 255) : new Color32(154, 118, 82, 255);
                }
                Transform plate = slot.transform.Find("NamePlate");
                if (plate != null) plate.gameObject.SetActive(false);
                TMP_Text nameText = plate?.Find("NameText")?.GetComponent<TMP_Text>();
                string displayName = wagon
                    ? $"마차 {i + 1:00}"
                    : $"동물 {i + 1:00}";
                if (nameText != null) nameText.text = displayName;
                Transform badge = slot.transform.Find("InfoBadge");
                if (badge != null) badge.gameObject.SetActive(false);
                TMP_Text badgeText = badge?.Find("InfoText")?.GetComponent<TMP_Text>();
                if (badgeText != null) badgeText.text = string.Empty;
                if (!hasItem)
                {
                    Transform empty = slot.transform.Find("EmptyLabel");
                    if (empty != null)
                    {
                        empty.gameObject.SetActive(false);
                        TMP_Text emptyText = empty.GetComponent<TMP_Text>();
                        if (emptyText != null) emptyText.text = string.Empty;
                    }
                }
            }

            CreateLockedAreaOverlay(content.transform, maximumSlots, unlockedSlots);

            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.content = contentRect;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.08f;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 35f;

            TransportInventoryPanelView panelView = panel.GetComponent<TransportInventoryPanelView>();
            Transform overlay = content.transform.Find("LockedAreaOverlay");
            panelView.ConfigureReferences(
                content.GetComponentsInChildren<TransportInventorySlotView>(true),
                templateAsset.GetComponent<TransportInventorySlotView>(),
                contentRect,
                title,
                count,
                overlay != null ? FindText(overlay, "RequiredLevelText") : null,
                overlay != null ? FindText(overlay, "UnlockDescriptionText") : null,
                overlay != null ? overlay.gameObject : null,
                scroll);
        }

        private static void CreateLockedAreaOverlay(Transform content, int maximumSlots, int unlockedSlots)
        {
            GameObject overlayAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WarehouseLockedOverlayPath);
            if (overlayAsset == null || unlockedSlots >= maximumSlots) return;

            GameObject overlay = (GameObject)PrefabUtility.InstantiatePrefab(overlayAsset, content);
            overlay.name = "LockedAreaOverlay";
            overlay.SetActive(true);
            LayoutElement layout = overlay.GetComponent<LayoutElement>();
            if (layout == null) layout = overlay.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;

            const float cellHeight = 112f;
            const float spacing = 18f;
            const float padding = 18f;
            int unlockedRows = Mathf.CeilToInt(unlockedSlots / (float)GridColumns);
            int totalRows = Mathf.CeilToInt(maximumSlots / (float)GridColumns);
            const float overlayOverlap = 4f;
            float startY = padding + unlockedRows * cellHeight
                + Mathf.Max(0, unlockedRows - 1) * spacing + spacing - overlayOverlap;
            float totalHeight = padding * 2f + totalRows * cellHeight + Mathf.Max(0, totalRows - 1) * spacing;

            RectTransform rect = overlay.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -startY);
            rect.sizeDelta = new Vector2(0, Mathf.Max(180f, totalHeight - startY));
            overlay.transform.SetAsLastSibling();

            TMP_Text required = overlay.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == "RequiredLevelText");
            TMP_Text description = overlay.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.name == "UnlockDescriptionText");
            if (required != null) required.text = "목장 Lv.2 필요";
            if (description != null) description.text = "목장 레벨을 올리면 추가 슬롯이 열립니다.";
        }

        private static void CreateTab(string name, string label, Transform parent, float x)
        {
            GameObject tab = PanelNode(name, parent, Panel2);
            RectTransform rect = tab.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(x, 0);
            rect.sizeDelta = new Vector2(266, 58);
            tab.GetComponent<Image>().raycastTarget = true;
            tab.AddComponent<Button>();
            TMP_Text text = TextNode("Label", tab.transform, label, 20, TextAlignmentOptions.Center).GetComponent<TMP_Text>();
            text.fontStyle = FontStyles.Bold;
            Stretch(text.rectTransform);
        }

        private static void PlaceInScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Canvas canvas = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
            if (canvas == null) throw new System.InvalidOperationException("Build UI scene has no Canvas.");
            Transform existing = canvas.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "TransportInventoryPopup");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PopupPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
            instance.name = "TransportInventoryPopup";
            RectTransform rect = instance.GetComponent<RectTransform>();
            Stretch(rect);
            instance.SetActive(true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        private static GameObject Node(string name, Transform parent)
        {
            GameObject result = new GameObject(name, typeof(RectTransform));
            if (parent != null) result.transform.SetParent(parent, false);
            return result;
        }

        private static GameObject PanelNode(string name, Transform parent, Color color)
        {
            GameObject result = Node(name, parent);
            Image image = result.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return result;
        }

        private static GameObject TextNode(string name, Transform parent, string value, float size, TextAlignmentOptions alignment)
        {
            GameObject result = Node(name, parent);
            TextMeshProUGUI text = result.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = TextColor;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal;
            return result;
        }

        private static Transform FindChild(Transform root, string objectName) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == objectName);

        private static TMP_Text FindText(Transform root, string objectName) =>
            root.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(text => text.name == objectName);

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

        private static void Anchor(RectTransform rect, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
