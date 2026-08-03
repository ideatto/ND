using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace ND.UI.InGame.Warehouse
{
    /// <summary>
    /// 저장 객체를 보관하지 않고 한 Refresh의 ViewData와 사용자 의도만 전달한다.
    /// 전송/rollback 뒤 오래된 SaveData row 참조를 사용하지 않기 위해서다.
    /// </summary>
    public sealed class WarehouseInventorySlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Button button;
        private Image itemIcon;
        private TMP_Text quantityText;
        private GameObject quantityBadge;
        private GameObject emptyLabel;
        private string itemId = string.Empty;
        private Action<string> clicked;
        private Action<string, RectTransform> hovered;
        private Action hoverExited;

        private void Awake() => Resolve();

        /// <summary>Binds render-only data and forwards click or hover intent without owning inventory state.</summary>
        public void Bind(WarehouseInventorySlotViewData data, Action<string> onClicked,
            Action<string, RectTransform> onHovered, Action onHoverExited)
        {
            Resolve();
            itemId = data != null ? data.ItemId ?? string.Empty : string.Empty;
            clicked = onClicked;
            hovered = onHovered;
            hoverExited = onHoverExited;
            bool occupied = data != null && !string.IsNullOrEmpty(itemId);
            if (itemIcon != null)
            {
                itemIcon.sprite = occupied ? data.Icon : null;
                itemIcon.enabled = occupied && data.Icon != null;
            }
            if (quantityText != null) quantityText.text = occupied ? data.StackQuantity.ToString() : string.Empty;
            if (quantityBadge != null) quantityBadge.SetActive(occupied);
            if (emptyLabel != null) emptyLabel.SetActive(!occupied);
            if (button != null)
            {
                button.interactable = occupied;
                button.onClick.RemoveListener(NotifyClicked);
                button.onClick.AddListener(NotifyClicked);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(itemId)) hovered?.Invoke(itemId, transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData) => hoverExited?.Invoke();

        private void NotifyClicked()
        {
            if (!string.IsNullOrEmpty(itemId)) clicked?.Invoke(itemId);
        }

        private void Resolve()
        {
            if (button != null) return;
            button = GetComponent<Button>() ?? gameObject.AddComponent<Button>();
            itemIcon = Find("ItemIcon")?.GetComponent<Image>();
            quantityText = Find("QuantityText")?.GetComponent<TMP_Text>();
            quantityBadge = Find("QuantityBadge")?.gameObject;
            emptyLabel = Find("EmptyLabel")?.gameObject;
        }

        private Transform Find(string objectName) =>
            GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == objectName);
    }
}