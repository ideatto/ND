// =============================================================================
// HomeViewButton — "거점" 버튼: 언제든 거점마을 화면으로 돌아간다
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 무역마을 화면(TradeTownView)을 숨겨 그 아래 항상 렌더링 중인 거점마을(VillageView)이
//        다시 보이게 한다. 거점은 언제나 확인 가능해야 하므로 이 버튼은 항상 표시한다.
//
// [부착] HUD의 "거점" 버튼(Button)에 붙인다. 클릭 시 TradeTownView.Hide() 호출.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

/// <summary>거점마을로 돌아가는 버튼 (무역마을 화면을 숨김).</summary>
[RequireComponent(typeof(Button))]
public class HomeViewButton : MonoBehaviour
{
    [SerializeField] private TradeTownView tradeTownView;   // 숨길 무역마을 화면

    private void Awake()
    {
        var button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(GoHome);
    }

    /// <summary>무역마을 화면을 숨긴다 → 아래의 거점마을(VillageView)이 드러난다.</summary>
    public void GoHome()
    {
        if (tradeTownView != null) tradeTownView.Hide();
    }
}
