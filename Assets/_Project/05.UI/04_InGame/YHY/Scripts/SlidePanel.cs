// =============================================================================
// SlidePanel — 화면 밖에 숨겨뒀다가 버튼으로 슬라이드 인/아웃하는 패널
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 월드맵처럼 평소엔 화면 밖(오른쪽 등)에 숨어 있다가, 버튼을 누르면
//        지정 위치로 부드럽게 슬라이드되어 열리고, 다시 누르면 닫힌다.
//   · openPos   : 열렸을 때 패널의 anchoredPosition
//   · closedPos : 닫혔을 때(화면 밖) 패널의 anchoredPosition
//   버튼은 Toggle()에 연결한다. (또는 SetOpen(bool)로 직접 제어)
//
// [연출] unscaledDeltaTime + easeOutCubic — 게임 일시정지 중에도 부드럽게 동작.
// =============================================================================

using System.Collections;
using UnityEngine;

/// <summary>화면 밖에서 슬라이드로 열리고 닫히는 패널(월드맵 등).</summary>
public class SlidePanel : MonoBehaviour
{
    [Header("슬라이드 대상")]
    [SerializeField] private RectTransform panel;   // 슬라이드할 패널(자기 자신 또는 자식)
    [SerializeField] private Camera rendCam;        // RendTexture Camera

    [Header("위치")]
    [SerializeField] private Vector2 openPos;       // 열렸을 때 위치
    [SerializeField] private Vector2 closedPos;     // 닫혔을 때 위치(화면 밖)

    [Header("연출")]
    [SerializeField] private float duration = 0.3f; // 슬라이드 시간(초)
    [SerializeField] private bool startOpen = false; // 시작 시 열림 상태로?

    private bool isOpen;
    private Coroutine anim;

    public bool IsOpen => isOpen;

private void Awake()
    {
        if (panel == null)
        {
            panel = transform as RectTransform;
        }

        isOpen = startOpen;

        if (panel != null)
        {
            panel.anchoredPosition = isOpen ? openPos : closedPos;
        }

        if (rendCam != null)
        {
            // Keep the RenderTexture camera state consistent with the initial panel visibility.
            rendCam.enabled = isOpen;
        }
    }

    /// <summary>열림/닫힘 토글(버튼에 연결).</summary>
    public void Toggle()
    {
        SetOpen(!isOpen);
    }

    /// <summary>열림/닫힘 상태를 지정해 슬라이드.</summary>
public void SetOpen(bool open)
    {
        isOpen = open;

        if (open && rendCam != null)
        {
            // Begin rendering before the opening animation exposes the map.
            rendCam.enabled = true;
        }

        if (panel == null)
        {
            return;
        }

        if (anim != null)
        {
            StopCoroutine(anim);
        }

        if (gameObject.activeInHierarchy)
        {
            anim = StartCoroutine(Slide(open ? openPos : closedPos));
        }
        else
        {
            panel.anchoredPosition = open ? openPos : closedPos;

            if (rendCam != null)
            {
                // An inactive panel cannot animate, so apply the final camera state immediately.
                rendCam.enabled = open;
            }
        }
    }

private IEnumerator Slide(Vector2 target)
    {
        Vector2 start = panel.anchoredPosition;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float e = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 3f);
            panel.anchoredPosition = Vector2.LerpUnclamped(start, target, e);
            yield return null;
        }

        panel.anchoredPosition = target;
        anim = null;

        if (!isOpen && rendCam != null)
        {
            // Stop rendering only after the closing animation is completely hidden.
            rendCam.enabled = false;
        }
    }
}
