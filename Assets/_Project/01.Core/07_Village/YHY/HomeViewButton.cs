// =============================================================================
// HomeViewButton — "거점" 버튼: 언제든 거점마을 화면으로 돌아간다
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] VillageCamera를 거점(BaseCamp) 좌표로 이동시켜 거점 화면으로 돌아간다.
//        거점은 언제나 확인 가능해야 하므로 이 버튼은 항상 표시한다.
//
// [부착] HUD의 "거점" 버튼(Button)에 붙인다. 클릭 시 카메라를 거점으로 이동.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

/// <summary>거점마을로 카메라를 이동시키는 버튼.</summary>
[RequireComponent(typeof(Button))]
public class HomeViewButton : MonoBehaviour
{
    [SerializeField] private string homeTownId = "BaseCamp";   // 거점 townId(마커 이름과 일치)

    private void Awake()
    {
        var button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(GoHome);
    }

    /// <summary>카메라를 거점 좌표로 이동 → 거점 화면 표시.</summary>
    public void GoHome()
    {
        TradeTownCameraMover.RequestedTownId = homeTownId;
    }
}
