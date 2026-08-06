// =============================================================================
// TreadmillCaravanButton — "마차N" 버튼 → 트레드밀 패널 열기
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템 1단계
//
// [역할] 미니맵에서 작은 마차 마커를 정확히 클릭하기 어려워, 화면 우하단에 마차1~4 버튼을
//        두고 누르면 그 마차 기준으로 왼쪽 트레드밀 패널(TreadmillPanel)을 연다.
//        1단계에선 caravanId를 슬롯 라벨("1"~"4")로 넘긴다. 2단계에서 실제 캐러밴에 매핑.
//
// [부착] Button이 있는 오브젝트에 붙인다. onClick에 자동으로 리스너를 건다.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

/// <summary>우하단 "마차N" 버튼. 누르면 그 마차로 트레드밀 패널을 연다.</summary>
[RequireComponent(typeof(Button))]
public class TreadmillCaravanButton : MonoBehaviour
{
    [SerializeField] private string caravanId = "1";   // 이 버튼이 열 마차 식별자(1단계: 슬롯 라벨)
    [SerializeField] private TreadmillPanel panel;     // 열 패널(비면 런타임 탐색)

    private void Awake()
    {
        if (panel == null)
            panel = Object.FindAnyObjectByType<TreadmillPanel>(FindObjectsInactive.Include);

        Button btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OnClick);
    }

    private void OnClick()
    {
        if (panel == null)
        {
            Debug.LogWarning("[Treadmill] 마차 버튼 눌림(" + caravanId + ")인데 TreadmillPanel을 못 찾음");
            return;
        }

        // 토글: 이 마차로 이미 열려 있으면 닫고, 아니면(닫힘 or 다른 마차) 이 마차로 연다.
        if (panel.IsOpen && panel.CurrentCaravanId == caravanId)
            panel.Close();
        else
            panel.Open(caravanId);
    }

    /// <summary>버튼바(TreadmillCaravanButtonBar)가 실제 캐러밴을 이 버튼에 매핑한다.</summary>
    public void SetCaravan(string id, string label)
    {
        caravanId = id;                                   // 이 버튼이 열 실제 캐러밴 id
        var tmp = GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmp != null) { tmp.text = label; return; }    // 라벨(예: "마차 1") 갱신
        var t = GetComponentInChildren<Text>(true);
        if (t != null) t.text = label;
    }
}
