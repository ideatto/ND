using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ND.UI.InGame.TransportInventory
{
    public sealed class TransportInventoryPanelView : MonoBehaviour
    {
        [SerializeField] private TransportInventorySlotView[] slots;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text capacityText;
        [SerializeField] private TMP_Text requiredLevelText;
        [SerializeField] private TMP_Text unlockDescriptionText;
        [SerializeField] private GameObject lockedOverlay;
        [SerializeField] private ScrollRect scrollRect;

        public void ConfigureReferences(
            TransportInventorySlotView[] slotViews,
            TMP_Text title,
            TMP_Text capacity,
            TMP_Text requiredLevel,
            TMP_Text unlockDescription,
            GameObject overlay,
            ScrollRect scroll)
        {
            slots = slotViews ?? System.Array.Empty<TransportInventorySlotView>();
            titleText = title;
            capacityText = capacity;
            requiredLevelText = requiredLevel;
            unlockDescriptionText = unlockDescription;
            lockedOverlay = overlay;
            scrollRect = scroll;
        }

        private void Awake()
        {
            SetContentHeight(slots?.Length ?? 0);
        }

        public void SetTooltip(TransportInventoryTooltipView tooltip)
        {
            foreach (TransportInventorySlotView slot in slots ?? System.Array.Empty<TransportInventorySlotView>()) slot.SetTooltip(tooltip);
        }

        public void Render(TransportInventoryPanelViewData data)
        {
            if (data == null) return;
            if (titleText != null) titleText.text = data.Title;
            if (capacityText != null) capacityText.text = data.CapacityText;

            int count = Mathf.Min(slots.Length, data.Slots.Count);
            int visibleCount = Mathf.Min(count, data.AvailableSlots);
            for (int index = 0; index < slots.Length; index++)
            {
                bool visible = index < visibleCount;
                slots[index].gameObject.SetActive(visible);
                if (visible) slots[index].Render(data.Slots[index]);
                else slots[index].Clear();
            }

            SetContentHeight(data.Slots.Count);

            bool hasLockedArea = data.LockedStartIndex < data.Slots.Count;
            if (lockedOverlay != null) lockedOverlay.SetActive(hasLockedArea);
            if (hasLockedArea) PositionLockedOverlay(data.LockedStartIndex, data.Slots.Count);
            if (requiredLevelText != null)
                requiredLevelText.text = data.NextRequiredFarmLevel > 0 ? $"목장 Lv.{data.NextRequiredFarmLevel} 필요" : string.Empty;
            if (unlockDescriptionText != null)
                unlockDescriptionText.text = data.NextRequiredFarmLevel > 0 ? "목장 레벨을 올리면 추가 슬롯이 열립니다." : string.Empty;
        }

        public void ResetScrollPosition()
        {
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }

        private void PositionLockedOverlay(int lockedStartIndex, int totalSlots)
        {
            RectTransform rect = lockedOverlay != null ? lockedOverlay.transform as RectTransform : null;
            GridLayoutGroup grid = scrollRect?.content?.GetComponent<GridLayoutGroup>();
            if (rect == null || grid == null) return;
            int columns = Mathf.Max(1, grid.constraintCount);
            int unlockedRows = Mathf.CeilToInt(lockedStartIndex / (float)columns);
            int totalRows = Mathf.CeilToInt(totalSlots / (float)columns);
            const float overlayOverlap = 4f;
            float startY = grid.padding.top + unlockedRows * grid.cellSize.y
                + Mathf.Max(0, unlockedRows - 1) * grid.spacing.y + grid.spacing.y - overlayOverlap;
            float totalHeight = grid.padding.vertical + totalRows * grid.cellSize.y
                + Mathf.Max(0, totalRows - 1) * grid.spacing.y;
            rect.anchoredPosition = new Vector2(0f, -startY);
            rect.sizeDelta = new Vector2(0f, Mathf.Max(180f, totalHeight - startY));
            lockedOverlay.transform.SetAsLastSibling();
        }

        private void SetContentHeight(int totalSlots)
        {
            RectTransform content = scrollRect?.content;
            GridLayoutGroup grid = content?.GetComponent<GridLayoutGroup>();
            if (content == null || grid == null) return;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter != null) fitter.enabled = false;

            int columns = Mathf.Max(1, grid.constraintCount);
            int rows = Mathf.CeilToInt(Mathf.Max(0, totalSlots) / (float)columns);
            float height = grid.padding.vertical + rows * grid.cellSize.y
                + Mathf.Max(0, rows - 1) * grid.spacing.y;
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

    }
}
