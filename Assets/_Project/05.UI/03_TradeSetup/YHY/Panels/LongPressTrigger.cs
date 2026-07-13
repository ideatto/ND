// =============================================================================
// LongPressTrigger — 짧게 탭 / 길게 누름 구분 (도시 버튼용)
// =============================================================================
// [담당] Core Gameplay (윤호영)
// [역할] 버튼에 붙여, 짧게 누르면 onTap(도시 선택), 0.5초 이상 누르고 있으면
//        onLongPress(도시 정보 팝업)를 부른다. 두 동작이 겹치지 않게 구분.
// [주의] 길게 누름이 발동하면 그 손가락에서 탭은 발동하지 않는다. 벗어나면 취소.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>짧게 탭 vs 길게 누름을 구분하는 트리거. [1차 빌드]</summary>
public class LongPressTrigger : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    private float threshold = 0.5f;
    private Action onTap;
    private Action onLongPress;

    private bool pressing;
    private bool fired;      // 이번 누름에서 길게 누름이 이미 발동했나
    private float downTime;

    /// <summary>콜백·시간 임계값 주입.</summary>
    public void Init(Action onTap, Action onLongPress, float threshold)
    {
        this.onTap = onTap;
        this.onLongPress = onLongPress;
        this.threshold = threshold;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressing = true;
        fired = false;
        downTime = Time.unscaledTime;
    }

    private void Update()
    {
        if (!pressing || fired) return;
        if (Time.unscaledTime - downTime >= threshold)
        {
            fired = true;
            pressing = false;
            onLongPress?.Invoke();   // 0.5초 이상 → 도시 정보
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (pressing && !fired) onTap?.Invoke();   // 임계값 전에 뗌 → 탭(선택)
        pressing = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pressing = false;   // 벗어나면 취소
    }
}
