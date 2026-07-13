// =============================================================================
// AnimalTooltip — 동물 정보 마우스오버 툴팁 (3번 화면)
// =============================================================================
// [담당] Core Gameplay (윤호영)
// [역할] 동물 인벤토리 칸에 마우스를 올리면 그 동물의 정보를 커서 옆에 띄운다(WoW 스타일).
//        내용/표시는 호출하는 쪽(AnimalTooltipTrigger)이 문자열로 넘긴다. 순수 표시 UI.
// [주의] 시작은 비활성(씬 상태). Show/Hide로 토글. 마우스를 가리지 않게 커서 옆으로 오프셋.
// =============================================================================

using TMPro;
using UnityEngine;

/// <summary>동물 정보 마우스오버 툴팁 — 커서 옆에 정보 표시. [1차 빌드]</summary>
public class AnimalTooltip : MonoBehaviour
{
    [SerializeField] private RectTransform panel;   // 움직일 툴팁 사각형(보통 자기 자신)
    [SerializeField] private TMP_Text text;         // 정보 텍스트
    [SerializeField] private Vector2 offset = new Vector2(18f, -18f);   // 커서로부터 오프셋

    /// <summary>정보를 세팅하고 커서 위치(screenPos) 옆에 띄운다.</summary>
    public void Show(string body, Vector2 screenPos)
    {
        if (text != null) text.text = body;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();   // 팝업 위에 뜨도록 항상 맨 위로
        if (panel != null)
            panel.position = new Vector3(screenPos.x + offset.x, screenPos.y + offset.y, 0f);
    }

    /// <summary>툴팁을 숨긴다.</summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
