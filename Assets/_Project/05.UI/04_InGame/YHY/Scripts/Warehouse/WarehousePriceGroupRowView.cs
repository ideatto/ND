using System;
using System.Linq;
using ND.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.InGame.Warehouse
{
    /// <summary>전체 item 수량이 아니라 선택한 purchaseUnitPrice 그룹만 전달한다.</summary>
    public sealed class WarehousePriceGroupRowView : MonoBehaviour
    {
        private Button button;
        private TMP_Text priceText;
        private TMP_Text quantityText;
        private WarehousePriceGroup group;
        private Action<WarehousePriceGroup> clicked;

        private void Awake() => Resolve();

        /// <summary>Forwards exactly one purchase-price group rather than the item's aggregate quantity.</summary>
        public void Bind(WarehousePriceGroup value, Action<WarehousePriceGroup> onClicked)
        {
            Resolve();
            group = value;
            clicked = onClicked;
            if (priceText != null) priceText.text = value.PurchaseUnitPrice + " G";
            if (quantityText != null) quantityText.text = value.Quantity + "개";
            if (button != null)
            {
                button.onClick.RemoveListener(NotifyClicked);
                button.onClick.AddListener(NotifyClicked);
            }
        }

        private void NotifyClicked() => clicked?.Invoke(group);

        private void Resolve()
        {
            if (button != null) return;
            button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            priceText = Find("PurchaseUnitPriceText")?.GetComponent<TMP_Text>();
            quantityText = Find("OwnedQuantityText")?.GetComponent<TMP_Text>();
        }

        private Transform Find(string objectName) =>
            GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == objectName);
    }
}