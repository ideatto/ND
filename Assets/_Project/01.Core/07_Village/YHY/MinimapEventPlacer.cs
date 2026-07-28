// =============================================================================
// MinimapEventPlacer — 미니맵 클릭으로 기압 이벤트(큰불/메테오)를 그 지점에 배치
// =============================================================================
// [담당] Core Gameplay (윤호영)  ※ 개발 디버그 도구
//
// [역할] 하단 버튼으로 "큰불" 또는 "메테오"를 무장(arm)한 뒤 미니맵을 클릭하면,
//        클릭한 월드 좌표에 MinimapWind 이벤트 기압원을 떨어뜨린다.
//        (기존 DropEventAtCenter = 맵 중앙 랜덤 → 위치 지정 불가 문제를 대체)
//
// [연동] 미니맵 RawImage(RT 표시)에 붙인다. 카메라·바람은 RT 루트로 자동 탐색.
//        MinimapGridPainter와 같은 방식의 클릭→월드 변환.
// [주의] 클릭은 Play 모드(EventSystem)에서만. 무장 중엔 마을 클릭 라우터를 잠시 끔.
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>미니맵 클릭으로 큰불/메테오 기압 이벤트를 배치하는 디버그 툴.</summary>
public class MinimapEventPlacer : MonoBehaviour, IPointerClickHandler
{
    // 무장 상태: 어떤 이벤트를 다음 클릭에 놓을지
    private enum Armed { None, Fire, Meteor }

    [SerializeField] private RawImage view;         // 미니맵 RawImage(비면 자기 자신)
    [SerializeField] private bool showPanel = true;

    // 이벤트 튜닝값(기존 DropEventAtCenter와 동일). (강도, 반경비율, 수명)
    // 큰불/전쟁 = 저기압(빨아들임), 메테오 = 고기압(밀어냄)
    private static readonly float FireStrength = 2.5f, FireRadius = 0.18f, FireLife = 8f;
    private static readonly float MeteorStrength = 3.5f, MeteorRadius = 0.14f, MeteorLife = 5f;

    private Armed armed = Armed.None;
    private Camera cachedCam;
    private MinimapWind wind;
    private MinimapTownClickRouter router;
    private bool routerWasEnabled;
    private GUIStyle btnStyle;

    private void Awake()
    {
        if (view == null) view = GetComponent<RawImage>();
        router = GetComponent<MinimapTownClickRouter>();
    }

    // ------------------------------------------------------------------ 클릭 → 배치

    public void OnPointerClick(PointerEventData e)
    {
        if (armed == Armed.None) return;
        MinimapWind w = ResolveWind();
        if (w == null) { Debug.LogWarning("[이벤트배치] MinimapWind 없음"); Disarm(); return; }

        // 클릭 스크린좌표 → RawImage 로컬 → 뷰포트 UV → 월드 (페인터와 동일)
        Camera cam = ResolveCamera();
        if (cam == null || view == null) { Disarm(); return; }
        RectTransform rt = view.rectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local))
        { Disarm(); return; }
        float u = Mathf.InverseLerp(rt.rect.xMin, rt.rect.xMax, local.x);
        float v = Mathf.InverseLerp(rt.rect.yMin, rt.rect.yMax, local.y);
        Vector3 world = cam.ViewportToWorldPoint(new Vector3(u, v, Mathf.Abs(cam.transform.position.z)));

        // 무장한 종류대로 그 지점에 이벤트 주입
        if (armed == Armed.Fire)
            w.DropEventAt(world, false, FireStrength, FireRadius, FireLife);
        else
            w.DropEventAt(world, true, MeteorStrength, MeteorRadius, MeteorLife);

        Debug.Log($"[이벤트배치] {armed} @ ({world.x:0.0},{world.y:0.0})");
        Disarm();   // 한 번 놓으면 무장 해제(연속 배치는 버튼 다시 누르기)
    }

    // ------------------------------------------------------------------ UI (하단 중앙)

    private void OnGUI()
    {
        if (!showPanel) return;
        if (btnStyle == null) btnStyle = new GUIStyle(GUI.skin.button);
        float s = Mathf.Max(1f, Screen.height / 1080f);
        btnStyle.fontSize = Mathf.RoundToInt(20f * s);
        float w = 190f * s, h = 58f * s, pad = 8f * s;
        // 좌열, 격자 토글 바로 아래(페인트 모드 켜기 밑으로 세로 정렬)
        float x = 545f * s, y = 445f * s;

        DrawArmButton(new Rect(x, y, w, h), Armed.Fire,
            armed == Armed.Fire ? "🔥 클릭해 배치…" : "🔥 큰불 놓기");
        DrawArmButton(new Rect(x, y + h + pad, w, h), Armed.Meteor,
            armed == Armed.Meteor ? "☄ 클릭해 배치…" : "☄ 메테오 놓기");
    }

    private void DrawArmButton(Rect r, Armed type, string label)
    {
        Color prev = GUI.backgroundColor;
        if (armed == type) GUI.backgroundColor = new Color(1f, 0.85f, 0.3f);   // 무장 중 강조
        if (GUI.Button(r, label, btnStyle))
        {
            if (armed == type) Disarm();     // 같은 버튼 다시 누르면 취소
            else Arm(type);
        }
        GUI.backgroundColor = prev;
    }

    // ------------------------------------------------------------------ 무장/해제

    private void Arm(Armed type)
    {
        armed = type;
        // 무장 중엔 마을 클릭 진입(라우터)을 꺼 충돌 방지
        if (router != null) { routerWasEnabled = router.enabled; router.enabled = false; }
    }

    private void Disarm()
    {
        armed = Armed.None;
        if (router != null) router.enabled = routerWasEnabled;   // 원상 복구
    }

    // ------------------------------------------------------------------ 탐색

    private Camera ResolveCamera()
    {
        if (cachedCam != null) return cachedCam;
        RenderTexture rtTex = (view != null) ? view.texture as RenderTexture : null;
        foreach (Camera cam in Camera.allCameras)
        {
            if (cam.targetTexture == null) continue;
            if (rtTex == null || cam.targetTexture == rtTex) { cachedCam = cam; return cam; }
        }
        return null;
    }

    private MinimapWind ResolveWind()
    {
        if (wind != null) return wind;
        var cam = ResolveCamera();
        if (cam != null) wind = cam.transform.root.GetComponentInChildren<MinimapWind>(true);
        return wind;
    }
}
