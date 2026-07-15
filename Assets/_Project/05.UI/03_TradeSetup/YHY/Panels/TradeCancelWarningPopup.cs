// =============================================================================
// TradeCancelWarningPopup — ⑦-1 무역 취소 경고창 (와이어프레임 Section 9 v2 - 7-1)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 무역 진행 중 "무역 취소"를 누르면 뜨는 확인 경고창(오버레이).
//   · "경고" 헤더(빨강) + "무역을 취소할 경우 불이익이 가해질 수 있습니다."
//   · [돌아가기] → 경고창만 닫기(진행 계속)  / [무역 취소] → 무역 중단 확정
//   버튼 동작은 매니저가 연결한다(여긴 열기/닫기만).
// =============================================================================

using UnityEngine;

/// <summary>⑦-1 무역 취소 경고창(오버레이). 열기/닫기만 담당.</summary>
public class TradeCancelWarningPopup : MonoBehaviour
{
    /// <summary>경고창 열기 — 최상위로 올려 진행 화면 위에 표시.</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    /// <summary>경고창 닫기.</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }
}
