using TMPro;
using UnityEngine;

namespace ND.UI.InGame.TransportInventory
{
    public sealed class TransportInventoryTooltipView : MonoBehaviour
    {
        [SerializeField] private TMP_Text displayNameText;
        [SerializeField] private TMP_Text basePriceText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private RectTransform bounds;
        private RectTransform rect;

        public void ConfigureReferences(
            TMP_Text displayName,
            TMP_Text basePrice,
            TMP_Text description,
            RectTransform positionBounds)
        {
            displayNameText = displayName;
            basePriceText = basePrice;
            descriptionText = description;
            bounds = positionBounds;
        }

        private void Awake()
        {
            rect = transform as RectTransform;
            Hide();
        }

        public void Show(TransportInventorySlotViewData data, RectTransform anchor)
        {
            if (data == null || !data.IsOccupied || anchor == null) return;
            if (displayNameText != null) displayNameText.text = data.DisplayName ?? string.Empty;
            if (basePriceText != null) basePriceText.text = $"기본 구매가  {System.Math.Max(0L, data.BaseBuyPrice)} G";
            if (descriptionText != null)
            {
                string assignment = data.IsAssigned ? $"\n{data.AssignmentText}" : string.Empty;
                descriptionText.text = (data.Description ?? string.Empty) + assignment;
            }

            gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            PositionNextTo(anchor);
        }

        public void Hide() => gameObject.SetActive(false);

        private void PositionNextTo(RectTransform anchor)
        {
            if (rect == null || bounds == null) return;
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(bounds, screen, null, out Vector2 local)) return;

            Vector2 position = local + new Vector2(18f, -12f);
            Rect area = bounds.rect;
            Vector2 size = rect.rect.size;
            position.x = Mathf.Clamp(position.x, area.xMin, area.xMax - size.x);
            position.y = Mathf.Clamp(position.y, area.yMin + size.y, area.yMax);
            rect.anchoredPosition = position;
        }

    }
}
