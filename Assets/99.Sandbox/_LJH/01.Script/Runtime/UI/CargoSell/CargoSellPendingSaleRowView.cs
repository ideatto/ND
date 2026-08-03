using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ND.UI.CargoSell
{
    [Serializable]
    public sealed class CargoSellPendingSaleRowViewData
    {
        public string ItemId = string.Empty;
        public long PurchaseUnitPrice;
        public string DisplayName = string.Empty;
        public Sprite Icon;
        public int Quantity;
        public long SellUnitPrice;

        public long LineRevenue
        {
            get
            {
                decimal value = (decimal)Math.Max(0L, SellUnitPrice) * Math.Max(0, Quantity);
                return value >= long.MaxValue ? long.MaxValue : (long)value;
            }
        }
    }

    /// <summary>
    /// Renders one pending-sale draft row. It owns no Cargo or SaveData and only forwards user intent.
    /// </summary>
    public sealed class CargoSellPendingSaleRowView : MonoBehaviour, IPointerClickHandler
    {
        private Button button;
        private Image itemIcon;
        private TMP_Text itemNameText;
        private TMP_Text quantityText;
        private TMP_Text unitSellPriceText;
        private TMP_Text lineRevenueText;
        private CargoSellPendingSaleRowViewData data;
        private Action<CargoSellPendingSaleRowViewData> leftClicked;
        private Action<string> rightClicked;

        public void Bind(
            CargoSellPendingSaleRowViewData value,
            Action<CargoSellPendingSaleRowViewData> onLeftClicked,
            Action<string> onRightClicked)
        {
            Resolve();
            data = value;
            leftClicked = onLeftClicked;
            rightClicked = onRightClicked;

            if (itemIcon != null)
            {
                itemIcon.sprite = value != null ? value.Icon : null;
                itemIcon.enabled = value != null && value.Icon != null;
            }

            if (itemNameText != null) itemNameText.text = value != null ? value.DisplayName : string.Empty;
            if (quantityText != null) quantityText.text = value != null ? Math.Max(0, value.Quantity) + "개" : string.Empty;
            if (unitSellPriceText != null) unitSellPriceText.text = value != null ? Math.Max(0L, value.SellUnitPrice) + "G" : string.Empty;
            if (lineRevenueText != null) lineRevenueText.text = value != null ? value.LineRevenue + "G" : string.Empty;

            if (button != null)
            {
                button.onClick.RemoveListener(NotifyLeftClick);
                button.onClick.AddListener(NotifyLeftClick);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button == PointerEventData.InputButton.Right && data != null)
                rightClicked?.Invoke(data.ItemId);
        }

        private void NotifyLeftClick()
        {
            if (data != null) leftClicked?.Invoke(data);
        }

        private void Resolve()
        {
            if (button != null) return;
            button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            itemIcon = Find("ItemIcon")?.GetComponent<Image>();
            itemNameText = Find("ItemNameText")?.GetComponent<TMP_Text>();
            quantityText = Find("QuantityText")?.GetComponent<TMP_Text>();
            unitSellPriceText = Find("UnitSellPriceText")?.GetComponent<TMP_Text>();
            lineRevenueText = Find("LineRevenueText")?.GetComponent<TMP_Text>();
        }

        private Transform Find(string objectName)
        {
            return GetComponentsInChildren<Transform>(true).FirstOrDefault(child => child.name == objectName);
        }
    }
}
