// =============================================================================
// MinimapCaravanClickRouter — [비활성화됨] 미니맵 마차 클릭 → 트레드밀 열기 기능 제거
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [배경] 예전엔 미니맵에서 마차 마커를 클릭하면 왼쪽 트레드밀 패널을 열었다. 그러나
//        정박/유령 마차 마커가 '마을 위에 겹쳐' 있어, 마을을 클릭하면 트레드밀이 잘못
//        열리는 버그가 있어 이 기능을 제거하기로 했다.
//
// [방식] 이 컴포넌트는 InGame 등 '씬'에 직접 붙어 있어, 컴포넌트를 삭제하면 씬 파일이
//        수정된다(다른 사람이 같은 씬을 편집 중일 때 병합 충돌 위험). 그래서 컴포넌트와
//        직렬화 필드는 씬에 그대로 두고, 클릭 동작만 no-op으로 막는다(스크립트만 변경 →
//        병합 안전). 트레드밀은 마차 버튼 바(TreadmillCaravanButtonBar)로 계속 연다.
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 아래 필드들은 코드에서 읽지 않고 '씬 직렬화 값과의 매칭'만을 위해 유지한다(제거하면 씬이 바뀜).
#pragma warning disable 0169, 0414

/// <summary>[비활성화됨] 미니맵 마차 클릭 → 트레드밀 열기. 클릭해도 아무 동작도 하지 않는다.</summary>
[DisallowMultipleComponent]
public class MinimapCaravanClickRouter : MonoBehaviour, IPointerClickHandler
{
    // 씬(InGame/InGame_Test3)에 직렬화된 값과 그대로 매칭되도록 필드 구성을 유지한다.
    [SerializeField] private RawImage view;
    [SerializeField] private Transform renderRoot;
    [SerializeField] private float hitRadius = 0.8f;
    [SerializeField] private TreadmillPanel treadmillPanel;

    /// <summary>기능 제거됨 — 클릭해도 아무 동작도 하지 않는다.</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        // 의도적 no-op. (미니맵 마차 클릭 → 트레드밀 열기 기능 비활성화)
    }
}

#pragma warning restore 0169, 0414
