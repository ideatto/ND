using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TradeMapUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RawImage mapRawImage;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform mapContent;
    [SerializeField] private Camera uiCamera;

    [Header("Zoom")]
    [SerializeField] private float minZoom = 1f;
    [SerializeField] private float maxZoom = 4f;
    [SerializeField] private float zoomSpeed = 0.0015f;

    [Header("Drag")]
    [SerializeField] private float dragSpeed = 1f;

    private float zoom = 1f;
    private bool visible = true;
    private bool isDragging;
    private Vector2 lastMousePosition;

    public void Initialize(RenderTexture mapTexture)
    {
        SetRenderTexture(mapTexture);
        SetVisible(true);
        ResetView();
    }

    public void SetRenderTexture(RenderTexture texture)
    {
        if (mapRawImage != null)
            mapRawImage.texture = texture;
    }

    public void SetVisible(bool isVisible)
    {
        visible = isVisible;

        if (canvasGroup == null)
        {
            gameObject.SetActive(isVisible);
            return;
        }

        canvasGroup.alpha = isVisible ? 1f : 0f;
        canvasGroup.interactable = isVisible;
        canvasGroup.blocksRaycasts = isVisible;
    }

    public void ResetView()
    {
        zoom = minZoom;
        isDragging = false;

        if (mapContent == null)
            return;

        mapContent.localScale = Vector3.one * zoom;
        mapContent.anchoredPosition = Vector2.zero;

        ClampContentToViewport();
    }

    private void Update()
    {
        if (!visible)
            return;

        HandleZoom();
        HandleDrag();
    }

    private void HandleZoom()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || mapContent == null)
            return;

        float wheel = mouse.scroll.ReadValue().y;
        if (Mathf.Approximately(wheel, 0f))
            return;

        zoom = Mathf.Clamp(zoom + wheel * zoomSpeed, minZoom, maxZoom);
        mapContent.localScale = Vector3.one * zoom;

        ClampContentToViewport();
    }

    private void HandleDrag()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || viewport == null || mapContent == null)
            return;

        Vector2 mousePosition = mouse.position.ReadValue();

        bool isPointerInside = RectTransformUtility.RectangleContainsScreenPoint(
            viewport,
            mousePosition,
            uiCamera
        );

        if (mouse.leftButton.wasPressedThisFrame && isPointerInside)
        {
            isDragging = true;
            lastMousePosition = mousePosition;
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
        }

        if (!isDragging)
            return;

        if (!mouse.leftButton.isPressed)
        {
            isDragging = false;
            return;
        }

        Vector2 delta = mousePosition - lastMousePosition;
        mapContent.anchoredPosition += delta * dragSpeed;
        lastMousePosition = mousePosition;

        ClampContentToViewport();
    }

    private void ClampContentToViewport()
    {
        if (viewport == null || mapContent == null)
            return;

        Rect viewportRect = viewport.rect;
        Rect contentRect = mapContent.rect;

        Vector2 position = mapContent.anchoredPosition;

        float scaledContentWidth = contentRect.width * zoom;
        float scaledContentHeight = contentRect.height * zoom;

        float viewportWidth = viewportRect.width;
        float viewportHeight = viewportRect.height;

        float maxX = Mathf.Max(0f, (scaledContentWidth - viewportWidth) * 0.5f);
        float maxY = Mathf.Max(0f, (scaledContentHeight - viewportHeight) * 0.5f);

        if (scaledContentWidth <= viewportWidth)
            position.x = 0f;
        else
            position.x = Mathf.Clamp(position.x, -maxX, maxX);

        if (scaledContentHeight <= viewportHeight)
            position.y = 0f;
        else
            position.y = Mathf.Clamp(position.y, -maxY, maxY);

        mapContent.anchoredPosition = position;
    }
}