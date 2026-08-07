using TMPro;
using UnityEngine;

namespace ND.UI.InGame.SellPriceModifierBuff
{
    public sealed class SellPriceModifierBuffTooltipController : MonoBehaviour
    {
        private enum TooltipState { Closed, Normal, Detail }

        [SerializeField] private RectTransform panel;
        [Tooltip("툴팁 위치를 제한할 UI 영역입니다. 비워 두면 Root Canvas를 사용합니다.")]
        [SerializeField] private RectTransform bounds;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private float gap = 12f;

        private TooltipState state;
        private RectTransform anchor;
        private SellPriceModifierBuffViewData data;

        public void ConfigureReferences(RectTransform tooltipPanel, RectTransform positionBounds, TMP_Text title, TMP_Text body)
        {
            panel = tooltipPanel;
            bounds = positionBounds;
            titleText = title;
            bodyText = body;
        }

        private void Awake()
        {
            if (panel == null) panel = transform as RectTransform;
        }

        public void OpenNormal(SellPriceModifierBuffViewData viewData, RectTransform target)
        {
            data = viewData;
            anchor = target;
            state = TooltipState.Normal;
            Render();
        }

        public void ToggleDetail()
        {
            if (state == TooltipState.Closed || data == null || anchor == null)
                return;

            state = state == TooltipState.Normal ? TooltipState.Detail : TooltipState.Normal;
            Render();
        }

        public void Close()
        {
            state = TooltipState.Closed;
            data = null;
            anchor = null;
            gameObject.SetActive(false);
        }

        private void Render()
        {
            if (data == null || anchor == null)
            {
                Close();
                return;
            }

            if (titleText != null) titleText.text = data.Title;
            if (bodyText != null) bodyText.text = state == TooltipState.Detail ? data.DetailBody : data.NormalBody;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            PositionNextToAnchor();
        }

        private void PositionNextToAnchor()
        {
            RectTransform tooltip = panel != null ? panel : transform as RectTransform;
            RectTransform parent = tooltip != null ? tooltip.parent as RectTransform : null;
            if (tooltip == null || parent == null) return;

            RectTransform positionBounds = ResolvePositionBounds(parent);

            var anchorCorners = new Vector3[4];
            var boundCorners = new Vector3[4];
            anchor.GetWorldCorners(anchorCorners);
            positionBounds.GetWorldCorners(boundCorners);
            Vector2 leftTop = parent.InverseTransformPoint(anchorCorners[1]);
            Vector2 rightTop = parent.InverseTransformPoint(anchorCorners[2]);
            Vector2 boundMax = parent.InverseTransformPoint(boundCorners[2]);
            Vector2 size = tooltip.rect.size;
            bool placeRight = rightTop.x + gap + size.x <= boundMax.x;
            tooltip.pivot = placeRight ? new Vector2(0f, 1f) : new Vector2(1f, 1f);
            float x = placeRight ? rightTop.x + gap : leftTop.x - gap;
            float y = rightTop.y;
            tooltip.localPosition = new Vector3(x, y, tooltip.localPosition.z);

            Canvas.ForceUpdateCanvases();
            ClampToBounds(tooltip, positionBounds, parent);
        }

        private RectTransform ResolvePositionBounds(RectTransform fallback)
        {
            if (bounds != null) return bounds;

            Canvas parentCanvas = GetComponentInParent<Canvas>();
            RectTransform rootCanvasRect = parentCanvas != null
                ? parentCanvas.rootCanvas.transform as RectTransform
                : null;
            return rootCanvasRect != null ? rootCanvasRect : fallback;
        }

        private static void ClampToBounds(
            RectTransform tooltip,
            RectTransform positionBounds,
            RectTransform coordinateSpace)
        {
            var tooltipCorners = new Vector3[4];
            var boundCorners = new Vector3[4];
            tooltip.GetWorldCorners(tooltipCorners);
            positionBounds.GetWorldCorners(boundCorners);

            Vector2 tooltipMin = coordinateSpace.InverseTransformPoint(tooltipCorners[0]);
            Vector2 tooltipMax = coordinateSpace.InverseTransformPoint(tooltipCorners[2]);
            Vector2 boundMin = coordinateSpace.InverseTransformPoint(boundCorners[0]);
            Vector2 boundMax = coordinateSpace.InverseTransformPoint(boundCorners[2]);
            Vector2 offset = Vector2.zero;

            if (tooltipMax.x > boundMax.x) offset.x = boundMax.x - tooltipMax.x;
            if (tooltipMin.x + offset.x < boundMin.x) offset.x = boundMin.x - tooltipMin.x;
            if (tooltipMax.y > boundMax.y) offset.y = boundMax.y - tooltipMax.y;
            if (tooltipMin.y + offset.y < boundMin.y) offset.y = boundMin.y - tooltipMin.y;

            tooltip.localPosition += new Vector3(offset.x, offset.y, 0f);
        }
    }
}
