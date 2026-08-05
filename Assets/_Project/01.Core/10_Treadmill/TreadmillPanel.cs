// =============================================================================
// TreadmillPanel — 왼쪽에서 슬라이드되는 트레드밀 패널(1단계: 열기/닫기 + 대상 표시)
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 트레드밀 시스템
//
// [역할] 마차 버튼 바(TreadmillCaravanButtonBar, 마차1~4)에서 마차를 고르면 이 패널이
//        왼쪽에서 오른쪽으로 슬라이드되어 열린다. Open(caravanId)로 대상 마차를 표시한다.
//        (※ 예전의 '미니맵 마차 클릭 → 트레드밀' 라우터는 비활성화됨(no-op) — 마을 클릭 오작동 유발.)
//
// [슬라이드] SlidePanel을 재사용한다. 미니맵 패널이 오른쪽에서 나온다면, 이건 반대로
//            closedPos를 화면 왼쪽 밖(음수 X)에 두어 왼→오로 슬라이드시킨다.
//
// [부착] MainUICanvas 아래 새 패널 오브젝트에 SlidePanel과 함께 붙인다.
// =============================================================================

using UnityEngine;
using TMPro;

/// <summary>왼쪽 슬라이드 트레드밀 패널. Open(caravanId)로 열고 Close로 닫는다.</summary>
public class TreadmillPanel : MonoBehaviour
{
    [SerializeField] private SlidePanel slide;        // 슬라이드 제어(비면 같은 오브젝트에서 탐색)
    [SerializeField] private TMP_Text caravanLabel;   // 어느 마차인지 표시(1단계 플레이스홀더)
    [SerializeField] private TreadmillStage stage;    // 마차+동물 3D 스테이지(2단계). 비면 런타임 탐색

    /// <summary>현재 이 패널이 보여주는 마차 id(2단계 트레드밀이 참조).</summary>
    public string CurrentCaravanId { get; private set; }
    public bool IsOpen => slide != null && slide.IsOpen;

    private void Awake()
    {
        if (slide == null) slide = GetComponent<SlidePanel>();
        if (stage == null) stage = Object.FindAnyObjectByType<TreadmillStage>(FindObjectsInactive.Include);
    }

    /// <summary>지정 마차 기준으로 패널을 연다(왼→오 슬라이드).</summary>
    public void Open(string caravanId)
    {
        Open(caravanId, string.Empty);
    }

    /// <summary>Opens one Caravan lane and renders the supplied presentation name.</summary>
    public void Open(string caravanId, string displayName)
    {
        CurrentCaravanId = caravanId;
        if (caravanLabel != null)
            caravanLabel.text = string.IsNullOrWhiteSpace(displayName)
                ? "마차 " + Short(caravanId)
                : displayName.Trim();
        if (slide != null) slide.SetOpen(true);
        // 마차별 독립 레인 매니저가 있으면 그 마차 전용 레인을 보인다(레인 간 상태가 안 섞임).
        // 없으면(단일 인스턴스 하위호환) 예전처럼 하나뿐인 스테이지를 재구성한다.
        if (TreadmillLaneManager.Instance != null) TreadmillLaneManager.Instance.Show(caravanId);
        else if (stage != null) stage.ShowCaravan(caravanId);   // 데이터 기반 마차+동물 세우기
        Debug.Log("[Treadmill] 패널 열림 — 마차 " + caravanId);
    }

    /// <summary>
    /// Closes an already open panel when the same Caravan is selected again.
    /// Selecting another Caravan keeps the panel open and switches its lane.
    /// </summary>
    public void Toggle(string caravanId, string displayName)
    {
        bool isSameCaravan = string.Equals(
            CurrentCaravanId,
            caravanId,
            System.StringComparison.Ordinal);

        if (slide != null && slide.IsOpen && isSameCaravan)
        {
            Close();
            return;
        }

        Open(caravanId, displayName);
    }

    /// <summary>Refreshes only the label for the Caravan currently shown by this panel.</summary>
    public void RefreshDisplayName(string caravanId, string displayName)
    {
        if (!IsOpen
            || !string.Equals(
                CurrentCaravanId,
                caravanId,
                System.StringComparison.Ordinal))
        {
            return;
        }

        if (caravanLabel != null)
            caravanLabel.text = displayName?.Trim() ?? string.Empty;
    }

    /// <summary>패널을 닫는다(닫기 버튼 등에 연결).</summary>
    public void Close()
    {
        if (slide != null) slide.SetOpen(false);
        if (stage != null) stage.Clear();   // 스테이지 모델 정리
    }

    private static string Short(string id)
        => string.IsNullOrEmpty(id) ? "?" : (id.Length > 6 ? id.Substring(0, 6) : id);
}
