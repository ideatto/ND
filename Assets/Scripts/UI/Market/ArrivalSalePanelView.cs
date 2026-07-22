using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.Market
{
    /// <summary>Minimal runtime view for the arrival sell-only flow.</summary>
    public sealed class ArrivalSalePanelView : MonoBehaviour
    {
        [SerializeField] private MarketTradePanelController marketPanel;
        [SerializeField] private CaravanArrivalSaleController saleController;

        private GameObject panelRoot;
        private RectTransform itemRoot;
        private Text titleText;
        private Text summaryText;
        private Text errorText;
        private Font font;

        public void Configure(
            MarketTradePanelController panel,
            CaravanArrivalSaleController controller)
        {
            marketPanel = panel;
            saleController = controller;
        }

        private void Awake()
        {
            BuildView();
            panelRoot.SetActive(false);
        }

        private void OnEnable()
        {
            if (marketPanel != null)
                marketPanel.StateChanged += Render;
            if (saleController != null)
            {
                saleController.SaleOpened += HandleSaleOpened;
                saleController.ErrorChanged += HandleError;
                saleController.SettlementRequested += HandleSettlementRequested;
            }
        }

        private void OnDisable()
        {
            if (marketPanel != null)
                marketPanel.StateChanged -= Render;
            if (saleController != null)
            {
                saleController.SaleOpened -= HandleSaleOpened;
                saleController.ErrorChanged -= HandleError;
                saleController.SettlementRequested -= HandleSettlementRequested;
            }
        }

        private void HandleSaleOpened(string caravanId, string tradeId)
        {
            panelRoot.SetActive(true);
            titleText.text = $"도착 화물 판매  ·  {caravanId}";
            HandleError(string.Empty);
            Render(marketPanel?.Model?.Items ?? Array.Empty<MarketTradeItemState>());
        }

        private void HandleSettlementRequested(string caravanId, string tradeId)
        {
            panelRoot.SetActive(false);
        }

        private void HandleError(string error)
        {
            if (errorText != null)
                errorText.text = string.IsNullOrWhiteSpace(error) ? string.Empty : $"오류: {error}";
        }

        private void Render(IReadOnlyList<MarketTradeItemState> items)
        {
            if (itemRoot == null)
                return;
            for (int i = itemRoot.childCount - 1; i >= 0; i--)
                Destroy(itemRoot.GetChild(i).gameObject);

            int sellKinds = 0;
            long revenue = 0;
            foreach (MarketTradeItemState item in items ?? Array.Empty<MarketTradeItemState>())
            {
                if (item == null || item.CargoQuantity <= 0)
                    continue;
                CreateItemRow(item);
                if (item.SellDraftQuantity > 0)
                {
                    sellKinds++;
                    revenue += item.SellUnitPrice * item.SellDraftQuantity;
                }
            }
            summaryText.text = $"판매 선택 {sellKinds}종  /  예상 수익 {revenue}";
        }

        private void CreateItemRow(MarketTradeItemState item)
        {
            GameObject row = CreateUi("Item_" + item.ItemId, itemRoot, typeof(Image), typeof(HorizontalLayoutGroup));
            row.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 5, 5);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, 42f);

            string displayName = item.Item != null && !string.IsNullOrWhiteSpace(item.Item.DisplayName)
                ? item.Item.DisplayName
                : item.ItemId;
            CreateLabel(row.transform, $"{displayName}  보유 {item.CargoQuantity}  판매가 {item.SellUnitPrice}", 360f);
            CreateButton(row.transform, "-", 38f, () => ChangeDraft(item.ItemId, item.SellDraftQuantity - 1));
            CreateLabel(row.transform, item.SellDraftQuantity.ToString(), 50f);
            CreateButton(row.transform, "+", 38f, () => ChangeDraft(item.ItemId, item.SellDraftQuantity + 1));
            CreateButton(row.transform, "전부", 60f, () => ChangeDraft(item.ItemId, item.CargoQuantity));
        }

        private void ChangeDraft(string itemId, int quantity)
        {
            marketPanel?.SetSellDraft(itemId, Mathf.Max(0, quantity));
        }

        private void BuildView()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panelRoot = CreateUi("SalePanel", transform, typeof(Image));
            RectTransform panelRect = panelRoot.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(720f, 560f);
            panelRoot.GetComponent<Image>().color = new Color(0.09f, 0.1f, 0.12f, 0.98f);

            titleText = CreateAnchoredLabel(panelRoot.transform, "Title", "도착 화물 판매", 24,
                new Vector2(20f, -18f), new Vector2(-20f, -58f));
            summaryText = CreateAnchoredLabel(panelRoot.transform, "Summary", string.Empty, 18,
                new Vector2(20f, -62f), new Vector2(-20f, -96f));

            GameObject list = CreateUi("ItemList", panelRoot.transform, typeof(Image), typeof(VerticalLayoutGroup));
            RectTransform listRect = list.GetComponent<RectTransform>();
            listRect.anchorMin = new Vector2(0f, 0f);
            listRect.anchorMax = new Vector2(1f, 1f);
            listRect.offsetMin = new Vector2(20f, 92f);
            listRect.offsetMax = new Vector2(-20f, -108f);
            list.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.18f);
            var vertical = list.GetComponent<VerticalLayoutGroup>();
            vertical.padding = new RectOffset(8, 8, 8, 8);
            vertical.spacing = 5f;
            vertical.childForceExpandHeight = false;
            vertical.childForceExpandWidth = true;
            itemRoot = listRect;

            errorText = CreateAnchoredLabel(panelRoot.transform, "Error", string.Empty, 15,
                new Vector2(20f, 54f), new Vector2(-210f, 84f));
            errorText.color = new Color(1f, 0.45f, 0.4f);
            GameObject confirm = CreateButton(panelRoot.transform, "판매 확인", 170f,
                () => saleController?.ConfirmSaleFromUi());
            RectTransform confirmRect = confirm.GetComponent<RectTransform>();
            confirmRect.anchorMin = new Vector2(1f, 0f);
            confirmRect.anchorMax = new Vector2(1f, 0f);
            confirmRect.pivot = new Vector2(1f, 0f);
            confirmRect.anchoredPosition = new Vector2(-20f, 20f);
            confirmRect.sizeDelta = new Vector2(170f, 52f);
        }

        private GameObject CreateButton(Transform parent, string label, float width, UnityEngine.Events.UnityAction action)
        {
            GameObject result = CreateUi("Button_" + label, parent, typeof(Image), typeof(Button), typeof(LayoutElement));
            result.GetComponent<Image>().color = new Color(0.25f, 0.39f, 0.6f, 1f);
            result.GetComponent<LayoutElement>().preferredWidth = width;
            result.GetComponent<Button>().onClick.AddListener(action);
            Text text = CreateLabel(result.transform, label, width);
            RectTransform rect = text.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return result;
        }

        private Text CreateLabel(Transform parent, string value, float width)
        {
            GameObject label = CreateUi("Label", parent, typeof(Text), typeof(LayoutElement));
            Text text = label.GetComponent<Text>();
            text.font = font;
            text.fontSize = 17;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = value;
            label.GetComponent<LayoutElement>().preferredWidth = width;
            return text;
        }

        private Text CreateAnchoredLabel(Transform parent, string name, string value, int size, Vector2 min, Vector2 max)
        {
            GameObject label = CreateUi(name, parent, typeof(Text));
            RectTransform rect = label.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = min;
            rect.offsetMax = max;
            Text text = label.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = value;
            return text;
        }

        private static GameObject CreateUi(string name, Transform parent, params Type[] components)
        {
            var all = new Type[components.Length + 1];
            all[0] = typeof(RectTransform);
            Array.Copy(components, 0, all, 1, components.Length);
            var result = new GameObject(name, all);
            result.transform.SetParent(parent, false);
            return result;
        }
    }
}
