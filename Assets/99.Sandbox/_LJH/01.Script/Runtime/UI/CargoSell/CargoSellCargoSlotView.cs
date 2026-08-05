using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.CargoSell
{
    /// <summary>Renders one saved purchase-price group and forwards its selection intent.</summary>
    public sealed class CargoSellCargoSlotView : MonoBehaviour
    {
        private Button button;
        private Image icon;
        private TMP_Text quantityText;
        private GameObject quantityBadge;
        private GameObject emptyLabel;
        private CargoSellCargoItemViewData data;
        private Action<CargoSellCargoItemViewData> clicked;

        public void Bind(
            CargoSellCargoItemViewData value,
            Action<CargoSellCargoItemViewData> onClicked)
        {
            Resolve();
            data = value;
            clicked = onClicked;
            bool occupied = value != null && !string.IsNullOrWhiteSpace(value.itemId);
            if (icon != null)
            {
                icon.sprite = occupied ? value.icon : null;
                icon.enabled = occupied && value.icon != null;
            }
            if (quantityText != null)
            {
                quantityText.text = occupied
                    ? Math.Max(0, value.cargoQuantity).ToString()
                    : string.Empty;
            }
            if (quantityBadge != null) quantityBadge.SetActive(occupied);
            if (emptyLabel != null) emptyLabel.SetActive(!occupied);
            if (button != null)
            {
                button.interactable = occupied;
                button.onClick.RemoveListener(NotifyClicked);
                button.onClick.AddListener(NotifyClicked);
            }
        }

        private void NotifyClicked()
        {
            if (data != null) clicked?.Invoke(data);
        }

        private void Resolve()
        {
            if (button != null) return;
            button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            icon = Find("ItemIcon")?.GetComponent<Image>();
            quantityText = Find("QuantityText")?.GetComponent<TMP_Text>();
            quantityBadge = Find("QuantityBadge")?.gameObject;
            emptyLabel = Find("EmptyLabel")?.gameObject;
        }

        private Transform Find(string objectName) =>
            GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(child => child.name == objectName);
    }
}
