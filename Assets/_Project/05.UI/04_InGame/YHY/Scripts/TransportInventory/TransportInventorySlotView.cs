using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ND.UI.InGame.TransportInventory
{
    public sealed class TransportInventorySlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Image itemIcon;
        [SerializeField] private GameObject durabilityBadge;
        [SerializeField] private TMP_Text durabilityText;
        [SerializeField] private GameObject assignmentPlate;
        [SerializeField] private TMP_Text assignmentText;
        private TransportInventoryTooltipView tooltip;
        private TransportInventorySlotViewData data;
        private Color normalBackground;

        public void ConfigureReferences(
            Image slotBackground,
            Image icon,
            GameObject durability,
            TMP_Text durabilityLabel,
            GameObject assignment,
            TMP_Text assignmentLabel)
        {
            background = slotBackground;
            itemIcon = icon;
            durabilityBadge = durability;
            durabilityText = durabilityLabel;
            assignmentPlate = assignment;
            assignmentText = assignmentLabel;
        }

        private void Awake()
        {
            if (background != null) normalBackground = background.color;
            Clear();
        }

        public void SetTooltip(TransportInventoryTooltipView value) => tooltip = value;

        public void Render(TransportInventorySlotViewData value)
        {
            data = value;
            bool occupied = value != null && value.IsOccupied;
            bool unlocked = value != null && value.IsUnlocked;

            if (background != null)
                background.color = unlocked ? normalBackground : new Color(normalBackground.r * .55f, normalBackground.g * .55f, normalBackground.b * .55f, normalBackground.a);
            if (itemIcon != null)
            {
                itemIcon.enabled = occupied && value.Icon != null;
                itemIcon.sprite = occupied ? value.Icon : null;
                itemIcon.color = value != null && value.IsCatalogMissing ? new Color32(190, 80, 74, 255) : Color.white;
            }

            bool showDurability = occupied && value.HasDurability;
            if (durabilityBadge != null) durabilityBadge.SetActive(showDurability);
            if (durabilityText != null) durabilityText.text = showDurability ? $"{value.CurrentDurability} / {value.MaxDurability}" : string.Empty;

            bool assigned = occupied && value.IsAssigned;
            if (assignmentPlate != null) assignmentPlate.SetActive(assigned);
            if (assignmentText != null) assignmentText.text = assigned ? value.AssignmentText : string.Empty;
        }

        public void Clear()
        {
            data = null;
            if (itemIcon != null) { itemIcon.enabled = false; itemIcon.sprite = null; }
            if (durabilityBadge != null) durabilityBadge.SetActive(false);
            if (assignmentPlate != null) assignmentPlate.SetActive(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (data != null && data.IsOccupied) tooltip?.Show(data, transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData) => tooltip?.Hide();
    }
}
