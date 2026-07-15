// =============================================================================
// PanelOpener — 버튼 클릭 시 지정 패널을 열고 제목을 세팅
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] 재화 라인의 메뉴 버튼(거점·무역·인벤토리·설정)이 공용 팝업 패널을 여는
//        진입점. Open()은 패널을 켜고 제목을 해당 이름으로 바꾼다.
//        ClosePanel()은 닫기 버튼용.
//
// [그레이박스] 지금은 하나의 공용 팝업에 제목만 바뀜 → 실제 기능 확정되면
//              각 패널을 별도로 분리하면 된다.
// =============================================================================

using TMPro;
using UnityEngine;

/// <summary>버튼 → 공용 팝업 열기/닫기 + 제목 세팅.</summary>
public class PanelOpener : MonoBehaviour
{
    [SerializeField] private GameObject panel;      // 열 팝업 패널
    [SerializeField] private TMP_Text titleText;    // 팝업 제목 텍스트
    [SerializeField] private string title = "패널"; // 이 버튼이 세팅할 제목

    /// <summary>팝업 열기 + 제목 세팅(메뉴 버튼에 연결).</summary>
    public void Open()
    {
        if (panel != null)
        {
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();   // 최상위로
        }
        if (titleText != null) titleText.text = title;
    }

    /// <summary>팝업 닫기(닫기 버튼에 연결).</summary>
    public void ClosePanel()
    {
        if (panel != null) panel.SetActive(false);
    }
}
