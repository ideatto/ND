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
            RectTransform parent = rect != null ? rect.parent as RectTransform : null;
            if (parent == null || bounds == null) return;

            Vector3[] anchorCorners = new Vector3[4];
            Vector3[] boundCorners = new Vector3[4];
            anchor.GetWorldCorners(anchorCorners);
            bounds.GetWorldCorners(boundCorners);
            Vector2 anchorTopLeft = parent.InverseTransformPoint(anchorCorners[1]);
            Vector2 anchorTopRight = parent.InverseTransformPoint(anchorCorners[2]);
            Vector2 boundsBottomLeft = parent.InverseTransformPoint(boundCorners[0]);
            Vector2 boundsTopRight = parent.InverseTransformPoint(boundCorners[2]);
            Vector2 size = rect.rect.size;
            const float gap = 12f;
            bool placeRight = anchorTopRight.x + gap + size.x <= boundsTopRight.x;
            rect.pivot = placeRight ? new Vector2(0f, 1f) : new Vector2(1f, 1f);

            float x = placeRight ? anchorTopRight.x + gap : anchorTopLeft.x - gap;
            float minX = placeRight ? boundsBottomLeft.x : boundsBottomLeft.x + size.x;
            float maxX = placeRight ? boundsTopRight.x - size.x : boundsTopRight.x;
            x = Mathf.Clamp(x, minX, maxX);
            float y = Mathf.Clamp(anchorTopRight.y, boundsBottomLeft.y + size.y, boundsTopRight.y);
            rect.localPosition = new Vector3(x, y, rect.localPosition.z);
        }

    }
}
