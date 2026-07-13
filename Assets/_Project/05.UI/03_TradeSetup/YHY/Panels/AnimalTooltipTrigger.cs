// =============================================================================
// AnimalTooltipTrigger — 동물 칸 마우스오버 감지 → 툴팁 표시
// =============================================================================
// [담당] Core Gameplay (윤호영)
// [역할] 인벤토리 동물 버튼에 붙여, 마우스가 올라오면 AnimalTooltip에 정보를 띄우고
//        벗어나면 숨긴다. 표시할 정보 문자열과 툴팁 참조는 Init로 주입.
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>동물 칸 위 마우스 진입/이탈로 툴팁을 켜고 끈다. [1차 빌드]</summary>
public class AnimalTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private AnimalTooltip tooltip;
    private string body;

    /// <summary>툴팁 참조 + 표시할 정보 문자열 주입.</summary>
    public void Init(AnimalTooltip tooltip, string body)
    {
        this.tooltip = tooltip;
        this.body = body;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Show(body, eventData.position);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltip != null) tooltip.Hide();
    }

    // 호버 중 화면 전환/오브젝트 파괴 시 PointerExit이 안 와서 툴팁이 남는 것 방지
    private void OnDisable()
    {
        if (tooltip != null) tooltip.Hide();
    }
}
