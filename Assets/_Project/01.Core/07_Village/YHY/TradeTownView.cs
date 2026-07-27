// =============================================================================
// TradeTownView — 메인 UI에서 "거래마을" 3D 화면(RT)을 띄우는 뷰 (추가 기능)
// =============================================================================
// [담당] Core Gameplay (윤호영)
//
// [역할] TradeTowns 3D 씬을 RenderTexture(RT_TradeTown)로 받아 RawImage에 그린다.
//        Village_Home/VillageView 구조를 그대로 본뜬 것 — 거점(홈) 대신 "거래마을" 버전.
//
// [작동] Show() 호출 시:
//   1) TradeTowns 씬이 아직 안 떠 있으면 additive로 로드 (그래야 TradeTownCamera가 RT에 그림)
//   2) RawImage를 켠다
//   Hide()는 RawImage만 끈다(씬은 유지 — 다시 볼 때 즉시 표시).
//
// [규칙 준수] 이 뷰는 프리팹으로 만들어 메인 씬에 "얹기"만 한다.
//   - 메인 씬(InGame) 파일은 수정/저장하지 않는다(런타임에 프리팹 인스턴스로만 존재).
//   - 남의 스크립트를 고치지 않는다 — 씬 로드도 우리 자체 로직으로 한다.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>거래마을 3D 화면(RT_TradeTown)을 RawImage에 표시하는 뷰.</summary>
public class TradeTownView : MonoBehaviour
{
    [SerializeField] private RawImage view;                 // RT를 그리는 RawImage (비면 자기 자신에서 탐색)
    [SerializeField] private string sceneName = "TradeTowns"; // 3D 거래마을 씬 이름 (Build Settings에 등록 필요)
    [SerializeField] private bool showOnStart = false;       // 테스트용: 시작하자마자 표시할지

    /// <summary>지금 무역마을 화면이 표시 중인가(이름표 등에서 참조).</summary>
    public bool IsShowing => view != null && view.enabled;

    private void Awake()
    {
        // RawImage를 지정 안 했으면 자기 자신에서 찾는다.
        if (view == null) view = GetComponent<RawImage>();
    }

    private void Start()
    {
        // 기본은 숨김. 테스트 플래그가 켜져 있으면 바로 표시.
        if (showOnStart) Show();
        else Hide();
    }

    /// <summary>거래마을 화면을 표시한다(필요하면 3D 씬을 먼저 additive 로드).</summary>
    public void Show()
    {
        EnsureSceneLoaded();
        if (view != null) view.enabled = true;
    }

    /// <summary>거래마을 화면을 숨긴다(RawImage만 끔 — 3D 씬은 유지).</summary>
    public void Hide()
    {
        if (view != null) view.enabled = false;
    }

    /// <summary>TradeTowns 3D 씬이 안 떠 있으면 additive로 로드한다.</summary>
    private void EnsureSceneLoaded()
    {
        if (string.IsNullOrEmpty(sceneName)) return;
        if (SceneManager.GetSceneByName(sceneName).isLoaded) return;
        SceneManager.LoadScene(sceneName, LoadSceneMode.Additive);
    }
}
